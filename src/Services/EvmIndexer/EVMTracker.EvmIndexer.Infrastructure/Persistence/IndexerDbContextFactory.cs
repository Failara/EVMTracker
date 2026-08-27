using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence;

public sealed class IndexerDbContextFactory : IDesignTimeDbContextFactory<IndexerDbContext>
{
    public IndexerDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__EvmIndexer") ??
            "Host=localhost;Port=5432;Database=evmtracker_indexer;" +
            "Username=postgres;Password=";
        var optionsBuilder = new DbContextOptionsBuilder<IndexerDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            postgresOptions => postgresOptions.MigrationsHistoryTable(
                "__ef_migrations_history",
                "evm_indexer"));
        return new IndexerDbContext(optionsBuilder.Options);
    }
}
