using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Dev.SeedInterviewJob
{
    /// <summary>
    /// DEV-ONLY (ADR-053): dựng một tin tuyển dụng ĐỦ 3 VÒNG để test end-to-end
    /// (phỏng vấn thử → phỏng vấn thật ở Kiosk → trắc nghiệm → HR xác nhận → "Đạt").
    ///
    /// Khác <c>SeedPracticeCommand</c> (chỉ đủ điều kiện phỏng vấn thử): ở đây có
    /// <c>InterviewRoundConfig</c> thật cho cả 3 vòng — điều kiện để kiểm chứng luật "chỉ Đạt khi
    /// qua hết vòng", có ngân hàng trắc nghiệm cho vòng 2, có lịch cho vòng 1+3 (bắt buộc để cấp
    /// Interview Code) và **cấp sẵn mã Kiosk vòng 1**. Kèm tài khoản HR **đăng nhập được** để
    /// xác nhận kết quả — seed cũ tạo owner không mật khẩu nên không vào StaffSite được.
    /// Idempotent theo marker email/tiêu đề; <c>Fresh=true</c> tạo hồ sơ ứng tuyển mới.
    /// </summary>
    public record SeedInterviewJobCommand(bool Fresh = false) : IRequest<Result<SeedInterviewJobResult>>;

    public record SeedInterviewJobResult(
        string CandidateEmail,
        string CandidateLogin,
        string StaffEmail,
        string StaffLogin,
        Guid JobPostingId,
        Guid ApplicationId,
        string PracticeUrl,
        string? KioskCode,
        DateTimeOffset? KioskCodeExpiresAt,
        bool ReusedApplication);

    public class SeedInterviewJobCommandHandler
        : IRequestHandler<SeedInterviewJobCommand, Result<SeedInterviewJobResult>>
    {
        // Marker cố định để idempotent.
        private const string CandidateEmail = "practice.dev@arisp.local";   // dùng chung với seed-practice
        private const string CandidatePassword = "Practice123!";
        private const string StaffEmail = "hr.dev@arisp.local";
        private const string StaffPassword = "Hr123456!";
        private const string JobTitle = "[DEV] Kiosk Sandbox";

        private const int RoundScreening = 1;
        private const int RoundOnlineTest = 2;
        private const int RoundTechnical = 3;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IInterviewCodeService _interviewCodeService;

        public SeedInterviewJobCommandHandler(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IInterviewCodeService interviewCodeService)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _interviewCodeService = interviewCodeService;
        }

        public async Task<Result<SeedInterviewJobResult>> Handle(SeedInterviewJobCommand request, CancellationToken ct)
        {
            // 1) Ứng viên — đăng nhập ngay (EmailVerified=true).
            var candidate = (await _unitOfWork.Repository<CandidateAccount>()
                .FindAsync(c => c.Email == CandidateEmail, ct)).FirstOrDefault();
            if (candidate == null)
            {
                candidate = new CandidateAccount
                {
                    Email = CandidateEmail,
                    PasswordHash = _passwordHasher.Hash(CandidatePassword),
                    FullName = "Practice Dev",
                    EmailVerified = true,
                    IsActive = true
                };
                await _unitOfWork.Repository<CandidateAccount>().AddAsync(candidate, ct);
            }

            // 2) Nhân sự HR — CÓ mật khẩu để đăng nhập StaffSite (cấp mã vòng sau + xác nhận kết quả).
            var staff = (await _unitOfWork.Repository<User>()
                .FindAsync(u => u.Email == StaffEmail, ct)).FirstOrDefault();
            if (staff == null)
            {
                staff = new User
                {
                    Email = StaffEmail,
                    PasswordHash = _passwordHasher.Hash(StaffPassword),
                    Role = AppRoles.HrAdmin,
                    FullName = "HR Dev",
                    IsActive = true
                };
                await _unitOfWork.Repository<User>().AddAsync(staff, ct);
            }
            else if (string.IsNullOrEmpty(staff.PasswordHash))
            {
                staff.PasswordHash = _passwordHasher.Hash(StaffPassword);
                _unitOfWork.Repository<User>().Update(staff);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 3) Job — on-site (phỏng vấn thật tại văn phòng), JD tiếng Việt khớp DetectedLanguage.
            var job = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => j.Title == JobTitle, ct)).FirstOrDefault();
            if (job == null)
            {
                job = new JobPosting
                {
                    CreatedByUserId = staff.Id,
                    Title = JobTitle,
                    JobDescription =
                        "Tuyển Lập trình viên Backend (.NET) cho nền tảng tuyển dụng AI. Yêu cầu: C#, ASP.NET Core, "
                        + "Entity Framework Core, PostgreSQL, thiết kế REST API, nắm Clean Architecture. Ưu tiên có "
                        + "kinh nghiệm CI/CD (GitHub Actions), Docker và hệ thống realtime (SignalR). "
                        + "Quy trình: 3 vòng — sơ loại, thi trắc nghiệm, phỏng vấn chuyên môn.",
                    DetectedLanguage = "vi",
                    Status = "active",
                    InterviewMode = "onsite",
                    Department = "Engineering",
                    Location = "Hà Nội",
                    SalaryCurrency = "VND",
                    OnlineTestPassScore = 70,
                    OnlineTestQuestionsPerTest = 10,
                    OnlineTestDurationMinutes = 15
                };
                await _unitOfWork.Repository<JobPosting>().AddAsync(job, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // 4) Cấu hình 3 vòng — mốc để backend biết đâu là vòng CUỐI (chỉ khi đó mới "Đạt").
            var existingRounds = (await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == job.Id, ct)).ToList();
            var wanted = new (int Number, string Type)[]
            {
                (RoundScreening, "screening"),
                (RoundOnlineTest, "online_test"),
                (RoundTechnical, "technical"),
            };
            foreach (var (number, type) in wanted)
            {
                if (existingRounds.Any(r => r.RoundNumber == number)) continue;
                await _unitOfWork.Repository<InterviewRoundConfig>().AddAsync(new InterviewRoundConfig
                {
                    JobPostingId = job.Id,
                    RoundNumber = number,
                    RoundType = type,
                    InterviewLanguage = "vi",
                    InterviewCodeTtlHours = 2,
                    MaxDurationMinutes = 45
                }, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 5) Ngân hàng trắc nghiệm cho vòng 2 (chỉ nạp khi job chưa có câu nào).
            var hasQuestions = (await _unitOfWork.Repository<OnlineTestQuestion>()
                .FindAsync(q => q.JobPostingId == job.Id, ct)).Any();
            if (!hasQuestions)
            {
                foreach (var q in BuildQuestionBank(job.Id))
                    await _unitOfWork.Repository<OnlineTestQuestion>().AddAsync(q, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // 6) Hồ sơ ứng tuyển — "interview" = đã qua CV + đã xếp lịch → đủ điều kiện phỏng vấn thử.
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

            // 7) Lịch cho 2 vòng phỏng vấn (1 và 3) — GenerateCodeAsync BẮT BUỘC có booking "scheduled".
            //    Vòng 2 là trắc nghiệm nên không cần lịch.
            await EnsureBookingAsync(job.Id, app.Id, RoundScreening, ct);
            await EnsureBookingAsync(job.Id, app.Id, RoundTechnical, ct);

            // 8) Mã Kiosk vòng 1 — đi qua đúng service nghiệp vụ (ghi audit + notification như thật).
            //    Tái dùng mã còn hiệu lực nếu có, tránh mỗi lần seed lại đẻ thêm mã rác.
            var nowUtc = DateTimeOffset.UtcNow;
            var activeCode = (await _unitOfWork.Repository<InterviewCode>().FindAsync(
                    c => c.ApplicationId == app.Id && c.RoundNumber == RoundScreening
                         && c.UsedAt == null && c.ExpiresAt > nowUtc, ct))
                .OrderByDescending(c => c.CreatedAt).FirstOrDefault();
            if (activeCode == null)
            {
                var codeResult = await _interviewCodeService.GenerateCodeAsync(app.Id, RoundScreening, staff.Id, ct);
                if (codeResult.IsSuccess) activeCode = codeResult.Value;
            }

            return Result.Success(new SeedInterviewJobResult(
                CandidateEmail,
                CandidatePassword,
                StaffEmail,
                StaffPassword,
                job.Id,
                app.Id,
                $"/interview/practice/{app.Id}?round={RoundScreening}",
                activeCode?.Code,
                activeCode?.ExpiresAt,
                reused));
        }

        /// <summary>Slot tương lai + booking "scheduled" cho một vòng (mô phỏng HR đã gán lịch).</summary>
        private async Task EnsureBookingAsync(Guid jobId, Guid applicationId, int roundNumber, CancellationToken ct)
        {
            var hasBooking = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId && b.RoundNumber == roundNumber && b.Status == "scheduled", ct)).Any();
            if (hasBooking) return;

            var start = DateTimeOffset.UtcNow.AddDays(roundNumber);
            var slot = new AvailabilitySlot
            {
                JobPostingId = jobId,
                RoundNumber = roundNumber,
                StartTime = start,
                EndTime = start.AddHours(1),
                Capacity = 5,
                BookedCount = 1
            };
            await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.Repository<InterviewBooking>().AddAsync(new InterviewBooking
            {
                ApplicationId = applicationId,
                AvailabilitySlotId = slot.Id,
                RoundNumber = roundNumber,
                Status = "scheduled"
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        /// <summary>10 câu mẫu (.NET/EF/SignalR) — pha cả single lẫn multiple để test cả 2 kiểu chấm.</summary>
        private static List<OnlineTestQuestion> BuildQuestionBank(Guid jobId)
        {
            var bank = new (string Text, string[] Options, int[] Correct, string Type)[]
            {
                ("Trong ASP.NET Core, middleware được đăng ký theo thứ tự nào khi xử lý request?",
                    new[] { "Ngược với thứ tự khai báo", "Đúng thứ tự khai báo trong pipeline", "Ngẫu nhiên", "Theo tên middleware" },
                    new[] { 1 }, "single"),
                ("Entity Framework Core: phương thức nào KHÔNG theo dõi thay đổi của entity?",
                    new[] { "AsNoTracking()", "Include()", "SaveChanges()", "Attach()" },
                    new[] { 0 }, "single"),
                ("Những cách nào giúp giảm vấn đề N+1 query trong EF Core? (chọn nhiều)",
                    new[] { "Include/ThenInclude", "Projection bằng Select", "Gọi SaveChanges nhiều lần", "Split query hợp lý" },
                    new[] { 0, 1, 3 }, "multiple"),
                ("SignalR dùng cơ chế nào làm mặc định khi trình duyệt hỗ trợ?",
                    new[] { "Long Polling", "Server-Sent Events", "WebSocket", "gRPC" },
                    new[] { 2 }, "single"),
                ("Trong Clean Architecture, tầng Domain được phép phụ thuộc vào tầng nào?",
                    new[] { "Infrastructure", "Application", "API", "Không phụ thuộc tầng nào" },
                    new[] { 3 }, "single"),
                ("Dependency Injection trong .NET: vòng đời nào tạo MỘT instance cho mỗi HTTP request?",
                    new[] { "Singleton", "Scoped", "Transient", "Pooled" },
                    new[] { 1 }, "single"),
                ("Những phát biểu nào ĐÚNG về async/await trong C#? (chọn nhiều)",
                    new[] { "Giải phóng thread trong lúc chờ I/O", "Luôn tạo thread mới", "Nên tránh async void trừ event handler", "Bắt buộc dùng với Task hoặc ValueTask" },
                    new[] { 0, 2, 3 }, "multiple"),
                ("PostgreSQL: chỉ mục nào phù hợp nhất cho truy vấn full-text search?",
                    new[] { "B-tree", "GIN", "Hash", "BRIN" },
                    new[] { 1 }, "single"),
                ("Docker: lệnh nào tạo image từ Dockerfile?",
                    new[] { "docker run", "docker build", "docker compose up", "docker pull" },
                    new[] { 1 }, "single"),
                ("REST API: mã trạng thái nào phù hợp khi tạo mới tài nguyên thành công?",
                    new[] { "200 OK", "201 Created", "202 Accepted", "204 No Content" },
                    new[] { 1 }, "single"),
            };

            return bank.Select(q => new OnlineTestQuestion
            {
                JobPostingId = jobId,
                QuestionText = q.Text,
                Options = JsonSerializer.Serialize(q.Options),
                QuestionType = q.Type,
                CorrectOptions = JsonSerializer.Serialize(q.Correct),
                CorrectOption = q.Correct[0]
            }).ToList();
        }
    }
}
