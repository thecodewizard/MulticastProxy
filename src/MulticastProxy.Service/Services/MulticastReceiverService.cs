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
        _logger.LogInformation("Multicast receiver started for {PortCount} ports.", _relayOptions.MulticastPorts.Count);

        var tasks = _relayOptions.MulticastPorts
            .Distinct()
            .Select(port => RunReceiverForPortAsync(port, stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task RunReceiverForPortAsync(int port, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);

            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));

                var multicast = IPAddress.Parse(_relayOptions.MulticastGroup);
                JoinMulticastGroup(udp, multicast);

                _logger.LogInformation("Listening for multicast group {Group} on port {Port}.", _relayOptions.MulticastGroup, port);

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

    private void JoinMulticastGroup(UdpClient udp, IPAddress multicast)
    {
        if (string.IsNullOrWhiteSpace(_relayOptions.ListenInterfaceIP)
            || !IPAddress.TryParse(_relayOptions.ListenInterfaceIP, out var listenInterface))
        {
            udp.JoinMulticastGroup(multicast);
            return;
        }

        try
        {
            udp.JoinMulticastGroup(multicast, listenInterface);
            _logger.LogInformation(
                "Joined multicast group {Group} using listen interface {InterfaceAddress}.",
                multicast,
                listenInterface);
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to apply Relay:ListenInterfaceIP '{InterfaceAddress}' for group {Group}. Falling back to default interface.",
                listenInterface,
                multicast);
            udp.JoinMulticastGroup(multicast);
        }
    }
}
