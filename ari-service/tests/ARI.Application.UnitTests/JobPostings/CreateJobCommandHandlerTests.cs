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
/// Tạo tin tuyển dụng + cấu hình vòng (UC-46/47, <see cref="CreateJobCommandHandler"/>): validate request,
/// tạo job trạng thái draft của người tạo, sinh InterviewRoundConfig từng vòng (ngôn ngữ vòng kế thừa ngôn
/// ngữ phát hiện từ JD nếu bỏ trống), đẩy JD vào RAG và báo realtime cho người tạo.
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
        InMemoryUnitOfWork uow, RecordingRagIngestionService rag, RecordingNotificationService notif,
        CreateJobPostingRequest req, Guid userId)
        => new CreateJobCommandHandler(uow, rag, notif, NullLogger<CreateJobCommandHandler>.Instance)
            .Handle(new CreateJobCommand(req, userId), CancellationToken.None);

    [Fact]
    public async Task Valid_request_creates_draft_job_with_rounds()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "screening"), JobPostingData.Round(2, "technical") };
        WithApprovedRequest(uow, req, userId);

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("draft", job.Status);
        Assert.Equal(userId, job.CreatedByUserId);
        Assert.Equal(2, uow.Repo<InterviewRoundConfig>().Items.Count);
        Assert.Equal(2, res.Value.RoundConfigs.Count);
    }

    [Fact]
    public async Task Creator_not_found_fails_unauthorized()
    {
        var uow = new InMemoryUnitOfWork(); // không seed user

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(),
            JobPostingData.Request(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

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

    // ---------- Validation (chạy trước, không cần user) ----------

    [Fact]
    public async Task Missing_title_fails_validation()
    {
        var uow = new InMemoryUnitOfWork();
        var req = JobPostingData.Request();
        req.Title = "";

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Title is required", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task No_rounds_fails_validation()
    {
        var req = JobPostingData.Request();
        req.RoundConfigs = new();

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("At least one interview round", res.Error);
    }

    [Fact]
    public async Task Invalid_interview_mode_fails()
    {
        var req = JobPostingData.Request(interviewMode: "hybrid");

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("InterviewMode must be", res.Error);
    }

    [Fact]
    public async Task Onsite_without_location_fails()
    {
        var req = JobPostingData.Request(interviewMode: "onsite"); // Location null

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Location is required", res.Error);
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
