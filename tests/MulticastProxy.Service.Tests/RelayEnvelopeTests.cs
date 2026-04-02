using MulticastProxy.Service.Protocol;

namespace MulticastProxy.Service.Tests;

public class RelayEnvelopeTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTripsSuccessfully()
    {
        var serializer = new RelayEnvelopeSerializer();
        var sender = Guid.NewGuid();
        var packetId = Guid.NewGuid();
        byte[] payload = [1, 2, 3, 4, 5];

        var serialized = serializer.Serialize(sender, packetId, 9053, payload);

        var success = serializer.TryDeserialize(serialized, out var envelope, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(envelope);
        Assert.Equal(sender, envelope!.SenderInstanceId);
        Assert.Equal(packetId, envelope.PacketId);
        Assert.Equal(9053, envelope.OriginalMulticastPort);
        Assert.Equal(payload, envelope.Payload);
    }

    [Fact]
    public void TryDeserialize_WithMalformedPayloadLength_Fails()
    {
        var serializer = new RelayEnvelopeSerializer();
        var bytes = serializer.Serialize(Guid.NewGuid(), Guid.NewGuid(), 9000, [1, 2, 3]);
        bytes[^1] = 99;

        var success = serializer.TryDeserialize(bytes[..^1], out _, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }
}
