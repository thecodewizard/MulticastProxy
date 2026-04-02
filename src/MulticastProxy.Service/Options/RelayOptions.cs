namespace MulticastProxy.Service.Options;

public sealed class RelayOptions
{
    public const string SectionName = "Relay";

    public string MulticastGroup { get; set; } = string.Empty;
    public List<int> MulticastPorts { get; set; } = [];
    public int TunnelPort { get; set; }
    public string DestinationIP { get; set; } = string.Empty;
    public string? ListenInterfaceIP { get; set; }
    public string? SendInterfaceIP { get; set; }
    public Guid InstanceId { get; set; } = Guid.NewGuid();
    public int DeduplicationWindowSeconds { get; set; } = 30;
}
