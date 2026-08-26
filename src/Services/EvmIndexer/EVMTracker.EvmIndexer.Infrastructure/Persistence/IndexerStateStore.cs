using System.Globalization;
using System.Text.Json;
using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Application.Abstractions;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task<int> CommitBatchAsync(
        IndexerCheckpoint checkpoint,
        IReadOnlyCollection<IndexedTransfer> transfers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(transfers);

        string[] transactionHashes = transfers
            .Select(item => item.Transfer.TransactionHash.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ProcessedTransferRecord[] existingRecords = transactionHashes.Length == 0
            ? []
            : await dbContext.ProcessedTransfers
                .AsNoTracking()
                .Where(item => item.ChainId == checkpoint.ChainId &&
                    transactionHashes.Contains(item.TransactionHash))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var existingIdentities = existingRecords
            .Select(item => (item.TransactionHash, item.LogIndex))
            .ToHashSet();

        int insertedCount = 0;
        foreach (IndexedTransfer indexedTransfer in transfers)
        {
            Erc20Transfer transfer = indexedTransfer.Transfer;
            if (!existingIdentities.Add((transfer.TransactionHash.Value, transfer.LogIndex)))
            {
                continue;
            }

            Erc20TransferObservedV1 integrationEvent = indexedTransfer.IntegrationEvent;
            dbContext.ProcessedTransfers.Add(
                new ProcessedTransferRecord
                {
                    Id = integrationEvent.EventId,
                    ChainId = transfer.ChainId,
                    TransactionHash = transfer.TransactionHash.Value,
                    LogIndex = transfer.LogIndex,
                    BlockNumber = transfer.BlockNumber,
                    BlockHash = transfer.BlockHash.Value,
                    TokenContract = transfer.TokenContract.Value,
                    FromAddress = transfer.FromAddress.Value,
                    ToAddress = transfer.ToAddress.Value,
                    RawValue = transfer.RawValue.ToString(CultureInfo.InvariantCulture),
                    TokenSymbol = indexedTransfer.TokenMetadata.Symbol,
                    TokenName = indexedTransfer.TokenMetadata.Name,
                    TokenDecimals = indexedTransfer.TokenMetadata.Decimals,
                    ObservedAtUtc = integrationEvent.OccurredAtUtc,
                });
            dbContext.OutboxMessages.Add(
                new OutboxMessage
                {
                    Id = integrationEvent.EventId,
                    EventType = integrationEvent.EventType,
                    EventVersion = integrationEvent.EventVersion,
                    OccurredAtUtc = integrationEvent.OccurredAtUtc,
                    CorrelationId = integrationEvent.EventId,
                    Payload = JsonSerializer.Serialize(
                        integrationEvent,
                        SerializerOptions),
                });
            insertedCount++;
        }

        await UpsertCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return insertedCount;
    }

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
