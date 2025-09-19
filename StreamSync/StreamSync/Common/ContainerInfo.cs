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
        
        public DateTime AllocatedAt { get; set; }
        public bool IsAllocated => AllocatedAt != DateTime.MinValue;
        
        public string ComposeFile { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsHealthy { get; set; }
        
        public string BrowserUrl => $"http://localhost:{HttpPort}";
        public string WebRtcUrl => $"ws://localhost:{HttpPort}/ws";
    }
}