using System.Numerics;
using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.EvmIndexer.IntegrationTests;

public sealed class IndexerPersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TransferCheckpointAndOutboxCommitTogether()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new IndexerStateStore(database.Context);
        IndexerCheckpoint checkpoint = new(1, 101, 200, Now);

        int inserted = await store.CommitBatchAsync(
            checkpoint,
            [CreateIndexedTransfer()],
            CancellationToken.None);

        Assert.Equal(1, inserted);
        Assert.Equal(1, await database.Context.ProcessedTransfers.CountAsync());
        Assert.Equal(1, await database.Context.OutboxMessages.CountAsync());
        Assert.Equal(101, (await database.Context.Checkpoints.SingleAsync()).NextBlockNumber);
        var outboxMessage = await database.Context.OutboxMessages.SingleAsync();
        Assert.Contains(
            TransferEventTypes.Erc20TransferObservedV1,
            outboxMessage.Payload,
            StringComparison.Ordinal);
        Assert.Equal(1, outboxMessage.EventVersion);
        Assert.Equal(outboxMessage.Id, outboxMessage.CorrelationId);
    }

    [Fact]
    public async Task ReplayingTransferDoesNotDuplicateRecordOrOutbox()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new IndexerStateStore(database.Context);
        IndexedTransfer transfer = CreateIndexedTransfer();

        int firstInsert = await store.CommitBatchAsync(
            new IndexerCheckpoint(1, 101, 200, Now),
            [transfer],
            CancellationToken.None);
        int replayInsert = await store.CommitBatchAsync(
            new IndexerCheckpoint(1, 102, 400, Now.AddSeconds(1)),
            [transfer],
            CancellationToken.None);

        Assert.Equal(1, firstInsert);
        Assert.Equal(0, replayInsert);
        Assert.Equal(1, await database.Context.ProcessedTransfers.CountAsync());
        Assert.Equal(1, await database.Context.OutboxMessages.CountAsync());
        Assert.Equal(102, (await database.Context.Checkpoints.SingleAsync()).NextBlockNumber);
    }

    [Fact]
    public async Task RangeReductionPersistsWithoutAdvancingBlock()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var store = new IndexerStateStore(database.Context);

        await store.UpdateRangeSizeAsync(
            new IndexerCheckpoint(1, 500, 50, Now),
            CancellationToken.None);
        IndexerCheckpoint? reloaded = await store.GetCheckpointAsync(1, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(500, reloaded.NextBlockNumber);
        Assert.Equal(50, reloaded.CurrentRangeSize);
    }

    [Fact]
    public async Task ModelDefinesCanonicalTransferUniqueIndex()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        string[] indexNames = database.Context.Model
            .FindEntityType("EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities.ProcessedTransferRecord")!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .OfType<string>()
            .ToArray();

        Assert.Contains(
            ProcessedTransferRecordConfiguration.UniqueTransferConstraintName,
            indexNames);
    }

    private static IndexedTransfer CreateIndexedTransfer()
    {
        Assert.True(EvmAddress.TryCreate(
            "0x9999999999999999999999999999999999999999",
            out EvmAddress? token));
        Assert.True(EvmAddress.TryCreate(
            "0x1111111111111111111111111111111111111111",
            out EvmAddress? from));
        Assert.True(EvmAddress.TryCreate(
            "0x2222222222222222222222222222222222222222",
            out EvmAddress? to));
        Assert.True(Hash256.TryCreate(
            "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            out Hash256? transactionHash));
        Assert.True(Hash256.TryCreate(
            "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            out Hash256? blockHash));

        var transfer = new Erc20Transfer(
            1,
            token!,
            from!,
            to!,
            BigInteger.One,
            transactionHash!,
            100,
            blockHash!,
            3);
        var metadata = new TokenMetadata("USDT", "Tether USD", 6);
        var integrationEvent = new Erc20TransferObservedV1(
            Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09a7"),
            Now,
            1,
            token!.Value,
            from!.Value,
            to!.Value,
            "1",
            metadata.Symbol,
            metadata.Name,
            metadata.Decimals,
            transactionHash!.Value,
            100,
            blockHash!.Value,
            3);
        return new IndexedTransfer(transfer, metadata, integrationEvent);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, IndexerDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }

        public IndexerDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<IndexerDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new IndexerDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
