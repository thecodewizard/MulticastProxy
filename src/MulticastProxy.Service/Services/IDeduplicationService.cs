namespace MulticastProxy.Service.Services;

public interface IDeduplicationService
{
    bool TryRegister(Guid packetId);
}
