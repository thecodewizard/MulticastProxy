using System.Net;

namespace MulticastProxy.Service.Services;

public interface ILocalMulticastOriginService
{
    void RegisterSender(IPEndPoint localEndpoint);
    bool IsLocallyEmitted(IPEndPoint remoteEndpoint);
    IReadOnlyCollection<string> GetRegisteredSenders();
}
