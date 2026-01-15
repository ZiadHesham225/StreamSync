namespace StreamSync.Common
{
    public class ContainerInfo
    {
        public string ContainerId { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public int ContainerIndex { get; set; }
        
        public int HttpPort { get; set; }
        public int UdpPortStart { get; set; }
        public int UdpPortEnd { get; set; }
        
        public string NekoPassword { get; set; } = string.Empty;
        public string NekoAdminPassword { get; set; } = string.Empty;
        
        /// <summary>
        /// The host address (IP or hostname) where this container is running.
        /// Used for cross-server container access in horizontally scaled deployments.
        /// Defaults to localhost for single-server setups.
        /// </summary>
        public string HostAddress { get; set; } = "localhost";
        
        public DateTime AllocatedAt { get; set; }
        public bool IsAllocated => AllocatedAt != DateTime.MinValue;
        
        public string ComposeFile { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsHealthy { get; set; }
        
        public string BrowserUrl => $"http://{HostAddress}:{HttpPort}";
        public string WebRtcUrl => $"ws://{HostAddress}:{HttpPort}/ws";
    }
}