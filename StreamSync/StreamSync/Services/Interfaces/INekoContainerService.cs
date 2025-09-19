using StreamSync.BusinessLogic.Services;
using StreamSync.Common;

namespace StreamSync.BusinessLogic.Interfaces
{
    public interface INekoContainerService : IDockerContainerService
    {
        Task<ContainerInfo?> GetContainerInfoAsync(int containerIndex);
        Task<Dictionary<int, ContainerInfo>> GetAllRunningContainersAsync();
        Task<int> GetAvailableContainerSlotAsync();
        new Task<bool> IsContainerHealthyAsync(string containerId);
        Task<bool> RemoveContainerAsync(string containerId);
        Task CleanupAllContainersAsync();
    }
}
