using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;
using MulticastProxy.Service.Protocol;

namespace MulticastProxy.Service.Services;

public sealed class TunnelSenderService : BackgroundService
{
    private readonly RelayOptions _relayOptions;
    private readonly ITunnelSendQueue _queue;
    private readonly RelayEnvelopeSerializer _serializer;
    private readonly ILogger<TunnelSenderService> _logger;

    public TunnelSenderService(
        IOptions<RelayOptions> relayOptions,
        ITunnelSendQueue queue,
        RelayEnvelopeSerializer serializer,
        ILogger<TunnelSenderService> logger)
    {
        _relayOptions = relayOptions.Value;
        _queue = queue;
        _serializer = serializer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!NetworkInterfaceHelper.TryParseIPv4(_relayOptions.DestinationIP, out var destinationIp) || destinationIp is null)
        {
            _logger.LogError(
                "Tunnel sender disabled because Relay:DestinationIP '{DestinationIP}' is not a valid IPv4 address.",
                _relayOptions.DestinationIP);
            return;
        }

        if (_relayOptions.TunnelPort is < 0 or > 65535)
        {
            _logger.LogError(
                "Tunnel sender disabled because Relay:TunnelPort '{TunnelPort}' is outside 0-65535.",
                _relayOptions.TunnelPort);
            return;
        }

        var destination = new IPEndPoint(destinationIp, _relayOptions.TunnelPort);
        using var sender = new UdpClient(AddressFamily.InterNetwork);

        _logger.LogInformation("Tunnel sender started for destination {Destination}.", destination);

        await foreach (var datagram in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var packetId = Guid.NewGuid();
                var envelope = _serializer.Serialize(_relayOptions.InstanceId, packetId, datagram.Port, datagram.Payload);
                await sender.SendAsync(envelope, destination, stoppingToken);
                _logger.LogDebug("Sent tunneled packet {PacketId} for port {Port}.", packetId, datagram.Port);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Failed to send tunneled packet to {Destination}.", destination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected tunnel sender failure.");
            }
        }
    }
}
