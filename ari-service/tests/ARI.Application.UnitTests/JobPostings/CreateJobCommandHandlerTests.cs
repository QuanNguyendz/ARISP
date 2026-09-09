using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.CreateJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Tạo tin tuyển dụng (<see cref="CreateJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CreateJob" (UTCID01–12): validate request (title/JD/độ dài/mode/location/salary/rounds), tồn tại người tạo,
/// happy path (draft + trim + ngôn ngữ phát hiện + round ascending), RAG lỗi không chặn, và mặc định ngôn ngữ/tiền tệ/vacancies.
///
/// Kèm theo là bộ ràng buộc ADR-063 ở cuối file: mọi tin phải bắt nguồn từ một phiếu yêu cầu tuyển
/// dụng đã duyệt, nên mọi ca đường-thành-công đều phải đi qua <see cref="WithApprovedRequest"/>.
/// </summary>
public class CreateJobCommandHandlerTests
{
    /// <summary>
    /// Dựng sẵn phiếu yêu cầu tuyển dụng đã duyệt và gắn vào request (ADR-063). Từ ADR-063 mọi tin
    /// đều phải bắt nguồn từ một phiếu đã được HR Leader duyệt, nên mọi test đường-thành-công đều
    /// phải đi qua bước này.
    /// </summary>
    private static void WithApprovedRequest(InMemoryUnitOfWork uow, CreateJobPostingRequest req, Guid recruiterId)
    {
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), recruiterId);
        uow.Seed(rr);
        req.RecruitmentRequestId = rr.Id;
    }

    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, CreateJobPostingRequest req, Guid userId, RecordingRagIngestionService? rag = null)
        => Run(uow, rag ?? new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, RecordingRagIngestionService rag, RecordingNotificationService notif,
        CreateJobPostingRequest req, Guid userId)
        => new CreateJobCommandHandler(uow, rag, notif, NullLogger<CreateJobCommandHandler>.Instance)
            .Handle(new CreateJobCommand(req, userId), CancellationToken.None);

    private static CreateJobPostingRequest Req() => JobPostingData.Request();

    // UTCID01 — hợp lệ + người tạo tồn tại → draft, trim, ngôn ngữ phát hiện, round ascending, save 2 lần
    [Fact]
    public async Task UTCID01_Valid_creates_draft()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = Req();
        req.Title = "  Backend Developer  ";
        req.RoundConfigs = new() { JobPostingData.Round(2, "technical"), JobPostingData.Round(1, "screening") };
        WithApprovedRequest(uow, req, userId);

        var res = await Run(uow, req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("draft", job.Status);
        Assert.Equal("Backend Developer", job.Title);
        Assert.Equal(userId, job.CreatedByUserId);
        Assert.Equal("vi", job.DetectedLanguage);
        Assert.Equal(2, uow.Repo<InterviewRoundConfig>().Items.Count);
        Assert.Equal(1, uow.Repo<InterviewRoundConfig>().Items.First().RoundNumber);   // ascending
        Assert.Equal(2, res.Value.RoundConfigs.Count);
        Assert.Equal(2, uow.SaveChangesCount);                                          // job + rounds
    }

    // UTCID02 — Title trống
    [Fact]
    public async Task UTCID02_Title_required()
    {
        var req = Req(); req.Title = " ";
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, req, Guid.NewGuid());
        Assert.Equal("Title is required.", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    // UTCID03 — JobDescription trống
    [Fact]
    public async Task UTCID03_JobDescription_required()
    {
        var req = Req(); req.JobDescription = " ";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("JobDescription is required.", res.Error);
    }

    // UTCID04 — Title > 200 ký tự
    [Fact]
    public async Task UTCID04_Title_too_long()
    {
        var req = Req(); req.Title = new string('a', 201);
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("Title cannot exceed 200 characters.", res.Error);
    }

    // UTCID05 — InterviewMode không hợp lệ
    [Fact]
    public async Task UTCID05_Invalid_interview_mode()
    {
        var req = Req(); req.InterviewMode = "hybrid-office";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("InterviewMode must be 'remote', 'onsite', or 'both'.", res.Error);
    }

    // UTCID06 — onsite nhưng không có Location
    [Fact]
    public async Task UTCID06_Onsite_without_location()
    {
        var req = Req(); req.InterviewMode = "onsite"; req.Location = " ";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("Location is required when InterviewMode is not 'remote'.", res.Error);
    }

    // UTCID07 — SalaryMax < SalaryMin
    [Fact]
    public async Task UTCID07_Salary_max_below_min()
    {
        var req = Req(); req.SalaryMin = 30000000; req.SalaryMax = 20000000;
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("SalaryMax cannot be less than SalaryMin.", res.Error);
    }

    // UTCID08 — SalaryIsNegotiable=true nhưng vẫn có salary
    [Fact]
    public async Task UTCID08_Negotiable_with_salary()
    {
        var req = Req(); req.SalaryIsNegotiable = true; req.SalaryMin = 10000000;
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("SalaryMin and SalaryMax must be null when SalaryIsNegotiable is true.", res.Error);
    }

    // UTCID09 — RoundConfigs rỗng
    [Fact]
    public async Task UTCID09_No_rounds()
    {
        var req = Req(); req.RoundConfigs = new();
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("At least one interview round configuration is required.", res.Error);
    }

    // UTCID10 — người tạo không tồn tại → unauthorized
    [Fact]
    public async Task UTCID10_Creator_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, Req(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("User not found for the current token.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    // UTCID11 — RAG ingest ném lỗi → job vẫn được tạo
    [Fact]
    public async Task UTCID11_Rag_failure_still_creates_job()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = Req();
        WithApprovedRequest(uow, req, userId);

        var res = await Run(uow, req, userId, new RecordingRagIngestionService { ThrowOnIngest = true });

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<JobPosting>().Items);
    }

    // UTCID12 — LanguageRequirement/SalaryCurrency trống + Vacancies=0 → mặc định
    [Fact]
    public async Task UTCID12_Defaults_applied()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = Req();
        req.LanguageRequirement = " ";
        req.SalaryCurrency = " ";
        req.Vacancies = 0;
        WithApprovedRequest(uow, req, userId);

        var res = await Run(uow, req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("Tiếng Việt", job.LanguageRequirement);   // detectedLang=vi
        Assert.Equal("VND", job.SalaryCurrency);
        Assert.Null(job.Vacancies);
    }

    // ---------- Tác dụng phụ ----------

    [Fact]
    public async Task Jd_is_ingested_to_rag()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var rag = new RecordingRagIngestionService();
        var req = JobPostingData.Request();
        WithApprovedRequest(uow, req, userId);

        var res = await Run(uow, rag, new RecordingNotificationService(), req, userId);

        var ingest = Assert.Single(rag.Ingested);
        Assert.Equal("jd", ingest.SourceType);
        Assert.Equal(res.Value.Id, ingest.SourceId);
    }

    [Fact]
    public async Task Notifies_creator()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var notif = new RecordingNotificationService();
        var req = JobPostingData.Request();
        WithApprovedRequest(uow, req, userId);

        await Run(uow, new RecordingRagIngestionService(), notif, req, userId);

        Assert.Contains(notif.UserEvents, e => e.UserId == userId && e.EventType == "ReceiveJobPostingUpdate");
    }

    // ---------- Ngôn ngữ vòng ----------

    [Fact]
    public async Task Round_without_language_inherits_detected_language()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, language: null) };
        WithApprovedRequest(uow, req, userId);

        await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        var config = Assert.Single(uow.Repo<InterviewRoundConfig>().Items);
        Assert.Equal(job.DetectedLanguage, config.InterviewLanguage);
    }

    [Fact]
    public async Task Round_explicit_language_is_kept()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, language: "ja") };
        WithApprovedRequest(uow, req, userId);

        await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.Equal("ja", Assert.Single(uow.Repo<InterviewRoundConfig>().Items).InterviewLanguage);
    }

    // ---------- ADR-063: mọi tin phải bắt nguồn từ phiếu yêu cầu tuyển dụng đã duyệt ----------

    [Fact]
    public async Task Khong_co_phieu_thi_khong_tao_duoc_tin()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(),
            JobPostingData.Request(), userId);

        Assert.True(res.IsFailure);
        Assert.Contains("phiếu yêu cầu tuyển dụng", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Theory]
    [InlineData("hr_admin")]
    [InlineData("super_admin")]
    public async Task Admin_cung_khong_tao_duoc_tin_khong_co_phieu(string role)
    {
        // Ràng buộc "mọi tin phải từ phiếu" KHÔNG có ngoại lệ cho quản trị viên: HR Leader tạo tin
        // cũng phải đi qua phiếu do Hiring Manager lập. Nếu admin lách được thì cổng này chỉ còn
        // ràng buộc Recruiter, tức là ràng buộc một nửa.
        var adminId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(adminId, role));

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(),
            JobPostingData.Request(), adminId);

        Assert.True(res.IsFailure);
        Assert.Contains("phiếu yêu cầu tuyển dụng", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task Admin_dung_duoc_tin_tu_phieu_giao_cho_recruiter_khac()
    {
        // Ngược lại: admin KHÔNG bị chặn bởi việc phiếu giao cho người khác — họ vận hành hộ được.
        // Chỉ Recruiter mới phải đúng người được phân công (xem test ngay bên dưới).
        var adminId = Guid.NewGuid();
        var otherRecruiter = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(adminId, "hr_admin"));
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), otherRecruiter);
        uow.Seed(rr);

        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, adminId);

        Assert.True(res.IsSuccess);
        Assert.Equal(rr.Id, Assert.Single(uow.Repo<JobPosting>().Items).RecruitmentRequestId);
    }

    [Fact]
    public async Task Phieu_chua_duyet_thi_khong_tao_duoc_tin()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), userId);
        rr.Status = ARI.Domain.Constants.RecruitmentRequestStatus.Pending;
        uow.Seed(rr);

        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa được HR Leader duyệt", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task Recruiter_khac_khong_nhan_duoc_viec_cua_nguoi_duoc_phan_cong()
    {
        // Không có bước kiểm này thì việc HR Leader chọn người lúc duyệt chỉ còn là gợi ý.
        var assigned = Guid.NewGuid();
        var otherRecruiter = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(otherRecruiter));
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), assigned);
        uow.Seed(rr);

        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, otherRecruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task Mot_phieu_chi_sinh_mot_tin()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), userId);
        uow.Seed(rr);
        uow.Seed(new JobPosting
        {
            CreatedByUserId = userId, Title = "Đã dựng rồi", JobDescription = "x",
            InterviewMode = "remote", Status = "draft", RecruitmentRequestId = rr.Id,
        });

        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Single(uow.Repo<JobPosting>().Items);   // vẫn chỉ có tin cũ
    }

    [Fact]
    public async Task Nguoi_lap_phieu_tu_dong_thanh_hiring_manager_cua_tin()
    {
        // Mắt xích không được để hở: cổng ký duyệt JD của ADR-061 chỉ tồn tại khi tin CÓ người được
        // gán làm HM. Bắt Recruiter nhớ gán tay thì quên một lần là tin ra job board không ai ký.
        var recruiterId = Guid.NewGuid();
        var hmId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(recruiterId));
        var rr = JobPostingData.ApprovedRecruitmentRequest(hmId, recruiterId);
        uow.Seed(rr);

        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, recruiterId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal(rr.Id, job.RecruitmentRequestId);

        var member = Assert.Single(uow.Repo<JobHiringTeamMember>().Items);
        Assert.Equal(hmId, member.UserId);
        Assert.Equal(job.Id, member.JobPostingId);
        Assert.Equal(ARI.Domain.Constants.JobTeamRoles.HiringManager, member.RoleOnJob);
        Assert.True(member.IsPrimary);
    }
}
