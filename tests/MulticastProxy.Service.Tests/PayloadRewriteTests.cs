using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class PayloadRewriteTests
{
    [Fact]
    public void RewriteIfNeeded_WhenDisabled_ReturnsOriginalPayload()
    {
        var service = CreateService(new RewriteOptions());
        var payload = Encoding.UTF8.GetBytes("device=192.0.2.10");

        var result = service.RewriteIfNeeded(Guid.NewGuid(), 9053, payload);

        Assert.Equal(payload, result);
    }

    [Fact]
    public void RewriteIfNeeded_WhenEnabled_RewritesConfiguredSubnet()
    {
        var service = CreateService(new RewriteOptions
        {
            PayloadRewriteSourceSubnet = "192.0.2.",
            PayloadRewriteDestinationSubnet = "198.51.100."
        });

        var payload = Encoding.UTF8.GetBytes("device=192.0.2.10");

        var result = service.RewriteIfNeeded(Guid.NewGuid(), 9053, payload);

        Assert.Equal("device=198.51.100.10", Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void RewriteIfNeeded_ForInvalidUtf8_ReturnsOriginalPayload()
    {
        var service = CreateService(new RewriteOptions
        {
            PayloadRewriteSourceSubnet = "192.0.2.",
            PayloadRewriteDestinationSubnet = "198.51.100."
        });

        byte[] payload = [0xFF, 0xFE, 0xFD, 0xFC];

        var result = service.RewriteIfNeeded(Guid.NewGuid(), 9053, payload);

        Assert.Equal(payload, result);
    }

    private static PayloadRewriteService CreateService(RewriteOptions options) =>
        new(
            Microsoft.Extensions.Options.Options.Create(options),
            new NullDebugEventSink(),
            NullLogger<PayloadRewriteService>.Instance);

    private sealed class NullDebugEventSink : IDebugEventSink
    {
        public void PublishPacket(
            string stage,
            Guid traceId,
            int port,
            byte[] payload,
            string? remoteEndpoint = null,
            string? details = null,
            byte[]? rewrittenPayload = null)
        {
        }
    }
}
