using System.Globalization;
using System.Text.Json;
using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Indexing;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

internal sealed class IndexerStateStore(IndexerDbContext dbContext) : IIndexerStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IndexerCheckpoint?> GetCheckpointAsync(
        long chainId,
        CancellationToken cancellationToken)
    {
        IndexerCheckpointRecord? checkpoint = await dbContext.Checkpoints
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ChainId == chainId,
                cancellationToken).ConfigureAwait(false);
        return checkpoint is null
            ? null
            : new IndexerCheckpoint(
                checkpoint.ChainId,
                checkpoint.NextBlockNumber,
                checkpoint.CurrentRangeSize,
                checkpoint.UpdatedAtUtc);
    }

    public async Task UpdateRangeSizeAsync(
        IndexerCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        await UpsertCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CanonicalBlock>> GetRecentCanonicalBlocksAsync(
        long chainId,
        CancellationToken cancellationToken) =>
        await dbContext.CanonicalBlocks
            .AsNoTracking()
            .Where(block => block.ChainId == chainId)
            .OrderByDescending(block => block.BlockNumber)
            .Select(block => new CanonicalBlock(
                block.ChainId,
                block.BlockNumber,
                block.BlockHash))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ReorganizationResult> RewindAsync(
        IndexerCheckpoint checkpoint,
        long rewindBlockNumber,
        DateTimeOffset detectedAtUtc,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        ProcessedTransferRecord[] affected = await dbContext.ProcessedTransfers
            .Where(transfer =>
                transfer.ChainId == checkpoint.ChainId &&
                transfer.BlockNumber >= rewindBlockNumber &&
                !transfer.IsOrphaned)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] observedEventIds = affected.Select(transfer => transfer.ObservedEventId).ToArray();
        Dictionary<Guid, OutboxMessage> observedMessages = observedEventIds.Length == 0
            ? []
            : await dbContext.OutboxMessages
                .Where(message => observedEventIds.Contains(message.Id))
                .ToDictionaryAsync(message => message.Id, cancellationToken)
                .ConfigureAwait(false);

        int publishedOrphans = 0;
        foreach (ProcessedTransferRecord transfer in affected)
        {
            transfer.IsOrphaned = true;
            transfer.OrphanedAtUtc = detectedAtUtc;
            if (!observedMessages.TryGetValue(transfer.ObservedEventId, out OutboxMessage? observed) ||
                observed.ProcessedAtUtc is null)
            {
                if (observed is not null)
                {
                    observed.DeadLetteredAtUtc = detectedAtUtc;
                    observed.LastError = "Cancelled because its source block became orphaned before publication.";
                    observed.NextAttemptAtUtc = null;
                }
            }

            Guid orphanEventId = TransferEventIdentity.CreateOrphanedEventId(
                transfer.ChainId,
                transfer.TransactionHash,
                transfer.LogIndex,
                transfer.BlockHash);
            if (await dbContext.OutboxMessages.AnyAsync(
                    message => message.Id == orphanEventId,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var orphanedEvent = new Erc20TransferOrphanedV1(
                orphanEventId,
                detectedAtUtc,
                transfer.ChainId,
                transfer.TransactionHash,
                transfer.LogIndex,
                transfer.BlockHash);
            dbContext.OutboxMessages.Add(ToOutbox(orphanedEvent, transfer.ObservedEventId));
            publishedOrphans++;
        }

        CanonicalBlockRecord[] invalidBlocks = await dbContext.CanonicalBlocks
            .Where(block => block.ChainId == checkpoint.ChainId &&
                block.BlockNumber >= rewindBlockNumber)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.CanonicalBlocks.RemoveRange(invalidBlocks);
        await UpsertCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReorganizationResult(
            rewindBlockNumber,
            affected.Length,
            publishedOrphans);
    }

    public async Task<int> CommitBatchAsync(
        IndexerCheckpoint checkpoint,
        IReadOnlyCollection<IndexedTransfer> transfers,
        IReadOnlyCollection<CanonicalBlock> canonicalBlocks,
        int reorganizationWindow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(canonicalBlocks);

        string[] transactionHashes = transfers
            .Select(item => item.Transfer.TransactionHash.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ProcessedTransferRecord[] existingRecords = transactionHashes.Length == 0
            ? []
            : await dbContext.ProcessedTransfers
                .Where(item => item.ChainId == checkpoint.ChainId &&
                    transactionHashes.Contains(item.TransactionHash))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var existingByIdentity = existingRecords.ToDictionary(
            item => (item.TransactionHash, item.LogIndex));

        int insertedCount = 0;
        foreach (IndexedTransfer indexedTransfer in transfers)
        {
            Erc20Transfer transfer = indexedTransfer.Transfer;
            var identity = (transfer.TransactionHash.Value, transfer.LogIndex);
            if (existingByIdentity.TryGetValue(identity, out ProcessedTransferRecord? existing) &&
                !existing.IsOrphaned)
            {
                continue;
            }

            Erc20TransferObservedV1 integrationEvent = indexedTransfer.IntegrationEvent;
            ProcessedTransferRecord record = existing ?? new ProcessedTransferRecord
            {
                Id = TransferEventIdentity.CreateTransferId(
                    transfer.ChainId,
                    transfer.TransactionHash.Value,
                    transfer.LogIndex),
                ChainId = transfer.ChainId,
                TransactionHash = transfer.TransactionHash.Value,
                LogIndex = transfer.LogIndex,
            };
            ApplyObservedTransfer(record, indexedTransfer);
            if (existing is null)
            {
                dbContext.ProcessedTransfers.Add(record);
                existingByIdentity.Add(identity, record);
            }

            dbContext.OutboxMessages.Add(ToOutbox(integrationEvent, null));
            insertedCount++;
        }

        await PersistCanonicalBlocksAsync(
            checkpoint,
            canonicalBlocks,
            reorganizationWindow,
            cancellationToken).ConfigureAwait(false);

        await UpsertCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return insertedCount;
    }

    private async Task PersistCanonicalBlocksAsync(
        IndexerCheckpoint checkpoint,
        IReadOnlyCollection<CanonicalBlock> canonicalBlocks,
        int reorganizationWindow,
        CancellationToken cancellationToken)
    {
        long retainedFrom = Math.Max(0, checkpoint.NextBlockNumber - reorganizationWindow);
        CanonicalBlockRecord[] expired = await dbContext.CanonicalBlocks
            .Where(block => block.ChainId == checkpoint.ChainId &&
                block.BlockNumber < retainedFrom)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        dbContext.CanonicalBlocks.RemoveRange(expired);

        long[] blockNumbers = canonicalBlocks.Select(block => block.BlockNumber).ToArray();
        Dictionary<long, CanonicalBlockRecord> existing = blockNumbers.Length == 0
            ? []
            : await dbContext.CanonicalBlocks
                .Where(block => block.ChainId == checkpoint.ChainId &&
                    blockNumbers.Contains(block.BlockNumber))
                .ToDictionaryAsync(block => block.BlockNumber, cancellationToken)
                .ConfigureAwait(false);
        foreach (CanonicalBlock block in canonicalBlocks.Where(
                     block => block.BlockNumber >= retainedFrom))
        {
            if (existing.TryGetValue(block.BlockNumber, out CanonicalBlockRecord? record))
            {
                record.BlockHash = block.BlockHash.ToLowerInvariant();
                record.RecordedAtUtc = checkpoint.UpdatedAtUtc;
            }
            else
            {
                dbContext.CanonicalBlocks.Add(
                    new CanonicalBlockRecord
                    {
                        ChainId = block.ChainId,
                        BlockNumber = block.BlockNumber,
                        BlockHash = block.BlockHash.ToLowerInvariant(),
                        RecordedAtUtc = checkpoint.UpdatedAtUtc,
                    });
            }
        }
    }

    private static void ApplyObservedTransfer(
        ProcessedTransferRecord record,
        IndexedTransfer indexedTransfer)
    {
        Erc20Transfer transfer = indexedTransfer.Transfer;
        record.ObservedEventId = indexedTransfer.IntegrationEvent.EventId;
        record.BlockNumber = transfer.BlockNumber;
        record.BlockHash = transfer.BlockHash.Value;
        record.TokenContract = transfer.TokenContract.Value;
        record.FromAddress = transfer.FromAddress.Value;
        record.ToAddress = transfer.ToAddress.Value;
        record.RawValue = transfer.RawValue.ToString(CultureInfo.InvariantCulture);
        record.TokenSymbol = indexedTransfer.TokenMetadata.Symbol;
        record.TokenName = indexedTransfer.TokenMetadata.Name;
        record.TokenDecimals = indexedTransfer.TokenMetadata.Decimals;
        record.ObservedAtUtc = indexedTransfer.IntegrationEvent.OccurredAtUtc;
        record.IsOrphaned = false;
        record.OrphanedAtUtc = null;
    }

    private static OutboxMessage ToOutbox(
        ITransferIntegrationEvent integrationEvent,
        Guid? causationId) =>
        new()
        {
            Id = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            EventVersion = integrationEvent.EventVersion,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            CorrelationId = integrationEvent.EventId,
            CausationId = causationId,
            Payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions),
        };

    private async Task UpsertCheckpointAsync(
        IndexerCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        IndexerCheckpointRecord? record = await dbContext.Checkpoints
            .SingleOrDefaultAsync(
                item => item.ChainId == checkpoint.ChainId,
                cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            dbContext.Checkpoints.Add(
                new IndexerCheckpointRecord
                {
                    ChainId = checkpoint.ChainId,
                    NextBlockNumber = checkpoint.NextBlockNumber,
                    CurrentRangeSize = checkpoint.CurrentRangeSize,
                    UpdatedAtUtc = checkpoint.UpdatedAtUtc,
                });
            return;
        }

        record.NextBlockNumber = checkpoint.NextBlockNumber;
        record.CurrentRangeSize = checkpoint.CurrentRangeSize;
        record.UpdatedAtUtc = checkpoint.UpdatedAtUtc;
    }
}
