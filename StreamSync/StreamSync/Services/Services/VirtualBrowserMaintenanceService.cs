using StreamSync.BusinessLogic.Interfaces;

namespace StreamSync.BusinessLogic.Services
{
    public class VirtualBrowserMaintenanceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VirtualBrowserMaintenanceService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Check every 10 seconds

        public VirtualBrowserMaintenanceService(
            IServiceProvider serviceProvider,
            ILogger<VirtualBrowserMaintenanceService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Virtual Browser Maintenance Service started");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var virtualBrowserService = scope.ServiceProvider.GetRequiredService<IVirtualBrowserService>();

                        await virtualBrowserService.ProcessExpiredSessionsAsync();

                        await virtualBrowserService.ProcessQueueNotificationsAsync();

                        _logger.LogDebug("Virtual browser maintenance cycle completed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during virtual browser maintenance cycle");
                    }

                    try
                    {
                        await Task.Delay(_checkInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Virtual Browser Maintenance Service stopped during startup delay");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Virtual Browser Maintenance Service");
            }

            _logger.LogInformation("Virtual Browser Maintenance Service stopped");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Browser Maintenance Service is stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
