using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Watchlist.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "watchlist");

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "watchlist",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DeliveryAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "tracked_addresses",
            schema: "watchlist",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tracked_addresses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "tracked_address_networks",
            schema: "watchlist",
            columns: table => new
            {
                TrackedAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                Address = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tracked_address_networks", x => new { x.TrackedAddressId, x.ChainId });
                table.ForeignKey(
                    name: "FK_tracked_address_networks_tracked_addresses_TrackedAddressId",
                    column: x => x.TrackedAddressId,
                    principalSchema: "watchlist",
                    principalTable: "tracked_addresses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_processed_at_utc",
            schema: "watchlist",
            table: "outbox_messages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "ux_tracked_address_networks_user_address_chain",
            schema: "watchlist",
            table: "tracked_address_networks",
            columns: new[] { "TelegramUserId", "Address", "ChainId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tracked_addresses_telegram_user_id",
            schema: "watchlist",
            table: "tracked_addresses",
            column: "TelegramUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "watchlist");

        migrationBuilder.DropTable(
            name: "tracked_address_networks",
            schema: "watchlist");

        migrationBuilder.DropTable(
            name: "tracked_addresses",
            schema: "watchlist");
    }
}
