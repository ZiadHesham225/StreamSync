using StreamSync.Services;
using StreamSync.Common;

namespace StreamSync.Services.Interfaces
{
    public interface IContainerPoolService
    {
        /// <summary>
        /// Initialize the container pool with pre-allocated containers
        /// </summary>
        Task<bool> InitializePoolAsync();

        /// <summary>
        /// Get an available container from the pool
        /// </summary>
        Task<ContainerInfo?> AllocateContainerAsync();

        /// <summary>
        /// Return a container to the pool and reset it for reuse
        /// </summary>
        Task ReturnContainerToPoolAsync(string containerId);

        /// <summary>
        /// Get the number of available containers in the pool
        /// </summary>
        Task<int> GetAvailableCountAsync();

        /// <summary>
        /// Get pool statistics
        /// </summary>
        Task<PoolStatistics> GetPoolStatisticsAsync();

        /// <summary>
        /// Cleanup and dispose all pool resources
        /// </summary>
        Task CleanupPoolAsync();
    }

    public class PoolStatistics
    {
        public int TotalContainers { get; set; }
        public int AvailableContainers { get; set; }
        public int AllocatedContainers { get; set; }
        public DateTime LastInitialized { get; set; }
        public List<string> AllocatedContainerIds { get; set; } = new();
    }
}