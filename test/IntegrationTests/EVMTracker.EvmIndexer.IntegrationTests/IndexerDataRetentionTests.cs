using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EVMTracker.EvmIndexer.IntegrationTests;

public sealed class IndexerDataRetentionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PruneRemovesExpiredPublishedHistoryButPreservesPendingAndRecentRows()
    {
        await using RetentionDatabase database = await RetentionDatabase.CreateAsync();
        Guid expiredEventId = Guid.NewGuid();
        Guid pendingEventId = Guid.NewGuid();
        Guid recentEventId = Guid.NewGuid();
        database.Context.ProcessedTransfers.AddRange(
            Transfer(expiredEventId, 1, Now.AddDays(-8)),
            Transfer(pendingEventId, 2, Now.AddDays(-8)),
            Transfer(recentEventId, 3, Now.AddHours(-1)));
        database.Context.OutboxMessages.AddRange(
            Outbox(expiredEventId, Now.AddDays(-8), Now.AddDays(-8)),
            Outbox(pendingEventId, Now.AddDays(-8), null),
            Outbox(recentEventId, Now.AddHours(-1), Now.AddHours(-1)));
        database.Context.WatchlistInboxMessages.Add(
            new IndexerWatchlistInboxRecord
            {
                EventId = Guid.NewGuid(),
                EventType = "watchlist.address-added.v1",
                ProcessedAtUtc = Now.AddDays(-31),
            });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var service = new IndexerDataRetentionService(
            database.Context,
            Options.Create(new IndexerDataRetentionOptions()),
            new FixedTimeProvider(Now));
        IndexerRetentionResult result = await service.PruneAsync(CancellationToken.None);

        Assert.Equal(1, result.OutboxMessages);
        Assert.Equal(1, result.ProcessedTransfers);
        Assert.Equal(1, result.WatchlistInboxMessages);
        Assert.False(await database.Context.ProcessedTransfers.AnyAsync(
            transfer => transfer.ObservedEventId == expiredEventId));
        Assert.True(await database.Context.ProcessedTransfers.AnyAsync(
            transfer => transfer.ObservedEventId == pendingEventId));
        Assert.True(await database.Context.ProcessedTransfers.AnyAsync(
            transfer => transfer.ObservedEventId == recentEventId));
    }

    private static ProcessedTransferRecord Transfer(
        Guid eventId,
        long logIndex,
        DateTimeOffset observedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ObservedEventId = eventId,
            ChainId = 56,
            TransactionHash = $"0x{logIndex:x64}",
            LogIndex = logIndex,
            BlockNumber = 100 + logIndex,
            BlockHash = $"0x{(logIndex + 10):x64}",
            TokenContract = "0x9999999999999999999999999999999999999999",
            FromAddress = "0x1111111111111111111111111111111111111111",
            ToAddress = "0x2222222222222222222222222222222222222222",
            RawValue = "1",
            TokenSymbol = "TEST",
            ObservedAtUtc = observedAt,
        };

    private static OutboxMessage Outbox(
        Guid eventId,
        DateTimeOffset occurredAt,
        DateTimeOffset? processedAt) =>
        new()
        {
            Id = eventId,
            EventType = "transfer.erc20-observed.v1",
            EventVersion = 1,
            OccurredAtUtc = occurredAt,
            CorrelationId = eventId,
            Payload = "{}",
            ProcessedAtUtc = processedAt,
        };

    private sealed class RetentionDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private RetentionDatabase(SqliteConnection connection, IndexerDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public IndexerDbContext Context { get; }

        public static async Task<RetentionDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new IndexerDbContext(
                new DbContextOptionsBuilder<IndexerDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await context.Database.EnsureCreatedAsync();
            return new RetentionDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
