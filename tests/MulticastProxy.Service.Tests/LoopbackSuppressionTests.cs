using MulticastProxy.Service.Options;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class LoopbackSuppressionTests
{
    [Fact]
    public void ShouldSuppress_ReturnsTrueForRecentlyEmittedPacket()
    {
        var service = CreateService(windowSeconds: 5);
        byte[] payload = [1, 2, 3, 4];

        service.RegisterRecentlyEmitted(9053, payload);

        Assert.True(service.ShouldSuppress(9053, payload));
    }

    [Fact]
    public async Task ShouldSuppress_ReturnsFalseAfterWindowExpires()
    {
        var service = CreateService(windowSeconds: 1);
        byte[] payload = [1, 2, 3, 4];

        service.RegisterRecentlyEmitted(9053, payload);
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(service.ShouldSuppress(9053, payload));
    }

    [Fact]
    public void ShouldSuppress_ReturnsFalseForDifferentPortOrPayload()
    {
        var service = CreateService(windowSeconds: 5);
        byte[] payload = [1, 2, 3, 4];

        service.RegisterRecentlyEmitted(9053, payload);

        Assert.False(service.ShouldSuppress(9054, payload));
        Assert.False(service.ShouldSuppress(9053, [1, 2, 3, 5]));
    }

    private static LoopbackSuppressionService CreateService(int windowSeconds) =>
        new(Microsoft.Extensions.Options.Options.Create(new RelayOptions
        {
            MulticastGroup = "239.0.0.1",
            MulticastPorts = [9053],
            TunnelPort = 19053,
            DestinationIP = "198.51.100.10",
            DeduplicationWindowSeconds = 30,
            LoopbackSuppressionWindowSeconds = windowSeconds
        }));
}
