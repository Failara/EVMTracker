using System.Numerics;
using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Indexing;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EVMTracker.EvmIndexer.IntegrationTests;

[Collection(ContainerInfrastructureDefinition.Name)]
public sealed class PostgreSqlRecoveryTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "ContainerIntegration")]
    public async Task MigrationCommitDuplicateReplayAndRestartUseRealPostgreSql()
    {
        await using (IndexerDbContext context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
            var store = new IndexerStateStore(context);
            IndexedTransfer transfer = CreateTransfer();

            int inserted = await store.CommitBatchAsync(
                new IndexerCheckpoint(31337, 11, 64, OccurredAt),
                [transfer],
                [new CanonicalBlock(31337, 10, transfer.Transfer.BlockHash.Value)],
                64,
                CancellationToken.None);
            int replayed = await store.CommitBatchAsync(
                new IndexerCheckpoint(31337, 12, 64, OccurredAt.AddSeconds(1)),
                [transfer],
                [new CanonicalBlock(31337, 11, CreateHash('c'))],
                64,
                CancellationToken.None);

            Assert.Equal(1, inserted);
            Assert.Equal(0, replayed);
        }

        await fixture.Container.StopAsync();
        await fixture.Container.StartAsync();

        await using IndexerDbContext restarted = CreateContext();
        Assert.Equal(1, await restarted.ProcessedTransfers.CountAsync());
        Assert.Equal(1, await restarted.OutboxMessages.CountAsync());
        Assert.Equal(12, (await restarted.Checkpoints.SingleAsync()).NextBlockNumber);
        Assert.Contains(
            await restarted.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith(
                "AddIndexerWatchlistProjection",
                StringComparison.Ordinal));
    }

    private IndexerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IndexerDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new IndexerDbContext(options);
    }

    private static IndexedTransfer CreateTransfer()
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
        Assert.True(Hash256.TryCreate(CreateHash('a'), out Hash256? transactionHash));
        Assert.True(Hash256.TryCreate(CreateHash('b'), out Hash256? blockHash));

        var transfer = new Erc20Transfer(
            31337,
            token!,
            from!,
            to!,
            new BigInteger(5_000_000),
            transactionHash!,
            10,
            blockHash!,
            0);
        var metadata = new TokenMetadata("USDT", "Tether USD", 6);
        var observed = new Erc20TransferObservedV1(
            Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09b1"),
            OccurredAt,
            transfer.ChainId,
            token!.Value,
            from!.Value,
            to!.Value,
            transfer.RawValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            metadata.Symbol,
            metadata.Name,
            metadata.Decimals,
            transactionHash!.Value,
            transfer.BlockNumber,
            blockHash!.Value,
            transfer.LogIndex);
        return new IndexedTransfer(transfer, metadata, observed);
    }

    private static string CreateHash(char character) => $"0x{new string(character, 64)}";
}
