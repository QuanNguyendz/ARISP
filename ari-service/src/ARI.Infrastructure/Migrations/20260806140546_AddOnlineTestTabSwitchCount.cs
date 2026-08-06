using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineTestTabSwitchCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tab_switch_count",
                table: "online_test_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tab_switch_count",
                table: "online_test_submissions");
        }
    }
}
