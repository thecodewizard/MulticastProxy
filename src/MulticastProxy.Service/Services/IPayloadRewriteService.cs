namespace MulticastProxy.Service.Services;

public interface IPayloadRewriteService
{
    byte[] RewriteIfNeeded(ReadOnlySpan<byte> payload);
}
