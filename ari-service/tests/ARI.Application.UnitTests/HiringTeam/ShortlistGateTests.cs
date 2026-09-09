using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.HiringTeam;
using ARI.Application.Scheduling;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.HiringTeam;

/// <summary>
/// Cổng duyệt shortlist của Hiring Manager (ADR-061, 3a).
///
/// Bất biến quan trọng nhất: tính nguyên tử của ADR-059 phải còn nguyên — hồ sơ chỉ rời cổng khi
/// cổng MỞ, và bước xếp lịch (nơi thư mời thật sự được gửi) vẫn do chủ tin thực hiện.
/// </summary>
public class ShortlistGateTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app) Seed(
        bool withHiringManager = true, string status = "cv_submitted")
    {
        var job = new JobPosting
        {
            Id = Guid.NewGuid(),
            Title = "Backend Developer",
            JobDescription = "JD",
            Status = "active",
            CreatedByUserId = _ownerId,
        };
        var app = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            JobPostingId = job.Id,
            CandidateEmail = "cand@example.io",
            CandidateName = "Nguyen Van A",
            Status = status,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new User { Id = _ownerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true });

        if (withHiringManager)
        {
            uow.Seed(new User { Id = _hmId, Email = "hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });
            uow.Seed(new JobHiringTeamMember
            {
                JobPostingId = job.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
                IsPrimary = true, AddedByUserId = _ownerId,
            });
        }

        return (uow, job, app);
    }

    private Task<Result<bool>> Request(InMemoryUnitOfWork uow, Guid appId, Guid? actor = null)
        => new RequestHmApprovalCommandHandler(uow, new RecordingNotificationService(), Svc(uow))
            .Handle(new RequestHmApprovalCommand(appId, actor ?? _ownerId, AppRoles.Recruiter), CancellationToken.None);

    /// <summary>Khung giờ rảnh mặc định để lệnh duyệt của HM đi qua được (ADR-067).</summary>
    private static IReadOnlyList<HmAvailabilityWindowInput> Windows() => new[]
    {
        new HmAvailabilityWindowInput(
            DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddHours(4), null),
    };

    private Task<Result<bool>> Decide(
        InMemoryUnitOfWork uow, Guid appId, string decision, string? note = null, Guid? actor = null,
        IReadOnlyList<HmAvailabilityWindowInput>? availabilities = null)
        => new HmDecideApplicationCommandHandler(uow, new RecordingNotificationService(), Svc(uow))
            .Handle(new HmDecideApplicationCommand(
                    appId, decision, note, actor ?? _hmId, AppRoles.HiringManager,
                    availabilities ?? Windows()),
                CancellationToken.None);

    private static Task<Result<bool>> Bypass(InMemoryUnitOfWork uow, Guid appId, string? reason, string role = AppRoles.HrAdmin)
        => new BypassHmApprovalCommandHandler(uow, new RecordingNotificationService(), Svc(uow))
            .Handle(new BypassHmApprovalCommand(appId, reason, Guid.NewGuid(), role), CancellationToken.None);

    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    // ===== Gửi duyệt =====

    [Fact]
    public async Task Owner_sends_the_application_to_the_hiring_manager()
    {
        var (uow, _, app) = Seed();

        var res = await Request(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.HmReview, app.Status);
        Assert.Equal(HmDecision.Pending, app.HmDecision);
        Assert.Contains(uow.Repo<Notification>().Items, n => n.RecipientUserId == _hmId);
    }

    [Fact]
    public async Task Duyet_ho_so_tren_tin_khong_co_HM_thi_di_thang_sang_cho_xep_lich()
    {
        // Tin chưa gán Hiring Manager thì KHÔNG có cổng nào để chờ (ADR-061: cổng suy ra từ việc có
        // người được gán). Trước ADR-067 chỗ này trả lỗi, nghĩa là nút "Duyệt hồ sơ" chết cứng trên
        // mọi tin cũ — hồ sơ không có đường nào đi tiếp.
        var (uow, _, app) = Seed(withHiringManager: false);

        var res = await Request(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
    }

    [Fact]
    public async Task A_stranger_cannot_send_someone_elses_application_for_approval()
    {
        var (uow, _, app) = Seed();

        var res = await Request(uow, app.Id, actor: Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ===== Quyết định =====

    [Fact]
    public async Task HM_duyet_thi_ho_so_ve_thang_hang_cho_xep_lich_cua_Recruiter()
    {
        // ADR-067: duyệt xong là hồ sơ ĐI TIẾP ngay. Trước đó nó nằm lại `hm_review` chờ Recruiter
        // bấm thêm một nút "duyệt" nữa — hai lần duyệt cho một quyết định, và Recruiter không có
        // tín hiệu nào nói đã tới lượt mình.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Approved);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
        Assert.Equal(HmDecision.Approved, app.HmDecision);
        Assert.Equal(_hmId, app.HmDecisionByUserId);
    }

    [Fact]
    public async Task HM_duyet_thi_khung_gio_ranh_duoc_luu_theo_tin_va_vong()
    {
        var (uow, job, app) = Seed();
        await Request(uow, app.Id);

        await Decide(uow, app.Id, HmDecision.Approved);

        var windows = uow.Repo<HiringManagerAvailability>().Items;
        Assert.Single(windows);
        Assert.Equal(job.Id, windows[0].JobPostingId);
        Assert.Equal(1, windows[0].RoundNumber);
        Assert.Equal(_hmId, windows[0].HiringManagerUserId);
    }

    [Fact]
    public async Task Duyet_ma_khong_co_khung_gio_nao_thi_bi_chan()
    {
        // Duyệt mà không có giờ nào để xếp thì hồ sơ rơi vào hàng chờ rồi đứng im: Recruiter mở ra
        // thấy một danh sách không thao tác được và không biết phải hỏi ai.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Approved,
            availabilities: Array.Empty<HmAvailabilityWindowInput>());

        Assert.True(res.IsFailure);
        Assert.Contains("khung giờ", res.Error);
        Assert.Equal(ApplicationStatuses.HmReview, app.Status);
        Assert.Equal(HmDecision.Pending, app.HmDecision);
    }

    [Fact]
    public async Task Duyet_nguoi_thu_hai_khong_phai_khai_lai_lich()
    {
        // Khung giờ đã khai còn hiệu lực thì lượt duyệt sau không cần gửi kèm gì — bắt khai lại mỗi
        // hồ sơ là biến một đợt duyệt mười người thành mười lần nhập lịch giống hệt nhau.
        var (uow, job, app) = Seed();
        await Request(uow, app.Id);
        await Decide(uow, app.Id, HmDecision.Approved);

        var second = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id,
            CandidateEmail = "cand2@example.io", CandidateName = "Tran Thi B", Status = "cv_submitted",
        };
        uow.Seed(second);
        await Request(uow, second.Id);

        var res = await Decide(uow, second.Id, HmDecision.Approved,
            availabilities: Array.Empty<HmAvailabilityWindowInput>());

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.Screening, second.Status);
    }

    [Fact]
    public async Task Rejecting_requires_a_reason()
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Rejected);

        Assert.True(res.IsFailure);
        Assert.Equal(HmDecision.Pending, app.HmDecision);
    }

    [Fact]
    public async Task Rejecting_closes_the_application_through_the_existing_reject_path()
    {
        // Dùng lại đường loại hồ sơ sẵn có (thư cảm ơn, huỷ lịch) thay vì viết nhánh đóng thứ hai.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Rejected, note: "Chưa đủ kinh nghiệm mảng thanh toán");

        Assert.True(res.IsSuccess);
        Assert.Equal(HmDecision.Rejected, app.HmDecision);
        // Bị loại khi còn ở giai đoạn CV → cv_rejected, không phải not_pass.
        Assert.Equal(ApplicationStatuses.CvRejected, app.Status);
    }

    [Fact]
    public async Task Only_the_assigned_hiring_manager_may_decide()
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Approved, actor: Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal(HmDecision.Pending, app.HmDecision);
    }

    // ===== Vượt cổng =====

    [Fact]
    public async Task Bypass_requires_an_admin_and_a_substantial_reason()
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var byRecruiter = await Bypass(uow, app.Id, "Hiring Manager đang nghỉ phép", AppRoles.Recruiter);
        Assert.True(byRecruiter.IsFailure);

        var tooShort = await Bypass(uow, app.Id, "gấp");
        Assert.True(tooShort.IsFailure);

        Assert.Equal(HmDecision.Pending, app.HmDecision);
    }

    [Fact]
    public async Task Bypass_records_an_audit_trail_and_tells_the_hiring_manager()
    {
        // Vượt cổng IM LẶNG mới là thất bại quản trị; vượt cổng có dấu vết là nghiệp vụ bình thường.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Bypass(uow, app.Id, "Hiring Manager nghỉ phép dài ngày, ứng viên cần trả lời gấp");

        Assert.True(res.IsSuccess);
        Assert.Equal(HmDecision.Bypassed, app.HmDecision);
        // Vượt cổng cũng phải ĐẨY hồ sơ đi tiếp (ADR-067) — mở cửa rồi để hồ sơ đứng nguyên tại chỗ
        // là đúng thứ bế tắc mà thao tác này sinh ra để gỡ.
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hm_shortlist_bypassed");
        Assert.Contains(uow.Repo<Notification>().Items,
            n => n.RecipientUserId == _hmId && n.DedupKey!.StartsWith("hm_shortlist_bypassed:"));
    }

    // ===== Ảnh hưởng tới bước xếp lịch (ADR-059 phải còn nguyên) =====

    [Fact]
    public async Task Accepting_is_blocked_while_the_gate_is_closed()
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Svc(uow).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Hiring Manager", res.Error);
        Assert.Equal(ApplicationStatuses.HmReview, app.Status);
    }

    [Theory]
    [InlineData(HmDecision.Approved)]
    [InlineData(HmDecision.Bypassed)]
    public async Task Accepting_succeeds_once_the_gate_is_open(string openDecision)
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);
        app.HmDecision = openDecision;

        var res = await Svc(uow).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        // Được nhấc khỏi cổng vào giai đoạn xếp lịch — nếu không, hồ sơ mắc ở hm_review vĩnh viễn.
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
    }

    [Fact]
    public async Task Jobs_without_a_hiring_manager_keep_the_old_flow_untouched()
    {
        // Đây là đường mặc định cho toàn bộ dữ liệu cũ: không gán HM thì không có cổng nào.
        var (uow, _, app) = Seed(withHiringManager: false);

        var res = await Svc(uow).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
    }
}
