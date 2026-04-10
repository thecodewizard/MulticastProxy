using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class LoopbackSuppressionService : ILoopbackSuppressionService
{
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<LoopbackFingerprint, DateTimeOffset> _recentlyEmitted = new();

    public LoopbackSuppressionService(IOptions<RelayOptions> options)
    {
        _window = TimeSpan.FromSeconds(Math.Max(0, options.Value.LoopbackSuppressionWindowSeconds));
    }

    public void RegisterRecentlyEmitted(int port, ReadOnlySpan<byte> payload)
    {
        if (_window <= TimeSpan.Zero)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        Cleanup(now);
        _recentlyEmitted[LoopbackFingerprint.Create(port, payload)] = now + _window;
    }

    public bool ShouldSuppress(int port, ReadOnlySpan<byte> payload)
    {
        if (_window <= TimeSpan.Zero)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        Cleanup(now);

        return _recentlyEmitted.TryGetValue(LoopbackFingerprint.Create(port, payload), out var expiresAt)
            && expiresAt > now;
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var item in _recentlyEmitted)
        {
            if (item.Value <= now)
            {
                _recentlyEmitted.TryRemove(item.Key, out _);
            }
        }
    }

    private readonly record struct LoopbackFingerprint(int Port, int PayloadLength, ulong Hash)
    {
        public static LoopbackFingerprint Create(int port, ReadOnlySpan<byte> payload) =>
            new(port, payload.Length, ComputeHash(payload));

        private static ulong ComputeHash(ReadOnlySpan<byte> payload)
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;

            var hash = offsetBasis;
            foreach (var value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            return hash;
        }
    }
}
