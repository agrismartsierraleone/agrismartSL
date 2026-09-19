using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerSellerOrderReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedByAdminId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReconciledByAdminId",
                table: "Orders");

            // Pre-existing orders predate per-seller reconciliation. Backfill with an empty JSON array
            // rather than an empty string, which isn't valid JSON and would throw when Order.SellerPayments
            // is next deserialized. Legacy orders simply have no seller-level breakdown to reconcile against.
            migrationBuilder.AddColumn<string>(
                name: "SellerPayments",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerPayments",
                table: "Orders");

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
                name: "ReconciledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReconciledByAdminId",
                table: "Orders",
                type: "uuid",
                nullable: true);
        }
    }
}
