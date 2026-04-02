using System.Threading.Channels;
using MulticastProxy.Service.Models;

namespace MulticastProxy.Service.Services;

public sealed class MulticastEmitQueue : IMulticastEmitQueue
{
    private readonly Channel<RelayDatagram> _channel = Channel.CreateBounded<RelayDatagram>(new BoundedChannelOptions(5000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(RelayDatagram datagram, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(datagram, cancellationToken);

    public IAsyncEnumerable<RelayDatagram> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
