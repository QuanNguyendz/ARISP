using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineTestScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "correct_count",
                table: "online_test_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_questions",
                table: "online_test_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "correct_options",
                table: "online_test_questions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "question_type",
                table: "online_test_questions",
                type: "text",
                nullable: false,
                defaultValue: "single");

            migrationBuilder.AddColumn<int>(
                name: "online_test_duration_minutes",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "online_test_questions_per_test",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correct_count",
                table: "online_test_submissions");

            migrationBuilder.DropColumn(
                name: "total_questions",
                table: "online_test_submissions");

            migrationBuilder.DropColumn(
                name: "correct_options",
                table: "online_test_questions");

            migrationBuilder.DropColumn(
                name: "question_type",
                table: "online_test_questions");

            migrationBuilder.DropColumn(
                name: "online_test_duration_minutes",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "online_test_questions_per_test",
                table: "job_postings");
        }
    }
}
