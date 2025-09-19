using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace StreamSync.Extensions
{
    public static class SignalRServiceExtensions
    {
        public static IServiceCollection AddConfiguredSignalR(this IServiceCollection services)
        {
            services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });
            return services;
        }
    }
}
