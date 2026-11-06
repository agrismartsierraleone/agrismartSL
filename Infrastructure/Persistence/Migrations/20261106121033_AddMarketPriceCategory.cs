using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketPriceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketPrices_Crop_District_PriceDate",
                table: "MarketPrices");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "MarketPrices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Crop");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_Category_Crop_District_PriceDate",
                table: "MarketPrices",
                columns: new[] { "Category", "Crop", "District", "PriceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old index allows one row per name, so rolling back keeps only the crop prices.
            migrationBuilder.Sql("DELETE FROM \"MarketPrices\" WHERE \"Category\" <> 'Crop';");

            migrationBuilder.DropIndex(
                name: "IX_MarketPrices_Category_Crop_District_PriceDate",
                table: "MarketPrices");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "MarketPrices");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_Crop_District_PriceDate",
                table: "MarketPrices",
                columns: new[] { "Crop", "District", "PriceDate" },
                unique: true);
        }
    }
}
