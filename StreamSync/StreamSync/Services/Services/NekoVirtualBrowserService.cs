using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using StreamSync.BusinessLogic.Interfaces;
using StreamSync.BusinessLogic.Services.InMemory;
using StreamSync.Common;
using StreamSync.Data;
using StreamSync.DTOs;
using StreamSync.Hubs;
using StreamSync.Models;

namespace StreamSync.BusinessLogic.Services
{
    public class NekoVirtualBrowserService : IVirtualBrowserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IContainerPoolService _poolService;
        private readonly INekoContainerService _nekoService;
        private readonly IVirtualBrowserQueueService _queueService;
        private readonly IHubContext<RoomHub, IRoomClient> _hubContext;
        private readonly InMemoryRoomManager _roomManager;
        private readonly ILogger<NekoVirtualBrowserService> _logger;
        private const int MAX_CONTAINERS = 1; // Maximum number of Neko containers
        private const int SESSION_DURATION_HOURS = 3; // 3 hour limit from allocation time
        private readonly Random _random = new();
        private readonly SemaphoreSlim _queueProcessingSemaphore = new(1, 1); // Prevent concurrent queue processing

        public NekoVirtualBrowserService(
            IUnitOfWork unitOfWork,
            IContainerPoolService poolService,
            INekoContainerService nekoService,
            IVirtualBrowserQueueService queueService,
            IHubContext<RoomHub, IRoomClient> hubContext,
            InMemoryRoomManager roomManager,
            ILogger<NekoVirtualBrowserService> logger)
        {
            _unitOfWork = unitOfWork;
            _poolService = poolService;
            _nekoService = nekoService;
            _queueService = queueService;
            _hubContext = hubContext;
            _roomManager = roomManager;
            _logger = logger;
        }

        public async Task<bool> InitializePoolAsync()
        {
            try
            {
                _logger.LogInformation("Initializing virtual browser pool...");
                if (!await _nekoService.InitializeContainersAsync())
                {
                    _logger.LogError("Failed to initialize Neko container service");
                    return false;
                }

                if (!await _poolService.InitializePoolAsync())
                {
                    _logger.LogError("Failed to initialize container pool");
                    return false;
                }

                await CleanupOrphanedBrowsersAsync();

                _logger.LogInformation("Virtual browser pool initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing virtual browser pool");
                return false;
            }
        }

        public async Task<VirtualBrowserDto?> RequestVirtualBrowserAsync(string roomId)
        {
            try
            {
                _logger.LogInformation("Requesting virtual browser for room {RoomId}", roomId);
                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room?.LastVirtualBrowserReleasedAt != null)
                {
                    var timeSinceLastRelease = DateTime.UtcNow - room.LastVirtualBrowserReleasedAt.Value;
                    var cooldownPeriod = TimeSpan.FromMinutes(2);
                    
                    if (timeSinceLastRelease < cooldownPeriod)
                    {
                        var remainingCooldown = cooldownPeriod - timeSinceLastRelease;
                        _logger.LogInformation("Room {RoomId} is still on cooldown for {RemainingSeconds} seconds", 
                            roomId, remainingCooldown.TotalSeconds);
                        
                        throw new InvalidOperationException(
                            $"Please wait {Math.Ceiling(remainingCooldown.TotalMinutes)} more minute(s) before requesting another virtual browser");
                    }
                }

                var existingBrowser = await _unitOfWork.VirtualBrowsers.GetByRoomIdAsync(roomId);
                if (existingBrowser != null && existingBrowser.Status == VirtualBrowserStatus.Allocated)
                {
                    _logger.LogInformation("Room {RoomId} already has virtual browser {BrowserId}", roomId, existingBrowser.Id);
                    return MapToDto(existingBrowser);
                }

                var existingQueueEntry = await _queueService.GetQueueStatusAsync(roomId);
                if (existingQueueEntry != null)
                {
                    _logger.LogInformation("Room {RoomId} is already in queue at position {Position}", roomId, existingQueueEntry.Position);
                    return null;
                }

                var queueLength = _queueService.GetQueueLength();
                if (queueLength > 0)
                {
                    _logger.LogInformation("There are {QueueLength} rooms waiting, adding room {RoomId} to queue for fairness", queueLength, roomId);
                    var position = await _queueService.AddToQueueAsync(roomId);
                    
                    var queueStatus = await _queueService.GetQueueStatusAsync(roomId);
                    if (queueStatus != null)
                    {
                        await _hubContext.Clients.Group(roomId).VirtualBrowserQueued(queueStatus);
                        _logger.LogInformation("Notified all participants in room {RoomId} about queue position {Position}", roomId, position);
                    }
                    
                    return null;
                }

                var containerFromPool = await _poolService.AllocateContainerAsync();
                if (containerFromPool == null)
                {
                    _logger.LogWarning("No containers available in pool, adding room {RoomId} to queue", roomId);
                    var position = await _queueService.AddToQueueAsync(roomId);
                    
                    var queueStatus = await _queueService.GetQueueStatusAsync(roomId);
                    if (queueStatus != null)
                    {
                        await _hubContext.Clients.Group(roomId).VirtualBrowserQueued(queueStatus);
                        _logger.LogInformation("Notified all participants in room {RoomId} about queue position {Position}", roomId, position);
                    }
                    
                    return null;
                }

                var virtualBrowser = new VirtualBrowser
                {
                    RoomId = roomId,
                    ContainerId = containerFromPool.ContainerId,
                    ContainerName = containerFromPool.ContainerName,
                    BrowserUrl = containerFromPool.BrowserUrl,
                    WebRtcUrl = containerFromPool.WebRtcUrl,
                    ContainerIndex = containerFromPool.ContainerIndex,
                    HttpPort = containerFromPool.HttpPort,
                    UdpPortStart = containerFromPool.UdpPortStart,
                    UdpPortEnd = containerFromPool.UdpPortEnd,
                    Status = VirtualBrowserStatus.Allocated,
                    CreatedAt = DateTime.UtcNow,
                    AllocatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(SESSION_DURATION_HOURS), DateTimeKind.Utc),
                    NekoPassword = containerFromPool.NekoPassword,
                    NekoAdminPassword = containerFromPool.NekoAdminPassword
                };

                await _unitOfWork.VirtualBrowsers.CreateAsync(virtualBrowser);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Allocated virtual browser {BrowserId} to room {RoomId} on container {ContainerIndex}", 
                    virtualBrowser.Id, roomId, virtualBrowser.ContainerIndex);

                if (room != null && !string.IsNullOrEmpty(room.VideoUrl))
                {
                    _logger.LogInformation("Clearing video URL for room {RoomId} as virtual browser is being allocated", roomId);
                    room.VideoUrl = string.Empty;
                    room.IsPlaying = false;
                    room.CurrentPosition = 0;
                    _unitOfWork.Rooms.Update(room);
                    await _unitOfWork.SaveAsync();

                    await _hubContext.Clients.Group(roomId)
                        .VideoChanged(string.Empty, "Virtual browser mode activated", null);
                }

                await _hubContext.Clients.Group(roomId)
                    .VirtualBrowserAllocated(MapToDto(virtualBrowser));

                return MapToDto(virtualBrowser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting virtual browser for room {RoomId}", roomId);
                return null;
            }
        }

        private async Task<VirtualBrowserDto?> AllocateVirtualBrowserForQueueAcceptance(string roomId)
        {
            try
            {
                _logger.LogInformation("Allocating virtual browser for room {RoomId} from queue acceptance", roomId);

                var existingBrowser = await _unitOfWork.VirtualBrowsers.GetByRoomIdAsync(roomId);
                if (existingBrowser != null && existingBrowser.Status == VirtualBrowserStatus.Allocated)
                {
                    _logger.LogInformation("Room {RoomId} already has virtual browser {BrowserId}", roomId, existingBrowser.Id);
                    return MapToDto(existingBrowser);
                }

                var containerFromPool = await _poolService.AllocateContainerAsync();
                if (containerFromPool == null)
                {
                    _logger.LogError("No containers available for queue acceptance - this should not happen for room {RoomId}", roomId);
                    return null;
                }

                var virtualBrowser = new VirtualBrowser
                {
                    RoomId = roomId,
                    ContainerId = containerFromPool.ContainerId,
                    ContainerName = containerFromPool.ContainerName,
                    BrowserUrl = containerFromPool.BrowserUrl,
                    WebRtcUrl = containerFromPool.WebRtcUrl,
                    ContainerIndex = containerFromPool.ContainerIndex,
                    HttpPort = containerFromPool.HttpPort,
                    UdpPortStart = containerFromPool.UdpPortStart,
                    UdpPortEnd = containerFromPool.UdpPortEnd,
                    Status = VirtualBrowserStatus.Allocated,
                    CreatedAt = DateTime.UtcNow,
                    AllocatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(SESSION_DURATION_HOURS), DateTimeKind.Utc),
                    NekoPassword = containerFromPool.NekoPassword,
                    NekoAdminPassword = containerFromPool.NekoAdminPassword
                };

                await _unitOfWork.VirtualBrowsers.CreateAsync(virtualBrowser);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully allocated virtual browser {BrowserId} for room {RoomId} from queue acceptance", 
                    virtualBrowser.Id, roomId);

                await _hubContext.Clients.Group(roomId)
                    .VirtualBrowserAllocated(MapToDto(virtualBrowser));

                return MapToDto(virtualBrowser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating virtual browser for queue acceptance for room {RoomId}", roomId);
                return null;
            }
        }

        public async Task<bool> ReleaseVirtualBrowserAsync(string roomId)
        {
            try
            {
                _logger.LogInformation("Releasing virtual browser for room {RoomId}", roomId);

                var browser = await _unitOfWork.VirtualBrowsers.GetByRoomIdAsync(roomId);
                if (browser == null)
                {
                    _logger.LogWarning("No virtual browser found for room {RoomId}", roomId);
                    return false;
                }

                var containerId = browser.ContainerId;
                var browserId = browser.Id;

                try
                {
                    await _unitOfWork.VirtualBrowsers.DeleteAsync(browser.Id);
                    
                    var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                    if (room != null)
                    {
                        room.LastVirtualBrowserReleasedAt = DateTime.UtcNow;
                        _unitOfWork.Rooms.Update(room);
                    }
                    
                    await _unitOfWork.SaveAsync();
                    _logger.LogInformation("Removed virtual browser {BrowserId} from database for room {RoomId} and updated cooldown", browserId, roomId);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Virtual browser {BrowserId} was already removed from database for room {RoomId}", browserId, roomId);
                }

                await _hubContext.Clients.Group(roomId)
                    .VirtualBrowserReleased();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(2000);
                        
                        await _poolService.ReturnContainerToPoolAsync(containerId);
                        _logger.LogInformation("Returned container {ContainerId} to pool for room {RoomId}", containerId, roomId);
                        
                        await ProcessQueueAsync();
                        _logger.LogInformation("Processed queue after container {ContainerId} was returned to pool", containerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error returning container {ContainerId} to pool for room {RoomId}", containerId, roomId);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing virtual browser for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<VirtualBrowserDto?> GetRoomVirtualBrowserAsync(string roomId)
        {
            try
            {
                var browser = await _unitOfWork.VirtualBrowsers.GetByRoomIdAsync(roomId);
                return browser != null ? MapToDto(browser) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual browser for room {RoomId}", roomId);
                return null;
            }
        }

        public async Task<VirtualBrowserDto?> GetVirtualBrowserAsync(string virtualBrowserId)
        {
            try
            {
                var browser = await _unitOfWork.VirtualBrowsers.GetByIdAsync(virtualBrowserId);
                return browser != null ? MapToDto(browser) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual browser {VirtualBrowserId}", virtualBrowserId);
                return null;
            }
        }

        public async Task<object> GetRoomCooldownStatusAsync(string roomId)
        {
            try
            {
                var room = await _unitOfWork.Rooms.GetByIdAsync(roomId);
                if (room?.LastVirtualBrowserReleasedAt == null)
                {
                    return new { isOnCooldown = false, remainingSeconds = 0 };
                }

                var timeSinceLastRelease = DateTime.UtcNow - room.LastVirtualBrowserReleasedAt.Value;
                var cooldownPeriod = TimeSpan.FromMinutes(2);
                
                if (timeSinceLastRelease >= cooldownPeriod)
                {
                    return new { isOnCooldown = false, remainingSeconds = 0 };
                }

                var remainingTime = cooldownPeriod - timeSinceLastRelease;
                return new { 
                    isOnCooldown = true, 
                    remainingSeconds = (int)Math.Ceiling(remainingTime.TotalSeconds) 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cooldown status for room {RoomId}", roomId);
                return new { isOnCooldown = false, remainingSeconds = 0 };
            }
        }

        public async Task<VirtualBrowserQueueDto?> GetRoomQueueStatusAsync(string roomId)
        {
            try
            {
                return await _queueService.GetQueueStatusAsync(roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue status for room {RoomId}", roomId);
                return null;
            }
        }

        public async Task<bool> AcceptQueueNotificationAsync(string roomId)
        {
            try
            {
                var accepted = await _queueService.AcceptNotificationAsync(roomId);
                if (accepted)
                {
                    var browser = await AllocateVirtualBrowserForQueueAcceptance(roomId);
                    return browser != null;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting queue notification for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> DeclineQueueNotificationAsync(string roomId)
        {
            try
            {
                var declined = await _queueService.DeclineNotificationAsync(roomId);
                if (declined)
                {
                    _logger.LogInformation("Room {RoomId} declined queue notification and was removed from queue", roomId);
                    
                    // Notify all clients in the room that the queue was cancelled/declined
                    await _hubContext.Clients.Group(roomId).VirtualBrowserQueueCancelled();
                    
                    // Process the next item in queue
                    await ProcessQueueAsync();
                }
                return declined;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining queue notification for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> CancelQueueAsync(string roomId)
        {
            try
            {
                var removed = await _queueService.RemoveFromQueueAsync(roomId);
                if (removed)
                {
                    _logger.LogInformation("Room {RoomId} was removed from virtual browser queue", roomId);
                    
                    // Notify all clients in the room that the queue was cancelled
                    await _hubContext.Clients.Group(roomId).VirtualBrowserQueueCancelled();
                }
                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling queue for room {RoomId}", roomId);
                return false;
            }
        }

        public async Task<bool> NavigateVirtualBrowserAsync(string virtualBrowserId, string url)
        {
            try
            {
                var browser = await _unitOfWork.VirtualBrowsers.GetByIdAsync(virtualBrowserId);
                if (browser == null)
                {
                    return false;
                }

                // Update last accessed URL
                browser.LastAccessedUrl = url;
                _unitOfWork.VirtualBrowsers.Update(browser);
                await _unitOfWork.SaveAsync();

                // TODO: Implement Neko API call to navigate browser
                // This would require making HTTP requests to the Neko container's API
                
                _logger.LogInformation("Navigated virtual browser {BrowserId} to {Url}", virtualBrowserId, url);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating virtual browser {BrowserId}", virtualBrowserId);
                return false;
            }
        }

        public async Task<bool> ControlVirtualBrowserAsync(string virtualBrowserId, VirtualBrowserControlDto controlRequest)
        {
            try
            {
                var browser = await _unitOfWork.VirtualBrowsers.GetByIdAsync(virtualBrowserId);
                if (browser == null)
                {
                    return false;
                }

                // TODO: Implement Neko WebSocket control commands
                // This would require WebSocket communication with the Neko container
                
                _logger.LogInformation("Executed control action {Action} on virtual browser {BrowserId}", 
                    controlRequest.Action, virtualBrowserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error controlling virtual browser {BrowserId}", virtualBrowserId);
                return false;
            }
        }

        public async Task<List<VirtualBrowserDto>> GetAllVirtualBrowsersAsync()
        {
            try
            {
                var browsers = await _unitOfWork.VirtualBrowsers.GetAllAsync();
                return browsers.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all virtual browsers");
                return new List<VirtualBrowserDto>();
            }
        }

        public async Task<List<VirtualBrowserQueueDto>> GetQueueStatusAsync()
        {
            try
            {
                return await _queueService.GetAllQueueStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue status");
                return new List<VirtualBrowserQueueDto>();
            }
        }

        public async Task ProcessExpiredSessionsAsync()
        {
            await CleanupExpiredBrowsersAsync();
        }

        public async Task ProcessQueueNotificationsAsync()
        {
            // Process expired notifications
            var hadExpiredNotifications = await _queueService.ProcessExpiredNotificationsAsync();
            
            // If notifications expired and there are available containers, 
            // trigger queue processing to notify the next person
            if (hadExpiredNotifications)
            {
                var availableCount = await _poolService.GetAvailableCountAsync();
                if (availableCount > 0)
                {
                    _logger.LogInformation("Notifications expired and containers available, processing queue");
                    await ProcessQueueAsync();
                }
            }
            
            _logger.LogDebug("Processed expired queue notifications");
        }

        public async Task<bool> CleanupExpiredBrowsersAsync()
        {
            try
            {
                var expiredBrowsers = await _unitOfWork.VirtualBrowsers.GetExpiredAsync();
                foreach (var browser in expiredBrowsers)
                {
                    _logger.LogInformation("Cleaning up expired virtual browser {BrowserId} for room {RoomId}", 
                        browser.Id, browser.RoomId);

                    // Return container to pool instead of destroying it
                    await _poolService.ReturnContainerToPoolAsync(browser.ContainerId);
                    
                    // Remove from database
                    await _unitOfWork.VirtualBrowsers.DeleteAsync(browser.Id);

                    if (!string.IsNullOrEmpty(browser.RoomId))
                    {
                        await _hubContext.Clients.Group(browser.RoomId)
                            .VirtualBrowserExpired();
                    }
                }

                if (expiredBrowsers.Any())
                {
                    await _unitOfWork.SaveAsync();
                    await ProcessQueueAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired browsers");
                return false;
            }
        }

        private async Task ProcessQueueAsync()
        {
            // Use semaphore to prevent concurrent queue processing
            if (!await _queueProcessingSemaphore.WaitAsync(100)) // 100ms timeout
            {
                _logger.LogDebug("Queue processing already in progress, skipping");
                return;
            }

            try
            {
                var availableCount = await _poolService.GetAvailableCountAsync();
                _logger.LogDebug("Available containers: {AvailableCount}", availableCount);
                
                if (availableCount > 0)
                {
                    var nextRoomId = await _queueService.GetNextInQueueAsync();
                    if (!string.IsNullOrEmpty(nextRoomId))
                    {
                        _logger.LogInformation("Processing queue: Attempting to notify room {RoomId}", nextRoomId);
                        
                        var notified = await _queueService.NotifyRoomAsync(nextRoomId);
                        if (notified)
                        {
                            // Get the updated queue status to send to client
                            var queueStatus = await _queueService.GetQueueStatusAsync(nextRoomId);
                            if (queueStatus != null)
                            {
                                // Get the room controller and send notification only to them
                                var controller = _roomManager.GetController(nextRoomId);
                                if (controller != null)
                                {
                                    _logger.LogInformation("Notifying room controller {ControllerId} in room {RoomId} about available virtual browser (Position: {Position})", 
                                        controller.Id, nextRoomId, queueStatus.Position);
                                    await _hubContext.Clients.Client(controller.ConnectionId).VirtualBrowserAvailable(queueStatus);
                                }
                                else
                                {
                                    _logger.LogWarning("No controller found for room {RoomId}, sending notification to entire room group", nextRoomId);
                                    await _hubContext.Clients.Group(nextRoomId).VirtualBrowserAvailable(queueStatus);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to notify room {RoomId} - room may have been removed from queue", nextRoomId);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No rooms in queue to notify");
                    }
                }
                else
                {
                    _logger.LogDebug("No available containers for queue processing");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queue");
            }
            finally
            {
                _queueProcessingSemaphore.Release();
            }
        }

        private async Task CleanupOrphanedBrowsersAsync()
        {
            try
            {
                var allBrowsers = await _unitOfWork.VirtualBrowsers.GetAllAsync();
                var runningContainers = await _nekoService.GetAllRunningContainersAsync();

                foreach (var browser in allBrowsers)
                {
                    var containerExists = runningContainers.Values.Any(c => c.ContainerId == browser.ContainerId);
                    if (!containerExists)
                    {
                        _logger.LogInformation("Cleaning up orphaned virtual browser {BrowserId}", browser.Id);
                        await _unitOfWork.VirtualBrowsers.DeleteAsync(browser.Id);
                    }
                }

                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up orphaned browsers");
            }
        }

        private VirtualBrowserDto MapToDto(VirtualBrowser browser)
        {
            var timeRemaining = browser.ExpiresAt > DateTime.UtcNow ? 
                browser.ExpiresAt - DateTime.UtcNow : 
                TimeSpan.Zero;

            // Only mark as expired if the browser is actually past expiration AND still exists in DB
            // If it exists in DB and hasn't expired yet, keep original status
            var actualStatus = browser.Status.ToString();

            return new VirtualBrowserDto
            {
                Id = browser.Id,
                RoomId = browser.RoomId ?? string.Empty,
                ContainerId = browser.ContainerId,
                ContainerName = browser.ContainerName,
                BrowserUrl = browser.BrowserUrl,
                WebRtcUrl = browser.WebRtcUrl,
                ContainerIndex = browser.ContainerIndex,
                HttpPort = browser.HttpPort,
                UdpPortStart = browser.UdpPortStart,
                UdpPortEnd = browser.UdpPortEnd,
                Status = actualStatus,
                CreatedAt = browser.CreatedAt,
                AllocatedAt = browser.AllocatedAt,
                DeallocatedAt = browser.DeallocatedAt,
                ExpiresAt = DateTime.SpecifyKind(browser.ExpiresAt, DateTimeKind.Utc),
                LastAccessedUrl = browser.LastAccessedUrl,
                NekoPassword = browser.NekoPassword,
                NekoAdminPassword = browser.NekoAdminPassword,
                TimeRemaining = timeRemaining
            };
        }

        private async Task<ContainerInfo?> GetContainerInfoByIdAsync(string containerId)
        {
            // Get all running containers and find the one with matching ID
            var allContainers = await _nekoService.GetAllRunningContainersAsync();
            return allContainers.Values.FirstOrDefault(c => c.ContainerId == containerId);
        }

        private int GetContainerIndexFromName(string containerName)
        {
            // Extract index from container name like "neko-browser-0"
            if (containerName.StartsWith("neko-browser-"))
            {
                var indexStr = containerName.Substring("neko-browser-".Length);
                if (int.TryParse(indexStr, out var index))
                {
                    return index;
                }
            }
            return -1;
        }

        private string GenerateRandomPassword(int length = 12)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public async Task<bool> RestartBrowserProcessAsync(string virtualBrowserId)
        {
            try
            {
                _logger.LogInformation("Restarting browser process for virtual browser {VirtualBrowserId}", virtualBrowserId);

                // Get virtual browser from database
                var virtualBrowser = await _unitOfWork.VirtualBrowsers.GetByIdAsync(virtualBrowserId);
                if (virtualBrowser == null)
                {
                    _logger.LogWarning("Virtual browser not found: {VirtualBrowserId}", virtualBrowserId);
                    return false;
                }

                if (string.IsNullOrEmpty(virtualBrowser.ContainerId))
                {
                    _logger.LogWarning("No container ID found for virtual browser: {VirtualBrowserId}", virtualBrowserId);
                    return false;
                }

                // Use the new fast restart method instead of full container restart
                var success = await _nekoService.RestartBrowserProcessAsync(virtualBrowser.ContainerId);

                if (success)
                {
                    _logger.LogInformation("Successfully restarted browser process for virtual browser {VirtualBrowserId}", virtualBrowserId);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to restart browser process for virtual browser {VirtualBrowserId}", virtualBrowserId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting browser process for virtual browser {VirtualBrowserId}", virtualBrowserId);
                return false;
            }
        }
    }
}
