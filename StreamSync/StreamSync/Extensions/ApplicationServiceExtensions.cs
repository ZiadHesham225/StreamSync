using Microsoft.Extensions.DependencyInjection;
using StreamSync.Services.Interfaces;
using StreamSync.Services;
using StreamSync.Services.InMemory;
using StreamSync.Services.Redis;
using StreamSync.Services.Decorators;
using StreamSync.DataAccess.Interfaces;
using StreamSync.DataAccess.Cache;
using StreamSync.DataAccess.Repositories;
using StreamSync.Models;
using StreamSync.Data;
using StreamSync.Hubs;
using StackExchange.Redis;

namespace StreamSync.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Redis connection and caching configuration
            var redisConnectionString = configuration.GetConnectionString("Redis");
            var useRedis = !string.IsNullOrEmpty(redisConnectionString);

            if (useRedis)
            {
                // Redis connection multiplexer (singleton)
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
                    try
                    {
                        return ConnectionMultiplexer.Connect(redisConnectionString!);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to connect to Redis. Caching will be disabled.");
                        throw;
                    }
                });

                // Cache service (infrastructure layer for distributed caching)
                services.AddSingleton<ICacheService, RedisCacheService>();

                // Room state service (uses Redis)
                services.AddSingleton<IRoomStateService, RedisRoomStateService>();
            }
            else
            {
                // Fallback to in-memory storage
                services.AddSingleton<IRoomStateService, InMemoryRoomStateService>();
            }

            // Repository registrations
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, EmailService>();

            // Core services registration
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IYouTubeService, YouTubeService>();

            // Apply caching decorators using Scrutor (when Redis is available)
            if (useRedis)
            {
                services.Decorate<IRoomService, RoomServiceCachingDecorator>();
                services.Decorate<IUserService, UserServiceCachingDecorator>();
                services.Decorate<IYouTubeService, YouTubeServiceCachingDecorator>();
            }

            services.AddScoped<IGenericRepository<Room>, GenericRepository<Room>>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ITokenService, JwtTokenService>();

            // Room-related services (extracted from RoomHub for SRP)
            services.AddScoped<IRoomParticipantService, RoomParticipantService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IPlaybackService, PlaybackService>();

            // Virtual Browser services
            services.AddScoped<IVirtualBrowserRepository, VirtualBrowserRepository>();
            
            // Virtual Browser Queue - Redis for horizontal scaling, in-memory for single instance
            if (useRedis)
            {
                services.AddSingleton<IVirtualBrowserQueueService, RedisVirtualBrowserQueueService>();
            }
            else
            {
                services.AddSingleton<IVirtualBrowserQueueService, InMemoryVirtualBrowserQueueService>();
            }
            
            services.AddScoped<IVirtualBrowserNotificationService, VirtualBrowserNotificationService>();

            // Container configuration and health services
            services.AddSingleton<IContainerConfigurationService, ContainerConfigurationService>();
            services.AddSingleton<IContainerHealthService, ContainerHealthService>();

            // Container and pool services
            services.AddSingleton<INekoContainerService, NekoContainerService>();
            services.AddSingleton<IContainerPoolService, ContainerPoolService>();
            services.AddSingleton<IDockerContainerService>(provider => 
                provider.GetRequiredService<INekoContainerService>());
            services.AddScoped<IVirtualBrowserService, NekoVirtualBrowserService>();

            // Background services
            services.AddHostedService<VirtualBrowserStartupService>();
            services.AddHostedService<VirtualBrowserMaintenanceService>();

            return services;
        }
    }
}
