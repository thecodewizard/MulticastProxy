using System.Collections.Concurrent;
using System.Net;

namespace MulticastProxy.Service.Services;

public sealed class LocalMulticastOriginService : ILocalMulticastOriginService
{
    private readonly ConcurrentDictionary<int, IPAddress> _ports = new();

    public void RegisterSender(IPEndPoint localEndpoint)
    {
        _ports[localEndpoint.Port] = localEndpoint.Address;
    }

    public bool IsLocallyEmitted(IPEndPoint remoteEndpoint)
    {
        if (!_ports.TryGetValue(remoteEndpoint.Port, out var registeredAddress))
        {
            return false;
        }

        if (IPAddress.Any.Equals(registeredAddress))
        {
            return NetworkInterfaceHelper.IsLocalIPv4Address(remoteEndpoint.Address);
        }

        if (registeredAddress.Equals(remoteEndpoint.Address))
        {
            return true;
        }

        return NetworkInterfaceHelper.IsLocalIPv4Address(remoteEndpoint.Address);
    }

    public IReadOnlyCollection<string> GetRegisteredSenders()
    {
        return _ports
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{entry.Value}:{entry.Key}")
            .ToArray();
    }
}
