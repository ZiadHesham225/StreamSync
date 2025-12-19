using Microsoft.Extensions.DependencyInjection;

namespace StreamSync.Extensions
{
    public static class SignalRServiceExtensions
    {
        public static IServiceCollection AddConfiguredSignalR(this IServiceCollection services)
        {
            services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.AddCommonJsonOptions();
                });
            return services;
        }
    }
}
