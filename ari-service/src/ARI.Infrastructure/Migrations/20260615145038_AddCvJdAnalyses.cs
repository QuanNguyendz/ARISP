using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvJdAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cv_jd_analysis_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cv_jd_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cv_hash = table.Column<string>(type: "text", nullable: false),
                    match_score = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    skills_matched = table.Column<string>(type: "jsonb", nullable: false),
                    skills_gaps = table.Column<string>(type: "jsonb", nullable: false),
                    red_flags = table.Column<string>(type: "jsonb", nullable: false),
                    experience_relevance = table.Column<string>(type: "text", nullable: false),
                    overall_recommendation = table.Column<string>(type: "text", nullable: false),
                    ai_model = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    processing_time_ms = table.Column<int>(type: "integer", nullable: false),
                    raw_response = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_jd_analyses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_cv_jd_analysis_id",
                table: "applications",
                column: "cv_jd_analysis_id");

            migrationBuilder.CreateIndex(
                name: "IX_cv_jd_analyses_job_posting_id_cv_hash",
                table: "cv_jd_analyses",
                columns: new[] { "job_posting_id", "cv_hash" });

            migrationBuilder.AddForeignKey(
                name: "FK_applications_cv_jd_analyses_cv_jd_analysis_id",
                table: "applications",
                column: "cv_jd_analysis_id",
                principalTable: "cv_jd_analyses",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_cv_jd_analyses_cv_jd_analysis_id",
                table: "applications");

            migrationBuilder.DropTable(
                name: "cv_jd_analyses");

            migrationBuilder.DropIndex(
                name: "IX_applications_cv_jd_analysis_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "cv_jd_analysis_id",
                table: "applications");
        }
    }
}
