namespace MulticastProxy.Service.Services;

public interface ILoopbackSuppressionService
{
    void RegisterRecentlyEmitted(int port, ReadOnlySpan<byte> payload);
    bool ShouldSuppress(int port, ReadOnlySpan<byte> payload);
}
