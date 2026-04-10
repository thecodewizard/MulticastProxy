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
    private readonly ILocalMulticastOriginService _localMulticastOriginService;
    private readonly ILoopbackSuppressionService _loopbackSuppressionService;
    private readonly IDebugEventSink _debugEventSink;
    private readonly ILogger<MulticastEmitterService> _logger;

    public MulticastEmitterService(
        IOptions<RelayOptions> relayOptions,
        IMulticastEmitQueue queue,
        IPayloadRewriteService payloadRewriteService,
        ILocalMulticastOriginService localMulticastOriginService,
        ILoopbackSuppressionService loopbackSuppressionService,
        IDebugEventSink debugEventSink,
        ILogger<MulticastEmitterService> logger)
    {
        _relayOptions = relayOptions.Value;
        _queue = queue;
        _payloadRewriteService = payloadRewriteService;
        _localMulticastOriginService = localMulticastOriginService;
        _loopbackSuppressionService = loopbackSuppressionService;
        _debugEventSink = debugEventSink;
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
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            using var sender = new UdpClient(AddressFamily.InterNetwork);
            IPEndPoint? localSenderEndpoint = null;

            try
            {
                sender.MulticastLoopback = false;
                sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                sender.Client.Bind(new IPEndPoint(sendInterface ?? IPAddress.Any, 0));
                if (sender.Client.LocalEndPoint is IPEndPoint localEndpoint)
                {
                    localSenderEndpoint = localEndpoint;
                    _localMulticastOriginService.RegisterSender(localEndpoint);
                    _debugEventSink.PublishPacket(
                        stage: "LocalSenderRegistered",
                        traceId: Guid.Empty,
                        port: 0,
                        payload: Array.Empty<byte>(),
                        remoteEndpoint: localEndpoint.ToString(),
                        details: $"Registered local multicast sender endpoint {localEndpoint}.");
                    _logger.LogInformation("Registered local multicast sender endpoint {LocalEndpoint}.", localEndpoint);
                }

                if (!TryConfigureSendInterface(sender.Client, sendInterface))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Multicast emitter started for group {Group} with local multicast loopback disabled.", multicastGroup);

                await foreach (var datagram in _queue.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        var payload = _payloadRewriteService.RewriteIfNeeded(datagram.TraceId, datagram.Port, datagram.Payload);
                        var endpoint = new IPEndPoint(multicastGroup, datagram.Port);
                        _loopbackSuppressionService.RegisterRecentlyEmitted(datagram.Port, payload);
                        await sender.SendAsync(payload, endpoint, stoppingToken);
                        _debugEventSink.PublishPacket(
                            stage: "MulticastEmitted",
                            traceId: datagram.TraceId,
                            port: datagram.Port,
                            payload: payload,
                            remoteEndpoint: endpoint.ToString(),
                            details: localSenderEndpoint is null
                                ? $"Re-multicasted packet to {endpoint} after arming loopback suppression for the emitted payload."
                                : $"Re-multicasted packet to {endpoint} from local sender {localSenderEndpoint} after arming loopback suppression for the emitted payload.");
                        _logger.LogDebug("Emitted multicast datagram to {Endpoint} with {Length} bytes.", endpoint, payload.Length);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        _debugEventSink.PublishPacket(
                            stage: "MulticastEmitFailed",
                            traceId: datagram.TraceId,
                            port: datagram.Port,
                            payload: datagram.Payload,
                            remoteEndpoint: multicastGroup.ToString(),
                            details: $"Multicast emit failure: {ex.SocketErrorCode}.");
                        _logger.LogWarning(ex, "Multicast emit failed and packet was dropped.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected multicast emitter failure.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Multicast emitter socket setup failed. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected multicast emitter startup error. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
            _logger.LogWarning(
                ex,
                "Relay:SendInterfaceIP '{InterfaceAddress}' could not be applied yet. Retrying.",
                sendInterface);
            return false;
        }
    }
}
