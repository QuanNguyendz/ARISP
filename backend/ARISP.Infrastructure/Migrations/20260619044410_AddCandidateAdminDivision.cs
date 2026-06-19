using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAdminDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "province_code",
                table: "candidate_accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province_name",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ward_code",
                table: "candidate_accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ward_name",
                table: "candidate_accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "province_code",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "province_name",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "ward_code",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "ward_name",
                table: "candidate_accounts");
        }
    }
}
