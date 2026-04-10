using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Validation;

public sealed class RelayOptionsPostConfigure : IPostConfigureOptions<RelayOptions>
{
    private readonly ILogger<RelayOptionsPostConfigure> _logger;

    public RelayOptionsPostConfigure(ILogger<RelayOptionsPostConfigure> logger)
    {
        _logger = logger;
    }

    public void PostConfigure(string? name, RelayOptions options)
    {
        if (options.InstanceId != Guid.Empty && options.InstanceId != RelayOptions.PlaceholderInstanceId)
        {
            return;
        }

        options.InstanceId = Guid.NewGuid();
        _logger.LogWarning(
            "Relay:InstanceId was left empty or at the packaged placeholder value. Generated runtime instance ID {InstanceId} so this node does not drop tunnel traffic from its peer.",
            options.InstanceId);
    }
}
