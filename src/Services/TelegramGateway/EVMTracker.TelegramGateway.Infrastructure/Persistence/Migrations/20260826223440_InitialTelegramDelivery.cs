using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialTelegramDelivery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "telegram_gateway");

        migrationBuilder.CreateTable(
            name: "telegram_deliveries",
            schema: "telegram_gateway",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                Message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                ParseMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                TelegramMessageId = table.Column<int>(type: "integer", nullable: true),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_telegram_deliveries", x => x.EventId);
            });

        migrationBuilder.CreateTable(
            name: "telegram_delivery_attempts",
            schema: "telegram_gateway",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                AttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                TelegramMessageId = table.Column<int>(type: "integer", nullable: true),
                Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                RetryDelayMilliseconds = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_telegram_delivery_attempts", x => new { x.EventId, x.AttemptNumber });
                table.ForeignKey(
                    name: "FK_telegram_delivery_attempts_telegram_deliveries_EventId",
                    column: x => x.EventId,
                    principalSchema: "telegram_gateway",
                    principalTable: "telegram_deliveries",
                    principalColumn: "EventId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_telegram_deliveries_status",
            schema: "telegram_gateway",
            table: "telegram_deliveries",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "ux_telegram_deliveries_notification_id",
            schema: "telegram_gateway",
            table: "telegram_deliveries",
            column: "NotificationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "telegram_delivery_attempts",
            schema: "telegram_gateway");

        migrationBuilder.DropTable(
            name: "telegram_deliveries",
            schema: "telegram_gateway");
    }
}
