using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineTestFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "online_test_questions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "online_test_pass_score",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 70);

            migrationBuilder.CreateIndex(
                name: "IX_online_test_submissions_application_id_round_number",
                table: "online_test_submissions",
                columns: new[] { "application_id", "round_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_online_test_submissions_application_id_round_number",
                table: "online_test_submissions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "online_test_questions");

            migrationBuilder.DropColumn(
                name: "online_test_pass_score",
                table: "job_postings");
        }
    }
}
