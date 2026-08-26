using System.Text.Json;
using EVMTracker.Contracts;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Notification.Domain.Notifications;

namespace EVMTracker.Notification.Application.Transfers;

public sealed class TransferEventProcessor(
    IWatchlistProjectionStore watchlistProjection,
    ITransferNotificationStore notificationStore,
    INetworkDisplayNameResolver networkNames)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<TransferProcessingResult> ProcessAsync(
        TransferIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Erc20TransferObservedV1 transfer = Deserialize(envelope);
        IReadOnlyList<ProjectedWatchlistEntry> matches =
            await watchlistProjection.FindMatchesAsync(
                transfer.ChainId,
                transfer.FromAddress,
                transfer.ToAddress,
                cancellationToken).ConfigureAwait(false);

        var prepared = new List<PreparedTransferNotification>(matches.Count);
        foreach (ProjectedWatchlistEntry match in matches)
        {
            TransferDirection direction = ClassifyDirection(
                match.Address,
                transfer.FromAddress,
                transfer.ToAddress);
            TransferNotification notification = TransferNotification.Create(
                match.TrackedAddressId,
                envelope.EventId,
                match.TelegramUserId,
                match.TelegramChatId,
                transfer.ChainId,
                networkNames.Resolve(transfer.ChainId),
                match.Address,
                match.Label,
                direction,
                transfer.TokenContract,
                transfer.TokenSymbol,
                transfer.TokenName,
                transfer.TokenDecimals,
                transfer.RawValue,
                transfer.FromAddress,
                transfer.ToAddress,
                transfer.TransactionHash,
                transfer.BlockNumber,
                transfer.BlockHash,
                transfer.LogIndex,
                envelope.OccurredAt);
            var deliveryRequest = new TelegramDeliveryRequestedV1(
                notification.Id,
                envelope.OccurredAt,
                notification.Id,
                notification.TelegramChatId,
                TransferNotificationRenderer.Render(notification),
                "PlainText");
            prepared.Add(new PreparedTransferNotification(notification, deliveryRequest));
        }

        int created = await notificationStore.PersistAsync(
            envelope,
            prepared,
            cancellationToken).ConfigureAwait(false);
        return new TransferProcessingResult(matches.Count, created);
    }

    private static Erc20TransferObservedV1 Deserialize(
        TransferIntegrationEventEnvelope envelope)
    {
        if (envelope.EventType != TransferEventTypes.Erc20TransferObservedV1 ||
            envelope.EventVersion != 1)
        {
            throw new UnsupportedTransferEventException(
                $"Transfer event '{envelope.EventType}' version {envelope.EventVersion} is unsupported.");
        }

        Erc20TransferObservedV1? transfer =
            envelope.Payload.Deserialize<Erc20TransferObservedV1>(SerializerOptions);
        if (transfer is null ||
            transfer.EventId != envelope.EventId ||
            transfer.EventType != envelope.EventType ||
            transfer.EventVersion != envelope.EventVersion)
        {
            throw new JsonException(
                $"The payload metadata for '{envelope.EventType}' does not match its envelope.");
        }

        return transfer;
    }

    private static TransferDirection ClassifyDirection(
        string monitoredAddress,
        string fromAddress,
        string toAddress)
    {
        bool isFrom = string.Equals(
            monitoredAddress,
            fromAddress,
            StringComparison.OrdinalIgnoreCase);
        bool isTo = string.Equals(
            monitoredAddress,
            toAddress,
            StringComparison.OrdinalIgnoreCase);
        return (isFrom, isTo) switch
        {
            (true, true) => TransferDirection.Self,
            (true, false) => TransferDirection.Outgoing,
            (false, true) => TransferDirection.Incoming,
            _ => throw new InvalidOperationException(
                "A projected match must be either the transfer sender or recipient."),
        };
    }
}
