using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Hợp nhất schema bootstrap từng chạy bằng raw SQL lúc startup (Program.cs cũ) vào migration.
    /// DB thật có thể đã bootstrap một phần (mỗi câu lệnh cũ nuốt lỗi riêng lẻ) nên toàn bộ Up/Down
    /// dùng SQL idempotent (IF NOT EXISTS / IF EXISTS) thay vì operations scaffold — an toàn trên
    /// mọi trạng thái: DB đã bootstrap đầy đủ, bootstrap dở dang, hoặc DB trống replay từ đầu.
    /// </summary>
    public partial class ReconcileStartupBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cột phê duyệt HR Leader + file JD đã đóng dấu.
            migrationBuilder.Sql("ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_by_user_id uuid");
            migrationBuilder.Sql("ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_at timestamptz");
            migrationBuilder.Sql("ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approver_name text");
            migrationBuilder.Sql("ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS signed_jd_file_url text");

            // Bảng lời mời phỏng vấn theo vòng (InterviewInvite).
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS interview_invites (
            id uuid PRIMARY KEY,
            application_id uuid NOT NULL,
            round_number integer NOT NULL DEFAULT 1,
            token_hash text NOT NULL,
            expires_at timestamptz NOT NULL,
            scheduled_at timestamptz NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        )");

            // Index cho các cột lọc nóng (trước đây tạo lúc startup — InitialCreate gần như
            // không tạo index FK nên mọi truy vấn theo candidate/application bị seq scan).
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_applications_candidate_account_id ON applications (candidate_account_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_applications_candidate_email ON applications (candidate_email)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_applications_job_posting_id ON applications (job_posting_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_notifications_candidate_account_id ON notifications (candidate_account_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_saved_jobs_candidate_account_id ON saved_jobs (candidate_account_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_interview_sessions_application_id ON interview_sessions (application_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_evaluations_session_id ON evaluations (session_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_evaluations_application_id ON evaluations (application_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_hr_reviews_evaluation_id ON hr_reviews (evaluation_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_interview_codes_application_id ON interview_codes (application_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_interview_bookings_application_id ON interview_bookings (application_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_job_postings_created_by_user_id ON job_postings (created_by_user_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_interview_invites_application_id ON interview_invites (application_id)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_interview_invites_token_hash ON interview_invites (token_hash)");

            // Chống đặt trùng: tối đa 1 booking 'scheduled' / (hồ sơ, vòng).
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_interview_bookings_app_round_scheduled ON interview_bookings (application_id, round_number) WHERE status = 'scheduled'");

            // Hợp nhất index trùng trên applications.cv_jd_analysis_id: AddCvJdAnalyses tạo
            // "IX_applications_cv_jd_analysis_id" (tên EF mặc định), bootstrap cũ tạo thêm bản
            // lowercase — giữ bản lowercase (khớp model), bỏ bản trùng.
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_applications_cv_jd_analysis_id ON applications (cv_jd_analysis_id)");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_applications_cv_jd_analysis_id\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_applications_cv_jd_analysis_id\" ON applications (cv_jd_analysis_id)");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_applications_cv_jd_analysis_id");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_interview_bookings_app_round_scheduled");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_job_postings_created_by_user_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_interview_bookings_application_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_interview_codes_application_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_hr_reviews_evaluation_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_evaluations_application_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_evaluations_session_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_interview_sessions_application_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_saved_jobs_candidate_account_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_notifications_candidate_account_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_applications_job_posting_id");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_applications_candidate_email");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_applications_candidate_account_id");

            migrationBuilder.Sql("DROP TABLE IF EXISTS interview_invites");

            migrationBuilder.Sql("ALTER TABLE job_postings DROP COLUMN IF EXISTS signed_jd_file_url");
            migrationBuilder.Sql("ALTER TABLE job_postings DROP COLUMN IF EXISTS approver_name");
            migrationBuilder.Sql("ALTER TABLE job_postings DROP COLUMN IF EXISTS approved_at");
            migrationBuilder.Sql("ALTER TABLE job_postings DROP COLUMN IF EXISTS approved_by_user_id");
        }
    }
}
