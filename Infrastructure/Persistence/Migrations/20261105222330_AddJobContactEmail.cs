using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrismartAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobContactEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "JobPosts",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "JobPosts");
        }
    }
}
