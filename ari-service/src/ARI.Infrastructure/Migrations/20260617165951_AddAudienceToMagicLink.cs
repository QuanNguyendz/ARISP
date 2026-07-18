using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceToMagicLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audience",
                table: "magic_links",
                type: "text",
                nullable: false,
                defaultValue: "candidate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audience",
                table: "magic_links");
        }
    }
}
