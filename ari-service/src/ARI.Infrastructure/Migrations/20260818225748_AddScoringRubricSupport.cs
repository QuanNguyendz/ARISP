using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringRubricSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rubric_json",
                table: "playbook_documents",
                type: "text",
                nullable: true);

            // Mặc định 70 (khớp default của entity). Để 0 như EF tự sinh thì mọi tin CŨ có điểm sàn 0
            // — tức buổi phỏng vấn nào cũng "đạt", hỏng im lặng ngay khi migrate.
            migrationBuilder.AddColumn<int>(
                name: "interview_pass_score",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 70);

            // "{}" chứ không phải "" — chuỗi rỗng không phải JSON hợp lệ, cột jsonb sẽ từ chối.
            migrationBuilder.AddColumn<string>(
                name: "criterion_scores",
                table: "cv_jd_analyses",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rubric_json",
                table: "playbook_documents");

            migrationBuilder.DropColumn(
                name: "interview_pass_score",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "criterion_scores",
                table: "cv_jd_analyses");
        }
    }
}
