using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Models;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Protocol;

namespace MulticastProxy.Service.Services;

public sealed class TunnelReceiverService : BackgroundService
{
    private readonly RelayOptions _relayOptions;
    private readonly IMulticastEmitQueue _emitQueue;
    private readonly RelayEnvelopeSerializer _serializer;
    private readonly IDeduplicationService _deduplicationService;
    private readonly IDebugEventSink _debugEventSink;
    private readonly ILogger<TunnelReceiverService> _logger;

    public TunnelReceiverService(
        IOptions<RelayOptions> relayOptions,
        IMulticastEmitQueue emitQueue,
        RelayEnvelopeSerializer serializer,
        IDeduplicationService deduplicationService,
        IDebugEventSink debugEventSink,
        ILogger<TunnelReceiverService> logger)
    {
        _relayOptions = relayOptions.Value;
        _emitQueue = emitQueue;
        _serializer = serializer;
        _deduplicationService = deduplicationService;
        _debugEventSink = debugEventSink;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_relayOptions.TunnelPort is < 0 or > 65535)
        {
            _logger.LogError(
                "Tunnel receiver disabled because Relay:TunnelPort '{TunnelPort}' is outside 0-65535.",
                _relayOptions.TunnelPort);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpClient? receiver = null;

            try
            {
                receiver = new UdpClient(new IPEndPoint(IPAddress.Any, _relayOptions.TunnelPort));
                _logger.LogInformation("Tunnel receiver listening on UDP port {Port}.", _relayOptions.TunnelPort);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = await receiver.ReceiveAsync(stoppingToken);
                    if (!_serializer.TryDeserialize(result.Buffer, out var envelope, out var error) || envelope is null)
                    {
                        _debugEventSink.PublishPacket(
                            stage: "TunnelReceiveRejected",
                            traceId: Guid.Empty,
                            port: _relayOptions.TunnelPort,
                            payload: result.Buffer,
                            remoteEndpoint: result.RemoteEndPoint.ToString(),
                            details: $"Malformed relay envelope: {error}");
                        _logger.LogWarning("Malformed relay envelope from {Remote}: {Error}", result.RemoteEndPoint, error);
                        continue;
                    }

                    _debugEventSink.PublishPacket(
                        stage: "TunnelReceived",
                        traceId: envelope.PacketId,
                        port: envelope.OriginalMulticastPort,
                        payload: envelope.Payload,
                        remoteEndpoint: result.RemoteEndPoint.ToString(),
                        details: $"Accepted tunnel payload from {result.RemoteEndPoint}.");

                    if (envelope.SenderInstanceId == _relayOptions.InstanceId)
                    {
                        _debugEventSink.PublishPacket(
                            stage: "TunnelDropped",
                            traceId: envelope.PacketId,
                            port: envelope.OriginalMulticastPort,
                            payload: envelope.Payload,
                            remoteEndpoint: result.RemoteEndPoint.ToString(),
                            details: "Dropped packet because the remote sender instance ID matches the local instance ID.");
                        _logger.LogDebug("Dropped packet {PacketId} from local instance ID.", envelope.PacketId);
                        continue;
                    }

                    if (!_deduplicationService.TryRegister(envelope.PacketId))
                    {
                        _debugEventSink.PublishPacket(
                            stage: "TunnelDropped",
                            traceId: envelope.PacketId,
                            port: envelope.OriginalMulticastPort,
                            payload: envelope.Payload,
                            remoteEndpoint: result.RemoteEndPoint.ToString(),
                            details: "Dropped duplicate tunneled packet.");
                        _logger.LogDebug("Dropped duplicate packet {PacketId}.", envelope.PacketId);
                        continue;
                    }

                    await _emitQueue.EnqueueAsync(new RelayDatagram(envelope.PacketId, envelope.OriginalMulticastPort, envelope.Payload), stoppingToken);
                    _logger.LogDebug("Accepted tunneled packet {PacketId} for multicast port {Port}.", envelope.PacketId, envelope.OriginalMulticastPort);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Tunnel receiver socket failure on UDP port {Port}; retrying.",
                    _relayOptions.TunnelPort);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected tunnel receiver error; continuing.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            finally
            {
                receiver?.Dispose();
            }
        }
    }
}
