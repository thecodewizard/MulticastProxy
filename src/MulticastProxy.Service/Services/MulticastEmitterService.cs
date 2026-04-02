using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class MulticastEmitterService : BackgroundService
{
    private readonly RelayOptions _relayOptions;
    private readonly IMulticastEmitQueue _queue;
    private readonly IPayloadRewriteService _payloadRewriteService;
    private readonly ILogger<MulticastEmitterService> _logger;

    public MulticastEmitterService(
        IOptions<RelayOptions> relayOptions,
        IMulticastEmitQueue queue,
        IPayloadRewriteService payloadRewriteService,
        ILogger<MulticastEmitterService> logger)
    {
        _relayOptions = relayOptions.Value;
        _queue = queue;
        _payloadRewriteService = payloadRewriteService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var multicastGroup = IPAddress.Parse(_relayOptions.MulticastGroup);
        using var sender = new UdpClient(AddressFamily.InterNetwork);

        sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

        if (!string.IsNullOrWhiteSpace(_relayOptions.SendInterfaceIP)
            && IPAddress.TryParse(_relayOptions.SendInterfaceIP, out var sendInterface))
        {
            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, sendInterface.GetAddressBytes());
        }

        _logger.LogInformation("Multicast emitter started for group {Group}.", multicastGroup);

        await foreach (var datagram in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var payload = _payloadRewriteService.RewriteIfNeeded(datagram.Payload);
                var endpoint = new IPEndPoint(multicastGroup, datagram.Port);
                await sender.SendAsync(payload, endpoint, stoppingToken);
                _logger.LogDebug("Emitted multicast datagram to {Endpoint} with {Length} bytes.", endpoint, payload.Length);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Multicast emit failed and packet was dropped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected multicast emitter failure.");
            }
        }
    }
}
