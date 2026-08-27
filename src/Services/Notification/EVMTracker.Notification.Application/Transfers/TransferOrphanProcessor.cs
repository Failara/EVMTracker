using System.Text.Json;
using EVMTracker.Contracts.Transfers;

namespace EVMTracker.Notification.Application.Transfers;

public sealed class TransferOrphanProcessor(ITransferOrphanStore store)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OrphanProcessingResult> ProcessAsync(
        TransferIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.EventType != TransferEventTypes.Erc20TransferOrphanedV1 ||
            envelope.EventVersion != 1)
        {
            throw new UnsupportedTransferEventException(
                $"Transfer event '{envelope.EventType}' version {envelope.EventVersion} is unsupported.");
        }

        Erc20TransferOrphanedV1? orphan =
            envelope.Payload.Deserialize<Erc20TransferOrphanedV1>(SerializerOptions);
        if (orphan is null ||
            orphan.EventId != envelope.EventId ||
            orphan.EventType != envelope.EventType ||
            orphan.EventVersion != envelope.EventVersion)
        {
            throw new JsonException(
                $"The payload metadata for '{envelope.EventType}' does not match its envelope.");
        }

        return await store.ApplyAsync(
            envelope,
            orphan,
            cancellationToken).ConfigureAwait(false);
    }
}
