namespace StreamSync.Services.Interfaces
{
    public interface IDockerContainerService
    {
        Task<bool> InitializeContainersAsync();
        Task<string?> StartContainerAsync(int containerIndex);
        Task<bool> StopContainerAsync(string containerId);
        Task<bool> RestartContainerAsync(string containerId);
        Task<bool> RestartBrowserProcessAsync(string containerId);
        Task<string> GetContainerUrlAsync(string containerId, int slotIndex);
        Task<bool> IsContainerHealthyAsync(string containerId);
        Task<List<string>> GetRunningContainersAsync();
        Task CleanupContainersAsync();
    }
}
