using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialIndexerCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "evm_indexer");

        migrationBuilder.CreateTable(
            name: "indexer_checkpoints",
            schema: "evm_indexer",
            columns: table => new
            {
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                NextBlockNumber = table.Column<long>(type: "bigint", nullable: false),
                CurrentRangeSize = table.Column<int>(type: "integer", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_indexer_checkpoints", x => x.ChainId);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "evm_indexer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "processed_transfers",
            schema: "evm_indexer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                TransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                LogIndex = table.Column<long>(type: "bigint", nullable: false),
                BlockNumber = table.Column<long>(type: "bigint", nullable: false),
                BlockHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                TokenContract = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                FromAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                ToAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                RawValue = table.Column<string>(type: "character varying(78)", maxLength: 78, nullable: false),
                TokenSymbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TokenName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                TokenDecimals = table.Column<int>(type: "integer", nullable: true),
                ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_processed_transfers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_indexer_outbox_processed_at_utc",
            schema: "evm_indexer",
            table: "outbox_messages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_processed_transfers_ChainId_BlockNumber",
            schema: "evm_indexer",
            table: "processed_transfers",
            columns: new[] { "ChainId", "BlockNumber" });

        migrationBuilder.CreateIndex(
            name: "ux_processed_transfers_chain_transaction_log",
            schema: "evm_indexer",
            table: "processed_transfers",
            columns: new[] { "ChainId", "TransactionHash", "LogIndex" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "indexer_checkpoints",
            schema: "evm_indexer");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "evm_indexer");

        migrationBuilder.DropTable(
            name: "processed_transfers",
            schema: "evm_indexer");
    }
}
