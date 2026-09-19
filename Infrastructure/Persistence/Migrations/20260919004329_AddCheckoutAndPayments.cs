using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutSessionId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmedByAdminId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldExpiresAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            // Pre-existing orders predate payment tracking. Backfill them as CashOnPickup/Pending — the
            // closest match to how orders were actually settled before this migration — rather than an
            // empty string that wouldn't parse as either enum on the next read.
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CashOnPickup");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReconciledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReconciledByAdminId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckoutAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CheckoutSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonimeSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RedirectUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancelUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SuccessUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpireTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawResponseJson = table.Column<string>(type: "text", nullable: false),
                    PaymentCodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UssdCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PaymentCodeStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PaymentCodeExpireTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CheckoutSessionId",
                table: "Orders",
                column: "CheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus",
                table: "Orders",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAuditLogs_CheckoutSessionId",
                table: "CheckoutAuditLogs",
                column: "CheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAuditLogs_CreatedAt",
                table: "CheckoutAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAuditLogs_OrderId",
                table: "CheckoutAuditLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_MonimeSessionId",
                table: "CheckoutSessions",
                column: "MonimeSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_OrderId",
                table: "CheckoutSessions",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_PaymentCodeId",
                table: "CheckoutSessions",
                column: "PaymentCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_UserId",
                table: "CheckoutSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutAuditLogs");

            migrationBuilder.DropTable(
                name: "CheckoutSessions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CheckoutSessionId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedByAdminId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReconciledByAdminId",
                table: "Orders");
        }
    }
}
