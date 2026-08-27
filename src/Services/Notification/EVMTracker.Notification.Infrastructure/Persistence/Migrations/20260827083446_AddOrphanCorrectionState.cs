using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVMTracker.Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrphanCorrectionState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CorrectionDeliveryEventId",
            schema: "notification",
            table: "transfer_notifications",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeliveryEventId",
            schema: "notification",
            table: "transfer_notifications",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<bool>(
            name: "IsOrphaned",
            schema: "notification",
            table: "transfer_notifications",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "OrphanedAtUtc",
            schema: "notification",
            table: "transfer_notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE notification.transfer_notifications
            SET "DeliveryEventId" = "Id";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_transfer_notifications_ChainId_TransactionHash_LogIndex",
            schema: "notification",
            table: "transfer_notifications",
            columns: new[] { "ChainId", "TransactionHash", "LogIndex" });

        migrationBuilder.CreateIndex(
            name: "ix_transfer_notifications_is_orphaned",
            schema: "notification",
            table: "transfer_notifications",
            column: "IsOrphaned");

        migrationBuilder.CreateIndex(
            name: "ux_transfer_notifications_delivery_event_id",
            schema: "notification",
            table: "transfer_notifications",
            column: "DeliveryEventId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_transfer_notifications_ChainId_TransactionHash_LogIndex",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropIndex(
            name: "ix_transfer_notifications_is_orphaned",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropIndex(
            name: "ux_transfer_notifications_delivery_event_id",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropColumn(
            name: "CorrectionDeliveryEventId",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropColumn(
            name: "DeliveryEventId",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropColumn(
            name: "IsOrphaned",
            schema: "notification",
            table: "transfer_notifications");

        migrationBuilder.DropColumn(
            name: "OrphanedAtUtc",
            schema: "notification",
            table: "transfer_notifications");
    }
}
