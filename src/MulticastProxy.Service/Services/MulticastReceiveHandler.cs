using System.Net;
using Microsoft.Extensions.Logging;
using MulticastProxy.Service.Models;

namespace MulticastProxy.Service.Services;

internal static class MulticastReceiveHandler
{
    public static async ValueTask<bool> ProcessAsync(
        int port,
        byte[] payload,
        IPEndPoint remoteEndpoint,
        ITunnelSendQueue queue,
        ILocalMulticastOriginService localMulticastOriginService,
        ILoopbackSuppressionService loopbackSuppressionService,
        IDebugEventSink debugEventSink,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var traceId = Guid.NewGuid();
        debugEventSink.PublishPacket(
            stage: "MulticastReceived",
            traceId: traceId,
            port: port,
            payload: payload,
            remoteEndpoint: remoteEndpoint.ToString(),
            details: $"Received multicast from {remoteEndpoint}.");

        if (localMulticastOriginService.IsLocallyEmitted(remoteEndpoint))
        {
            debugEventSink.PublishPacket(
                stage: "MulticastSuppressed",
                traceId: traceId,
                port: port,
                payload: payload,
                remoteEndpoint: remoteEndpoint.ToString(),
                details: $"Suppressed multicast from the relay's own sender endpoint {remoteEndpoint}.");
            logger.LogDebug(
                "Suppressed multicast datagram on port {Port} from local sender endpoint {RemoteEndpoint}.",
                port,
                remoteEndpoint);
            return false;
        }

        if (loopbackSuppressionService.ShouldSuppress(port, payload))
        {
            debugEventSink.PublishPacket(
                stage: "MulticastSuppressed",
                traceId: traceId,
                port: port,
                payload: payload,
                remoteEndpoint: remoteEndpoint.ToString(),
                details: $"Suppressed recently re-emitted multicast from {remoteEndpoint}.");
            logger.LogDebug(
                "Suppressed multicast datagram on port {Port} from {RemoteEndpoint} because it matched a recently emitted packet.",
                port,
                remoteEndpoint);
            return false;
        }

        if (NetworkInterfaceHelper.IsLocalIPv4Address(remoteEndpoint.Address))
        {
            var registeredSenders = localMulticastOriginService.GetRegisteredSenders();
            var registeredSendersText = registeredSenders.Count == 0
                ? "<none>"
                : string.Join(", ", registeredSenders);

            debugEventSink.PublishPacket(
                stage: "LoopProtectionMiss",
                traceId: traceId,
                port: port,
                payload: payload,
                remoteEndpoint: remoteEndpoint.ToString(),
                details: $"Received multicast from local address {remoteEndpoint}, but it did not match the relay's registered sender endpoints [{registeredSendersText}] and did not match a recent-emission fingerprint.");
        }

        await queue.EnqueueAsync(new RelayDatagram(traceId, port, payload), cancellationToken);
        logger.LogDebug("Received multicast datagram on port {Port} with {Length} bytes.", port, payload.Length);
        return true;
    }
}
