using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InterviewEvaluationPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "evaluation_attempts",
                table: "interview_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "evaluation_error",
                table: "interview_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evaluation_status",
                table: "interview_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "evaluation_updated_at",
                table: "interview_sessions",
                type: "timestamp with time zone",
                nullable: true);

            // ADR-073 — trạng thái chấm cho phiên ĐÃ đóng từ trước: có báo cáo → done; chưa có → pending để
            // hàng đợi nền chấm (phiên thiếu bộ tiêu chí sẽ tự chuyển sang blocked_no_rubric, không tốn lượt AI).
            migrationBuilder.Sql(@"
UPDATE interview_sessions s
SET evaluation_status = CASE WHEN EXISTS (SELECT 1 FROM evaluations e WHERE e.session_id = s.id)
                             THEN 'done' ELSE 'pending' END,
    evaluation_updated_at = now()
WHERE s.status = 'completed' AND s.evaluation_status IS NULL;");

            // Trước ADR-073 bộ tiêu chí phỏng vấn theo tin/vòng được nạp như một playbook thường, nên một (tin, vòng)
            // có thể có nhiều bản cùng sống — index duy nhất bên dưới sẽ không tạo được. Giữ bản MỚI NHẤT, xoá MỀM
            // các bản cũ hơn (khôi phục được bằng cách đặt lại deleted_at = NULL).
            migrationBuilder.Sql(@"
DO $$
DECLARE n integer;
BEGIN
    WITH ranked AS (
        SELECT id, row_number() OVER (
                   PARTITION BY scope, scope_ref_id, COALESCE(round_number, -1)
                   ORDER BY created_at DESC, id DESC) AS rn
        FROM playbook_documents
        WHERE document_type = 'interview_rubric'
          AND scope IN ('job_posting', 'round')
          AND deleted_at IS NULL)
    UPDATE playbook_documents p SET deleted_at = now()
    FROM ranked r WHERE p.id = r.id AND r.rn > 1;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n > 0 THEN
        RAISE NOTICE 'ADR-073: xoá mềm % bộ tiêu chí phỏng vấn trùng (tin, vòng) — giữ bản mới nhất.', n;
    END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "ux_playbook_documents_job_interview_rubric",
                table: "playbook_documents",
                column: "scope_ref_id",
                unique: true,
                filter: "scope = 'job_posting' AND document_type = 'interview_rubric' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_playbook_documents_round_interview_rubric",
                table: "playbook_documents",
                columns: new[] { "scope_ref_id", "round_number" },
                unique: true,
                filter: "scope = 'round' AND document_type = 'interview_rubric' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_interview_sessions_evaluation_pending",
                table: "interview_sessions",
                column: "evaluation_status",
                filter: "evaluation_status IS NOT NULL AND evaluation_status <> 'done'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_playbook_documents_job_interview_rubric",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "ux_playbook_documents_round_interview_rubric",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "idx_interview_sessions_evaluation_pending",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "evaluation_attempts",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "evaluation_error",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "evaluation_status",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "evaluation_updated_at",
                table: "interview_sessions");
        }
    }
}
