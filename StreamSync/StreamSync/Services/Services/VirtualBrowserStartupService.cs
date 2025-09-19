using StreamSync.BusinessLogic.Interfaces;

namespace StreamSync.BusinessLogic.Services
{
    public class VirtualBrowserStartupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VirtualBrowserStartupService> _logger;

        public VirtualBrowserStartupService(
            IServiceProvider serviceProvider,
            ILogger<VirtualBrowserStartupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Virtual Browser Pool initialization...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var virtualBrowserService = scope.ServiceProvider.GetRequiredService<IVirtualBrowserService>();

                var initialized = await virtualBrowserService.InitializePoolAsync();
                
                if (initialized)
                {
                    _logger.LogInformation("Virtual Browser Pool initialized successfully");
                }
                else
                {
                    _logger.LogError("Failed to initialize Virtual Browser Pool");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Virtual Browser Pool initialization");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Browser Pool startup service stopped");
            return Task.CompletedTask;
        }
    }
}
