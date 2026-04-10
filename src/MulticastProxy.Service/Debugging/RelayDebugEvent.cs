namespace MulticastProxy.Service.Debugging;

public sealed record RelayDebugEvent(
    DateTimeOffset TimestampUtc,
    string Stage,
    Guid TraceId,
    int Port,
    int PayloadLength,
    string? RemoteEndpoint,
    string? Details,
    string HexPreview,
    string? TextPreview,
    string? RewrittenTextPreview);
