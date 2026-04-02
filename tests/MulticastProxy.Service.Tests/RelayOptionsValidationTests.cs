using MulticastProxy.Service.Options;
using MulticastProxy.Service.Validation;

namespace MulticastProxy.Service.Tests;

public class RelayOptionsValidationTests
{
    private readonly RelayOptionsValidator _validator = new();

    [Fact]
    public void Validate_WhenConfigurationIsValid_ReturnsSuccess()
    {
        var options = CreateValidOptions();

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenPortIsOutOfRange_ReturnsFailure()
    {
        var options = CreateValidOptions();
        options.MulticastPorts = [70000];

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("0-65535", string.Join(',', result.Failures ?? Array.Empty<string>()));
    }

    [Fact]
    public void Validate_WhenMulticastGroupIsInvalid_ReturnsFailure()
    {
        var options = CreateValidOptions();
        options.MulticastGroup = "203.0.113.1";

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("multicast", string.Join(',', result.Failures ?? Array.Empty<string>()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhenMulticastGroupIsWildcardAddress_ReturnsSuccess()
    {
        var options = CreateValidOptions();
        options.MulticastGroup = "224.0.0.0";

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RewriteValidator_WhenOnlyOneSubnetProvided_Fails()
    {
        var validator = new RewriteOptionsValidator();
        var options = new RewriteOptions { PayloadRewriteSourceSubnet = "192.0.2." };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    private static RelayOptions CreateValidOptions() => new()
    {
        MulticastGroup = "239.0.0.1",
        MulticastPorts = [9053],
        TunnelPort = 19053,
        DestinationIP = "198.51.100.10",
        ListenInterfaceIP = "192.0.2.20",
        SendInterfaceIP = "198.51.100.20",
        InstanceId = Guid.NewGuid(),
        DeduplicationWindowSeconds = 30
    };
}
