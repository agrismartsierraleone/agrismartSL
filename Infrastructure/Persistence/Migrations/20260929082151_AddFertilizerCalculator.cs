using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFertilizerCalculator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FertilizerCrops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocalName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NitrogenKgPerHa = table.Column<double>(type: "double precision", nullable: false),
                    PhosphateKgPerHa = table.Column<double>(type: "double precision", nullable: false),
                    PotashKgPerHa = table.Column<double>(type: "double precision", nullable: false),
                    ReferenceYieldTPerHa = table.Column<double>(type: "double precision", nullable: false),
                    MaxRealisticYieldTPerHa = table.Column<double>(type: "double precision", nullable: false),
                    YieldBasis = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlantsPerHectare = table.Column<int>(type: "integer", nullable: true),
                    IsPerennial = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Schedule = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FertilizerCrops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FertilizerProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NitrogenPercent = table.Column<double>(type: "double precision", nullable: false),
                    PhosphatePercent = table.Column<double>(type: "double precision", nullable: false),
                    PotashPercent = table.Column<double>(type: "double precision", nullable: false),
                    SulphurPercent = table.Column<double>(type: "double precision", nullable: true),
                    ZincPercent = table.Column<double>(type: "double precision", nullable: true),
                    BagSizeKg = table.Column<double>(type: "double precision", nullable: false),
                    PricePerBag = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FertilizerProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FertilizerRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CropId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CropName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlotLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AreaHectares = table.Column<double>(type: "double precision", nullable: false),
                    TotalEstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InputsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FertilizerRecommendations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FertilizerCrops_Key",
                table: "FertilizerCrops",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FertilizerProducts_Key",
                table: "FertilizerProducts",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FertilizerRecommendations_CropId",
                table: "FertilizerRecommendations",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_FertilizerRecommendations_UserId_CreatedAt",
                table: "FertilizerRecommendations",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FertilizerCrops");

            migrationBuilder.DropTable(
                name: "FertilizerProducts");

            migrationBuilder.DropTable(
                name: "FertilizerRecommendations");
        }
    }
}
