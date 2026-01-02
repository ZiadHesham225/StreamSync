using Microsoft.Extensions.DependencyInjection;
using StreamSync.Services.Interfaces;
using StreamSync.Services;
using StreamSync.Services.InMemory;
using StreamSync.DataAccess.Interfaces;
using StreamSync.DataAccess.Repositories;
using StreamSync.Models;
using StreamSync.Data;
using StreamSync.Hubs;

namespace StreamSync.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // In-memory services
            services.AddSingleton<InMemoryRoomManager>();

            // Repository and service registrations
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IYouTubeService, YouTubeService>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IGenericRepository<Room>, GenericRepository<Room>>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ITokenService, JwtTokenService>();

            // Room-related services (extracted from RoomHub for SRP)
            services.AddScoped<IRoomParticipantService, RoomParticipantService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IPlaybackService, PlaybackService>();

            // Virtual Browser services
            services.AddScoped<IVirtualBrowserRepository, VirtualBrowserRepository>();
            services.AddSingleton<IVirtualBrowserQueueService, VirtualBrowserQueueService>();
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
