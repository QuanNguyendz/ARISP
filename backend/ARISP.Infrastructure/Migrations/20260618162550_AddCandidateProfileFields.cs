using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "about",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "date_of_birth",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "education_json",
                table: "candidate_accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "experience_json",
                table: "candidate_accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "github_url",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "linkedin_url",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portfolio_url",
                table: "candidate_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "skills_json",
                table: "candidate_accounts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "about",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "education_json",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "experience_json",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "github_url",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "linkedin_url",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "location",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "portfolio_url",
                table: "candidate_accounts");

            migrationBuilder.DropColumn(
                name: "skills_json",
                table: "candidate_accounts");
        }
    }
}
