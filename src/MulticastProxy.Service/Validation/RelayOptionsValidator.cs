using System.Net;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Validation;

public sealed class RelayOptionsValidator : IValidateOptions<RelayOptions>
{
    public ValidateOptionsResult Validate(string? name, RelayOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.MulticastGroup)
            || !IPAddress.TryParse(options.MulticastGroup, out var groupAddress)
            || groupAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || !IsMulticast(groupAddress))
        {
            failures.Add("Relay:MulticastGroup must be a valid IPv4 multicast address.");
        }

        if (options.MulticastPorts.Count == 0)
        {
            failures.Add("Relay:MulticastPorts must contain at least one port.");
        }
        else
        {
            foreach (var port in options.MulticastPorts)
            {
                if (port is < 0 or > 65535)
                {
                    failures.Add($"Relay:MulticastPorts contains invalid value '{port}'. Valid range is 0-65535.");
                }
            }
        }

        if (options.TunnelPort is < 0 or > 65535)
        {
            failures.Add("Relay:TunnelPort must be between 0 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.DestinationIP) || !IPAddress.TryParse(options.DestinationIP, out _))
        {
            failures.Add("Relay:DestinationIP must be a valid IP address.");
        }

        ValidateOptionalIp(options.ListenInterfaceIP, "Relay:ListenInterfaceIP", failures);
        ValidateOptionalIp(options.SendInterfaceIP, "Relay:SendInterfaceIP", failures);

        if (options.DeduplicationWindowSeconds <= 0)
        {
            failures.Add("Relay:DeduplicationWindowSeconds must be greater than 0.");
        }

        if (options.LoopbackSuppressionWindowSeconds < 0)
        {
            failures.Add("Relay:LoopbackSuppressionWindowSeconds must be 0 or greater.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static void ValidateOptionalIp(string? value, string name, ICollection<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IPAddress.TryParse(value, out _))
        {
            failures.Add($"{name} must be a valid IP address when provided.");
        }
    }

    private static bool IsMulticast(IPAddress address)
    {
        var firstOctet = address.GetAddressBytes()[0];
        return firstOctet is >= 224 and <= 239;
    }
}
