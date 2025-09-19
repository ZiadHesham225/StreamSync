using StreamSync.BusinessLogic.Interfaces;
using StreamSync.Common;

namespace StreamSync.BusinessLogic.Services
{
    public class ContainerConfigurationService : IContainerConfigurationService
    {
        private readonly ILogger<ContainerConfigurationService> _logger;

        public ContainerConfigurationService(ILogger<ContainerConfigurationService> logger)
        {
            _logger = logger;
        }

        public ContainerConfig GetContainerConfig(int containerIndex)
        {
            if (containerIndex < 0)
            {
                throw new ArgumentException("Container index cannot be negative", nameof(containerIndex));
            }

            var config = new ContainerConfig
            {
                ContainerIndex = containerIndex,
                ContainerName = $"neko-browser-{containerIndex}",
                HttpPort = 8080 + containerIndex,
                UdpPortStart = 59000 + (containerIndex * 100),
                UdpPortEnd = 59000 + (containerIndex * 100) + 99,
                NekoPassword = $"neko-admin",
                NekoAdminPassword = $"neko-admin"
            };

            _logger.LogDebug("Generated container config for index {ContainerIndex}: {ContainerName} on HTTP port {HttpPort}, UDP ports {UdpStart}-{UdpEnd}",
                containerIndex, config.ContainerName, config.HttpPort, config.UdpPortStart, config.UdpPortEnd);

            return config;
        }

        public string CreateContainerComposeContent(ContainerConfig config)
        {
            var composeContent = $@"services:
  {config.ContainerName}:
    image: ghcr.io/m1k1o/neko/chromium:latest
    container_name: {config.ContainerName}
    restart: unless-stopped
    shm_size: 2gb
    ports:
      - ""{config.HttpPort}:8080""
      - ""{config.UdpPortStart}-{config.UdpPortEnd}:{config.UdpPortStart}-{config.UdpPortEnd}/udp""
    environment:
      - NEKO_DESKTOP_SCREEN=1920x1080@30
      - NEKO_MEMBER_MULTIUSER_USER_PASSWORD={config.NekoPassword}
      - NEKO_MEMBER_MULTIUSER_ADMIN_PASSWORD={config.NekoAdminPassword}
      - NEKO_WEBRTC_EPR={config.UdpPortStart}-{config.UdpPortEnd}
      - NEKO_WEBRTC_ICELITE=1
      - NEKO_WEBRTC_NAT1TO1=127.0.0.1
      # Audio configuration
      - NEKO_AUDIO_CODEC=opus
      - NEKO_AUDIO_BITRATE=128000
      # Chromium specific settings with audio support
      - CHROME_FLAGS=--no-sandbox --disable-dev-shm-usage --disable-gpu --autoplay-policy=no-user-gesture-required --enable-audio-service-sandbox=false --disable-audio-sandbox --use-fake-ui-for-media-stream
    cap_add:
      - SYS_ADMIN
    security_opt:
      - seccomp:unconfined
    networks:
      - streamsync-network

networks:
  streamsync-network:
    external: true
";

            _logger.LogDebug("Generated Docker Compose content for {ContainerName}:\n{ComposeContent}",
                config.ContainerName, composeContent);

            return composeContent;
        }

        public string GetTempComposeFileName(string containerName)
        {
            var fileName = $"temp-{containerName}.yml";
            _logger.LogTrace("Generated temp compose file name: {FileName}", fileName);
            return fileName;
        }

        public bool ValidateContainerConfig(ContainerConfig config)
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(config.ContainerName))
                issues.Add("Container name is empty");

            if (config.HttpPort <= 0 || config.HttpPort > 65535)
                issues.Add($"Invalid HTTP port: {config.HttpPort}");

            if (config.UdpPortStart <= 0 || config.UdpPortStart > 65535)
                issues.Add($"Invalid UDP start port: {config.UdpPortStart}");

            if (config.UdpPortEnd <= 0 || config.UdpPortEnd > 65535)
                issues.Add($"Invalid UDP end port: {config.UdpPortEnd}");

            if (config.UdpPortStart > config.UdpPortEnd)
                issues.Add($"UDP start port ({config.UdpPortStart}) is greater than end port ({config.UdpPortEnd})");

            if (string.IsNullOrEmpty(config.NekoPassword))
                issues.Add("Neko password is empty");

            if (string.IsNullOrEmpty(config.NekoAdminPassword))
                issues.Add("Neko admin password is empty");

            if (issues.Any())
            {
                _logger.LogError("Container configuration validation failed for {ContainerName}: {Issues}",
                    config.ContainerName, string.Join(", ", issues));
                return false;
            }

            _logger.LogDebug("Container configuration validated successfully for {ContainerName}", config.ContainerName);
            return true;
        }
    }

    public class ContainerConfig
    {
        public int ContainerIndex { get; set; }
        public string ContainerName { get; set; } = string.Empty;
        public int HttpPort { get; set; }
        public int UdpPortStart { get; set; }
        public int UdpPortEnd { get; set; }
        public string NekoPassword { get; set; } = string.Empty;
        public string NekoAdminPassword { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"Container {ContainerIndex}: {ContainerName} (HTTP:{HttpPort}, UDP:{UdpPortStart}-{UdpPortEnd})";
        }
    }
}