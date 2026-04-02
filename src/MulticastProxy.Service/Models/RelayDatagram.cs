namespace MulticastProxy.Service.Models;

public sealed record RelayDatagram(int Port, byte[] Payload);
