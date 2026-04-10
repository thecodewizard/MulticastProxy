using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Validation;

namespace MulticastProxy.Service.Tests;

public class DebugWindowOptionsPostConfigureTests
{
    [Fact]
    public void PostConfigure_WhenTopLevelSectionUsesEnabledString_EnablesDebugWindow()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["DebugWindow"] = "Enabled"
        });

        var options = new DebugWindowOptions { Enabled = false };
        var postConfigure = CreatePostConfigure(configuration);

        postConfigure.PostConfigure(null, options);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void PostConfigure_WhenLegacyRelaySectionUsesEnabledString_EnablesDebugWindow()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Relay:DebugWindow"] = "Enabled"
        });

        var options = new DebugWindowOptions { Enabled = false };
        var postConfigure = CreatePostConfigure(configuration);

        postConfigure.PostConfigure(null, options);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void PostConfigure_WhenTopLevelSectionExists_IgnoresLegacyRelaySection()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["DebugWindow:Enabled"] = "false",
            ["Relay:DebugWindow"] = "Enabled"
        });

        var options = new DebugWindowOptions { Enabled = true };
        configuration.GetSection(DebugWindowOptions.SectionName).Bind(options);
        var postConfigure = CreatePostConfigure(configuration);

        postConfigure.PostConfigure(null, options);

        Assert.False(options.Enabled);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static DebugWindowOptionsPostConfigure CreatePostConfigure(IConfiguration configuration) =>
        new(configuration, NullLogger<DebugWindowOptionsPostConfigure>.Instance);
}
