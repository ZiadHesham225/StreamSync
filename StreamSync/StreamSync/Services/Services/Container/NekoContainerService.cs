using System.Diagnostics;
using System.Text;
using StreamSync.Services.Interfaces;
using StreamSync.Common;

namespace StreamSync.Services
{
    /// <summary>
    /// Manages Neko browser containers using Docker Compose.
    /// Handles container lifecycle: creation, startup, health checks, and cleanup.
    /// </summary>
    public class NekoContainerService : INekoContainerService
    {
        private readonly ILogger<NekoContainerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IContainerConfigurationService _configService;
        private readonly IContainerHealthService _healthService;
        private int MAX_CONTAINERS => _configuration.GetValue<int>("NekoContainer:MaxContainers", 2);
        private string _nekoImage => _configuration["NekoContainer:Image"] ?? "ghcr.io/m1k1o/neko/chromium:latest";
        private readonly Dictionary<int, ContainerInfo> _containerInfo = new();

        public NekoContainerService(
            ILogger<NekoContainerService> logger, 
            IConfiguration configuration,
            IContainerConfigurationService configService,
            IContainerHealthService healthService)
        {
            _logger = logger;
            _configuration = configuration;
            _configService = configService;
            _healthService = healthService;
        }

        public async Task<bool> InitializeContainersAsync()
        {
            try
            {
                _logger.LogInformation("Initializing Docker Compose Neko service...");

                await PullNekoImageAsync();

                await CreateNetworkAsync();

                _logger.LogInformation("Docker Compose Neko service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Docker Compose Neko service");
                return false;
            }
        }

        public async Task<string?> StartContainerAsync(int containerIndex)
        {
            if (containerIndex >= MAX_CONTAINERS)
            {
                _logger.LogError("Container index {Index} exceeds maximum of {Max}", containerIndex, MAX_CONTAINERS);
                return null;
            }

            try
            {
                var config = _configService.GetContainerConfig(containerIndex);
                
                var tempComposeContent = _configService.CreateContainerComposeContent(config);
                var tempComposeFile = _configService.GetTempComposeFileName(config.ContainerName);
                await File.WriteAllTextAsync(tempComposeFile, tempComposeContent);

                try
                {
                    var result = await RunDockerComposeCommand($"up -d", tempComposeFile);
                    if (!result.Success)
                    {
                        _logger.LogError("Failed to start container {ContainerName}: {Error}", config.ContainerName, result.Error);
                        return null;
                    }

                    var containerId = await GetContainerIdAsync(config.ContainerName);
                    if (string.IsNullOrEmpty(containerId))
                    {
                        _logger.LogError("Failed to get container ID for {ContainerName}", config.ContainerName);
                        return null;
                    }

                    var isReady = await _healthService.WaitForContainerReadyAsync(config.HttpPort);
                    if (!isReady)
                    {
                        _logger.LogError("Container {ContainerName} failed to become ready", config.ContainerName);
                        return null;
                    }

                    _containerInfo[containerIndex] = new ContainerInfo
                    {
                        ContainerId = containerId,
                        ContainerName = config.ContainerName,
                        ContainerIndex = containerIndex,
                        HttpPort = config.HttpPort,
                        UdpPortStart = config.UdpPortStart,
                        UdpPortEnd = config.UdpPortEnd,
                        NekoPassword = config.NekoPassword,
                        NekoAdminPassword = config.NekoAdminPassword,
                        HostAddress = config.HostAddress,
                        ComposeFile = tempComposeFile,
                        CreatedAt = DateTime.UtcNow,
                        IsHealthy = true
                    };

                    _logger.LogInformation(
                        "Started Neko container {ContainerName} with ID {ContainerId} on port {Port}",
                        config.ContainerName, containerId, config.HttpPort);

                    return containerId;
                }
                finally
                {
                    if (!_containerInfo.ContainsKey(containerIndex))
                    {
                        try { File.Delete(tempComposeFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting container {Index}", containerIndex);
                return null;
            }
        }

        public async Task<bool> StopContainerAsync(string containerId)
        {
            try
            {
                var containerInfo = _containerInfo.Values.FirstOrDefault(c => c.ContainerId == containerId);
                if (containerInfo == null)
                {
                    _logger.LogWarning("Container info not found for container ID {ContainerId}", containerId);
                    await RunDockerCommand($"stop {containerId}");
                    await RunDockerCommand($"rm {containerId}");
                    return true;
                }

                var result = await RunDockerComposeCommand("down", containerInfo.ComposeFile);
                if (result.Success)
                {
                    _logger.LogInformation("Stopped Neko container {ContainerId}", containerId);

                    try
                    {
                        if (File.Exists(containerInfo.ComposeFile))
                        {
                            File.Delete(containerInfo.ComposeFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp compose file {File}", containerInfo.ComposeFile);
                    }

                    var containerIndex = _containerInfo.FirstOrDefault(kvp => kvp.Value.ContainerId == containerId).Key;
                    if (containerIndex != 0 || _containerInfo.ContainsKey(0))
                    {
                        _containerInfo.Remove(containerIndex);
                    }

                    return true;
                }
                else
                {
                    _logger.LogError("Failed to stop container {ContainerId}: {Error}", containerId, result.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping container {ContainerId}", containerId);
                return false;
            }
        }

        public async Task CleanupAllContainersAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up all Neko containers...");

                var tasks = _containerInfo.Values.Select(async info =>
                {
                    try
                    {
                        await RunDockerComposeCommand("down", info.ComposeFile);
                        if (File.Exists(info.ComposeFile))
                        {
                            File.Delete(info.ComposeFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up container {ContainerName}", info.ContainerName);
                    }
                });

                await Task.WhenAll(tasks);

                await RunDockerCommand("ps -aq --filter \"name=neko-browser-\" | ForEach-Object { docker stop $_ ; docker rm $_ }");

                var tempFiles = Directory.GetFiles(".", "temp-neko-browser-*.yml");
                foreach (var file in tempFiles)
                {
                    try { File.Delete(file); } catch { }
                }

                _containerInfo.Clear();
                _logger.LogInformation("Cleaned up all Neko containers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up containers");
            }
        }

        public Task<ContainerInfo?> GetContainerInfoAsync(int containerIndex)
        {
            return Task.FromResult(_containerInfo.TryGetValue(containerIndex, out var info) ? info : null);
        }

        public Task<Dictionary<int, ContainerInfo>> GetAllRunningContainersAsync()
        {
            return Task.FromResult(new Dictionary<int, ContainerInfo>(_containerInfo));
        }

        public Task<int> GetAvailableContainerSlotAsync()
        {
            for (int i = 0; i < MAX_CONTAINERS; i++)
            {
                if (!_containerInfo.ContainsKey(i))
                {
                    return Task.FromResult(i);
                }
            }
            return Task.FromResult(-1);
        }

        public async Task<bool> IsContainerHealthyAsync(string containerId)
        {
            return await _healthService.IsContainerHealthyAsync(containerId);
        }

        public async Task<bool> RemoveContainerAsync(string containerId)
        {
            return await StopContainerAsync(containerId);
        }

        public async Task<bool> RestartContainerAsync(string containerId)
        {
            try
            {
                var containerInfo = _containerInfo.Values.FirstOrDefault(c => c.ContainerId == containerId);
                if (containerInfo == null)
                {
                    _logger.LogWarning("Container info not found for restart: {ContainerId}", containerId);
                    return false;
                }

                var result = await RunDockerComposeCommand("restart", containerInfo.ComposeFile);
                if (result.Success)
                {
                    _logger.LogInformation("Restarted container {ContainerId}", containerId);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to restart container {ContainerId}: {Error}", containerId, result.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting container {ContainerId}", containerId);
                return false;
            }
        }

        public async Task<bool> RestartBrowserProcessAsync(string containerId)
        {
            try
            {
                _logger.LogInformation("Performing complete browser reset in container {ContainerId} - stopping browser and clearing profile", containerId);

                var success = await ExecuteContainerRestartAsync(containerId);

                if (success)
                {
                    _logger.LogInformation("Successfully completed browser reset with profile cleanup in container {ContainerId}", containerId);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to complete browser reset in container {ContainerId}", containerId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing browser reset in container {ContainerId}", containerId);
                return false;
            }
        }

        private async Task<bool> ExecuteContainerRestartAsync(string containerId)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "docker";
                process.StartInfo.Arguments = $"exec {containerId} bash -c \"" +
                                            "kill -TERM -1 2>/dev/null || true; " +
                                            "sleep 1; " +
                                            "kill -KILL -1 2>/dev/null || true; " +
                                            "sleep 1; " +
                                            "rm -rf /home/neko/.config/chromium/Default; " +
                                            "mkdir -p /home/neko/.config/chromium/Default; " +
                                            "chown neko:neko /home/neko/.config/chromium/Default; " +
                                            "\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                _logger.LogDebug("Executing enhanced browser reset: docker {Arguments}", process.StartInfo.Arguments);

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Docker exec command had non-zero exit code {ExitCode}. Output: {Output}, Error: {Error}", 
                        process.ExitCode, output, error);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing container restart command");
                return false;
            }
        }

        public Task<string> GetContainerUrlAsync(string containerId, int slotIndex)
        {
            var containerInfo = _containerInfo.Values.FirstOrDefault(c => c.ContainerId == containerId);
            if (containerInfo != null)
            {
                return Task.FromResult($"http://localhost:{containerInfo.HttpPort}");
            }

            var port = 8080 + slotIndex;
            return Task.FromResult($"http://localhost:{port}");
        }

        public async Task<List<string>> GetRunningContainersAsync()
        {
            try
            {
                var result = await RunDockerCommand("ps -q --filter name=neko-browser-");
                if (result.Success)
                {
                    return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(line => line.Trim())
                                      .Where(line => !string.IsNullOrEmpty(line))
                                      .ToList();
                }
                return new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task CleanupContainersAsync()
        {
            await CleanupAllContainersAsync();
        }

        private async Task<(bool Success, string Output, string Error)> RunDockerComposeCommand(string arguments, string? composeFile = null)
        {
            var command = "docker-compose";
            if (!string.IsNullOrEmpty(composeFile))
            {
                command += $" -f {composeFile}";
            }
            command += $" {arguments}";

            return await RunCommandAsync(command);
        }

        private async Task<(bool Success, string Output, string Error)> RunDockerCommand(string arguments)
        {
            return await RunCommandAsync($"docker {arguments}");
        }

        private async Task<(bool Success, string Output, string Error)> RunCommandAsync(string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-Command \"{command}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null) output.AppendLine(args.Data);
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null) error.AppendLine(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                var success = process.ExitCode == 0;
                return (success, output.ToString(), error.ToString());
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        private async Task PullNekoImageAsync()
        {
            _logger.LogInformation("Checking if Neko image exists locally: {ImageName}", _nekoImage);
            var checkResult = await RunDockerCommand($"image inspect {_nekoImage}");
            
            if (checkResult.Success)
            {
                _logger.LogInformation("Neko image already exists locally, skipping pull");
                return;
            }
            
            _logger.LogInformation("Neko image not found locally, pulling: {ImageName}", _nekoImage);
            var result = await RunDockerCommand($"pull {_nekoImage}");
            if (result.Success)
            {
                _logger.LogInformation("Successfully pulled Neko image");
            }
            else
            {
                _logger.LogWarning("Failed to pull Neko image: {Error}", result.Error);
            }
        }

        private async Task CreateNetworkAsync()
        {
            // Check if the network exists first
            var inspectResult = await RunDockerCommand("network inspect streamsync-network");
            if (inspectResult.Success)
            {
                _logger.LogDebug("Docker network streamsync-network already exists");
                return;
            }

            // Try to create the network if it does not exist
            var result = await RunDockerCommand("network create streamsync-network");
            if (result.Success || result.Error.Contains("already exists"))
            {
                _logger.LogDebug("Docker network streamsync-network is ready");
            }
            else
            {
                _logger.LogWarning("Failed to create Docker network: {Error}", result.Error);
            }
        }

        private async Task<string?> GetContainerIdAsync(string containerName)
        {
            var result = await RunDockerCommand($"ps -q --filter name={containerName}");
            if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output.Trim();
            }
            return null;
        }
    }
}