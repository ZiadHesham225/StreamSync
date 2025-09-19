using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace StreamSync.Extensions
{
    public static class ControllerServiceExtensions
    {
        public static IServiceCollection AddConfiguredControllers(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });
            return services;
        }
    }
}
