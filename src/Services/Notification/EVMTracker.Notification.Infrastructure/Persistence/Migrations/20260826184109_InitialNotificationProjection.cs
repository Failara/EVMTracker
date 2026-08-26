using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialNotificationProjection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notification");

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "notification",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox_messages", x => x.EventId);
            });

        migrationBuilder.CreateTable(
            name: "watchlist_projection",
            schema: "notification",
            columns: table => new
            {
                TrackedAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                Address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_watchlist_projection", x => new { x.TrackedAddressId, x.ChainId });
            });

        migrationBuilder.CreateIndex(
            name: "ix_inbox_messages_processed_at_utc",
            schema: "notification",
            table: "inbox_messages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "ix_watchlist_projection_chain_address",
            schema: "notification",
            table: "watchlist_projection",
            columns: new[] { "ChainId", "Address" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inbox_messages",
            schema: "notification");

        migrationBuilder.DropTable(
            name: "watchlist_projection",
            schema: "notification");
    }
}
