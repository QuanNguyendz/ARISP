using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ARI.Domain.Entities;
using ARI.Domain.Constants;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Data
{
    public class AriDbContext : DbContext
    {
        public AriDbContext(DbContextOptions<AriDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<MagicLink> MagicLinks => Set<MagicLink>();
        public DbSet<CandidateAccount> CandidateAccounts => Set<CandidateAccount>();
        public DbSet<CandidateRefreshToken> CandidateRefreshTokens => Set<CandidateRefreshToken>();
        public DbSet<JobPosting> JobPostings => Set<JobPosting>();
        public DbSet<InterviewRoundConfig> InterviewRoundConfigs => Set<InterviewRoundConfig>();
        public DbSet<ARI.Domain.Entities.Application> Applications => Set<ARI.Domain.Entities.Application>();
        public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
        public DbSet<InterviewBooking> InterviewBookings => Set<InterviewBooking>();
        public DbSet<InterviewInvite> InterviewInvites => Set<InterviewInvite>();
        public DbSet<InterviewCode> InterviewCodes => Set<InterviewCode>();
        public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<Answer> Answers => Set<Answer>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<PlaybookDocument> PlaybookDocuments => Set<PlaybookDocument>();
        public DbSet<MustAskTracking> MustAskTracking => Set<MustAskTracking>();
        public DbSet<Evaluation> Evaluations => Set<Evaluation>();
        public DbSet<HrReview> HrReviews => Set<HrReview>();
        public DbSet<CheatDetectionSignal> CheatDetectionSignals => Set<CheatDetectionSignal>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<OnlineTestQuestion> OnlineTestQuestions => Set<OnlineTestQuestion>();
        public DbSet<OnlineTestSubmission> OnlineTestSubmissions => Set<OnlineTestSubmission>();
        public DbSet<CvJdAnalysis> CvJdAnalyses => Set<CvJdAnalysis>();
        public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AccountRequest> AccountRequests => Set<AccountRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enable pgvector extension inside PostgreSQL
            modelBuilder.HasPostgresExtension("uuid-ossp");
            modelBuilder.HasPostgresExtension("vector");

            // Define snake_case mappings and query filters for soft delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;

                // Lowercase snake_case table name mapping
                var tableName = GetSnakeCase(clrType.Name);
                if (tableName.EndsWith("y"))
                    tableName = tableName.Substring(0, tableName.Length - 1) + "ies";
                else if (!tableName.EndsWith("s"))
                    tableName += "s";
                
                modelBuilder.Entity(clrType).ToTable(tableName);

                foreach (var property in entityType.GetProperties())
                {
                    property.SetColumnName(GetSnakeCase(property.Name));
                }

                // Configure soft delete filter
                if (typeof(ISoftDelete).IsAssignableFrom(clrType))
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");
                    var propertyAccess = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDelete.DeletedAt));
                    var nullCheck = System.Linq.Expressions.Expression.Equal(propertyAccess, System.Linq.Expressions.Expression.Constant(null, typeof(DateTimeOffset?)));
                    var lambda = System.Linq.Expressions.Expression.Lambda(nullCheck, parameter);
                    
                    modelBuilder.Entity(clrType).HasQueryFilter(lambda);
                }
            }

            // Custom configuration for pgvector columns.
            // Value comparer cho float[] để EF so sánh phần tử đúng (bỏ warning "no value comparer").
            var embeddingComparer = new ValueComparer<float[]?>(
                (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                v => v == null ? 0 : v.Aggregate(0, (acc, f) => HashCode.Combine(acc, f)),
                v => v == null ? null : v.ToArray());

            var embeddingProperty = modelBuilder.Entity<DocumentChunk>()
                .Property(c => c.Embedding)
                .HasColumnType("vector(1536)")
                .HasConversion(
                    v => v == null ? null : $"[{string.Join(",", v)}]",
                    v => v == null ? Array.Empty<float>() : Array.ConvertAll(v.Trim('[', ']').Split(new char[] { ',' }, StringSplitOptions.None), float.Parse)
                );
            embeddingProperty.Metadata.SetValueComparer(embeddingComparer);

            // Custom configuration for JSONB columns
            modelBuilder.Entity<JobPosting>()
                .Property(j => j.ScoringRubric)
                .HasColumnType("jsonb");

            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .Property(a => a.DemographicData)
                .HasColumnType("jsonb");

            modelBuilder.Entity<DocumentChunk>()
                .Property(c => c.Metadata)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Evaluation>()
                .Property(e => e.CriterionScores)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Evaluation>()
                .Property(e => e.QuestionAnalyses)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Evaluation>()
                .Property(e => e.CheatSignals)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Evaluation>()
                .Property(e => e.LanguageAssessment)
                .HasColumnType("jsonb");

            modelBuilder.Entity<CheatDetectionSignal>()
                .Property(c => c.Payload)
                .HasColumnType("jsonb");

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Metadata)
                .HasColumnType("jsonb");

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.IpAddress)
                .HasColumnType("inet")
                .HasConversion(
                    v => string.IsNullOrEmpty(v) ? null : System.Net.IPAddress.Parse(v),
                    v => v == null ? null : v.ToString()
                );

            modelBuilder.Entity<WebhookDelivery>()
                .Property(w => w.Payload)
                .HasColumnType("jsonb");

            modelBuilder.Entity<OnlineTestQuestion>()
                .Property(q => q.Options)
                .HasColumnType("jsonb");

            modelBuilder.Entity<OnlineTestQuestion>()
                .Property(q => q.CorrectOptions)
                .HasColumnType("jsonb");

            modelBuilder.Entity<OnlineTestSubmission>()
                .Property(s => s.SelectedAnswers)
                .HasColumnType("jsonb");

            // Mỗi hồ sơ chỉ nộp bài trắc nghiệm 1 lần / vòng (single-attempt).
            modelBuilder.Entity<OnlineTestSubmission>()
                .HasIndex(s => new { s.ApplicationId, s.RoundNumber })
                .IsUnique();

            modelBuilder.Entity<InterviewRoundConfig>()
                .HasIndex(r => new { r.JobPostingId, r.RoundNumber })
                .IsUnique();

            // Cột reminder của InterviewBooking trong DB dùng dạng có gạch dưới quanh số
            // (reminder_1h_sent / reminder_24h_sent) — khác với quy ước snake_case mặc định
            // (reminder1h_sent). Map tường minh để khớp schema thực tế.
            modelBuilder.Entity<InterviewBooking>()
                .Property(b => b.Reminder1hSent).HasColumnName("reminder_1h_sent");
            modelBuilder.Entity<InterviewBooking>()
                .Property(b => b.Reminder24hSent).HasColumnName("reminder_24h_sent");
            modelBuilder.Entity<InterviewBooking>()
                .Property(b => b.InviteEmailMessageId).HasColumnName("invite_email_message_id");
            modelBuilder.Entity<InterviewBooking>()
                .Property(b => b.LastAutoReminderHours).HasColumnName("last_auto_reminder_hours");

            modelBuilder.Entity<MustAskTracking>()
                .ToTable("must_ask_tracking");

            modelBuilder.Entity<CvJdAnalysis>()
                .ToTable("cv_jd_analyses")
                .Property(c => c.SkillsMatched)
                .HasColumnType("jsonb");

            modelBuilder.Entity<CvJdAnalysis>()
                .Property(c => c.SkillsGaps)
                .HasColumnType("jsonb");

            modelBuilder.Entity<CvJdAnalysis>()
                .Property(c => c.RedFlags)
                .HasColumnType("jsonb");

            modelBuilder.Entity<CvJdAnalysis>()
                .Property(c => c.RawResponse)
                .HasColumnType("jsonb");

            modelBuilder.Entity<CvJdAnalysis>()
                .HasIndex(c => new { c.JobPostingId, c.CvHash });

            // Mỗi ứng viên chỉ lưu một job một lần (bookmark). Partial index trên các bản ghi
            // còn hiệu lực (deleted_at IS NULL) để có thể lưu lại sau khi đã bỏ lưu (soft delete).
            modelBuilder.Entity<SavedJob>()
                .HasIndex(s => new { s.CandidateAccountId, s.JobPostingId })
                .IsUnique()
                .HasFilter("deleted_at IS NULL");

            // Mỗi sự kiện (DedupKey) chỉ sinh 1 thông báo cho mỗi ứng viên.
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.CandidateAccountId, n.DedupKey })
                .IsUnique()
                .HasFilter("deleted_at IS NULL");

            // Tương tự cho người nhận là nhân sự nội bộ (HR Admin / Recruiter / Super Admin).
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.RecipientUserId, n.DedupKey })
                .IsUnique()
                .HasFilter("deleted_at IS NULL");

            // MagicLink.Audience: mặc định "candidate" để các bản ghi cũ (trước khi tách cổng staff) được hiểu là candidate
            modelBuilder.Entity<MagicLink>()
                .Property(m => m.Audience)
                .HasDefaultValue(MagicLinkAudience.Candidate);

            // Index cho các cột lọc nóng — tên khớp đúng index đã tạo bởi startup bootstrap cũ
            // (nay là migration ReconcileStartupBootstrap) để snapshot hội tụ với schema DB thật.
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasIndex(a => a.CandidateAccountId).HasDatabaseName("ix_applications_candidate_account_id");
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasIndex(a => a.CandidateEmail).HasDatabaseName("ix_applications_candidate_email");
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasIndex(a => a.JobPostingId).HasDatabaseName("ix_applications_job_posting_id");
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasIndex(a => a.CvJdAnalysisId).HasDatabaseName("ix_applications_cv_jd_analysis_id");
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.CandidateAccountId).HasDatabaseName("ix_notifications_candidate_account_id");
            modelBuilder.Entity<SavedJob>()
                .HasIndex(s => s.CandidateAccountId).HasDatabaseName("ix_saved_jobs_candidate_account_id");
            modelBuilder.Entity<InterviewSession>()
                .HasIndex(s => s.ApplicationId).HasDatabaseName("ix_interview_sessions_application_id");
            // Đường đọc transcript (xem lại buổi thử — ADR-051) quét theo session_id.
            modelBuilder.Entity<Question>()
                .HasIndex(q => q.SessionId).HasDatabaseName("ix_questions_session_id");
            modelBuilder.Entity<Answer>()
                .HasIndex(a => a.SessionId).HasDatabaseName("ix_answers_session_id");
            // Mỗi phiên chỉ có 1 bản đánh giá. Đường chấm lại (RegenerateEvaluationAsync) xoá CỨNG
            // bản cũ trước khi ghi mới (Evaluation không phải ISoftDelete) nên ràng buộc này an toàn.
            // Supabase có 2 index chồng nhau trên cột này (1 unique + 1 thường); ở đây gộp làm 1.
            modelBuilder.Entity<Evaluation>()
                .HasIndex(e => e.SessionId).IsUnique().HasDatabaseName("ix_evaluations_session_id");
            modelBuilder.Entity<Evaluation>()
                .HasIndex(e => e.ApplicationId).HasDatabaseName("ix_evaluations_application_id");
            modelBuilder.Entity<HrReview>()
                .HasIndex(h => h.EvaluationId).HasDatabaseName("ix_hr_reviews_evaluation_id");
            modelBuilder.Entity<InterviewCode>()
                .HasIndex(c => c.ApplicationId).HasDatabaseName("ix_interview_codes_application_id");
            modelBuilder.Entity<InterviewBooking>()
                .HasIndex(b => b.ApplicationId).HasDatabaseName("ix_interview_bookings_application_id");
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => j.CreatedByUserId).HasDatabaseName("ix_job_postings_created_by_user_id");
            modelBuilder.Entity<InterviewInvite>()
                .HasIndex(i => i.ApplicationId).HasDatabaseName("ix_interview_invites_application_id");
            modelBuilder.Entity<InterviewInvite>()
                .HasIndex(i => i.TokenHash).HasDatabaseName("ix_interview_invites_token_hash");

            // Chống đặt trùng: tối đa 1 booking 'scheduled' / (hồ sơ, vòng).
            modelBuilder.Entity<InterviewBooking>()
                .HasIndex(b => new { b.ApplicationId, b.RoundNumber })
                .IsUnique()
                .HasFilter("status = 'scheduled'")
                .HasDatabaseName("ux_interview_bookings_app_round_scheduled");

            ConfigureRelationships(modelBuilder);
            ConfigureOperationalIndexes(modelBuilder);
        }

        /// <summary>
        /// Khoá ngoại cho toàn bộ schema. Trước đây lớp này được áp TAY bằng SQL thẳng lên
        /// Supabase nên không nằm trong migration — khi ADR-055 dựng lại DB production từ
        /// migration EF trên thư mục trống thì toàn bộ 29 khoá ngoại biến mất im lặng.
        /// Khai ở đây để mọi lần dựng DB sau này đều có, không thể mất lần nữa.
        ///
        /// Cố tình KHÔNG dùng navigation property: entity giữ nguyên kiểu Guid trần, tầng
        /// service vẫn join thủ công như cũ — chỉ DB được thêm ràng buộc, không dòng query nào đổi.
        ///
        /// Quy ước Delete behavior:
        ///   Cascade  — quan hệ cha–con thật, xoá cha thì con vô nghĩa (câu hỏi của phiên đã xoá).
        ///   NoAction — tham chiếu cần giữ; chặn xoá nhầm dữ liệu tuyển dụng/kiểm toán.
        /// Hệ thống dùng soft delete (SaveChangesAsync đổi Delete → Modified) nên cascade hầu
        /// như không kích hoạt trong vận hành thường — nó là lưới an toàn cho xoá cứng.
        /// </summary>
        private static void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // ===== users =====
            modelBuilder.Entity<RefreshToken>()
                .HasOne<User>().WithMany().HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AuditLog>()
                .HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<HrReview>()
                .HasOne<User>().WithMany().HasForeignKey(h => h.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<InterviewCode>()
                .HasOne<User>().WithMany().HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<JobPosting>()
                .HasOne<User>().WithMany().HasForeignKey(j => j.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<JobPosting>()
                .HasOne<User>().WithMany().HasForeignKey(j => j.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PlaybookDocument>()
                .HasOne<User>().WithMany().HasForeignKey(p => p.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            // account_requests là hồ sơ kiểm toán "ai xin tạo tài khoản, ai duyệt" (ADR-041):
            // không được biến mất theo người dùng → NoAction cho cả 3 tham chiếu.
            modelBuilder.Entity<AccountRequest>()
                .HasOne<User>().WithMany().HasForeignKey(r => r.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AccountRequest>()
                .HasOne<User>().WithMany().HasForeignKey(r => r.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AccountRequest>()
                .HasOne<User>().WithMany().HasForeignKey(r => r.CreatedUserId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Notification>()
                .HasOne<User>().WithMany().HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== candidate_accounts =====
            modelBuilder.Entity<CandidateRefreshToken>()
                .HasOne<CandidateAccount>().WithMany().HasForeignKey(t => t.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasOne<CandidateAccount>().WithMany().HasForeignKey(a => a.CandidateAccountId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SavedJob>()
                .HasOne<CandidateAccount>().WithMany().HasForeignKey(s => s.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Notification>()
                .HasOne<CandidateAccount>().WithMany().HasForeignKey(n => n.CandidateAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== job_postings =====
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(a => a.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AvailabilitySlot>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(s => s.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InterviewRoundConfig>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(r => r.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OnlineTestQuestion>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(q => q.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CvJdAnalysis>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(c => c.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SavedJob>()
                .HasOne<JobPosting>().WithMany().HasForeignKey(s => s.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== applications =====
            modelBuilder.Entity<InterviewSession>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(s => s.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Evaluation>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<InterviewCode>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(c => c.ApplicationId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<InterviewBooking>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(b => b.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OnlineTestSubmission>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(s => s.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InterviewInvite>()
                .HasOne<ARI.Domain.Entities.Application>().WithMany().HasForeignKey(i => i.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== interview_sessions =====
            modelBuilder.Entity<Question>()
                .HasOne<InterviewSession>().WithMany().HasForeignKey(q => q.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Answer>()
                .HasOne<InterviewSession>().WithMany().HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CheatDetectionSignal>()
                .HasOne<InterviewSession>().WithMany().HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MustAskTracking>()
                .HasOne<InterviewSession>().WithMany().HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Evaluation>()
                .HasOne<InterviewSession>().WithMany().HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== questions / evaluations / slots / playbook =====
            modelBuilder.Entity<Answer>()
                .HasOne<Question>().WithMany().HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MustAskTracking>()
                .HasOne<Question>().WithMany().HasForeignKey(m => m.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<HrReview>()
                .HasOne<Evaluation>().WithMany().HasForeignKey(h => h.EvaluationId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<InterviewBooking>()
                .HasOne<AvailabilitySlot>().WithMany().HasForeignKey(b => b.AvailabilitySlotId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MustAskTracking>()
                .HasOne<PlaybookDocument>().WithMany().HasForeignKey(m => m.PlaybookDocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Đổi lịch trỏ về booking cũ (tự tham chiếu) — giữ vết, không xoá theo.
            modelBuilder.Entity<InterviewBooking>()
                .HasOne<InterviewBooking>().WithMany().HasForeignKey(b => b.RescheduledFromId)
                .OnDelete(DeleteBehavior.NoAction);

            // Quan hệ DUY NHẤT đã tồn tại sẵn (sinh ra do navigation property có từ trước).
            // Khai tường minh để EF dùng lại đúng quan hệ đó thay vì tạo thêm quan hệ bóng.
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasOne(a => a.CvJdAnalysis).WithMany().HasForeignKey(a => a.CvJdAnalysisId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== CỐ TÌNH KHÔNG ĐẶT KHOÁ NGOẠI =====
            // audit_logs.entity_id        — đa hình theo entity_type (trỏ nhiều bảng khác nhau).
            // playbook_documents.scope_ref_id — đa hình theo scope (org | job_posting | round).
            // document_chunks.source_id   — đa hình theo source_type (jd | cv | playbook).
            // account_requests.batch_id   — id gom nhóm, KHÔNG có bảng đích nào tồn tại.
            // questions.playbook_chunk_id — rag-service xoá cứng chunk mỗi lần nạp lại tài liệu
            //   (DELETE FROM document_chunks WHERE source_type=$1 AND source_id=$2), nên khoá ngoại
            //   ở đây sẽ chặn đứng việc nạp lại. Cột này hiện cũng chưa được dùng ở đâu trong code.
        }

        /// <summary>
        /// Index vận hành — cũng thuộc lớp áp tay đã mất cùng khoá ngoại ở ADR-055.
        /// Không khai lại index cho cột khoá ngoại: EF tự tạo cho mỗi quan hệ ở trên.
        /// </summary>
        private static void ConfigureOperationalIndexes(ModelBuilder modelBuilder)
        {
            // --- Lọc soft delete: mọi truy vấn đều kèm "deleted_at IS NULL" (query filter toàn cục).
            modelBuilder.Entity<ARI.Domain.Entities.Application>()
                .HasIndex(a => a.DeletedAt).HasDatabaseName("idx_applications_active_deleted");
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => j.DeletedAt).HasDatabaseName("idx_job_postings_active_deleted");
            modelBuilder.Entity<User>()
                .HasIndex(u => u.DeletedAt).HasDatabaseName("idx_users_active_deleted");
            modelBuilder.Entity<PlaybookDocument>()
                .HasIndex(p => p.DeletedAt).HasDatabaseName("idx_playbook_docs_active_deleted");

            // === RÀNG BUỘC DUY NHẤT — cũng thuộc lớp áp tay đã mất ở ADR-055 ===
            // Không có chúng, DB cho phép trùng email tài khoản, trùng mã phỏng vấn 6 ký tự,
            // trùng khoá cấu hình hệ thống và trùng token đăng nhập. Khôi phục ĐÚNG như Supabase
            // (không kèm filter deleted_at) — đây là hành vi ứng dụng đã chạy suốt nhiều tháng:
            // email của tài khoản đã xoá mềm sẽ không dùng lại được, và đó là chủ ý cho hệ nội bộ.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");
            modelBuilder.Entity<CandidateAccount>()
                .HasIndex(c => c.Email).IsUnique().HasDatabaseName("ux_candidate_accounts_email");
            modelBuilder.Entity<SystemSetting>()
                .HasIndex(s => s.Key).IsUnique().HasDatabaseName("ux_system_settings_key");
            modelBuilder.Entity<InterviewCode>()
                .HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_interview_codes_code");
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
            modelBuilder.Entity<CandidateRefreshToken>()
                .HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_candidate_refresh_tokens_token_hash");
            modelBuilder.Entity<MagicLink>()
                .HasIndex(m => m.TokenHash).IsUnique().HasDatabaseName("ux_magic_links_token_hash");

            // --- Job Board: bộ lọc công khai + tìm theo kỹ năng.
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => new { j.IsPublicListing, j.Status })
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("idx_job_postings_public_active");
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => new { j.IsPublicListing, j.Status, j.WorkMode, j.ExperienceLevel, j.JobCategory })
                .HasFilter("deleted_at IS NULL AND is_public_listing = true")
                .HasDatabaseName("idx_job_postings_public_filters");
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => new { j.SalaryMin, j.SalaryMax })
                .HasFilter("deleted_at IS NULL AND salary_is_negotiable = false")
                .HasDatabaseName("idx_job_postings_salary");
            modelBuilder.Entity<JobPosting>()
                .HasIndex(j => j.Skills)
                .HasMethod("gin")
                .HasDatabaseName("idx_job_postings_skills");

            // --- Tác vụ nền quét mã hết hạn (tra mã đã có ux_interview_codes_code ở trên).
            modelBuilder.Entity<InterviewCode>()
                .HasIndex(c => c.ExpiresAt)
                .HasFilter("used_at IS NULL")
                .HasDatabaseName("idx_interview_codes_expires_at");

            // --- Tách buổi thử / buổi thật (ADR-051 lọc session_type ở nhiều đường đọc).
            modelBuilder.Entity<InterviewSession>()
                .HasIndex(s => s.SessionType).HasDatabaseName("idx_interview_sessions_session_type");
            modelBuilder.Entity<Evaluation>()
                .HasIndex(e => e.SessionType).HasDatabaseName("idx_evaluations_session_type");

            // --- Playbook theo phạm vi (đa hình, không có khoá ngoại nên phải tự khai index).
            modelBuilder.Entity<PlaybookDocument>()
                .HasIndex(p => new { p.Scope, p.ScopeRefId }).HasDatabaseName("idx_playbook_documents_scope");

            // --- Job retry webhook nền.
            modelBuilder.Entity<WebhookDelivery>()
                .HasIndex(w => w.NextRetryAt)
                .HasFilter("delivered_at IS NULL")
                .HasDatabaseName("idx_webhook_deliveries_next_retry");

            // --- RAG: lọc chunk theo nguồn (đa hình) + tìm kiếm vector.
            modelBuilder.Entity<DocumentChunk>()
                .HasIndex(c => new { c.SourceType, c.SourceId }).HasDatabaseName("idx_document_chunks_source");

            // Index ANN cho tìm kiếm ngữ nghĩa. Supabase đang dùng ivfflat, ở đây CỐ Ý đổi sang
            // HNSW: ivfflat phải "học" phân cụm từ dữ liệu lúc tạo, mà DB production hiện trắng
            // → tạo bây giờ sẽ ra index phân cụm rác, phải REINDEX lại sau khi có dữ liệu.
            // HNSW xây dựng tăng dần theo từng lần chèn, không cần huấn luyện lại, recall tốt hơn
            // ở cùng mức tốc độ. Đây là thời điểm duy nhất đổi được mà không tốn gì.
            modelBuilder.Entity<DocumentChunk>()
                .HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasDatabaseName("idx_document_chunks_embedding_hnsw");
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                // Auto timestamps
                var clrType = entry.Entity.GetType();
                var updatedAtProp = clrType.GetProperty("UpdatedAt");
                if (updatedAtProp != null && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    updatedAtProp.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                }

                var createdAtProp = clrType.GetProperty("CreatedAt");
                if (createdAtProp != null && entry.State == EntityState.Added)
                {
                    createdAtProp.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                }

                // Handle Soft Delete
                if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDeleteEntity)
                {
                    entry.State = EntityState.Modified;
                    softDeleteEntity.DeletedAt = DateTimeOffset.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        private string GetSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            var startUnderscore = input.StartsWith("_");
            if (startUnderscore) input = input.Substring(1);

            var result = string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
            return startUnderscore ? "_" + result : result;
        }
    }
}
