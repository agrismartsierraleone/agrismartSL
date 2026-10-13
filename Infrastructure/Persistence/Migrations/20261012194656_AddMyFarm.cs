using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMyFarm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing farms and crops get an empty map outline ("[]" is the JSON the model reads) and stay in the field ("Active").
            migrationBuilder.AddColumn<string>(
                name: "Boundary",
                table: "Farms",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Boundary",
                table: "FarmCrops",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedHarvestDate",
                table: "FarmCrops",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HarvestedOn",
                table: "FarmCrops",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "FarmCrops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "FarmCrops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "FarmCrops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FarmCrops",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlotName",
                table: "FarmCrops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FarmCrops",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "FarmCrops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Variety",
                table: "FarmCrops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FarmActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmCropId = table.Column<Guid>(type: "uuid", nullable: true),
                    CropId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmActivities_FarmCrops_FarmCropId",
                        column: x => x.FarmCropId,
                        principalTable: "FarmCrops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FarmActivities_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FarmScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmCropId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlantScanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmScans_FarmCrops_FarmCropId",
                        column: x => x.FarmCropId,
                        principalTable: "FarmCrops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FarmScans_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmScans_PlantScans_PlantScanId",
                        column: x => x.PlantScanId,
                        principalTable: "PlantScans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmScans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmActivities_FarmCropId",
                table: "FarmActivities",
                column: "FarmCropId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmActivities_FarmId_Status_DueDate",
                table: "FarmActivities",
                columns: new[] { "FarmId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmActivities_UserId",
                table: "FarmActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmScans_FarmCropId",
                table: "FarmScans",
                column: "FarmCropId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmScans_FarmId_CreatedAt",
                table: "FarmScans",
                columns: new[] { "FarmId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmScans_PlantScanId",
                table: "FarmScans",
                column: "PlantScanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmScans_UserId",
                table: "FarmScans",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmActivities");

            migrationBuilder.DropTable(
                name: "FarmScans");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "ExpectedHarvestDate",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "HarvestedOn",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "PlotName",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "Variety",
                table: "FarmCrops");
        }
    }
}
