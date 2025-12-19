using Microsoft.Extensions.DependencyInjection;

namespace StreamSync.Extensions
{
    public static class ControllerServiceExtensions
    {
        public static IServiceCollection AddConfiguredControllers(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.AddCommonJsonOptions();
                });
            return services;
        }
    }
}
