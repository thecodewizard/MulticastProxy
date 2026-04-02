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
        if (!NetworkInterfaceHelper.TryParseIPv4(_relayOptions.MulticastGroup, out var multicastGroup)
            || multicastGroup is null
            || !NetworkInterfaceHelper.IsIPv4Multicast(multicastGroup))
        {
            _logger.LogError(
                "Multicast emitter disabled because Relay:MulticastGroup '{MulticastGroup}' is not a valid IPv4 multicast address.",
                _relayOptions.MulticastGroup);
            return;
        }

        IPAddress? sendInterface = null;
        if (!string.IsNullOrWhiteSpace(_relayOptions.SendInterfaceIP))
        {
            if (!NetworkInterfaceHelper.TryParseIPv4(_relayOptions.SendInterfaceIP, out sendInterface) || sendInterface is null)
            {
                _logger.LogError(
                    "Multicast emitter disabled because Relay:SendInterfaceIP '{SendInterface}' is not a valid IPv4 address.",
                    _relayOptions.SendInterfaceIP);
                return;
            }

            if (!NetworkInterfaceHelper.IsLocalIPv4Address(sendInterface))
            {
                _logger.LogError(
                    "Multicast emitter disabled because Relay:SendInterfaceIP '{SendInterface}' is not assigned to any local network interface.",
                    sendInterface);
                return;
            }
        }

        using var sender = new UdpClient(AddressFamily.InterNetwork);

        try
        {
            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            if (!TryConfigureSendInterface(sender.Client, sendInterface))
            {
                return;
            }
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Multicast emitter disabled because socket setup failed.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Multicast emitter disabled due to unexpected startup error.");
            return;
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

    private bool TryConfigureSendInterface(Socket socket, IPAddress? sendInterface)
    {
        if (sendInterface is null)
        {
            return true;
        }

        try
        {
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, sendInterface.GetAddressBytes());
            _logger.LogInformation("Using send interface {InterfaceAddress} for multicast emission.", sendInterface);
            return true;
        }
        catch (SocketException ex)
        {
            _logger.LogError(
                ex,
                "Multicast emitter disabled because Relay:SendInterfaceIP '{InterfaceAddress}' could not be applied.",
                sendInterface);
            return false;
        }
    }
}
