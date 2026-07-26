using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Dev.SeedPractice
{
    /// <summary>
    /// DEV-ONLY (ADR-050): tạo sẵn một hồ sơ ĐỦ ĐIỀU KIỆN phỏng vấn thử — candidate +
    /// job + application (<c>Status="interview"</c> → <c>PracticeEligible</c>) + booking/slot —
    /// để test luồng practice mà không phải dựng lại toàn phễu tuyển dụng (apply → duyệt CV →
    /// xếp lịch). Chỉ được gọi qua <c>DevController</c> khi <c>IsDevelopment()</c> (prod trả 404).
    /// Idempotent theo marker email/tiêu đề; <c>Fresh=true</c> tạo application mới. Kết hợp
    /// <c>Interview:PracticeAttemptsPerRound=0</c> để test lặp vô hạn trên cùng hồ sơ.
    /// </summary>
    public record SeedPracticeCommand(bool Fresh = false) : IRequest<Result<SeedPracticeResult>>;

    public record SeedPracticeResult(
        string CandidateEmail,
        string Password,
        Guid ApplicationId,
        string PracticeUrl,
        bool ReusedApplication);

    public class SeedPracticeCommandHandler : IRequestHandler<SeedPracticeCommand, Result<SeedPracticeResult>>
    {
        // Marker cố định để idempotent — mọi lần gọi tái dùng đúng candidate/job này.
        private const string SeedEmail = "practice.dev@arisp.local";
        private const string SeedPassword = "Practice123!";
        private const string SeedJobTitle = "[DEV] Practice Sandbox";
        private const int SeedRound = 1;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public SeedPracticeCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<SeedPracticeResult>> Handle(SeedPracticeCommand request, CancellationToken ct)
        {
            // 1) Candidate — đăng nhập được NGAY (EmailVerified=true, bỏ email xác minh của
            //    RegisterCandidateCommand). Chỉ hash lại/tạo khi chưa tồn tại.
            var candidate = (await _unitOfWork.Repository<CandidateAccount>()
                .FindAsync(c => c.Email == SeedEmail, ct)).FirstOrDefault();
            if (candidate == null)
            {
                candidate = new CandidateAccount
                {
                    Email = SeedEmail,
                    PasswordHash = _passwordHasher.Hash(SeedPassword),
                    FullName = "Practice Dev",
                    EmailVerified = true,
                    IsActive = true
                };
                await _unitOfWork.Repository<CandidateAccount>().AddAsync(candidate, ct);
            }

            // 2) Chủ tin (JobPosting.CreatedByUserId là FK tới users) — tái dùng 1 staff bất kỳ;
            //    DB dev luôn có SuperAdmin pre-provisioning. Nếu rỗng hẳn thì tạo owner tối thiểu
            //    (không có PasswordHash → KHÔNG đăng nhập được, chỉ để giữ FK hợp lệ).
            var owner = (await _unitOfWork.Repository<User>().FindAsync(u => true, ct)).FirstOrDefault();
            if (owner == null)
            {
                owner = new User
                {
                    Email = "dev.seed.owner@arisp.local",
                    Role = "hr_admin",
                    FullName = "Seed Owner",
                    IsActive = true
                };
                await _unitOfWork.Repository<User>().AddAsync(owner, ct);
                await _unitOfWork.SaveChangesAsync(ct); // owner trước để FK job hợp lệ
            }

            // 3) Job — JD tiếng Việt (khớp DetectedLanguage) để RAG có ngữ cảnh; active để hiển thị đúng.
            var job = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => j.Title == SeedJobTitle, ct)).FirstOrDefault();
            if (job == null)
            {
                job = new JobPosting
                {
                    CreatedByUserId = owner.Id,
                    Title = SeedJobTitle,
                    JobDescription =
                        "Tuyển Lập trình viên Backend (.NET). Yêu cầu: C#, ASP.NET Core, Entity Framework Core, "
                        + "PostgreSQL, thiết kế REST API, nắm Clean Architecture. Ưu tiên có kinh nghiệm CI/CD, "
                        + "Docker và hệ thống realtime (SignalR). Tham gia xây dựng nền tảng phỏng vấn AI.",
                    DetectedLanguage = "vi",
                    Status = "active",
                    InterviewMode = "remote",
                    Department = "Engineering",
                    Location = "Hà Nội",
                    // DB job_postings.salary_currency là NOT NULL (default 'VND') nhưng entity để
                    // nullable không HasDefaultValue → EF gửi NULL nếu bỏ trống → vi phạm. Đặt tường minh.
                    SalaryCurrency = "VND"
                };
                await _unitOfWork.Repository<JobPosting>().AddAsync(job, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct); // persist candidate + job trước khi tạo application

            // 4) Application đủ điều kiện: Status="interview" là trạng thái sau-xếp-lịch mà
            //    StaffScheduling.AssignSlotCommand tạo ra (screening→interview) → PracticeEligible.
            //    Reuse để test lặp (với PracticeAttemptsPerRound=0), trừ khi Fresh=true.
            ARI.Domain.Entities.Application? app = null;
            var reused = false;
            if (!request.Fresh)
            {
                app = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
                        a => a.JobPostingId == job.Id && a.CandidateAccountId == candidate.Id, ct))
                    .OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                reused = app != null;
            }
            if (app == null)
            {
                app = new ARI.Domain.Entities.Application
                {
                    JobPostingId = job.Id,
                    CandidateAccountId = candidate.Id,
                    CandidateEmail = candidate.Email,
                    CandidateName = candidate.FullName,
                    Status = "interview",
                    Source = "job_board",
                    CvText =
                        "Ứng viên: Practice Dev. Kinh nghiệm: 3 năm .NET/C#, ASP.NET Core, EF Core, PostgreSQL, "
                        + "Redis, Docker. Đã xây REST API + SignalR realtime, dựng CI/CD GitHub Actions. "
                        + "Quan tâm Clean Architecture và hệ thống phỏng vấn AI."
                };
                await _unitOfWork.Repository<ARI.Domain.Entities.Application>().AddAsync(app, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // 5) (Độ thực tế) 1 booking "scheduled" + slot tương lai — mô phỏng đúng trạng thái
            //    sau khi HR xếp lịch, để portal hiện "lịch sắp tới" và chống mọi gate có thể check booking.
            var hasBooking = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == app.Id && b.RoundNumber == SeedRound && b.Status == "scheduled", ct)).Any();
            if (!hasBooking)
            {
                var start = DateTimeOffset.UtcNow.AddDays(1);
                var slot = new AvailabilitySlot
                {
                    JobPostingId = job.Id,
                    RoundNumber = SeedRound,
                    StartTime = start,
                    EndTime = start.AddHours(1),
                    Capacity = 5,
                    BookedCount = 1
                };
                await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _unitOfWork.Repository<InterviewBooking>().AddAsync(new InterviewBooking
                {
                    ApplicationId = app.Id,
                    AvailabilitySlotId = slot.Id,
                    RoundNumber = SeedRound,
                    Status = "scheduled"
                }, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success(new SeedPracticeResult(
                SeedEmail,
                SeedPassword,
                app.Id,
                $"/interview/practice/{app.Id}",
                reused));
        }
    }
}
