using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.Application.Transfers;

public static class TransferNotificationRenderer
{
    public static string Render(TransferNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        string wallet = string.IsNullOrWhiteSpace(notification.Label)
            ? notification.MonitoredAddress
            : $"{notification.Label} ({notification.MonitoredAddress})";
        return string.Join(
            Environment.NewLine,
            $"ERC-20 {notification.Direction.ToString().ToLowerInvariant()} transfer",
            $"{notification.DisplayAmount} {notification.TokenSymbol} on {notification.NetworkName}",
            $"Wallet: {wallet}",
            $"Token: {notification.TokenContract}",
            $"Transaction: {notification.TransactionHash}",
            $"Block: {notification.BlockNumber}");
    }
}
