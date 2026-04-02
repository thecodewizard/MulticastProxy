using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Models;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class MulticastReceiverService : BackgroundService
{
    private readonly RelayOptions _relayOptions;
    private readonly ITunnelSendQueue _queue;
    private readonly ILogger<MulticastReceiverService> _logger;

    public MulticastReceiverService(
        IOptions<RelayOptions> relayOptions,
        ITunnelSendQueue queue,
        ILogger<MulticastReceiverService> logger)
    {
        _relayOptions = relayOptions.Value;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!NetworkInterfaceHelper.TryParseIPv4(_relayOptions.MulticastGroup, out var multicastGroup)
            || multicastGroup is null
            || !NetworkInterfaceHelper.IsIPv4Multicast(multicastGroup))
        {
            _logger.LogError(
                "Multicast receiver disabled because Relay:MulticastGroup '{MulticastGroup}' is not a valid IPv4 multicast address.",
                _relayOptions.MulticastGroup);
            return;
        }

        if (_relayOptions.MulticastPorts.Count == 0)
        {
            _logger.LogWarning("Multicast receiver disabled because Relay:MulticastPorts is empty.");
            return;
        }

        IPAddress? listenInterface = null;
        if (!string.IsNullOrWhiteSpace(_relayOptions.ListenInterfaceIP))
        {
            if (!NetworkInterfaceHelper.TryParseIPv4(_relayOptions.ListenInterfaceIP, out listenInterface) || listenInterface is null)
            {
                _logger.LogError(
                    "Multicast receiver disabled because Relay:ListenInterfaceIP '{ListenInterface}' is not a valid IPv4 address.",
                    _relayOptions.ListenInterfaceIP);
                return;
            }

            if (!NetworkInterfaceHelper.IsLocalIPv4Address(listenInterface))
            {
                _logger.LogError(
                    "Multicast receiver disabled because Relay:ListenInterfaceIP '{ListenInterface}' is not assigned to any local network interface.",
                    listenInterface);
                return;
            }
        }

        var ports = _relayOptions.MulticastPorts
            .Distinct()
            .Where(port => port is >= 0 and <= 65535)
            .ToArray();

        if (ports.Length == 0)
        {
            _logger.LogError("Multicast receiver disabled because Relay:MulticastPorts has no values in range 0-65535.");
            return;
        }

        _logger.LogInformation("Multicast receiver started for {PortCount} ports.", ports.Length);

        var tasks = ports
            .Select(port => RunReceiverForPortAsync(port, multicastGroup, listenInterface, stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task RunReceiverForPortAsync(int port, IPAddress multicastGroup, IPAddress? listenInterface, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);

            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                JoinMulticastGroup(udp, multicastGroup, listenInterface);

                _logger.LogInformation("Listening for multicast group {Group} on port {Port}.", multicastGroup, port);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var receiveResult = await udp.ReceiveAsync(cancellationToken);
                    await _queue.EnqueueAsync(new RelayDatagram(port, receiveResult.Buffer), cancellationToken);
                    _logger.LogDebug("Received multicast datagram on port {Port} with {Length} bytes.", port, receiveResult.Buffer.Length);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable && listenInterface is not null)
            {
                _logger.LogError(
                    ex,
                    "Multicast receiver for port {Port} disabled because listen interface {InterfaceAddress} is not available.",
                    port,
                    listenInterface);
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Multicast receive failure on port {Port}. Retrying.", port);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected multicast receiver failure on port {Port}. Retrying.", port);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private void JoinMulticastGroup(UdpClient udp, IPAddress multicast, IPAddress? listenInterface)
    {
        if (IsWildcardMulticastGroup(multicast))
        {
            _logger.LogWarning(
                "Relay:MulticastGroup is configured as {Group}. Running in wildcard mode (no IGMP join); receiving multicast now depends on OS socket behavior.",
                multicast);
            return;
        }

        if (listenInterface is null)
        {
            udp.JoinMulticastGroup(multicast);
            return;
        }

        udp.JoinMulticastGroup(multicast, listenInterface);
        _logger.LogInformation(
            "Joined multicast group {Group} using listen interface {InterfaceAddress}.",
            multicast,
            listenInterface);
    }

    private static bool IsWildcardMulticastGroup(IPAddress multicast)
    {
        var bytes = multicast.GetAddressBytes();
        return bytes[0] == 224 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0;
    }
}
