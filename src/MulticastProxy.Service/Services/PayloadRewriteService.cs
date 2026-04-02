using System.Text;
using Microsoft.Extensions.Options;
using MulticastProxy.Service.Options;

namespace MulticastProxy.Service.Services;

public sealed class PayloadRewriteService : IPayloadRewriteService
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly RewriteOptions _options;
    private readonly ILogger<PayloadRewriteService> _logger;

    public PayloadRewriteService(IOptions<RewriteOptions> options, ILogger<PayloadRewriteService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public byte[] RewriteIfNeeded(ReadOnlySpan<byte> payload)
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

            _logger.LogDebug("Payload rewrite applied.");
            return StrictUtf8.GetBytes(rewritten);
        }
        catch (DecoderFallbackException)
        {
            _logger.LogDebug("Payload rewrite skipped because payload is not valid UTF-8 text.");
            return payload.ToArray();
        }
    }
}
