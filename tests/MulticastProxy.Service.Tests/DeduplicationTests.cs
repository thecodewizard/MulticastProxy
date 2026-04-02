using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class DeduplicationTests
{
    [Fact]
    public void TryRegister_ReturnsFalseForDuplicateWithinWindow()
    {
        var service = CreateService(windowSeconds: 5);
        var packetId = Guid.NewGuid();

        var first = service.TryRegister(packetId);
        var second = service.TryRegister(packetId);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task TryRegister_AllowsPacketAfterWindowExpires()
    {
        var service = CreateService(windowSeconds: 1);
        var packetId = Guid.NewGuid();

        Assert.True(service.TryRegister(packetId));
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.True(service.TryRegister(packetId));
    }

    private static DeduplicationService CreateService(int windowSeconds) =>
        new(Microsoft.Extensions.Options.Options.Create(new RelayOptions
        {
            MulticastGroup = "239.0.0.1",
            MulticastPorts = [9053],
            TunnelPort = 19053,
            DestinationIP = "198.51.100.10",
            DeduplicationWindowSeconds = windowSeconds
        }));
}
