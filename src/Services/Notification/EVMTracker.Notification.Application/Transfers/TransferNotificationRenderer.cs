using System.Net;
using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.Application.Transfers;

public static class TransferNotificationRenderer
{
    public static string Render(
        TransferNotification notification,
        string? explorerBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(notification);

        string direction = notification.Direction.ToString();
        string walletLabel = string.IsNullOrWhiteSpace(notification.Label)
            ? Shorten(notification.MonitoredAddress)
            : $"{notification.Label} ({Shorten(notification.MonitoredAddress)})";
        string tokenLabel = string.IsNullOrWhiteSpace(notification.TokenName)
            ? notification.TokenSymbol
            : $"{notification.TokenName} ({notification.TokenSymbol})";
        return string.Join(
            Environment.NewLine,
            $"<b>{Encode(direction)} ERC-20 transfer</b>",
            $"<b>{Encode(notification.DisplayAmount)} {Encode(notification.TokenSymbol)}</b> on {Encode(notification.NetworkName)}",
            string.Empty,
            $"Wallet: {LinkOrText(walletLabel, explorerBaseUrl, "address", notification.MonitoredAddress)}",
            $"From: {LinkOrText(Shorten(notification.FromAddress), explorerBaseUrl, "address", notification.FromAddress)}",
            $"To: {LinkOrText(Shorten(notification.ToAddress), explorerBaseUrl, "address", notification.ToAddress)}",
            $"Token: {LinkOrText(tokenLabel, explorerBaseUrl, "token", notification.TokenContract)}",
            $"Transaction: {LinkOrText(Shorten(notification.TransactionHash), explorerBaseUrl, "tx", notification.TransactionHash)}",
            $"Block: {notification.BlockNumber} · Confirmed");
    }

    private static string LinkOrText(
        string label,
        string? explorerBaseUrl,
        string resource,
        string value)
    {
        string encodedLabel = Encode(label);
        if (string.IsNullOrWhiteSpace(explorerBaseUrl))
        {
            return encodedLabel;
        }

        string url = $"{explorerBaseUrl.TrimEnd('/')}/{resource}/{value}";
        return $"<a href=\"{Encode(url)}\">{encodedLabel}</a>";
    }

    private static string Shorten(string value) =>
        value.Length <= 14 ? value : $"{value[..8]}…{value[^6..]}";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
