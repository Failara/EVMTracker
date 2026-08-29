using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIndexerWatchlistProjection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "watchlist_inbox_messages",
            schema: "evm_indexer",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_watchlist_inbox_messages", x => x.EventId);
            });

        migrationBuilder.CreateTable(
            name: "watchlist_projection",
            schema: "evm_indexer",
            columns: table => new
            {
                TrackedAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                Address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_watchlist_projection", x => new { x.TrackedAddressId, x.ChainId });
            });

        migrationBuilder.CreateIndex(
            name: "ix_indexer_watchlist_projection_chain_address",
            schema: "evm_indexer",
            table: "watchlist_projection",
            columns: new[] { "ChainId", "Address" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "watchlist_inbox_messages",
            schema: "evm_indexer");

        migrationBuilder.DropTable(
            name: "watchlist_projection",
            schema: "evm_indexer");
    }
}
