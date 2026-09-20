using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCropsAndHarvestPredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Crops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseTemperatureC = table.Column<double>(type: "double precision", nullable: false),
                    UpperTemperatureC = table.Column<double>(type: "double precision", nullable: false),
                    GddToMaturity = table.Column<double>(type: "double precision", nullable: false),
                    TypicalDaysToMaturity = table.Column<int>(type: "integer", nullable: false),
                    Stages = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HarvestPredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CropId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CropName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    PlantingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    AlreadyMature = table.Column<bool>(type: "boolean", nullable: false),
                    Gdd_Accumulated = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_Required = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_Remaining = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_PercentComplete = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_BaseTemperatureC = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_UpperTemperatureC = table.Column<double>(type: "double precision", nullable: false),
                    Gdd_DaysObserved = table.Column<int>(type: "integer", nullable: false),
                    Gdd_DaysForecast = table.Column<int>(type: "integer", nullable: false),
                    Stage_Current = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Stage_Next = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Stage_NextStageStarts = table.Column<DateOnly>(type: "date", nullable: true),
                    Harvest_Earliest = table.Column<DateOnly>(type: "date", nullable: false),
                    Harvest_Expected = table.Column<DateOnly>(type: "date", nullable: false),
                    Harvest_Latest = table.Column<DateOnly>(type: "date", nullable: false),
                    Harvest_DaysFromNow = table.Column<int>(type: "integer", nullable: false),
                    Harvest_WindowWidthDays = table.Column<int>(type: "integer", nullable: false),
                    Harvest_Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Harvest_Basis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Methodology_Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Methodology_WeatherSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Methodology_AnalogYears = table.Column<int>(type: "integer", nullable: false),
                    Methodology_Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestPredictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crops_Key",
                table: "Crops",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HarvestPredictions_UserId",
                table: "HarvestPredictions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Crops");

            migrationBuilder.DropTable(
                name: "HarvestPredictions");
        }
    }
}
