namespace MulticastProxy.Service.Services;

public interface IDebugEventSink
{
    void PublishPacket(
        string stage,
        Guid traceId,
        int port,
        byte[] payload,
        string? remoteEndpoint = null,
        string? details = null,
        byte[]? rewrittenPayload = null);
}
