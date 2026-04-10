namespace MulticastProxy.DebugViewer;

internal sealed class RelayDebugEvent
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Stage { get; set; } = string.Empty;
    public Guid TraceId { get; set; }
    public int Port { get; set; }
    public int PayloadLength { get; set; }
    public string? RemoteEndpoint { get; set; }
    public string? Details { get; set; }
    public string HexPreview { get; set; } = string.Empty;
    public string? TextPreview { get; set; }
    public string? RewrittenTextPreview { get; set; }

    public string LocalTime => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
    public string ShortTraceId => TraceId == Guid.Empty ? "-" : TraceId.ToString("N")[..8];
}
