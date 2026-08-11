using System.Text;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class PayloadRewriteService : IPayloadRewriteService
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const string DiscoveryCrcMarker = "[CRC:0x";
    private readonly RewriteOptions _options;
    private readonly IDebugEventSink _debugEventSink;
    private readonly ILogger<PayloadRewriteService> _logger;

    public PayloadRewriteService(
        IOptions<RewriteOptions> options,
        IDebugEventSink debugEventSink,
        ILogger<PayloadRewriteService> logger)
    {
        _options = options.Value;
        _debugEventSink = debugEventSink;
        _logger = logger;
    }

    public byte[] RewriteIfNeeded(Guid traceId, int port, byte[] payload)
    {
        if (string.IsNullOrWhiteSpace(_options.PayloadRewriteSourceSubnet)
            || string.IsNullOrWhiteSpace(_options.PayloadRewriteDestinationSubnet))
        {
            return payload.ToArray();
        }

        try
        {
            var text = StrictUtf8.GetString(payload);
            if (!text.Contains(_options.PayloadRewriteSourceSubnet, StringComparison.Ordinal))
            {
                return payload.ToArray();
            }

            var rewritten = text.Replace(
                _options.PayloadRewriteSourceSubnet,
                _options.PayloadRewriteDestinationSubnet,
                StringComparison.Ordinal);

            rewritten = RewriteDiscoveryCrcIfPresent(rewritten);

            _logger.LogDebug("Payload rewrite applied.");
            var rewrittenBytes = StrictUtf8.GetBytes(rewritten);
            _debugEventSink.PublishPacket(
                stage: "PayloadRewriteApplied",
                traceId: traceId,
                port: port,
                payload: payload,
                details: $"Replaced subnet '{_options.PayloadRewriteSourceSubnet}' with '{_options.PayloadRewriteDestinationSubnet}'.",
                rewrittenPayload: rewrittenBytes);
            return rewrittenBytes;
        }
        catch (DecoderFallbackException)
        {
            _logger.LogDebug("Payload rewrite skipped because payload is not valid UTF-8 text.");
            return payload.ToArray();
        }
    }

    private static string RewriteDiscoveryCrcIfPresent(string text)
    {
        var crcMarkerIndex = text.IndexOf(DiscoveryCrcMarker, StringComparison.Ordinal);
        if (crcMarkerIndex < 0)
        {
            return text;
        }

        var crcValueIndex = crcMarkerIndex + DiscoveryCrcMarker.Length;
        if (crcValueIndex + 2 >= text.Length || text[crcValueIndex + 2] != ']')
        {
            return text;
        }

        if (!TryComputeXorChecksum(text.AsSpan(0, crcMarkerIndex), out var checksum))
        {
            return text;
        }

        return string.Concat(
            text.AsSpan(0, crcValueIndex),
            checksum.ToString("X2"),
            text.AsSpan(crcValueIndex + 2));
    }

    private static bool TryComputeXorChecksum(ReadOnlySpan<char> text, out byte checksum)
    {
        checksum = 0;
        foreach (var character in text)
        {
            if (character > byte.MaxValue)
            {
                checksum = 0;
                return false;
            }

            checksum ^= (byte)character;
        }

        return true;
    }
}
