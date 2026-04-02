using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class DeduplicationService : IDeduplicationService
{
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _seen = new();

    public DeduplicationService(IOptions<RelayOptions> options)
    {
        _window = TimeSpan.FromSeconds(options.Value.DeduplicationWindowSeconds);
    }

    public bool TryRegister(Guid packetId)
    {
        var now = DateTimeOffset.UtcNow;
        Cleanup(now);

        return _seen.TryAdd(packetId, now + _window);
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var item in _seen)
        {
            if (item.Value <= now)
            {
                _seen.TryRemove(item.Key, out _);
            }
        }
    }
}
