using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

public sealed class IndexerDbContextFactory : IDesignTimeDbContextFactory<IndexerDbContext>
{
    public IndexerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IndexerDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=evmtracker_indexer;Username=postgres;Password=",
            postgresOptions => postgresOptions.MigrationsHistoryTable(
                "__ef_migrations_history",
                "evm_indexer"));
        return new IndexerDbContext(optionsBuilder.Options);
    }
}
