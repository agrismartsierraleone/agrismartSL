using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Crop = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PriceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketPrices_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_Crop_District_PriceDate",
                table: "MarketPrices",
                columns: new[] { "Crop", "District", "PriceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_PriceDate",
                table: "MarketPrices",
                column: "PriceDate");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_RecordedByUserId",
                table: "MarketPrices",
                column: "RecordedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketPrices");
        }
    }
}
