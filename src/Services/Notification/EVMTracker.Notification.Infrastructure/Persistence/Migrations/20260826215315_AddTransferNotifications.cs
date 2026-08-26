using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTransferNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "notification",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EventVersion = table.Column<int>(type: "integer", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DeliveryAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "transfer_notifications",
            schema: "notification",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                ChainId = table.Column<long>(type: "bigint", nullable: false),
                NetworkName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                MonitoredAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                TokenContract = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                TokenSymbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TokenName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                TokenDecimals = table.Column<int>(type: "integer", nullable: true),
                RawValue = table.Column<string>(type: "character varying(78)", maxLength: 78, nullable: false),
                DisplayAmount = table.Column<string>(type: "character varying(336)", maxLength: 336, nullable: false),
                FromAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                ToAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                TransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                BlockNumber = table.Column<long>(type: "bigint", nullable: false),
                BlockHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                LogIndex = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transfer_notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_outbox_pending",
            schema: "notification",
            table: "outbox_messages",
            columns: new[] { "ProcessedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_transfer_notifications_source_event_id",
            schema: "notification",
            table: "transfer_notifications",
            column: "SourceEventId");

        migrationBuilder.CreateIndex(
            name: "ux_transfer_notifications_subscription_chain_transaction_log",
            schema: "notification",
            table: "transfer_notifications",
            columns: new[] { "SubscriptionId", "ChainId", "TransactionHash", "LogIndex" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "notification");

        migrationBuilder.DropTable(
            name: "transfer_notifications",
            schema: "notification");
    }
}
