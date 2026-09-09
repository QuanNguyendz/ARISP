using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.RecruitmentRequests;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.RecruitmentRequests;

/// <summary>
/// Thu hồi phê duyệt phiếu yêu cầu tuyển dụng (ADR-066).
///
/// Nhu cầu tuyển đổi sau khi HR Leader đã duyệt là chuyện thường — đội đổi yêu cầu, hoặc chỉ tiêu bị
/// cắt. Trước đây phiếu duyệt xong là đóng băng, nên cách duy nhất là lập phiếu mới và bỏ phiếu cũ
/// nằm lại vĩnh viễn ở trạng thái "sẵn sàng dựng tin".
///
/// Ranh giới quan trọng nhất được khoá ở đây: <b>phiếu đã dựng thành tin thì không thu hồi được</b> —
/// lúc đó TIN mới là nguồn sự thật.
/// </summary>
public class RecruitmentRequestRevokeTests
{
    private readonly Guid _hmId = Guid.NewGuid();
    private readonly Guid _hrLeaderId = Guid.NewGuid();
    private readonly Guid _recruiterId = Guid.NewGuid();
    private readonly Guid _otherHmId = Guid.NewGuid();

    private static readonly Department Engineering = new() { Id = Guid.NewGuid(), Name = "Engineering" };

    private const string GoodReason = "Đội đổi hướng, cần tuyển vị trí khác";

    private InMemoryUnitOfWork Seed()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Engineering);
        uow.Seed(
            new User { Id = _hmId, Email = "hm@x.io", Role = RoleNames.HiringManager, FullName = "HM",
                       IsActive = true, DepartmentId = Engineering.Id },
            new User { Id = _otherHmId, Email = "hm2@x.io", Role = RoleNames.HiringManager, FullName = "HM khác",
                       IsActive = true, DepartmentId = Engineering.Id },
            new User { Id = _hrLeaderId, Email = "hr@x.io", Role = RoleNames.HrAdmin, FullName = "HR Leader", IsActive = true },
            new User { Id = _recruiterId, Email = "rec@x.io", Role = RoleNames.Recruiter, FullName = "Recruiter", IsActive = true });
        return uow;
    }

    /// <summary>Một phiếu ĐÃ DUYỆT, đã phân công — trạng thái xuất phát của mọi ca ở đây.</summary>
    private RecruitmentRequest SeedApproved(InMemoryUnitOfWork uow, string status = RecruitmentRequestStatus.Approved)
    {
        var req = new RecruitmentRequest
        {
            RequestedByUserId = _hmId,
            Title = "Backend Developer",
            DepartmentId = Engineering.Id,
            Headcount = 2,
            Priority = RecruitmentPriority.High,
            Status = status,
            AssignedRecruiterId = _recruiterId,
            ReviewedByUserId = _hrLeaderId,
            ReviewedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ReviewReason = "Duyệt, dải lương hợp lý",
        };
        uow.Seed(req);
        return req;
    }

    private Task<Result> Reopen(InMemoryUnitOfWork uow, Guid id, string? reason = GoodReason,
                                Guid? actor = null, string? role = null) =>
        new ReopenRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ReopenRecruitmentRequestCommand(id, reason, actor ?? _hmId, role ?? RoleNames.HiringManager),
                CancellationToken.None);

    private Task<Result> Close(InMemoryUnitOfWork uow, Guid id, string? reason = GoodReason,
                               Guid? actor = null, string? role = null) =>
        new CloseRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CloseRecruitmentRequestCommand(id, reason, actor ?? _hmId, role ?? RoleNames.HiringManager),
                CancellationToken.None);

    // ---------- Mở lại để sửa ----------

    [Fact]
    public async Task Hm_mo_lai_phieu_da_duyet_thi_quay_ve_cho_duyet()
    {
        var uow = Seed();
        var req = SeedApproved(uow);

        var res = await Reopen(uow, req.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(RecruitmentRequestStatus.Pending, req.Status);
        Assert.Equal(GoodReason, req.RevokedReason);
        Assert.Equal(_hmId, req.RevokedByUserId);
    }

    [Fact]
    public async Task Mo_lai_thi_PHAN_CONG_va_CHU_KY_bi_go_theo()
    {
        // ADR-063 chốt "duyệt kèm phân công trong CÙNG một thao tác". Để lại Recruiter trên một phiếu
        // đang chờ duyệt là tự tạo ngoại lệ cho chính bất biến đó — và người đó vẫn thấy phiếu trong
        // danh sách việc của mình dù chữ ký đã bị thu hồi.
        var uow = Seed();
        var req = SeedApproved(uow);

        await Reopen(uow, req.Id);

        Assert.Null(req.AssignedRecruiterId);
        Assert.Null(req.ReviewedByUserId);
        Assert.Null(req.ReviewedAt);
        Assert.Null(req.ReviewReason);
    }

    [Fact]
    public async Task Mo_lai_thi_tang_so_vong_gui()
    {
        var uow = Seed();
        var req = SeedApproved(uow);
        var before = req.SubmissionCount;

        await Reopen(uow, req.Id);

        Assert.Equal(before + 1, req.SubmissionCount);
    }

    [Fact]
    public async Task Mo_lai_xong_thi_HM_sua_duoc_ngay()
    {
        // Mở lại mà không sửa được thì thao tác này vô nghĩa: `pending` phải nằm trong tập sửa được.
        var uow = Seed();
        var req = SeedApproved(uow);

        await Reopen(uow, req.Id);

        Assert.True(RecruitmentRequestStatus.IsEditable(req.Status));
    }

    // ---------- Đóng phiếu ----------

    [Fact]
    public async Task Dong_phieu_da_duyet_thi_sang_trang_thai_da_dong()
    {
        var uow = Seed();
        var req = SeedApproved(uow);

        var res = await Close(uow, req.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(RecruitmentRequestStatus.Cancelled, req.Status);
        Assert.Equal(GoodReason, req.RevokedReason);
    }

    [Fact]
    public async Task Dong_phieu_GIU_dau_vet_ai_duyet_ai_duoc_giao()
    {
        // Phiếu đóng là hồ sơ lịch sử. Xoá dấu vết là làm hỏng chính thứ cần tra lại sau này —
        // khác hẳn mở lại, nơi phân công phải đi theo chữ ký vì phiếu còn sống tiếp.
        var uow = Seed();
        var req = SeedApproved(uow);

        await Close(uow, req.Id);

        Assert.Equal(_recruiterId, req.AssignedRecruiterId);
        Assert.Equal(_hrLeaderId, req.ReviewedByUserId);
    }

    [Fact]
    public async Task Dong_phieu_KHONG_ghi_de_ghi_chu_cua_nguoi_duyet()
    {
        var uow = Seed();
        var req = SeedApproved(uow);

        await Close(uow, req.Id);

        Assert.Equal("Duyệt, dải lương hợp lý", req.ReviewReason);
        Assert.Equal(GoodReason, req.RevokedReason);
    }

    // ---------- Ràng buộc dùng chung ----------

    [Fact]
    public async Task Phieu_DA_DUNG_THANH_TIN_thi_khong_thu_hoi_duoc()
    {
        // Ràng buộc quan trọng nhất. Mở lại lúc này tạo ra một tin đang chạy mà phiếu nguồn của nó
        // lại "chờ duyệt", và unique index một-phiếu-một-tin (ADR-063) khiến vòng dựng tin lần hai
        // chết bằng 409 không ai hiểu.
        var uow = Seed();
        var req = SeedApproved(uow);
        uow.Seed(new JobPosting { Id = Guid.NewGuid(), Title = "Backend Developer", RecruitmentRequestId = req.Id });

        var reopen = await Reopen(uow, req.Id);
        var close = await Close(uow, req.Id);

        Assert.True(reopen.IsFailure);
        Assert.True(close.IsFailure);
        Assert.Contains("đã dựng thành tin", reopen.Error);
        Assert.Equal(RecruitmentRequestStatus.Approved, req.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ngắn")]
    public async Task Thu_hoi_ma_khong_neu_ro_ly_do_thi_bi_chan(string? reason)
    {
        var uow = Seed();
        var req = SeedApproved(uow);

        var res = await Reopen(uow, req.Id, reason);

        Assert.True(res.IsFailure);
        Assert.Equal(RecruitmentRequestStatus.Approved, req.Status);
    }

    [Fact]
    public async Task Nguoi_khong_phai_chu_phieu_thi_khong_thu_hoi_duoc()
    {
        // Recruiter được phân công cũng không: họ thực thi nhu cầu chứ không phát sinh hay huỷ bỏ nó.
        var uow = Seed();
        var req = SeedApproved(uow);

        var byOtherHm = await Reopen(uow, req.Id, actor: _otherHmId);
        var byRecruiter = await Close(uow, req.Id, actor: _recruiterId, role: RoleNames.Recruiter);

        Assert.Equal(CommonErrorCodes.Forbidden, byOtherHm.ErrorCode);
        Assert.Equal(CommonErrorCodes.Forbidden, byRecruiter.ErrorCode);
        Assert.Equal(RecruitmentRequestStatus.Approved, req.Status);
    }

    [Fact]
    public async Task Quan_tri_vien_thu_hoi_duoc_phieu_cua_nguoi_khac()
    {
        // Khác cổng DUYỆT: ở đây không có luật "không tự xử lý phiếu của mình" vì chính chủ phiếu là
        // người dùng chính của thao tác này.
        var uow = Seed();
        var req = SeedApproved(uow);

        var res = await Close(uow, req.Id, actor: _hrLeaderId, role: RoleNames.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(RecruitmentRequestStatus.Cancelled, req.Status);
    }

    [Theory]
    [InlineData(RecruitmentRequestStatus.Pending)]
    [InlineData(RecruitmentRequestStatus.Rejected)]
    [InlineData(RecruitmentRequestStatus.Cancelled)]
    public async Task Phieu_chua_duyet_hoac_da_dong_thi_khong_thu_hoi_duoc(string status)
    {
        var uow = Seed();
        var req = SeedApproved(uow, status);

        var res = await Reopen(uow, req.Id);

        Assert.True(res.IsFailure);
        Assert.Equal(status, req.Status);
    }

    [Fact]
    public async Task Thu_hoi_thi_bao_cho_recruiter_va_nguoi_duyet_nhung_khong_bao_cho_nguoi_bam_nut()
    {
        // Recruiter là người thiệt nhất nếu im lặng — họ có thể đang soạn JD cho nhu cầu vừa bị huỷ.
        // Và trigger realtime (ADR-057) KHÔNG cứu được: payload mang `assigned_recruiter_id` của hàng
        // MỚI, mà mở lại vừa xoá cột đó — nên phải báo tường minh ở đây.
        var uow = Seed();
        var req = SeedApproved(uow);

        await Reopen(uow, req.Id);

        var recipients = uow.Repo<Notification>().Items.Select(n => n.RecipientUserId).ToList();
        Assert.Contains(_recruiterId, recipients);
        Assert.Contains(_hrLeaderId, recipients);
        Assert.DoesNotContain(_hmId, recipients);
    }

    [Fact]
    public async Task Thu_hoi_thi_ghi_audit_log()
    {
        var uow = Seed();
        var req = SeedApproved(uow);

        await Close(uow, req.Id);

        var entry = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("recruitment_request_closed", entry.Action);
        Assert.Equal(_hmId, entry.ActorUserId);
    }

    [Fact]
    public async Task Rut_phieu_KHONG_dung_duoc_cho_phieu_da_duyet()
    {
        // Đường `cancel` cũ không đòi lý do và không báo cho ai — đúng cho phiếu chưa ai duyệt, sai
        // hoàn toàn khi đã có Recruiter cầm việc. Thông điệp phải chỉ sang đúng nút.
        var uow = Seed();
        var req = SeedApproved(uow);

        var res = await new CancelRecruitmentRequestCommandHandler(uow)
            .Handle(new CancelRecruitmentRequestCommand(req.Id, _hmId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Đóng phiếu", res.Error);
        Assert.Equal(RecruitmentRequestStatus.Approved, req.Status);
    }
}
