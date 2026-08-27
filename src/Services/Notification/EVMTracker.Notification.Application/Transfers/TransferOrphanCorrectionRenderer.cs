using System.Net;

namespace EVMTracker.Notification.Application.Transfers;

public static class TransferOrphanCorrectionRenderer
{
    public static string Render(OrphanedTransferNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        string wallet = string.IsNullOrWhiteSpace(notification.Label)
            ? Shorten(notification.MonitoredAddress)
            : $"{notification.Label} ({Shorten(notification.MonitoredAddress)})";
        return string.Join(
            Environment.NewLine,
            "<b>Chain reorganization correction</b>",
            $"A previously reported transfer on {Encode(notification.NetworkName)} is no longer canonical.",
            string.Empty,
            $"Wallet: {LinkOrText(wallet, notification.ExplorerBaseUrl, "address", notification.MonitoredAddress)}",
            $"Transaction: {LinkOrText(Shorten(notification.TransactionHash), notification.ExplorerBaseUrl, "tx", notification.TransactionHash)}",
            $"Former block: {notification.BlockNumber}",
            $"Former block hash: {Encode(Shorten(notification.FormerBlockHash))}");
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
