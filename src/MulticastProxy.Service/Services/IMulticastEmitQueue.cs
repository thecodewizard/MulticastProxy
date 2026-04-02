using MulticastProxy.Service.Models;

namespace MulticastProxy.Service.Services;

public interface IMulticastEmitQueue
{
    ValueTask EnqueueAsync(RelayDatagram datagram, CancellationToken cancellationToken);
    IAsyncEnumerable<RelayDatagram> ReadAllAsync(CancellationToken cancellationToken);
}
