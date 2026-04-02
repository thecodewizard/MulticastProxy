using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MulticastProxy.Service.Services;

public static class NetworkInterfaceHelper
{
    public static bool TryParseIPv4(string? value, out IPAddress? ipAddress)
    {
        ipAddress = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        ipAddress = parsed;
        return true;
    }

    public static bool IsLocalIPv4Address(IPAddress ipAddress)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var properties = networkInterface.GetIPProperties();
            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork
                    && Equals(unicastAddress.Address, ipAddress))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsIPv4Multicast(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var firstOctet = address.GetAddressBytes()[0];
        return firstOctet is >= 224 and <= 239;
    }
}
