﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.BusinessLogic.Services.InMemory;
using StreamSync.DTOs;
using StreamSync.Models;

namespace StreamSync.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;
        private readonly InMemoryRoomManager _roomManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RoomController> _logger;

        public RoomController(
            IRoomService roomService,
            InMemoryRoomManager roomManager,
            UserManager<ApplicationUser> userManager,
            ILogger<RoomController> logger)
        {
            _roomService = roomService;
            _roomManager = roomManager;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveRooms([FromQuery] PaginationQueryDto? pagination)
        {
            if (pagination != null)
            {
                var pagedResult = await _roomService.GetActiveRoomsAsync(pagination);
                
                foreach (var room in pagedResult.Data)
                {
                    room.UserCount = _roomManager.GetParticipantCount(room.Id);
                }
                
                return Ok(pagedResult);
            }
            else
            {
                var rooms = await _roomService.GetActiveRoomsAsync();
                
                var enhancedRooms = rooms.Select(room => 
                {
                    room.UserCount = _roomManager.GetParticipantCount(room.Id);
                    return room;
                }).ToList();
                
                return Ok(enhancedRooms);
            }
        }

        [HttpGet("{roomId}/participants")]
        public async Task<IActionResult> GetRoomParticipants(string roomId)
        {
            var room = await _roomService.GetRoomByIdAsync(roomId);
            if (room == null)
            {
                return NotFound("Room not found");
            }

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

            return Ok(participantDtos);
        }

        [HttpGet("{roomId}/messages")]
        public async Task<IActionResult> GetRoomMessages(string roomId)
        {
            var room = await _roomService.GetRoomByIdAsync(roomId);
            if (room == null)
            {
                return NotFound("Room not found");
            }

            var messages = _roomManager.GetRoomMessages(roomId);
            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                AvatarUrl = m.AvatarUrl,
                Content = m.Content,
                SentAt = m.SentAt
            }).ToList();

            return Ok(messageDtos);
        }

        [Authorize]
        [HttpGet("my-rooms")]
        public async Task<IActionResult> GetUserRooms([FromQuery] PaginationQueryDto? pagination)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            
            if (pagination != null)
            {
                var pagedResult = await _roomService.GetUserRoomsAsync(userId, pagination);
                
                foreach (var room in pagedResult.Data)
                {
                    room.UserCount = _roomManager.GetParticipantCount(room.Id);
                }
                
                return Ok(pagedResult);
            }
            else
            {
                var rooms = await _roomService.GetUserRoomsAsync(userId);
                
                var enhancedRooms = rooms.Select(room => 
                {
                    room.UserCount = _roomManager.GetParticipantCount(room.Id);
                    return room;
                }).ToList();
                
                return Ok(enhancedRooms);
            }
        }

        [HttpGet("{roomId}")]
        public async Task<IActionResult> GetRoomById(string roomId)
        {
            var room = await _roomService.GetRoomByIdAsync(roomId);
            if (room == null)
            {
                return NotFound();
            }

            var admin = await _userManager.FindByIdAsync(room.AdminId);
            var adminName = admin?.DisplayName ?? admin?.UserName ?? "Unknown";

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

            var roomDetailDto = new RoomDetailDto
            {
                Id = room.Id,
                Name = room.Name,
                VideoUrl = room.VideoUrl,
                AdminId = room.AdminId,
                AdminName = adminName,
                IsActive = room.IsActive,
                CreatedAt = room.CreatedAt,
                InviteCode = room.InviteCode,
                IsPrivate = room.IsPrivate,
                HasPassword = !string.IsNullOrEmpty(room.PasswordHash),
                UserCount = participants.Count,
                CurrentPosition = room.CurrentPosition,
                IsPlaying = room.IsPlaying,
                SyncMode = room.SyncMode,
                AutoPlay = room.AutoPlay,
                Participants = participantDtos
            };

            return Ok(roomDetailDto);
        }

        [HttpGet("invite/{inviteCode}")]
        public async Task<IActionResult> GetRoomByInviteCode(string inviteCode)
        {
            var room = await _roomService.GetRoomByInviteCodeAsync(inviteCode);
            if (room == null)
            {
                return NotFound();
            }

            var admin = await _userManager.FindByIdAsync(room.AdminId);
            var adminName = admin?.DisplayName ?? admin?.UserName ?? "Unknown";
            
            return Ok(new
            {
                room.Id,
                room.Name,
                room.IsPrivate,
                RequiresPassword = !string.IsNullOrEmpty(room.PasswordHash),
                AdminName = adminName
            });
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateRoom([FromBody] RoomCreateDto roomDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var room = await _roomService.CreateRoomAsync(roomDto, userId);
            if (room == null)
            {
                return BadRequest("Unable to create room");
            }
            
            var inviteLink = await _roomService.GenerateInviteLink(room.Id);

            return Ok(new
            {
                room.Id,
                room.Name,
                room.InviteCode,
                InviteLink = inviteLink
            });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateRoom([FromBody] RoomUpdateDto roomDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _roomService.UpdateRoomAsync(roomDto, userId);
            if (!success)
            {
                return BadRequest("Unable to update room");
            }

            return Ok();
        }

        [Authorize]
        [HttpPut("{roomId}/sync-mode")]
        public async Task<IActionResult> UpdateSyncMode(string roomId, [FromBody] UpdateSyncModeDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var roomUpdateDto = new RoomUpdateDto
            {
                RoomId = roomId,
                SyncMode = request.SyncMode
            };

            var success = await _roomService.UpdateRoomAsync(roomUpdateDto, userId);
            if (!success)
            {
                return BadRequest("Unable to update sync mode");
            }

            return Ok();
        }

        [Authorize]
        [HttpPost("{roomId}/end")]
        public async Task<IActionResult> EndRoom(string roomId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _roomService.EndRoomAsync(roomId, userId);
            if (!success)
            {
                return BadRequest("Unable to end room");
            }

            return Ok();
        }

        [Authorize]
        [HttpPost("{roomId}/transfer-control")]
        public async Task<IActionResult> TransferControl(string roomId, [FromBody] TransferControlDto transferDto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            bool isAdmin = await _roomService.IsUserAdminAsync(roomId, currentUserId);
            if (!isAdmin)
            {
                return Forbid("Only room administrators can transfer control.");
            }

            var targetParticipant = _roomManager.GetParticipant(roomId, transferDto.NewControllerId);
            if (targetParticipant == null)
            {
                return BadRequest("Target participant not found in room.");
            }

            _roomManager.SetController(roomId, transferDto.NewControllerId);

            return Ok(new { message = "Control transferred successfully" });
        }

        [Authorize]
        [HttpPost("{roomId}/take-control")]
        public async Task<IActionResult> TakeControl(string roomId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            bool isAdmin = await _roomService.IsUserAdminAsync(roomId, userId);
            if (!isAdmin)
            {
                return Forbid("Only room administrators can take control.");
            }

            var adminParticipant = _roomManager.GetParticipant(roomId, userId);
            if (adminParticipant == null)
            {
                return BadRequest("You must be in the room to take control.");
            }

            _roomManager.SetController(roomId, userId);

            return Ok(new { message = "Control taken successfully" });
        }

    }

    public class TransferControlDto
    {
        public required string NewControllerId { get; set; }
    }
}
