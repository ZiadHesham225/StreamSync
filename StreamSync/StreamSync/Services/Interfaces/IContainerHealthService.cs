namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Interface for container health checking service
    /// </summary>
    public interface IContainerHealthService
    {
        /// <summary>
        /// Wait for a container to become ready by polling its HTTP endpoint
        /// </summary>
        Task<bool> WaitForContainerReadyAsync(int port, int maxAttempts = 30, int delayMs = 1000);

        /// <summary>
        /// Check if a container is healthy using Docker inspect
        /// </summary>
        Task<bool> IsContainerHealthyAsync(string containerId);
    }
}