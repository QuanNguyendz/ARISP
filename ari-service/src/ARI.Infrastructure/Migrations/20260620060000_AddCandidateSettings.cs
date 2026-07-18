using ARISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    [DbContext(typeof(ARISPDbContext))]
    [Migration("20260620060000_AddCandidateSettings")]
    public partial class AddCandidateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "settings_json",
                table: "candidate_accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "settings_json",
                table: "candidate_accounts");
        }
    }
}
