namespace EVMTracker.Notification.Application.Transfers;

public interface INetworkDisplayNameResolver
{
    string Resolve(long chainId);
}
