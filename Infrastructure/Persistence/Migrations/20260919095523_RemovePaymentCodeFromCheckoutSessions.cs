using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePaymentCodeFromCheckoutSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckoutSessions_PaymentCodeId",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "PaymentCodeExpireTime",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "PaymentCodeId",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "PaymentCodeStatus",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "UssdCode",
                table: "CheckoutSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentCodeExpireTime",
                table: "CheckoutSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCodeId",
                table: "CheckoutSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCodeStatus",
                table: "CheckoutSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UssdCode",
                table: "CheckoutSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_PaymentCodeId",
                table: "CheckoutSessions",
                column: "PaymentCodeId",
                unique: true);
        }
    }
}
