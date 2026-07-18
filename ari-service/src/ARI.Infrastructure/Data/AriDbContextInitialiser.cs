using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Data
{
    /// <summary>
    /// Khởi tạo database lúc app boot (pattern theo template Jason Taylor CleanArchitecture):
    /// auto-migrate với retry + đảm bảo schema bootstrap idempotent (index/cột/bảng ngoài migration).
    /// </summary>
    public class AriDbContextInitialiser
    {
        private readonly AriDbContext _context;
        private readonly ILogger<AriDbContextInitialiser> _logger;

        public AriDbContextInitialiser(AriDbContext context, ILogger<AriDbContextInitialiser> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitialiseAsync()
        {
            await MigrateWithRetryAsync();
            await EnsureBootstrapSchemaAsync();
        }

        // Auto database migration on startup — thử lại vài lần vì kết nối Supabase đôi khi
        // chậm/timeout auth lúc khởi động (lỗi thoáng qua, không phải sai migration).
        private async Task MigrateWithRetryAsync()
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Console.WriteLine($"Applying EF Core migrations (attempt {attempt}/3)...");
                    await _context.Database.MigrateAsync();
                    Console.WriteLine("Migrations applied.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply migrations (attempt {Attempt}/3).", attempt);
                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
        }

        // Đảm bảo index cho các cột lọc nóng (idempotent, CREATE INDEX IF NOT EXISTS). InitialCreate
        // gần như không tạo index FK → mọi truy vấn theo candidate/application bị seq scan (gây timeout).
        // Biến seq scan → index seek. Mỗi câu chạy riêng để 1 lỗi không chặn các index còn lại.
        private async Task EnsureBootstrapSchemaAsync()
        {
            try
            {
                var indexStatements = new[]
                {
                    "CREATE INDEX IF NOT EXISTS ix_applications_candidate_account_id ON applications (candidate_account_id)",
                    "CREATE INDEX IF NOT EXISTS ix_applications_candidate_email ON applications (candidate_email)",
                    "CREATE INDEX IF NOT EXISTS ix_applications_job_posting_id ON applications (job_posting_id)",
                    "CREATE INDEX IF NOT EXISTS ix_applications_cv_jd_analysis_id ON applications (cv_jd_analysis_id)",
                    "CREATE INDEX IF NOT EXISTS ix_notifications_candidate_account_id ON notifications (candidate_account_id)",
                    "CREATE INDEX IF NOT EXISTS ix_saved_jobs_candidate_account_id ON saved_jobs (candidate_account_id)",
                    "CREATE INDEX IF NOT EXISTS ix_interview_sessions_application_id ON interview_sessions (application_id)",
                    "CREATE INDEX IF NOT EXISTS ix_evaluations_session_id ON evaluations (session_id)",
                    "CREATE INDEX IF NOT EXISTS ix_evaluations_application_id ON evaluations (application_id)",
                    "CREATE INDEX IF NOT EXISTS ix_hr_reviews_evaluation_id ON hr_reviews (evaluation_id)",
                    "CREATE INDEX IF NOT EXISTS ix_interview_codes_application_id ON interview_codes (application_id)",
                    "CREATE INDEX IF NOT EXISTS ix_interview_bookings_application_id ON interview_bookings (application_id)",
                    "CREATE INDEX IF NOT EXISTS ix_job_postings_created_by_user_id ON job_postings (created_by_user_id)",
                };
                foreach (var stmt in indexStatements)
                {
                    try { await _context.Database.ExecuteSqlRawAsync(stmt); }
                    catch (Exception exIdx) { _logger.LogWarning(exIdx, "Could not create index: {Stmt}", stmt); }
                }
                Console.WriteLine("Performance indexes ensured.");

                // Cột phê duyệt HR Leader + file JD đã đóng dấu (idempotent ADD COLUMN IF NOT EXISTS).
                var columnStatements = new[]
                {
                    "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_by_user_id uuid",
                    "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_at timestamptz",
                    "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approver_name text",
                    "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS signed_jd_file_url text",
                };
                foreach (var stmt in columnStatements)
                {
                    try { await _context.Database.ExecuteSqlRawAsync(stmt); }
                    catch (Exception exCol) { _logger.LogWarning(exCol, "Could not add column: {Stmt}", stmt); }
                }
                Console.WriteLine("Job approval columns ensured.");

                // Bảng lời mời phỏng vấn theo vòng (InterviewInvite) — idempotent CREATE TABLE IF NOT EXISTS.
                var tableStatements = new[]
                {
                    @"CREATE TABLE IF NOT EXISTS interview_invites (
            id uuid PRIMARY KEY,
            application_id uuid NOT NULL,
            round_number integer NOT NULL DEFAULT 1,
            token_hash text NOT NULL,
            expires_at timestamptz NOT NULL,
            scheduled_at timestamptz NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        )",
                    "CREATE INDEX IF NOT EXISTS ix_interview_invites_application_id ON interview_invites (application_id)",
                    "CREATE INDEX IF NOT EXISTS ix_interview_invites_token_hash ON interview_invites (token_hash)",
                    // Chống đặt trùng: tối đa 1 booking 'scheduled' / (hồ sơ, vòng).
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_interview_bookings_app_round_scheduled ON interview_bookings (application_id, round_number) WHERE status = 'scheduled'",
                };
                foreach (var stmt in tableStatements)
                {
                    try { await _context.Database.ExecuteSqlRawAsync(stmt); }
                    catch (Exception exTbl) { _logger.LogWarning(exTbl, "Could not ensure table/index: {Stmt}", stmt); }
                }
                Console.WriteLine("Interview invite table ensured.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ensure performance indexes / approval columns.");
            }
        }
    }
}
