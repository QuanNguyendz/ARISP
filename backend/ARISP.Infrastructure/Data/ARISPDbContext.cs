using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ARISP.Domain.Entities;
using ARISP.Domain.Constants;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Data
{
    public class ARISPDbContext : DbContext
    {
        public ARISPDbContext(DbContextOptions<ARISPDbContext> options)
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
        public DbSet<ARISP.Domain.Entities.Application> Applications => Set<ARISP.Domain.Entities.Application>();
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

            // Custom configuration for pgvector columns
            modelBuilder.Entity<DocumentChunk>()
                .Property(c => c.Embedding)
                .HasColumnType("vector(1536)")
                .HasConversion(
                    v => v == null ? null : $"[{string.Join(",", v)}]",
                    v => v == null ? Array.Empty<float>() : Array.ConvertAll(v.Trim('[', ']').Split(new char[] { ',' }, StringSplitOptions.None), float.Parse)
                );

            // Custom configuration for JSONB columns
            modelBuilder.Entity<JobPosting>()
                .Property(j => j.ScoringRubric)
                .HasColumnType("jsonb");

            modelBuilder.Entity<ARISP.Domain.Entities.Application>()
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

            modelBuilder.Entity<OnlineTestSubmission>()
                .Property(s => s.SelectedAnswers)
                .HasColumnType("jsonb");

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

            // MagicLink.Audience: mặc định "candidate" để các bản ghi cũ (trước khi tách cổng staff) được hiểu là candidate
            modelBuilder.Entity<MagicLink>()
                .Property(m => m.Audience)
                .HasDefaultValue(MagicLinkAudience.Candidate);
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
