using System.Buffers.Binary;

namespace MulticastProxy.Service.Protocol;

public sealed class RelayEnvelopeSerializer
{
    private const byte CurrentVersion = 1;
    private const int HeaderSize = 1 + 16 + 16 + 2 + 4;
    private const int MaxPayloadLength = 65507;

    public byte[] Serialize(Guid senderInstanceId, Guid packetId, int originalMulticastPort, ReadOnlySpan<byte> payload)
    {
        if (originalMulticastPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(originalMulticastPort), "Port must be 0-65535.");
        }

        if (payload.Length > MaxPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload exceeds UDP practical limits.");
        }

        var output = new byte[HeaderSize + payload.Length];
        output[0] = CurrentVersion;

        senderInstanceId.TryWriteBytes(output.AsSpan(1, 16));
        packetId.TryWriteBytes(output.AsSpan(17, 16));

        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(33, 2), (ushort)originalMulticastPort);
        BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(35, 4), payload.Length);
        payload.CopyTo(output.AsSpan(HeaderSize));

        return output;
    }

    public bool TryDeserialize(ReadOnlySpan<byte> packet, out RelayEnvelope? envelope, out string? error)
    {
        envelope = null;
        error = null;

        if (packet.Length < HeaderSize)
        {
            error = "Packet too short for relay envelope.";
            return false;
        }

        var version = packet[0];
        if (version != CurrentVersion)
        {
            error = $"Unsupported relay envelope version '{version}'.";
            return false;
        }

        var sender = new Guid(packet.Slice(1, 16));
        var packetId = new Guid(packet.Slice(17, 16));
        var originalPort = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(33, 2));
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(packet.Slice(35, 4));

        if (payloadLength < 0)
        {
            error = "Envelope payload length is negative.";
            return false;
        }

        if (packet.Length - HeaderSize != payloadLength)
        {
            error = "Envelope payload length mismatch.";
            return false;
        }

        var payload = packet.Slice(HeaderSize).ToArray();
        envelope = new RelayEnvelope(version, sender, packetId, originalPort, payload);
        return true;
    }
}
