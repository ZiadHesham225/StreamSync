using Microsoft.Extensions.DependencyInjection;

namespace StreamSync.Extensions
{
    public static class CorsServiceExtensions
    {
        public static IServiceCollection AddFrontendCors(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://127.0.0.1:5500", "http://localhost:3000", "http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            return services;
        }
    }
}
