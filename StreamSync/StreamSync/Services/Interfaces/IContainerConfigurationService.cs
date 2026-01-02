using StreamSync.Services;

namespace StreamSync.Services.Interfaces
{
    /// <summary>
    /// Interface for container configuration service
    /// </summary>
    public interface IContainerConfigurationService
    {
        /// <summary>
        /// Generate container configuration for a given container index
        /// </summary>
        ContainerConfig GetContainerConfig(int containerIndex);

        /// <summary>
        /// Create Docker Compose content for a container
        /// </summary>
        string CreateContainerComposeContent(ContainerConfig config);

        /// <summary>
        /// Generate temporary compose file name for a container
        /// </summary>
        string GetTempComposeFileName(string containerName);
    }
}