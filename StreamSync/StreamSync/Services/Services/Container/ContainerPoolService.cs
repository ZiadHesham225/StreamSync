using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using StreamSync.Services.Interfaces;
using StreamSync.Common;

namespace StreamSync.Services
{
    public class ContainerPoolService : IContainerPoolService, IDisposable
    {
        private readonly INekoContainerService _containerService;
        private readonly IContainerHealthService _healthService;
        private readonly ILogger<ContainerPoolService> _logger;
        private readonly IConfiguration _configuration;

        private int POOL_SIZE => _configuration.GetValue<int>("MaxContainers", 2);

        private readonly ConcurrentQueue<ContainerInfo> _availableContainers = new();
        private readonly ConcurrentDictionary<string, ContainerInfo> _allocatedContainers = new();
        private readonly SemaphoreSlim _poolSemaphore = new(1, 1);
        
        private bool _isInitialized = false;
        private DateTime _lastInitialized = DateTime.MinValue;
        private bool _disposed = false;

        public ContainerPoolService(
            INekoContainerService containerService,
            IContainerHealthService healthService,
            ILogger<ContainerPoolService> logger,
            IConfiguration configuration)
        {
            _containerService = containerService;
            _healthService = healthService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> InitializePoolAsync()
        {
            await _poolSemaphore.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("Container pool is already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing container pool with {PoolSize} containers...", POOL_SIZE);

                while (_availableContainers.TryDequeue(out _)) { }
                _allocatedContainers.Clear();

                await CleanupExistingContainersAsync();

                var creationTasks = new List<Task<ContainerInfo?>>();
                for (int i = 0; i < POOL_SIZE; i++)
                {
                    var containerIndex = i;
                    creationTasks.Add(CreateContainerAsync(containerIndex));
                }

                var containers = await Task.WhenAll(creationTasks);

                int successCount = 0;
                foreach (var container in containers)
                {
                    if (container != null)
                    {
                        _availableContainers.Enqueue(container);
                        successCount++;
                        _logger.LogDebug("Added container {ContainerName} to pool", container.ContainerName);
                    }
                }

                _isInitialized = successCount > 0;
                _lastInitialized = DateTime.UtcNow;

                if (_isInitialized)
                {
                    _logger.LogInformation("Container pool initialized successfully with {SuccessCount}/{TotalCount} containers", 
                        successCount, POOL_SIZE);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to initialize container pool - no containers were created successfully");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing container pool");
                return false;
            }
            finally
            {
                _poolSemaphore.Release();
            }
        }

        public async Task<ContainerInfo?> AllocateContainerAsync()
        {
            await _poolSemaphore.WaitAsync();
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Attempting to allocate from uninitialized pool");
                    return null;
                }

                if (!_availableContainers.TryDequeue(out var container))
                {
                    _logger.LogWarning("No containers available in pool for allocation");
                    return null;
                }

                container.AllocatedAt = DateTime.UtcNow;
                _allocatedContainers.TryAdd(container.ContainerId, container);

                _logger.LogInformation("Allocated container {ContainerName} from pool. Available: {Available}, Allocated: {Allocated}",
                    container.ContainerName, _availableContainers.Count, _allocatedContainers.Count);

                return container;
            }
            finally
            {
                _poolSemaphore.Release();
            }
        }

        public async Task ReturnContainerToPoolAsync(string containerId)
        {
            await _poolSemaphore.WaitAsync();
            try
            {
                if (!_allocatedContainers.TryRemove(containerId, out var container))
                {
                    _logger.LogWarning("Attempted to return container {ContainerId} that was not allocated from pool", containerId);
                    return;
                }

                var resetSuccess = await ResetContainerAsync(container);
                if (resetSuccess)
                {
                    _availableContainers.Enqueue(container);
                    _logger.LogInformation("Returned container {ContainerName} to pool after reset. Available: {Available}, Allocated: {Allocated}",
                        container.ContainerName, _availableContainers.Count, _allocatedContainers.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to reset container {ContainerName}, not returning to pool", container.ContainerName);
                }
            }
            finally
            {
                _poolSemaphore.Release();
            }
        }

        public async Task<int> GetAvailableCountAsync()
        {
            await Task.CompletedTask;
            return _availableContainers.Count;
        }

        public async Task<PoolStatistics> GetPoolStatisticsAsync()
        {
            await Task.CompletedTask;
            return new PoolStatistics
            {
                TotalContainers = POOL_SIZE,
                AvailableContainers = _availableContainers.Count,
                AllocatedContainers = _allocatedContainers.Count,
                LastInitialized = _lastInitialized,
                AllocatedContainerIds = _allocatedContainers.Keys.ToList()
            };
        }

        public async Task CleanupPoolAsync()
        {
            await _poolSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Cleaning up container pool...");

                var allContainers = new List<ContainerInfo>();
                while (_availableContainers.TryDequeue(out var container))
                {
                    allContainers.Add(container);
                }
                allContainers.AddRange(_allocatedContainers.Values);
                _allocatedContainers.Clear();

                var cleanupTasks = allContainers.Select(async container =>
                {
                    try
                    {
                        await _containerService.StopContainerAsync(container.ContainerId);
                        await _containerService.RemoveContainerAsync(container.ContainerId);
                        _logger.LogDebug("Cleaned up container {ContainerName}", container.ContainerName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up container {ContainerName}", container.ContainerName);
                    }
                });

                await Task.WhenAll(cleanupTasks);

                _isInitialized = false;
                _logger.LogInformation("Container pool cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during container pool cleanup");
            }
            finally
            {
                _poolSemaphore.Release();
            }
        }

        private async Task<ContainerInfo?> CreateContainerAsync(int containerIndex)
        {
            try
            {
                var containerId = await _containerService.StartContainerAsync(containerIndex);
                if (string.IsNullOrEmpty(containerId))
                {
                    _logger.LogError("Failed to create container for pool index {ContainerIndex}", containerIndex);
                    return null;
                }
                var containerInfo = await _containerService.GetContainerInfoAsync(containerIndex);
                if (containerInfo == null)
                {
                    _logger.LogError("Failed to get container info for index {ContainerIndex}", containerIndex);
                    await _containerService.RemoveContainerAsync(containerId);
                    return null;
                }
                var isReady = await _healthService.WaitForContainerReadyAsync(containerInfo.HttpPort);
                if (!isReady)
                {
                    _logger.LogError("Container failed to become ready for index {ContainerIndex}", containerIndex);
                    await _containerService.RemoveContainerAsync(containerId);
                    return null;
                }

                containerInfo.AllocatedAt = DateTime.MinValue;
                return containerInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating container {ContainerIndex}", containerIndex);
                return null;
            }
        }

        private async Task<bool> ResetContainerAsync(ContainerInfo container)
        {
            try
            {
                var success = await _containerService.RestartBrowserProcessAsync(container.ContainerId);
                if (success)
                {
                    await Task.Delay(2000);
                    await _healthService.WaitForContainerReadyAsync(container.HttpPort);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting container {ContainerName}", container.ContainerName);
                return false;
            }
        }

        private async Task CleanupExistingContainersAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up existing Neko containers...");
                await _containerService.CleanupAllContainersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up existing containers");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CleanupPoolAsync().GetAwaiter().GetResult();
                _poolSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}