using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-075 — công thức chấm CV do Hiring Manager quyết định. Chỉ THÊM cột cho phép NULL, không default, không
    /// backfill: NULL = công thức mặc định (90/70/40 · 80/65/50), đúng là công thức đã sinh ra mọi điểm có sẵn — nên sau
    /// migration không hồ sơ nào bị coi là cần chấm lại. Không bảng mới (quy tắc 24 không phát sinh).
    /// </summary>
    public partial class CvScoringFormula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cv_scoring_policy_json",
                table: "recruitment_requests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_policy_json",
                table: "playbook_documents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "derived_from_analysis_id",
                table: "cv_jd_analyses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gate_status",
                table: "cv_jd_analyses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_policy",
                table: "cv_jd_analyses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cv_jd_analyses_derived_from_analysis_id",
                table: "cv_jd_analyses",
                column: "derived_from_analysis_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cv_jd_analyses_cv_jd_analyses_derived_from_analysis_id",
                table: "cv_jd_analyses",
                column: "derived_from_analysis_id",
                principalTable: "cv_jd_analyses",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cv_jd_analyses_cv_jd_analyses_derived_from_analysis_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropIndex(
                name: "IX_cv_jd_analyses_derived_from_analysis_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropColumn(
                name: "cv_scoring_policy_json",
                table: "recruitment_requests");

            migrationBuilder.DropColumn(
                name: "scoring_policy_json",
                table: "playbook_documents");

            migrationBuilder.DropColumn(
                name: "derived_from_analysis_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropColumn(
                name: "gate_status",
                table: "cv_jd_analyses");

            migrationBuilder.DropColumn(
                name: "scoring_policy",
                table: "cv_jd_analyses");
        }
    }
}
