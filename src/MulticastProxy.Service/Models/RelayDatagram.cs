namespace MulticastProxy.Service.Models;

public sealed record RelayDatagram(Guid TraceId, int Port, byte[] Payload);
