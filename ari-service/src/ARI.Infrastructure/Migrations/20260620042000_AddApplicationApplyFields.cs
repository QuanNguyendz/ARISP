using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationApplyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_letter",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "desired_location",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notice_period",
                table: "applications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_letter",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "desired_location",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "notice_period",
                table: "applications");
        }
    }
}
