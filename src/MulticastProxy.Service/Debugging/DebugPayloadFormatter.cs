using System.Text;

namespace MulticastProxy.Service.Debugging;

internal static class DebugPayloadFormatter
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string CreateHexPreview(byte[] payload, int maxPayloadPreviewBytes)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        var length = Math.Min(payload.Length, Math.Max(1, maxPayloadPreviewBytes));
        var builder = new StringBuilder(length * 3);

        for (var i = 0; i < length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(payload[i].ToString("X2"));
        }

        if (length < payload.Length)
        {
            builder.Append(" ...");
        }

        return builder.ToString();
    }

    public static string? CreateTextPreview(byte[] payload, int maxPayloadPreviewBytes)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        var length = Math.Min(payload.Length, Math.Max(1, maxPayloadPreviewBytes));
        var preview = payload.AsSpan(0, length);

        if (preview.ToArray().All(IsAsciiTextByte))
        {
            return Encoding.ASCII.GetString(preview);
        }

        try
        {
            return StrictUtf8.GetString(preview);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsAsciiTextByte(byte value) =>
        value == 9 || value == 10 || value == 13 || (value >= 32 && value <= 126);
}
