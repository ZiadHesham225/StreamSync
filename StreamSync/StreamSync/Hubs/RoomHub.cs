using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.DTOs;
using StreamSync.Models;
using StreamSync.Models.InMemory;
using StreamSync.BusinessLogic.Services.InMemory;

namespace StreamSync.Hubs
{
    public class RoomHub : Hub<IRoomClient>
    {
        private readonly IRoomService _roomService;
        private readonly InMemoryRoomManager _roomManager;
        private readonly ILogger<RoomHub> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServiceProvider _serviceProvider;

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, double>>
            _positionReports = new();

        public RoomHub(
            IRoomService roomService,
            InMemoryRoomManager roomManager,
            ILogger<RoomHub> logger,
            UserManager<ApplicationUser> userManager,
            IServiceProvider serviceProvider)
        {
            _roomService = roomService;
            _roomManager = roomManager;
            _logger = logger;
            _userManager = userManager;
            _serviceProvider = serviceProvider;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var roomId = GetUserRoom();
                var participantId = Context.Items["ParticipantId"] as string;

                if (!string.IsNullOrEmpty(roomId) && !string.IsNullOrEmpty(participantId))
                {
                    _logger.LogInformation($"User {participantId} disconnected from room {roomId}, processing immediate leave");
                    await ProcessActualLeave(roomId, participantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnDisconnectedAsync for connection {Context.ConnectionId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        #region Room Management

        [Authorize]
        public async Task JoinRoom(string roomId, string? password = null)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                _logger.LogInformation($"User {user.UserName} ({userId}) attempting to join room {roomId}.");
                
                // Verify room exists and user can join
                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    await Clients.Caller.Error("Room not found or no longer active.");
                    return;
                }

                string participantId = userId;
                bool isAdmin = participantId == room.AdminId;

                if (room.IsPrivate && !isAdmin && !await _roomService.ValidateRoomPasswordAsync(roomId, password))
                {
                    await Clients.Caller.Error("Incorrect password.");
                    return;
                }
                
                _logger.LogInformation($"Authenticated user participant ID: {participantId}");

                var currentParticipants = _roomManager.GetRoomParticipants(roomId);
                _logger.LogInformation($"BEFORE JOIN - Room {roomId} has {currentParticipants.Count} participants: [{string.Join(", ", currentParticipants.Select(p => $"{p.DisplayName}({p.Id})"))}]");
                
                var existingParticipant = _roomManager.GetParticipant(roomId, participantId);
                if (existingParticipant != null)
                {
                    _logger.LogInformation($"Participant {participantId} ({user.UserName}) is reconnecting - updating connection ID and preserving state. HasControl: {existingParticipant.HasControl}");
                    
                    existingParticipant.ConnectionId = Context.ConnectionId;
                    
                    await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

                    Context.Items["RoomId"] = roomId;
                    Context.Items["ParticipantId"] = participantId;
                    Context.Items["DisplayName"] = existingParticipant.DisplayName;

                    await Clients.Caller.RoomJoined(roomId, participantId, existingParticipant.DisplayName, existingParticipant.AvatarUrl ?? "");
                    
                    _roomManager.EnsureControlConsistency(roomId);
                    
                    var updatedParticipants = _roomManager.GetRoomParticipants(roomId);
                    var updatedParticipantDtos = updatedParticipants.Select(p => new RoomParticipantDto
                    {
                        Id = p.Id,
                        DisplayName = p.DisplayName,
                        AvatarUrl = p.AvatarUrl,
                        HasControl = p.HasControl,
                        JoinedAt = p.JoinedAt,
                        IsAdmin = room != null && p.Id == room.AdminId
                    }).ToList();

                    await Clients.Group(roomId).ReceiveRoomParticipants(updatedParticipantDtos);
                    
                    if (existingParticipant.HasControl)
                    {
                        await Clients.Caller.ControlTransferred(existingParticipant.Id, existingParticipant.DisplayName);
                        _logger.LogInformation($"Reconnected user {participantId} has control, sending ControlTransferred event");
                    }
                    
                    await Clients.Caller.ForceSyncPlayback(room.CurrentPosition, room.IsPlaying);
                    
                    _logger.LogInformation($"User {participantId} successfully reconnected to room {roomId} with preserved state");
                    return;
                }

                bool isFirstParticipant = _roomManager.GetParticipantCount(roomId) == 0;
                bool shouldHaveControl = isAdmin || isFirstParticipant;

                _logger.LogInformation($"Participant {participantId} ({user.UserName}) joining room {roomId}. IsAdmin: {isAdmin}, IsFirstParticipant: {isFirstParticipant}, ShouldHaveControl: {shouldHaveControl}");
                
                var participant = new RoomParticipant(
                    participantId, 
                    Context.ConnectionId, 
                    user.UserName ?? "Unknown User", 
                    user.AvatarUrl,
                    shouldHaveControl
                );

                if (isAdmin && !isFirstParticipant)
                {
                    var currentController = _roomManager.GetController(roomId);
                    if (currentController != null && currentController.Id != participantId)
                    {
                        _roomManager.SetController(roomId, participantId);
                        shouldHaveControl = true;
                        participant.HasControl = true;
                        
                        await Clients.Group(roomId).ControlTransferred(participantId, user.UserName ?? "Unknown User");
                    }
                }

                _roomManager.AddParticipant(roomId, participant);

                var participantsAfterAdd = _roomManager.GetRoomParticipants(roomId);
                _logger.LogInformation($"AFTER ADD - Room {roomId} has {participantsAfterAdd.Count} participants: [{string.Join(", ", participantsAfterAdd.Select(p => $"{p.DisplayName}({p.Id})"))}]");

                _roomManager.EnsureControlConsistency(roomId);

                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

                Context.Items["RoomId"] = roomId;
                Context.Items["ParticipantId"] = participantId;
                Context.Items["DisplayName"] = user.UserName;

                await Clients.Caller.RoomJoined(roomId, participantId, user.UserName ?? "Unknown User", user.AvatarUrl ?? "");
                
                await Clients.OthersInGroup(roomId).RoomJoined(roomId, participantId, user.UserName ?? "Unknown User", user.AvatarUrl ?? "");
                await Clients.OthersInGroup(roomId).ParticipantJoinedNotification(user.UserName ?? "Unknown User");

                var allParticipants = _roomManager.GetRoomParticipants(roomId);
                _logger.LogInformation($"Sending participant list to room {roomId}: {allParticipants.Count} participants");
                
                var allParticipantDtos = allParticipants.Select(p => new RoomParticipantDto
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    AvatarUrl = p.AvatarUrl,
                    HasControl = p.HasControl,
                    JoinedAt = p.JoinedAt,
                    IsAdmin = p.Id == room.AdminId
                }).ToList();

                foreach (var dto in allParticipantDtos)
                {
                    _logger.LogInformation($"  - {dto.DisplayName} (ID: {dto.Id}, HasControl: {dto.HasControl})");
                }

                await Clients.Group(roomId).ReceiveRoomParticipants(allParticipantDtos);

                await Clients.Caller.ForceSyncPlayback(room.CurrentPosition, room.IsPlaying);
                
                var participants = _roomManager.GetRoomParticipants(roomId);
                var participantDtos = participants.Select(p => new RoomParticipantDto
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    AvatarUrl = p.AvatarUrl,
                    HasControl = p.HasControl,
                    JoinedAt = p.JoinedAt,
                    IsAdmin = p.Id == room.AdminId
                }).ToList();

                await Clients.Caller.ReceiveRoomParticipants(participantDtos);
                
                var chatHistory = _roomManager.GetRoomMessages(roomId);
                var messageDtos = chatHistory.Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName,
                    AvatarUrl = m.AvatarUrl,
                    Content = m.Content,
                    SentAt = m.SentAt
                }).ToList();

                await Clients.Caller.ReceiveChatHistory(messageDtos);

                _logger.LogInformation($"User {participantId} joined room {roomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining room {roomId}");
                await Clients.Caller.Error("An error occurred while joining the room.");
                throw;
            }
        }

        public async Task LeaveRoom(string roomId)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                var displayName = Context.Items["DisplayName"] as string ?? "Unknown User";

                if (string.IsNullOrEmpty(participantId))
                {
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                bool wasController = participant?.HasControl ?? false;

                _roomManager.RemoveParticipant(roomId, participantId);

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

                if (wasController && _roomManager.GetParticipantCount(roomId) > 0)
                {
                    _logger.LogInformation($"Controller {participantId} left room {roomId}, transferring control to next participant");
                    _roomManager.TransferControlToNext(roomId, participantId);
                    
                    _roomManager.EnsureControlConsistency(roomId);
                    
                    var newController = _roomManager.GetController(roomId);
                    if (newController != null)
                    {
                        await Clients.Group(roomId).ControlTransferred(newController.Id, newController.DisplayName);
                        _logger.LogInformation($"Control transferred to {newController.DisplayName} ({newController.Id}) in room {roomId}");
                    }
                    else
                    {
                        _logger.LogWarning($"No controller found after transfer in room {roomId}");
                    }
                }

                await Clients.OthersInGroup(roomId).RoomLeft(roomId, participantId, displayName);
                await Clients.OthersInGroup(roomId).ParticipantLeftNotification(displayName);

                if (_roomManager.GetParticipantCount(roomId) > 0)
                {
                    var remainingParticipants = _roomManager.GetRoomParticipants(roomId);
                    var room = await _roomService.GetRoomByIdAsync(roomId);
                    var remainingParticipantDtos = remainingParticipants.Select(p => new RoomParticipantDto
                    {
                        Id = p.Id,
                        DisplayName = p.DisplayName,
                        AvatarUrl = p.AvatarUrl,
                        HasControl = p.HasControl,
                        JoinedAt = p.JoinedAt,
                        IsAdmin = room != null && p.Id == room.AdminId
                    }).ToList();

                    await Clients.Group(roomId).ReceiveRoomParticipants(remainingParticipantDtos);
                }

                Context.Items.Remove("RoomId");
                Context.Items.Remove("ParticipantId");
                Context.Items.Remove("DisplayName");

                if (_roomManager.GetParticipantCount(roomId) == 0)
                {
                    _roomManager.ClearRoomData(roomId);
                    _positionReports.TryRemove(roomId, out _);
                }

                _logger.LogInformation($"User {participantId} left room {roomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leaving room {roomId}");
                throw;
            }
        }

        private async Task ProcessActualLeave(string roomId, string participantId)
        {
            try
            {
                var participant = _roomManager.GetParticipant(roomId, participantId);
                if (participant == null)
                {
                    _logger.LogInformation($"Participant {participantId} already left room {roomId}");
                    return;
                }

                string displayName = participant.DisplayName;
                bool wasController = participant.HasControl;

                _roomManager.RemoveParticipant(roomId, participantId);

                if (wasController && _roomManager.GetParticipantCount(roomId) > 0)
                {
                    _logger.LogInformation($"Controller {participantId} left room {roomId}, transferring control to next participant");
                    _roomManager.TransferControlToNext(roomId, participantId);
                    
                    _roomManager.EnsureControlConsistency(roomId);
                    
                    var newController = _roomManager.GetController(roomId);
                    if (newController != null)
                    {
                        var hubContext = _serviceProvider.GetService<IHubContext<RoomHub, IRoomClient>>();
                        if (hubContext != null)
                        {
                            await hubContext.Clients.Group(roomId).ControlTransferred(newController.Id, newController.DisplayName);
                            _logger.LogInformation($"Control transferred to {newController.DisplayName} ({newController.Id}) in room {roomId}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No controller found after transfer in room {roomId}");
                    }
                }

                var hubContextForNotifications = _serviceProvider.GetService<IHubContext<RoomHub, IRoomClient>>();
                if (hubContextForNotifications != null)
                {
                    await hubContextForNotifications.Clients.Group(roomId).RoomLeft(roomId, participantId, displayName);
                    await hubContextForNotifications.Clients.Group(roomId).ParticipantLeftNotification(displayName);

                    if (_roomManager.GetParticipantCount(roomId) > 0)
                    {
                        var remainingParticipants = _roomManager.GetRoomParticipants(roomId);
                        var room = await _roomService.GetRoomByIdAsync(roomId);
                        var remainingParticipantDtos = remainingParticipants.Select(p => new RoomParticipantDto
                        {
                            Id = p.Id,
                            DisplayName = p.DisplayName,
                            AvatarUrl = p.AvatarUrl,
                            HasControl = p.HasControl,
                            JoinedAt = p.JoinedAt,
                            IsAdmin = room != null && p.Id == room.AdminId
                        }).ToList();

                        await hubContextForNotifications.Clients.Group(roomId).ReceiveRoomParticipants(remainingParticipantDtos);
                    }
                }

                if (_roomManager.GetParticipantCount(roomId) == 0)
                {
                    _roomManager.ClearRoomData(roomId);
                    _positionReports.TryRemove(roomId, out _);
                }

                _logger.LogInformation($"User {participantId} left room {roomId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing actual leave for {participantId} in room {roomId}");
            }
        }

        public async Task GetRoomParticipants(string roomId)
        {
            try
            {
                var participants = _roomManager.GetRoomParticipants(roomId);
                var room = await _roomService.GetRoomByIdAsync(roomId);
                
                var participantDtos = participants.Select(p => new RoomParticipantDto
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    AvatarUrl = p.AvatarUrl,
                    HasControl = p.HasControl,
                    JoinedAt = p.JoinedAt,
                    IsAdmin = room != null && p.Id == room.AdminId
                }).ToList();

                await Clients.Caller.ReceiveRoomParticipants(participantDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting room participants for room {roomId}");
                await Clients.Caller.Error("Failed to get room participants.");
                throw;
            }
        }

        [Authorize]
        public async Task CloseRoom(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("Authentication required to close rooms.");
                    return;
                }

                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, userId);
                if (!isAdmin)
                {
                    _logger.LogWarning($"Non-admin user {userId} attempted to close room {roomId}");
                    await Clients.Caller.Error("Only room administrators can close rooms.");
                    return;
                }

                bool success = await _roomService.EndRoomAsync(roomId, userId);
                if (!success)
                {
                    _logger.LogWarning($"Failed to end room {roomId} in database");
                    await Clients.Caller.Error("Failed to close the room.");
                    return;
                }

                await Clients.Group(roomId).RoomClosed(roomId, "Room closed by admin");

                _roomManager.ClearRoomData(roomId);
                _positionReports.TryRemove(roomId, out _);
                _logger.LogInformation($"Room {roomId} closed by admin {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing room {roomId}");
                await Clients.Caller.Error("An error occurred while closing the room.");
                throw;
            }
        }

        public async Task ChangeVideo(string roomId, string videoUrl, string videoTitle, string? videoThumbnail)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                if (participant == null)
                {
                    await Clients.Caller.Error("You are not in this room.");
                    return;
                }

                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, participantId);
                if (!participant.HasControl && !isAdmin)
                {
                    await Clients.Caller.Error("You don't have permission to change the video.");
                    return;
                }

                await _roomService.UpdateRoomVideoAsync(roomId, videoUrl);

                await Clients.Group(roomId).VideoChanged(videoUrl, videoTitle, videoThumbnail);

                _logger.LogInformation($"Video changed in room {roomId} by {participantId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing video in room {roomId}");
                await Clients.Caller.Error("Failed to change video.");
                throw;
            }
        }
        #endregion

        #region Playback Control

        public async Task UpdatePlayback(string roomId, double position, bool isPlaying)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                if (participant == null || !participant.HasControl)
                {
                    await Clients.Caller.Error("You don't have permission to control playback.");
                    return;
                }

                await _roomService.UpdatePlaybackStateAsync(roomId, participantId, position, isPlaying);

                await Clients.Group(roomId).ReceivePlaybackUpdate(position, isPlaying);

                _logger.LogDebug($"Playback updated in room {roomId}: pos={position}, playing={isPlaying}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating playback in room {roomId}");
                await Clients.Caller.Error("Failed to update playback state.");
                throw;
            }
        }

        public async Task RequestSync(string roomId)
        {
            try
            {
                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room != null && room.IsActive)
                {
                    await Clients.Caller.ForceSyncPlayback(room.CurrentPosition, room.IsPlaying);
                }
                else
                {
                    await Clients.Caller.Error("Room not found or no longer active.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting sync for room {roomId}");
                await Clients.Caller.Error("Failed to sync playback.");
                throw;
            }
        }

        public async Task BroadcastHeartbeat(string roomId, double position)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                if (participant != null && participant.HasControl)
                {
                    await _roomService.UpdatePlaybackStateAsync(roomId, participantId, position, true);
                    await Clients.OthersInGroup(roomId).ReceiveHeartbeat(position);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error broadcasting heartbeat in room {roomId}");
            }
        }

        public async Task ReportPosition(string roomId, double position)
        {
            try
            {
                if (!_positionReports.TryGetValue(roomId, out var reports))
                {
                    reports = new ConcurrentDictionary<string, double>();
                    _positionReports[roomId] = reports;
                }

                reports[Context.ConnectionId] = position;

                int totalParticipants = _roomManager.GetParticipantCount(roomId);
                int reportedParticipants = reports.Count;

                if (reportedParticipants >= totalParticipants * 0.8 && reportedParticipants >= 2)
                {
                    await AnalyzeAndSyncPositions(roomId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reporting position in room {roomId}");
            }
        }

        private async Task AnalyzeAndSyncPositions(string roomId)
        {
            try
            {
                if (!_positionReports.TryGetValue(roomId, out var reports) || reports.Count < 2)
                {
                    return;
                }

                var positions = reports.Values.OrderBy(p => p).ToList();
                double medianPosition = positions[positions.Count / 2];

                const double tolerance = 3.0;
                var outliers = reports
                    .Where(r => Math.Abs(r.Value - medianPosition) > tolerance)
                    .Select(r => r.Key)
                    .ToList();

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room != null && room.IsActive)
                {
                    foreach (var outlierConnectionId in outliers)
                    {
                        await Clients.Client(outlierConnectionId).ForceSyncPlayback(medianPosition, room.IsPlaying);
                        _logger.LogInformation($"Forced sync for outlier in room {roomId}: connection {outlierConnectionId}");
                    }
                }
                _positionReports[roomId].Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing positions in room {roomId}");
            }
        }

        public async Task PlayVideo(string roomId)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                
                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, participantId);
                if (participant == null || (!participant.HasControl && !isAdmin))
                {
                    await Clients.Caller.Error("You don't have permission to control playback.");
                    return;
                }

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    await Clients.Caller.Error("Room not found or no longer active.");
                    return;
                }

                await _roomService.UpdatePlaybackStateAsync(roomId, participantId, room.CurrentPosition, true);

                await Clients.Group(roomId).ReceivePlaybackUpdate(room.CurrentPosition, true);

                _logger.LogDebug($"Video played in room {roomId} by {participantId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error playing video in room {roomId}");
                await Clients.Caller.Error("Failed to play video.");
                throw;
            }
        }

        public async Task PauseVideo(string roomId)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                
                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, participantId);
                if (participant == null || (!participant.HasControl && !isAdmin))
                {
                    await Clients.Caller.Error("You don't have permission to control playback.");
                    return;
                }

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    await Clients.Caller.Error("Room not found or no longer active.");
                    return;
                }

                await _roomService.UpdatePlaybackStateAsync(roomId, participantId, room.CurrentPosition, false);

                await Clients.Group(roomId).ReceivePlaybackUpdate(room.CurrentPosition, false);

                _logger.LogDebug($"Video paused in room {roomId} by {participantId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error pausing video in room {roomId}");
                await Clients.Caller.Error("Failed to pause video.");
                throw;
            }
        }

        public async Task SeekVideo(string roomId, double position)
        {
            try
            {
                var participantId = Context.Items["ParticipantId"] as string;
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                
                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, participantId);
                if (participant == null || (!participant.HasControl && !isAdmin))
                {
                    await Clients.Caller.Error("You don't have permission to control playback.");
                    return;
                }

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    await Clients.Caller.Error("Room not found or no longer active.");
                    return;
                }

                await _roomService.UpdatePlaybackStateAsync(roomId, participantId, position, room.IsPlaying);

                await Clients.Group(roomId).ReceivePlaybackUpdate(position, room.IsPlaying);

                _logger.LogDebug($"Video seeked to {position} in room {roomId} by {participantId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error seeking video in room {roomId}");
                await Clients.Caller.Error("Failed to seek video.");
                throw;
            }
        }
        
        #endregion

        #region Control Management
        [Authorize]
        public async Task TransferControl(string roomId, string newControllerId)
        {
            try
            {
                var currentUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    await Clients.Caller.Error("Authentication required to transfer control.");
                    return;
                }

                _logger.LogInformation($"TransferControl called. CurrentUserId: {currentUserId}, NewControllerId: {newControllerId}, RoomId: {roomId}");

                bool isAdmin = await _roomService.IsUserAdminAsync(roomId, currentUserId);
                var currentParticipant = _roomManager.GetParticipant(roomId, currentUserId);
                bool hasControl = currentParticipant?.HasControl ?? false;
                
                _logger.LogInformation($"Current participant: {currentParticipant?.DisplayName ?? "NOT_FOUND"}, HasControl: {hasControl}, IsAdmin: {isAdmin}");
                
                if (!isAdmin && !hasControl)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to transfer control without permission in room {roomId}. IsAdmin: {isAdmin}, HasControl: {hasControl}");
                    await Clients.Caller.Error("Only room administrators or current controllers can transfer control.");
                    return;
                }

                var newController = _roomManager.GetParticipant(roomId, newControllerId);
                if (newController == null)
                {
                    await Clients.Caller.Error("Target participant not found in room.");
                    return;
                }

                _roomManager.SetController(roomId, newControllerId);
                await Clients.Group(roomId).ControlTransferred(newControllerId, newController.DisplayName);

                var allParticipants = _roomManager.GetRoomParticipants(roomId);
                var room = await _roomService.GetRoomByIdAsync(roomId);
                var allParticipantDtos = allParticipants.Select(p => new RoomParticipantDto
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    AvatarUrl = p.AvatarUrl,
                    HasControl = p.HasControl,
                    JoinedAt = p.JoinedAt,
                    IsAdmin = room != null && p.Id == room.AdminId
                }).ToList();

                await Clients.Group(roomId).ReceiveRoomParticipants(allParticipantDtos);

                _logger.LogInformation($"Control transferred in room {roomId} from {currentUserId} to {newControllerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring control in room {roomId}");
                await Clients.Caller.Error("An error occurred while transferring control.");
                throw;
            }
        }

        #endregion

        #region Chat
        [Authorize]
        public async Task SendMessage(string roomId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                var participantId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(participantId))
                {
                    await Clients.Caller.Error("Unable to identify participant for message sending.");
                    return;
                }

                var participant = _roomManager.GetParticipant(roomId, participantId);
                if (participant == null)
                {
                    await Clients.Caller.Error("You are no longer in this room.");
                    return;
                }

                var chatMessage = new ChatMessage(participantId, participant.DisplayName, participant.AvatarUrl, message);
                _roomManager.AddMessage(roomId, chatMessage);

                await Clients.Group(roomId).ReceiveMessage(
                    participantId,
                    participant.DisplayName,
                    participant.AvatarUrl,
                    message,
                    DateTime.UtcNow,
                    false);

                _logger.LogDebug($"Message sent in room {roomId} by {participantId} ({participant.DisplayName}): {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message in room {roomId}");
                await Clients.Caller.Error("Failed to send message.");
                throw;
            }
        }

        [Authorize]
        public async Task KickUser(string roomId, string userIdToKick)
        {
            try
            {
                var adminId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminId))
                {
                    await Clients.Caller.Error("Unable to identify admin for kick action.");
                    return;
                }

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null)
                {
                    await Clients.Caller.Error("Room not found.");
                    return;
                }

                if (room.AdminId != adminId)
                {
                    await Clients.Caller.Error("Only the room admin can kick users.");
                    return;
                }

                var participantToKick = _roomManager.GetParticipant(roomId, userIdToKick);
                if (participantToKick == null)
                {
                    await Clients.Caller.Error("User not found in room.");
                    return;
                }

                if (userIdToKick == adminId)
                {
                    await Clients.Caller.Error("Admin cannot kick themselves.");
                    return;
                }

                var adminParticipant = _roomManager.GetParticipant(roomId, adminId);
                var adminDisplayName = adminParticipant?.DisplayName ?? "Admin";

                await Clients.User(userIdToKick).UserKicked(roomId, $"You have been kicked from the room by {adminDisplayName}");

                _roomManager.RemoveParticipant(roomId, userIdToKick);

                await Clients.Group(roomId).ReceiveMessage(
                    "system",
                    "System",
                    null,
                    $"{participantToKick.DisplayName} was kicked from the room by {adminDisplayName}",
                    DateTime.UtcNow,
                    true);

                var updatedParticipants = _roomManager.GetRoomParticipants(roomId)
                    .Select(p => new RoomParticipantDto
                    {
                        Id = p.Id,
                        DisplayName = p.DisplayName,
                        AvatarUrl = p.AvatarUrl,
                        IsAdmin = p.Id == room.AdminId,
                        HasControl = p.HasControl,
                        JoinedAt = p.JoinedAt
                    });
                
                await Clients.Group(roomId).ReceiveRoomParticipants(updatedParticipants);

                _logger.LogInformation($"User {userIdToKick} ({participantToKick.DisplayName}) was kicked from room {roomId} by admin {adminId} ({adminDisplayName})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error kicking user {userIdToKick} from room {roomId}");
                await Clients.Caller.Error("Failed to kick user.");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private string? GetUserRoom()
        {
            if (Context.Items.TryGetValue("RoomId", out var roomId) && roomId != null)
            {
                return roomId.ToString();
            }
            return null;
        }

        #endregion

        #region Virtual Browser Methods

        [Authorize]
        public async Task RequestVirtualBrowser(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
                if (!canControl)
                {
                    await Clients.Caller.Error("Only room admins or controllers can request virtual browsers.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var result = await virtualBrowserService.RequestVirtualBrowserAsync(roomId);
                
                if (result != null)
                {
                    await Clients.Caller.VirtualBrowserAllocated(result);
                    await Clients.OthersInGroup(roomId).VirtualBrowserAllocated(result);
                }
                else
                {
                    var queueStatus = await virtualBrowserService.GetRoomQueueStatusAsync(roomId);
                    if (queueStatus != null)
                    {
                        await Clients.Caller.VirtualBrowserQueued(queueStatus);
                        await Clients.OthersInGroup(roomId).VirtualBrowserQueued(queueStatus);
                    }
                    else
                    {
                        await Clients.Caller.Error("Failed to request virtual browser.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting virtual browser for room {RoomId}", roomId);
                await Clients.Caller.Error("An error occurred while requesting virtual browser.");
            }
        }

        [Authorize]
        public async Task ReleaseVirtualBrowser(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
                if (!canControl)
                {
                    await Clients.Caller.Error("Only room admins or controllers can release virtual browsers.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var result = await virtualBrowserService.ReleaseVirtualBrowserAsync(roomId);
                
                if (!result)
                {
                    await Clients.Caller.Error("Failed to release virtual browser.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing virtual browser for room {RoomId}", roomId);
                await Clients.Caller.Error("An error occurred while releasing virtual browser.");
            }
        }

        [Authorize]
        public async Task AcceptVirtualBrowserNotification(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
                if (!canControl)
                {
                    await Clients.Caller.Error("Only room admins or controllers can accept virtual browser notifications.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var result = await virtualBrowserService.AcceptQueueNotificationAsync(roomId);
                
                if (result)
                {
                    var browser = await virtualBrowserService.GetRoomVirtualBrowserAsync(roomId);
                    if (browser != null)
                    {
                        await Clients.Group(roomId).VirtualBrowserAllocated(browser);
                    }
                }
                else
                {
                    await Clients.Caller.Error("Failed to accept virtual browser notification.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting virtual browser notification for room {RoomId}", roomId);
                await Clients.Caller.Error("An error occurred while accepting virtual browser notification.");
            }
        }

        [Authorize]
        public async Task DeclineVirtualBrowserNotification(string roomId)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var canControl = await _roomService.CanUserControlRoomAsync(roomId, userId);
                if (!canControl)
                {
                    await Clients.Caller.Error("Only room admins or controllers can decline virtual browser notifications.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var result = await virtualBrowserService.DeclineQueueNotificationAsync(roomId);
                
                if (!result)
                {
                    await Clients.Caller.Error("Failed to decline virtual browser notification.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining virtual browser notification for room {RoomId}", roomId);
                await Clients.Caller.Error("An error occurred while declining virtual browser notification.");
            }
        }

        [Authorize]
        public async Task NavigateVirtualBrowser(string virtualBrowserId, string url)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var browser = await virtualBrowserService.GetVirtualBrowserAsync(virtualBrowserId);
                if (browser == null)
                {
                    await Clients.Caller.Error("Virtual browser not found.");
                    return;
                }

                var canControl = await _roomService.CanUserControlRoomAsync(browser.RoomId, userId);
                if (!canControl)
                {
                    await Clients.Caller.Error("Only room admins or controllers can navigate virtual browsers.");
                    return;
                }

                var result = await virtualBrowserService.NavigateVirtualBrowserAsync(virtualBrowserId, url);
                
                if (result)
                {
                    await Clients.Group(browser.RoomId).VirtualBrowserNavigated(url);
                }
                else
                {
                    await Clients.Caller.Error("Failed to navigate virtual browser.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating virtual browser {VirtualBrowserId} to {Url}", virtualBrowserId, url);
                await Clients.Caller.Error("An error occurred while navigating virtual browser.");
            }
        }

        [Authorize]
        public async Task ControlVirtualBrowser(string virtualBrowserId, VirtualBrowserControlDto control)
        {
            try
            {
                var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not found.");
                    return;
                }

                var virtualBrowserService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IVirtualBrowserService>();
                if (virtualBrowserService == null)
                {
                    await Clients.Caller.Error("Virtual browser service not available.");
                    return;
                }

                var browser = await virtualBrowserService.GetVirtualBrowserAsync(virtualBrowserId);
                if (browser == null)
                {
                    await Clients.Caller.Error("Virtual browser not found.");
                    return;
                }

                var isAdmin = await _roomService.IsUserAdminAsync(browser.RoomId, userId);
                if (!isAdmin)
                {
                    await Clients.Caller.Error("Only room admins can control virtual browsers.");
                    return;
                }

                var result = await virtualBrowserService.ControlVirtualBrowserAsync(virtualBrowserId, control);
                
                if (result)
                {
                    await Clients.Group(browser.RoomId).VirtualBrowserControlUpdate(control);
                }
                else
                {
                    await Clients.Caller.Error("Failed to execute virtual browser control action.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error controlling virtual browser {VirtualBrowserId}", virtualBrowserId);
                await Clients.Caller.Error("An error occurred while controlling virtual browser.");
            }
        }

        #endregion

        #region Room Settings

        public async Task UpdateSyncMode(string roomId, string syncMode)
        {
            try
            {
                var userId = Context.UserIdentifier;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.Error("User not authenticated.");
                    return;
                }

                var room = await _roomService.GetRoomByIdAsync(roomId);
                if (room == null)
                {
                    await Clients.Caller.Error("Room not found.");
                    return;
                }

                if (room.AdminId != userId)
                {
                    await Clients.Caller.Error("Only room admin can change sync mode.");
                    return;
                }

                if (syncMode != "relaxed" && syncMode != "strict")
                {
                    await Clients.Caller.Error("Invalid sync mode. Must be 'relaxed' or 'strict'.");
                    return;
                }

                await _roomService.UpdateSyncModeAsync(roomId, syncMode);

                await Clients.Group(roomId).SyncModeChanged(syncMode);

                if (syncMode == "strict")
                {
                    await Clients.Group(roomId).ForceSyncPlayback(room.CurrentPosition, room.IsPlaying);
                }

                _logger.LogInformation("Room {RoomId} sync mode changed to {SyncMode} by {UserId}", roomId, syncMode, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync mode for room {RoomId}", roomId);
                await Clients.Caller.Error("An error occurred while updating sync mode.");
            }
        }

        #endregion
    }
}
