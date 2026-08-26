using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPhase6OutboxEnvelope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_indexer_outbox_processed_at_utc",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.AddColumn<Guid>(
            name: "CausationId",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CorrelationId",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTime>(
            name: "DeadLetteredAtUtc",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DeliveryAttempts",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "EventVersion",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "LastError",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextAttemptAtUtc",
            schema: "evm_indexer",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE evm_indexer.outbox_messages
            SET "CorrelationId" = "Id", "EventVersion" = 1
            WHERE "CorrelationId" = '00000000-0000-0000-0000-000000000000'
               OR "EventVersion" = 0;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_indexer_outbox_pending",
            schema: "evm_indexer",
            table: "outbox_messages",
            columns: new[] { "ProcessedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_indexer_outbox_pending",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "CausationId",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "CorrelationId",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "DeadLetteredAtUtc",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "DeliveryAttempts",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "EventVersion",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "LastError",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "NextAttemptAtUtc",
            schema: "evm_indexer",
            table: "outbox_messages");

        migrationBuilder.CreateIndex(
            name: "ix_indexer_outbox_processed_at_utc",
            schema: "evm_indexer",
            table: "outbox_messages",
            column: "ProcessedAtUtc");
    }
}
