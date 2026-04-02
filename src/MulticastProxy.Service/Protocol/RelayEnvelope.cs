namespace MulticastProxy.Service.Protocol;

public sealed record RelayEnvelope(
    byte Version,
    Guid SenderInstanceId,
    Guid PacketId,
    int OriginalMulticastPort,
    byte[] Payload);
