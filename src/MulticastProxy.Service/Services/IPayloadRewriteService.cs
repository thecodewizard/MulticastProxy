namespace MulticastProxy.Service.Services;

public interface IPayloadRewriteService
{
    byte[] RewriteIfNeeded(Guid traceId, int port, byte[] payload);
}
