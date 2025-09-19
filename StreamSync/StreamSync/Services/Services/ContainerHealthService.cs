using StreamSync.BusinessLogic.Interfaces;
using System.Diagnostics;

namespace StreamSync.BusinessLogic.Services
{
    public class ContainerHealthService : IContainerHealthService
    {
        private readonly ILogger<ContainerHealthService> _logger;

        public ContainerHealthService(ILogger<ContainerHealthService> logger)
        {
            _logger = logger;
        }
        public async Task<bool> WaitForContainerReadyAsync(int port, int maxAttempts = 60, int delayMs = 1000)
        {
            _logger.LogDebug("Waiting for container on port {Port} to become ready...", port);

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var response = await httpClient.GetAsync($"http://localhost:{port}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("Container on port {Port} is ready after {Attempts} attempts", port, i + 1);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace("Attempt {Attempt}/{MaxAttempts} failed for port {Port}: {Error}", 
                        i + 1, maxAttempts, port, ex.Message);
                }

                if (i < maxAttempts - 1)
                {
                    await Task.Delay(delayMs);
                }
            }

            _logger.LogWarning("Container on port {Port} did not become ready within {Seconds} seconds", 
                port, (maxAttempts * delayMs) / 1000);
            return false;
        }

        public async Task<bool> IsContainerHealthyAsync(string containerId)
        {
            try
            {
                _logger.LogTrace("Checking health for container {ContainerId}", containerId);

                // First try to get health status
                var healthResult = await RunDockerCommandAsync($"inspect --format='{{{{.State.Health.Status}}}}' {containerId}");
                if (healthResult.Success)
                {
                    var health = healthResult.Output.Trim().Trim('\'', '"');
                    if (health == "healthy")
                    {
                        return true;
                    }
                    else if (!string.IsNullOrEmpty(health) && health != "")
                    {
                        _logger.LogTrace("Container {ContainerId} health status: {Health}", containerId, health);
                        return false;
                    }
                }

                var runningResult = await RunDockerCommandAsync($"inspect --format='{{{{.State.Running}}}}' {containerId}");
                if (runningResult.Success)
                {
                    bool isRunning = runningResult.Output.Trim().ToLower() == "true";
                    _logger.LogTrace("Container {ContainerId} running status: {IsRunning}", containerId, isRunning);
                    return isRunning;
                }

                _logger.LogWarning("Failed to check health for container {ContainerId}", containerId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking container health for {ContainerId}", containerId);
                return false;
            }
        }
        public string GenerateRandomPassword(int length = 12)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task<(bool Success, string Output, string Error)> RunDockerCommandAsync(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running docker command: {Arguments}", arguments);
                return (false, string.Empty, ex.Message);
            }
        }
    }
}