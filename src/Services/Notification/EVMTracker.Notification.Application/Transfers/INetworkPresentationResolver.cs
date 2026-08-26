namespace EVMTracker.Notification.Application.Transfers;

public interface INetworkPresentationResolver
{
    NetworkPresentation Resolve(long chainId);
}
