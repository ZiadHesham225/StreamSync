using Microsoft.AspNetCore.Identity;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.BusinessLogic.Services.InMemory;
using StreamSync.Data;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.BusinessLogic.Services
{
    public class RoomService : IRoomService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IVirtualBrowserQueueService _queueService;
        private readonly InMemoryRoomManager _roomManager;
        private readonly ILogger<RoomService> _logger;

        public RoomService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IVirtualBrowserQueueService queueService,
            InMemoryRoomManager roomManager,
            ILogger<RoomService> logger)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _queueService = queueService;
            _roomManager = roomManager;
            _logger = logger;
        }

        public async Task<Room> CreateRoomAsync(RoomCreateDto roomDto, string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomDto.Name))
                {
                    _logger.LogWarning("Room creation failed: Room name is required");
                    return null;
                }

                var room = new Room
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roomDto.Name.Trim(),
                    VideoUrl = roomDto.VideoUrl ?? "",
                    AdminId = userId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    InviteCode = GenerateInviteCode(),
                    IsPrivate = roomDto.IsPrivate,
                    AutoPlay = roomDto.AutoPlay,
                    SyncMode = roomDto.SyncMode ?? "strict",
                    CurrentPosition = 0,
                    IsPlaying = false
                };

                if (roomDto.IsPrivate && !string.IsNullOrEmpty(roomDto.Password))
                {
                    room.PasswordHash = BCrypt.Net.BCrypt.HashPassword(roomDto.Password);
                }

                await _unitOfWork.Rooms.CreateAsync(room);
                await _unitOfWork.SaveAsync();

                return room;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room");
                return null;
            }
        }

        public async Task<Room> GetRoomByIdAsync(string roomId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return null;
                }

                return await _unitOfWork.Rooms.GetByIdAsync(roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room by ID: {RoomId}", roomId);
                return null;
            }
        }

        public async Task<Room> GetRoomByInviteCodeAsync(string inviteCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inviteCode))
                {
                    return null;
                }

                return await _unitOfWork.Rooms.GetRoomByInviteCodeAsync(inviteCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room by invite code: {InviteCode}", inviteCode);
                return null;
            }
        }

        public async Task<IEnumerable<RoomDto>> GetActiveRoomsAsync()
        {
            try
            {
                // Use repository method that includes Admin navigation property to avoid N+1 queries
                var activeRooms = await _unitOfWork.Rooms.GetActiveRoomsAsync();
                
                var result = activeRooms.Select(room => new RoomDto
                {
                    Id = room.Id,
                    Name = room.Name,
                    VideoUrl = room.VideoUrl,
                    AdminId = room.AdminId,
                    AdminName = room.Admin?.DisplayName ?? room.Admin?.UserName ?? "Unknown",
                    IsActive = room.IsActive,
                    CreatedAt = room.CreatedAt,
                    InviteCode = room.InviteCode,
                    IsPrivate = room.IsPrivate,
                    HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                    UserCount = 0
                }).ToList();
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active rooms");
                return Enumerable.Empty<RoomDto>();
            }
        }

        public async Task<PagedResultDto<RoomDto>> GetActiveRoomsAsync(PaginationQueryDto pagination)
        {
            try
            {
                pagination.Validate();

                // Use repository method that includes Admin navigation property to avoid N+1 queries
                var activeRooms = await _unitOfWork.Rooms.GetActiveRoomsAsync();
                var query = activeRooms.AsQueryable();

                if (!string.IsNullOrWhiteSpace(pagination.Search))
                {
                    var searchLower = pagination.Search.ToLower();
                    query = query.Where(r => r.Name.ToLower().Contains(searchLower));
                }

                query = pagination.SortBy?.ToLower() switch
                {
                    "name" => pagination.SortOrder?.ToLower() == "asc" 
                        ? query.OrderBy(r => r.Name) 
                        : query.OrderByDescending(r => r.Name),
                    "createdat" => pagination.SortOrder?.ToLower() == "asc" 
                        ? query.OrderBy(r => r.CreatedAt) 
                        : query.OrderByDescending(r => r.CreatedAt),
                    _ => query.OrderByDescending(r => r.CreatedAt)
                };

                var totalCount = query.Count();

                var rooms = query
                    .Skip((pagination.Page - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList();

                // Map directly from rooms with included Admin - no additional queries needed
                var result = rooms.Select(room => new RoomDto
                {
                    Id = room.Id,
                    Name = room.Name,
                    VideoUrl = room.VideoUrl,
                    AdminId = room.AdminId,
                    AdminName = room.Admin?.DisplayName ?? room.Admin?.UserName ?? "Unknown",
                    IsActive = room.IsActive,
                    CreatedAt = room.CreatedAt,
                    InviteCode = room.InviteCode,
                    IsPrivate = room.IsPrivate,
                    HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                    UserCount = 0
                }).ToList();

                var totalPages = (int)Math.Ceiling((double)totalCount / pagination.PageSize);

                return new PagedResultDto<RoomDto>
                {
                    Data = result,
                    CurrentPage = pagination.Page,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasNext = pagination.Page < totalPages,
                    HasPrevious = pagination.Page > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active rooms with pagination");
                return new PagedResultDto<RoomDto>();
            }
        }

        public async Task<IEnumerable<RoomDto>> GetUserRoomsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Enumerable.Empty<RoomDto>();
                }

                // Use repository method that includes Admin navigation property to avoid N+1 queries
                var userRooms = await _unitOfWork.Rooms.GetActiveRoomsByAdminAsync(userId);
                
                var result = userRooms.Select(room => new RoomDto
                {
                    Id = room.Id,
                    Name = room.Name,
                    VideoUrl = room.VideoUrl,
                    AdminId = room.AdminId,
                    AdminName = room.Admin?.DisplayName ?? room.Admin?.UserName ?? "Unknown",
                    IsActive = room.IsActive,
                    CreatedAt = room.CreatedAt,
                    InviteCode = room.InviteCode,
                    IsPrivate = room.IsPrivate,
                    HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                    UserCount = 0
                }).ToList();
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rooms for user: {UserId}", userId);
                return Enumerable.Empty<RoomDto>();
            }
        }

        public async Task<PagedResultDto<RoomDto>> GetUserRoomsAsync(string userId, PaginationQueryDto pagination)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return new PagedResultDto<RoomDto>();
                }

                pagination.Validate();

                // Use repository method that includes Admin navigation property to avoid N+1 queries
                var userRooms = await _unitOfWork.Rooms.GetActiveRoomsByAdminAsync(userId);
                var query = userRooms.AsQueryable();

                if (!string.IsNullOrWhiteSpace(pagination.Search))
                {
                    var searchLower = pagination.Search.ToLower();
                    query = query.Where(r => r.Name.ToLower().Contains(searchLower));
                }

                query = pagination.SortBy?.ToLower() switch
                {
                    "name" => pagination.SortOrder?.ToLower() == "asc" 
                        ? query.OrderBy(r => r.Name) 
                        : query.OrderByDescending(r => r.Name),
                    "createdat" => pagination.SortOrder?.ToLower() == "asc" 
                        ? query.OrderBy(r => r.CreatedAt) 
                        : query.OrderByDescending(r => r.CreatedAt),
                    _ => query.OrderByDescending(r => r.CreatedAt)
                };

                var totalCount = query.Count();

                var rooms = query
                    .Skip((pagination.Page - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList();

                // Map directly from rooms with included Admin - no additional queries needed
                var result = rooms.Select(room => new RoomDto
                {
                    Id = room.Id,
                    Name = room.Name,
                    VideoUrl = room.VideoUrl,
                    AdminId = room.AdminId,
                    AdminName = room.Admin?.DisplayName ?? room.Admin?.UserName ?? "Unknown",
                    IsActive = room.IsActive,
                    CreatedAt = room.CreatedAt,
                    InviteCode = room.InviteCode,
                    IsPrivate = room.IsPrivate,
                    HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                    UserCount = 0
                }).ToList();

                var totalPages = (int)Math.Ceiling((double)totalCount / pagination.PageSize);

                return new PagedResultDto<RoomDto>
                {
                    Data = result,
                    CurrentPage = pagination.Page,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasNext = pagination.Page < totalPages,
                    HasPrevious = pagination.Page > 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rooms with pagination for user: {UserId}", userId);
                return new PagedResultDto<RoomDto>();
            }
        }

        public async Task<bool> UpdateRoomAsync(RoomUpdateDto roomDto, string userId)
        {
            try
            {
                if (roomDto == null || string.IsNullOrWhiteSpace(roomDto.RoomId) || string.IsNullOrWhiteSpace(userId))
                {
                    return false;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomDto.RoomId);
                if (room == null || room.AdminId != userId || !room.IsActive)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(roomDto.Name))
                {
                    room.Name = roomDto.Name.Trim();
                }

                if (!string.IsNullOrWhiteSpace(roomDto.VideoUrl))
                {
                    room.VideoUrl = roomDto.VideoUrl;
                }

                if (roomDto.IsPrivate.HasValue)
                {
                    room.IsPrivate = roomDto.IsPrivate.Value;
                }

                if (roomDto.AutoPlay.HasValue)
                {
                    room.AutoPlay = roomDto.AutoPlay.Value;
                }

                if (!string.IsNullOrWhiteSpace(roomDto.SyncMode))
                {
                    room.SyncMode = roomDto.SyncMode;
                }

                if (room.IsPrivate && !string.IsNullOrEmpty(roomDto.Password))
                {
                    room.PasswordHash = BCrypt.Net.BCrypt.HashPassword(roomDto.Password);
                }
                else if (!room.IsPrivate)
                {
                    room.PasswordHash = null;
                }

                _unitOfWork.Rooms.Update(room);
                await _unitOfWork.SaveAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room: {RoomId}", roomDto?.RoomId);
                return false;
            }
        }

        public async Task<bool> UpdateRoomVideoAsync(string roomId, string videoUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(videoUrl))
                {
                    return false;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    return false;
                }

                room.VideoUrl = videoUrl;
                room.CurrentPosition = 0;
                room.IsPlaying = false;

                _unitOfWork.Rooms.Update(room);
                await _unitOfWork.SaveAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room video: {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> EndRoomAsync(string roomId, string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                {
                    return false;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null || room.AdminId != userId || !room.IsActive)
                {
                    return false;
                }

                room.IsActive = false;
                room.EndedAt = DateTime.UtcNow;
                _unitOfWork.Rooms.Update(room);
                
                var virtualBrowser = await _unitOfWork.VirtualBrowsers.GetByRoomIdAsync(roomId);
                if (virtualBrowser != null)
                {
                    virtualBrowser.Status = Models.VirtualBrowserStatus.Available;
                    virtualBrowser.RoomId = null;
                    virtualBrowser.DeallocatedAt = DateTime.UtcNow;
                    virtualBrowser.BrowserState = null;
                    virtualBrowser.LastAccessedUrl = null;
                    _unitOfWork.VirtualBrowsers.Update(virtualBrowser);
                    
                    _logger.LogInformation("Released virtual browser {BrowserId} due to room {RoomId} ending", virtualBrowser.Id, roomId);
                }

                await _queueService.RemoveFromQueueAsync(roomId);

                await _unitOfWork.SaveAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending room: {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> UpdatePlaybackStateAsync(string roomId, string userId, double position, bool isPlaying)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || position < 0)
                {
                    return false;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    return false;
                }

                room.CurrentPosition = position;
                room.IsPlaying = isPlaying;

                _unitOfWork.Rooms.Update(room);
                await _unitOfWork.SaveAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playback state: Room: {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> ValidateRoomPasswordAsync(string roomId, string? password)
        {
            try
            {
                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null)
                {
                    return false;
                }

                if (!room.IsPrivate)
                {
                    return true;
                }

                if (string.IsNullOrEmpty(room.PasswordHash))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(password))
                {
                    return BCrypt.Net.BCrypt.Verify(password, room.PasswordHash);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating room password: {RoomId}", roomId);
                return false;
            }
        }

        public async Task<string> GenerateInviteLink(string roomId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return null;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null || !room.IsActive)
                {
                    return null;
                }
                return $"/watch-party/join/{room.InviteCode}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invite link for room: {RoomId}", roomId);
                return null;
            }
        }

        public async Task<bool> IsUserAdminAsync(string roomId, string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                {
                    return false;
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                return room != null && room.AdminId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user is admin: {RoomId}, User: {UserId}", roomId, userId);
                return false;
            }
        }

        public async Task<bool> CanUserControlRoomAsync(string roomId, string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                {
                    return false;
                }

                var isAdmin = await IsUserAdminAsync(roomId, userId);
                if (isAdmin)
                {
                    return true;
                }

                var participant = _roomManager.GetParticipant(roomId, userId);
                return participant != null && participant.HasControl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user can control room: {RoomId}, User: {UserId}", roomId, userId);
                return false;
            }
        }

        #region Helper Methods

        private string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #endregion

        #region Sync Mode Management

        public async Task<bool> UpdateSyncModeAsync(string roomId, string syncMode)
        {
            try
            {
                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room == null)
                {
                    _logger.LogWarning("Room not found: {RoomId}", roomId);
                    return false;
                }

                room.SyncMode = syncMode;
                _unitOfWork.GenericRooms.Update(room);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Room {RoomId} sync mode updated to {SyncMode}", roomId, syncMode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync mode for room {RoomId}", roomId);
                return false;
            }
        }

        #endregion
    }
}

