using Microsoft.Extensions.Logging.Abstractions;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Validation;

namespace MulticastProxy.Service.Tests;

public class RelayOptionsPostConfigureTests
{
    [Fact]
    public void PostConfigure_WhenInstanceIdIsPlaceholder_GeneratesRuntimeInstanceId()
    {
        var options = CreateOptions();
        options.InstanceId = RelayOptions.PlaceholderInstanceId;
        var postConfigure = new RelayOptionsPostConfigure(NullLogger<RelayOptionsPostConfigure>.Instance);

        postConfigure.PostConfigure(null, options);

        Assert.NotEqual(Guid.Empty, options.InstanceId);
        Assert.NotEqual(RelayOptions.PlaceholderInstanceId, options.InstanceId);
    }

    [Fact]
    public void PostConfigure_WhenInstanceIdIsExplicit_KeepsConfiguredValue()
    {
        var explicitInstanceId = Guid.NewGuid();
        var options = CreateOptions();
        options.InstanceId = explicitInstanceId;
        var postConfigure = new RelayOptionsPostConfigure(NullLogger<RelayOptionsPostConfigure>.Instance);

        postConfigure.PostConfigure(null, options);

        Assert.Equal(explicitInstanceId, options.InstanceId);
    }

    private static RelayOptions CreateOptions() => new()
    {
        MulticastGroup = "239.0.0.1",
        MulticastPorts = [9053],
        TunnelPort = 19053,
        DestinationIP = "198.51.100.10",
        DeduplicationWindowSeconds = 30
    };
}
