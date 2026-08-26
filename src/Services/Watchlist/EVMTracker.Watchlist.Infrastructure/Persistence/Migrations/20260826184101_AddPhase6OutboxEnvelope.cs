using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Watchlist.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPhase6OutboxEnvelope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_messages_processed_at_utc",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.AddColumn<Guid>(
            name: "CausationId",
            schema: "watchlist",
            table: "outbox_messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CorrelationId",
            schema: "watchlist",
            table: "outbox_messages",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTime>(
            name: "DeadLetteredAtUtc",
            schema: "watchlist",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "EventVersion",
            schema: "watchlist",
            table: "outbox_messages",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "NextAttemptAtUtc",
            schema: "watchlist",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE watchlist.outbox_messages
            SET "CorrelationId" = "Id", "EventVersion" = 1
            WHERE "CorrelationId" = '00000000-0000-0000-0000-000000000000'
               OR "EventVersion" = 0;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "watchlist",
            table: "outbox_messages",
            columns: new[] { "ProcessedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_messages_pending",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "CausationId",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "CorrelationId",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "DeadLetteredAtUtc",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "EventVersion",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "NextAttemptAtUtc",
            schema: "watchlist",
            table: "outbox_messages");

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_processed_at_utc",
            schema: "watchlist",
            table: "outbox_messages",
            column: "ProcessedAtUtc");
    }
}
