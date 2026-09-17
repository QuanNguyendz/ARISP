using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-070 — bộ tiêu chí chấm CV bắt buộc, điểm CV giải thích được.
    ///
    /// Cấu trúc: phiếu mang bộ tiêu chí (<c>cv_rubric_json</c>); mỗi bản chấm ghi bộ tiêu chí đã dùng
    /// (<c>rubric_document_id</c>) và khoá DUY NHẤT theo (tin, CV, bộ tiêu chí); mỗi tin chỉ một bộ tiêu chí sống.
    ///
    /// Dữ liệu (chạy TRƯỚC khi tạo hai index duy nhất, nếu không migration hỏng trên DB đã có trùng):
    /// <list type="bullet">
    /// <item><c>failed</c> → <c>invalid_cv</c> (tên cũ khiến màn nhân sự hiện "0 điểm").</item>
    /// <item>Khử bản chấm trùng (tin, CV): giữ bản <c>completed</c> mới nhất, GẮN LẠI hồ sơ đang trỏ vào bản
    ///       bị xoá rồi mới xoá — khoá ngoại <c>applications.cv_jd_analysis_id</c> là NO ACTION.</item>
    /// <item>Tin có nhiều bộ tiêu chí CV sống: giữ bản mới nhất, xoá mềm phần còn lại.</item>
    /// </list>
    /// Mọi bản chấm cũ có <c>rubric_document_id = NULL</c> nên đều bị coi là lỗi thời: lượt quét nền chấm
    /// lại hồ sơ của các tin đã có bộ tiêu chí, còn tin chưa có thì hồ sơ chờ và HM được nhắc.
    /// </summary>
    public partial class CvRubricMandatoryScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cv_jd_analyses_job_posting_id_cv_hash",
                table: "cv_jd_analyses");

            migrationBuilder.Sql("UPDATE cv_jd_analyses SET status = 'invalid_cv' WHERE status = 'failed';");

            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT id,
           FIRST_VALUE(id) OVER (
               PARTITION BY job_posting_id, cv_hash
               ORDER BY (status = 'completed') DESC, created_at DESC, id) AS keep_id
    FROM cv_jd_analyses
)
UPDATE applications a
SET cv_jd_analysis_id = r.keep_id
FROM ranked r
WHERE a.cv_jd_analysis_id = r.id AND r.id <> r.keep_id;

WITH ranked AS (
    SELECT id,
           FIRST_VALUE(id) OVER (
               PARTITION BY job_posting_id, cv_hash
               ORDER BY (status = 'completed') DESC, created_at DESC, id) AS keep_id
    FROM cv_jd_analyses
)
DELETE FROM cv_jd_analyses c
USING ranked r
WHERE c.id = r.id AND r.id <> r.keep_id;");

            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT id, ROW_NUMBER() OVER (PARTITION BY scope_ref_id ORDER BY created_at DESC, id) AS rn
    FROM playbook_documents
    WHERE scope = 'job_posting' AND document_type = 'cv_rubric' AND deleted_at IS NULL
)
UPDATE playbook_documents p
SET deleted_at = now(), updated_at = now()
FROM ranked r
WHERE p.id = r.id AND r.rn > 1;");

            // Liệt kê tin đang chạy mà chưa có bộ tiêu chí — người vận hành biết cần nhắc ai.
            migrationBuilder.Sql(@"
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT j.id, j.title
        FROM job_postings j
        WHERE j.status = 'active' AND j.deleted_at IS NULL
          AND NOT EXISTS (
              SELECT 1 FROM playbook_documents p
              WHERE p.scope = 'job_posting' AND p.document_type = 'cv_rubric'
                AND p.deleted_at IS NULL AND p.scope_ref_id = j.id)
    LOOP
        RAISE NOTICE 'ADR-070: tin dang chay chua co bo tieu chi cham CV: % (%)', r.title, r.id;
    END LOOP;
END $$;");

            migrationBuilder.AddColumn<string>(
                name: "cv_rubric_json",
                table: "recruitment_requests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rubric_document_id",
                table: "cv_jd_analyses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seniority_alignment",
                table: "cv_jd_analyses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_playbook_documents_job_cv_rubric",
                table: "playbook_documents",
                column: "scope_ref_id",
                unique: true,
                filter: "scope = 'job_posting' AND document_type = 'cv_rubric' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cv_jd_analyses_rubric_document_id",
                table: "cv_jd_analyses",
                column: "rubric_document_id");

            migrationBuilder.CreateIndex(
                name: "ux_cv_jd_analyses_job_cv_rubric",
                table: "cv_jd_analyses",
                columns: new[] { "job_posting_id", "cv_hash", "rubric_document_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_cv_jd_analyses_playbook_documents_rubric_document_id",
                table: "cv_jd_analyses",
                column: "rubric_document_id",
                principalTable: "playbook_documents",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cv_jd_analyses_playbook_documents_rubric_document_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropIndex(
                name: "ux_playbook_documents_job_cv_rubric",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "IX_cv_jd_analyses_rubric_document_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropIndex(
                name: "ux_cv_jd_analyses_job_cv_rubric",
                table: "cv_jd_analyses");

            migrationBuilder.DropColumn(
                name: "cv_rubric_json",
                table: "recruitment_requests");

            migrationBuilder.DropColumn(
                name: "rubric_document_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropColumn(
                name: "seniority_alignment",
                table: "cv_jd_analyses");

            migrationBuilder.CreateIndex(
                name: "IX_cv_jd_analyses_job_posting_id_cv_hash",
                table: "cv_jd_analyses",
                columns: new[] { "job_posting_id", "cv_hash" });

            migrationBuilder.Sql("UPDATE cv_jd_analyses SET status = 'failed' WHERE status = 'invalid_cv';");
        }
    }
}
