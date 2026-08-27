using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrphanTombstones : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "orphaned_transfers",
            schema: "notification",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                TransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                LogIndex = table.Column<long>(type: "bigint", nullable: false),
                FormerBlockHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                OrphanedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orphaned_transfers", x => x.EventId);
            });

        migrationBuilder.CreateIndex(
            name: "ux_orphaned_transfers_chain_transaction_log_block",
            schema: "notification",
            table: "orphaned_transfers",
            columns: new[] { "ChainId", "TransactionHash", "LogIndex", "FormerBlockHash" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "orphaned_transfers",
            schema: "notification");
    }
}
