using System.Net;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class NetworkInterfaceHelperTests
{
    [Fact]
    public void TryParseIPv4_WhenValidIPv4_ReturnsTrue()
    {
        var result = NetworkInterfaceHelper.TryParseIPv4("192.0.2.10", out var ipAddress);

        Assert.True(result);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), ipAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("2001:db8::1")]
    public void TryParseIPv4_WhenValueIsNotValidIPv4_ReturnsFalse(string value)
    {
        var result = NetworkInterfaceHelper.TryParseIPv4(value, out var ipAddress);

        Assert.False(result);
        Assert.Null(ipAddress);
    }

    [Fact]
    public void IsIPv4Multicast_WhenInRange_ReturnsTrue()
    {
        Assert.True(NetworkInterfaceHelper.IsIPv4Multicast(IPAddress.Parse("239.1.1.1")));
    }

    [Fact]
    public void IsIPv4Multicast_WhenOutOfRange_ReturnsFalse()
    {
        Assert.False(NetworkInterfaceHelper.IsIPv4Multicast(IPAddress.Parse("198.51.100.10")));
    }

    [Fact]
    public void IsLocalIPv4Address_WhenLoopback_ReturnsTrue()
    {
        Assert.True(NetworkInterfaceHelper.IsLocalIPv4Address(IPAddress.Loopback));
    }
}
