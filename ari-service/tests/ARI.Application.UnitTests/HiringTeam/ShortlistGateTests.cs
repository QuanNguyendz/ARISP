using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
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
        => new RequestHmApprovalCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RequestHmApprovalCommand(appId, actor ?? _ownerId, AppRoles.Recruiter), CancellationToken.None);

    private Task<Result<bool>> Decide(
        InMemoryUnitOfWork uow, Guid appId, string decision, string? note = null, Guid? actor = null)
        => new HmDecideApplicationCommandHandler(uow, new RecordingNotificationService(), Svc(uow))
            .Handle(new HmDecideApplicationCommand(appId, decision, note, actor ?? _hmId, AppRoles.HiringManager),
                CancellationToken.None);

    /// <summary>Thông báo "HM đã quyết" gửi cho chủ tin — chỗ Recruiter biết có xếp lịch ngay được không.</summary>
    private Notification OwnerDecisionNotice(InMemoryUnitOfWork uow, Guid appId)
        => Assert.Single(uow.Repo<Notification>().Items,
            n => n.RecipientUserId == _ownerId && n.DedupKey == $"hm_shortlist_decided:{appId}");

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
    public async Task Tin_khong_co_HM_thi_cong_DONG_va_noi_ro_HR_Leader_can_gan_HM()
    {
        // ADR-068: mọi tin luôn có Hiring Manager. Trước đây nhánh "tin chưa gán HM" đẩy thẳng hồ sơ
        // sang `screening` — tức Recruiter gỡ HM khỏi đội là tự mở cổng đang kiểm mình.
        var (uow, _, app) = Seed(withHiringManager: false);

        var res = await Request(uow, app.Id);

        Assert.True(res.IsFailure);
        Assert.Equal(JobAccessErrors.HiringManagerMissing, res.Error);
        Assert.Equal(ApplicationStatuses.CvSubmitted, app.Status);
    }

    [Fact]
    public async Task HM_bi_khoa_thi_cong_DONG_va_noi_ro_HR_Leader_can_chuyen_HM()
    {
        var (uow, _, app) = Seed();
        uow.Repo<User>().Items.Single(u => u.Id == _hmId).IsActive = false;

        var res = await Request(uow, app.Id);

        Assert.True(res.IsFailure);
        Assert.Equal(JobAccessErrors.HiringManagerInactive, res.Error);
        Assert.Equal(ApplicationStatuses.CvSubmitted, app.Status);
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
    public async Task Duyet_KHONG_can_kem_lich_ranh_va_khong_ghi_lich_nao()
    {
        // ADR-067, sửa 2026-09-14: duyệt chỉ là quyết định chuyên môn. Lịch có mặt của HM khai riêng ở
        // màn tin — trước đây nó là điều kiện của việc duyệt, nên HM không duyệt được một hồ sơ nào
        // cho tới khi mở form lịch ra khai, lần nào cũng thế.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Decide(uow, app.Id, HmDecision.Approved);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.Screening, app.Status);
        Assert.Empty(uow.Repo<HiringManagerAvailability>().Items);
    }

    [Fact]
    public async Task Duyet_khi_HM_chua_khai_lich_thi_Recruiter_duoc_bao_ro_con_thieu_gi()
    {
        // Bỏ điều kiện "duyệt kèm lịch" thì hồ sơ có thể về hàng chờ xếp lịch khi vòng 1 chưa có khung
        // nào. Đúng thứ bế tắc ADR-067 muốn xoá — nên Recruiter phải được nói thẳng là đang chờ ai,
        // thay vì mở màn xếp lịch rồi mới bị từ chối.
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        await Decide(uow, app.Id, HmDecision.Approved);

        Assert.Contains("chưa khai lịch", OwnerDecisionNotice(uow, app.Id).Body);
    }

    [Fact]
    public async Task Duyet_khi_HM_da_khai_lich_thi_Recruiter_duoc_bao_xep_trong_lich_do()
    {
        var (uow, job, app) = Seed();
        uow.Seed(new HiringManagerAvailability
        {
            JobPostingId = job.Id, RoundNumber = 1, HiringManagerUserId = _hmId,
            StartTime = DateTimeOffset.UtcNow.AddDays(2), EndTime = DateTimeOffset.UtcNow.AddDays(2).AddHours(4),
        });
        await Request(uow, app.Id);

        await Decide(uow, app.Id, HmDecision.Approved);

        var body = OwnerDecisionNotice(uow, app.Id).Body;
        Assert.Contains("lịch Hiring Manager đã khai", body);
        Assert.DoesNotContain("chưa khai lịch", body);
    }

    [Fact]
    public async Task Vong_1_trac_nghiem_thi_bao_xep_lich_thi_ngay_khong_can_HM()
    {
        // Bài thi trực tuyến không ai ngồi cùng: nhắc Recruiter đi đòi lịch HM ở đây là đẩy họ đi hỏi
        // một thứ không bao giờ cần tới.
        var (uow, job, app) = Seed();
        uow.Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" });
        await Request(uow, app.Id);

        await Decide(uow, app.Id, HmDecision.Approved);

        var body = OwnerDecisionNotice(uow, app.Id).Body;
        Assert.Contains("trắc nghiệm", body);
        Assert.DoesNotContain("chưa khai lịch", body);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Accepting_never_skips_the_gate_from_a_fresh_application(bool withHiringManager)
    {
        // ADR-068 bỏ nhánh "hồ sơ vừa nộp thì duyệt thẳng" — nó chỉ tồn tại cho tin không có HM.
        var (uow, _, app) = Seed(withHiringManager);

        var res = await Svc(uow).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(ApplicationStatuses.CvSubmitted, app.Status);
    }

    [Theory]
    [InlineData(ApplicationStatuses.Screening)]
    [InlineData(ApplicationStatuses.HmReview)]
    public async Task The_generic_status_patch_cannot_enter_or_leave_the_gate(string target)
    {
        // Trước đây `PATCH /applications/{id}/status` cho chủ tin đẩy thẳng `cv_submitted → screening` —
        // bỏ qua cổng Hiring Manager kể cả trên tin có HM, không dấu vết nào.
        var (uow, _, app) = Seed();

        var res = await Svc(uow).UpdateApplicationStatusAsync(app.Id, target, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(ApplicationStatuses.CvSubmitted, app.Status);
    }

    [Fact]
    public async Task The_generic_status_patch_cannot_push_an_application_out_of_hm_review()
    {
        var (uow, _, app) = Seed();
        await Request(uow, app.Id);

        var res = await Svc(uow).UpdateApplicationStatusAsync(app.Id, ApplicationStatuses.Screening, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(ApplicationStatuses.HmReview, app.Status);
    }
}
