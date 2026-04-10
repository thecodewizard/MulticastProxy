using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MulticastProxy.Service.Models;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Services;

namespace MulticastProxy.Service.Tests;

public class MulticastReceiveHandlerTests
{
    [Fact]
    public async Task ProcessAsync_WhenPayloadWasRecentlyEmitted_SuppressesInsteadOfQueueing()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var queue = new TestTunnelSendQueue();
        var debugSink = new RecordingDebugEventSink();
        var suppression = CreateSuppressionService(windowSeconds: 5);
        var localOriginService = new LocalMulticastOriginService();
        suppression.RegisterRecentlyEmitted(9053, payload);

        var forwarded = await MulticastReceiveHandler.ProcessAsync(
            9053,
            payload,
            new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63200),
            queue,
            localOriginService,
            suppression,
            debugSink,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.False(forwarded);
        Assert.Empty(queue.Items);
        Assert.Equal(["MulticastReceived", "MulticastSuppressed"], debugSink.Stages);
    }

    [Fact]
    public async Task ProcessAsync_WhenPayloadWasNotRecentlyEmitted_QueuesForTunnel()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var queue = new TestTunnelSendQueue();
        var debugSink = new RecordingDebugEventSink();
        var suppression = CreateSuppressionService(windowSeconds: 5);
        var localOriginService = new LocalMulticastOriginService();

        var forwarded = await MulticastReceiveHandler.ProcessAsync(
            9053,
            payload,
            new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63200),
            queue,
            localOriginService,
            suppression,
            debugSink,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.True(forwarded);
        var queued = Assert.Single(queue.Items);
        Assert.Equal(9053, queued.Port);
        Assert.Equal(payload, queued.Payload);
        Assert.Equal(["MulticastReceived"], debugSink.Stages);
    }

    [Fact]
    public async Task ProcessAsync_WhenPacketCameFromRegisteredLocalSender_SuppressesInsteadOfQueueing()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var queue = new TestTunnelSendQueue();
        var debugSink = new RecordingDebugEventSink();
        var suppression = CreateSuppressionService(windowSeconds: 5);
        var localOriginService = new LocalMulticastOriginService();
        localOriginService.RegisterSender(new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255));

        var forwarded = await MulticastReceiveHandler.ProcessAsync(
            9053,
            payload,
            new IPEndPoint(IPAddress.Parse("10.100.5.75"), 63255),
            queue,
            localOriginService,
            suppression,
            debugSink,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.False(forwarded);
        Assert.Empty(queue.Items);
        Assert.Equal(["MulticastReceived", "MulticastSuppressed"], debugSink.Stages);
    }

    private static LoopbackSuppressionService CreateSuppressionService(int windowSeconds) =>
        new(Microsoft.Extensions.Options.Options.Create(new RelayOptions
        {
            MulticastGroup = "239.0.0.1",
            MulticastPorts = [9053],
            TunnelPort = 19053,
            DestinationIP = "198.51.100.10",
            LoopbackSuppressionWindowSeconds = windowSeconds
        }));

    private sealed class TestTunnelSendQueue : ITunnelSendQueue
    {
        public ConcurrentQueue<RelayDatagram> Items { get; } = new();

        public ValueTask EnqueueAsync(RelayDatagram datagram, CancellationToken cancellationToken)
        {
            Items.Enqueue(datagram);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<RelayDatagram> ReadAllAsync(CancellationToken cancellationToken)
        {
            return EmptyAsync();
        }

        private static async IAsyncEnumerable<RelayDatagram> EmptyAsync()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingDebugEventSink : IDebugEventSink
    {
        public List<string> Stages { get; } = [];

        public void PublishPacket(
            string stage,
            Guid traceId,
            int port,
            byte[] payload,
            string? remoteEndpoint = null,
            string? details = null,
            byte[]? rewrittenPayload = null)
        {
            Stages.Add(stage);
        }
    }
}
