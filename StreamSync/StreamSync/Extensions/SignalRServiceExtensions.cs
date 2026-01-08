using Microsoft.Extensions.DependencyInjection;

namespace StreamSync.Extensions
{
    public static class SignalRServiceExtensions
    {
        public static IServiceCollection AddConfiguredSignalR(this IServiceCollection services, IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            
            var signalRBuilder = services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.AddCommonJsonOptions();
                });

            // Add Redis backplane if connection string is configured
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("StreamSync");
                });
            }

            return services;
        }
    }
}
