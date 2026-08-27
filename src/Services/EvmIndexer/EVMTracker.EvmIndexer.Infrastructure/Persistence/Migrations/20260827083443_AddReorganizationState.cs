using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReorganizationState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsOrphaned",
            schema: "evm_indexer",
            table: "processed_transfers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "ObservedEventId",
            schema: "evm_indexer",
            table: "processed_transfers",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "OrphanedAtUtc",
            schema: "evm_indexer",
            table: "processed_transfers",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE evm_indexer.processed_transfers
            SET "ObservedEventId" = "Id";
            """);

        migrationBuilder.CreateTable(
            name: "canonical_blocks",
            schema: "evm_indexer",
            columns: table => new
            {
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                BlockNumber = table.Column<long>(type: "bigint", nullable: false),
                BlockHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_canonical_blocks", x => new { x.ChainId, x.BlockNumber });
            });

        migrationBuilder.CreateIndex(
            name: "IX_processed_transfers_ChainId_IsOrphaned",
            schema: "evm_indexer",
            table: "processed_transfers",
            columns: new[] { "ChainId", "IsOrphaned" });

        migrationBuilder.CreateIndex(
            name: "ux_processed_transfers_observed_event_id",
            schema: "evm_indexer",
            table: "processed_transfers",
            column: "ObservedEventId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_canonical_blocks_chain_hash",
            schema: "evm_indexer",
            table: "canonical_blocks",
            columns: new[] { "ChainId", "BlockHash" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "canonical_blocks",
            schema: "evm_indexer");

        migrationBuilder.DropIndex(
            name: "IX_processed_transfers_ChainId_IsOrphaned",
            schema: "evm_indexer",
            table: "processed_transfers");

        migrationBuilder.DropIndex(
            name: "ux_processed_transfers_observed_event_id",
            schema: "evm_indexer",
            table: "processed_transfers");

        migrationBuilder.DropColumn(
            name: "IsOrphaned",
            schema: "evm_indexer",
            table: "processed_transfers");

        migrationBuilder.DropColumn(
            name: "ObservedEventId",
            schema: "evm_indexer",
            table: "processed_transfers");

        migrationBuilder.DropColumn(
            name: "OrphanedAtUtc",
            schema: "evm_indexer",
            table: "processed_transfers");
    }
}
