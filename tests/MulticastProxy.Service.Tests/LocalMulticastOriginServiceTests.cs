using System.Net;
using System.Net.NetworkInformation;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class LocalMulticastOriginServiceTests
{
    [Fact]
    public void IsLocallyEmitted_ReturnsTrueForMatchingEndpoint()
    {
        var service = new LocalMulticastOriginService();
        service.RegisterSender(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255));

        Assert.True(service.IsLocallyEmitted(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255)));
    }

    [Fact]
    public void IsLocallyEmitted_ReturnsFalseForDifferentEndpoint()
    {
        var service = new LocalMulticastOriginService();
        service.RegisterSender(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255));

        Assert.False(service.IsLocallyEmitted(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63256)));
        Assert.False(service.IsLocallyEmitted(new IPEndPoint(IPAddress.Parse("10.100.5.76"), 63255)));
    }

    [Fact]
    public void GetRegisteredSenders_ReturnsRegisteredEndpoints()
    {
        var service = new LocalMulticastOriginService();
        service.RegisterSender(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255));
        service.RegisterSender(new IPEndPoint(IPAddress.Parse("10.100.5.76"), 63256));

        Assert.Equal(
            ["10.100.5.75:63255", "10.100.5.76:63256"],
            service.GetRegisteredSenders());
    }

    [Fact]
    public void IsLocallyEmitted_ReturnsTrueForMatchingPortFromAnotherLocalAddress()
    {
        var localAddress = GetAnyLocalIPv4Address();
        var service = new LocalMulticastOriginService();
        service.RegisterSender(new IPEndPoint(IPAddress.Parse("198.51.100.10"), 63255));

        Assert.True(service.IsLocallyEmitted(new IPEndPoint(localAddress, 63255)));
    }

    private static IPAddress GetAnyLocalIPv4Address()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return unicastAddress.Address;
                }
            }
        }

        throw new InvalidOperationException("No local IPv4 address is available for the test.");
    }
}
