using System;
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
/// Phiếu yêu cầu tuyển dụng (ADR-063) — vòng đời và hai ràng buộc cốt lõi của quy trình:
/// không ai duyệt phiếu của chính mình, và từ chối bắt buộc kèm lý do.
/// </summary>
public class RecruitmentRequestFlowTests
{
    private readonly Guid _hmId = Guid.NewGuid();
    private readonly Guid _hrLeaderId = Guid.NewGuid();
    private readonly Guid _recruiterId = Guid.NewGuid();

    /// <summary>Đội của Hiring Manager — ADR-065 lấy đội từ tài khoản, không từ biểu mẫu.</summary>
    private static readonly Department Engineering = new() { Id = Guid.NewGuid(), Name = "Engineering" };

    private InMemoryUnitOfWork Seed()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Engineering);
        uow.Seed(
            // ADR-065: HM phải được gán đội, nếu không lập phiếu bị chặn hẳn.
            new User { Id = _hmId, Email = "hm@x.io", Role = RoleNames.HiringManager, FullName = "HM", IsActive = true,
                       DepartmentId = Engineering.Id },
            new User { Id = _hrLeaderId, Email = "hr@x.io", Role = RoleNames.HrAdmin, FullName = "HR Leader", IsActive = true },
            new User { Id = _recruiterId, Email = "rec@x.io", Role = RoleNames.Recruiter, FullName = "Recruiter", IsActive = true });
        return uow;
    }

    /// <summary>Yêu cầu ứng viên nhiều dòng — đúng dạng HM gõ, mỗi dòng một tiêu chí.</summary>
    private const string SampleRequirements = "Thành thạo C#\nNắm chắc OOP";

    private static RecruitmentRequestInput Input(
        decimal? min = 20_000_000, decimal? max = 30_000_000, string? requirements = SampleRequirements,
        string? priority = RecruitmentPriority.Medium, DateTimeOffset? startDate = null,
        string? reason = "Mở rộng đội", string? description = "Cần kỹ sư .NET", bool negotiable = false) =>
        new("Backend Developer", 2, priority, reason, description, requirements,
            "full_time", "onsite", "Hà Nội", "senior",
            startDate ?? DateTimeOffset.UtcNow.AddMonths(1), min, max, "VND", negotiable);

    /// <summary>
    /// Lập phiếu. Chỉ Hiring Manager làm được, và đội lấy thẳng từ tài khoản của họ (ADR-065).
    /// </summary>
    private Task<Result<Guid>> Create(InMemoryUnitOfWork uow, Guid? actor = null, string? role = null) =>
        new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(
                Input(), actor ?? _hmId, role ?? RoleNames.HiringManager), CancellationToken.None);

    /// <summary>
    /// Dựng thẳng một phiếu thuộc về <paramref name="ownerId"/>, không qua lệnh lập phiếu.
    ///
    /// Cần cho các ca mà <b>chủ phiếu không phải Hiring Manager</b> — nay không lập mới được nữa nhưng
    /// vẫn tồn tại trong dữ liệu cũ hoặc khi một tài khoản đổi vai.
    /// </summary>
    private static RecruitmentRequest SeedRequestOwnedBy(InMemoryUnitOfWork uow, Guid ownerId)
    {
        var req = new RecruitmentRequest
        {
            RequestedByUserId = ownerId,
            Title = "Backend Developer",
            DepartmentId = Engineering.Id,
            Headcount = 2,
            Priority = RecruitmentPriority.Medium,
            Status = RecruitmentRequestStatus.Pending,
        };
        uow.Seed(req);
        return req;
    }

    private Task<Result> Approve(InMemoryUnitOfWork uow, Guid id, Guid? actor = null, Guid? recruiter = null) =>
        new ApproveRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ApproveRecruitmentRequestCommand(id, recruiter ?? _recruiterId, null, actor ?? _hrLeaderId),
                CancellationToken.None);

    private Task<Result> Reject(InMemoryUnitOfWork uow, Guid id, string? reason, Guid? actor = null) =>
        new RejectRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RejectRecruitmentRequestCommand(id, reason, actor ?? _hrLeaderId), CancellationToken.None);

    // ---------- Đường thành công ----------

    [Fact]
    public async Task Hm_lap_phieu_thi_phieu_o_trang_thai_cho_duyet()
    {
        var uow = Seed();

        var res = await Create(uow);

        Assert.True(res.IsSuccess);
        var req = Assert.Single(uow.Repo<RecruitmentRequest>().Items);
        Assert.Equal(RecruitmentRequestStatus.Pending, req.Status);
        Assert.Equal(_hmId, req.RequestedByUserId);
        Assert.Equal(1, req.SubmissionCount);
    }

    [Fact]
    public async Task Duyet_phieu_phai_kem_phan_cong_recruiter()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;

        var res = await Approve(uow, id);

        Assert.True(res.IsSuccess);
        var req = Assert.Single(uow.Repo<RecruitmentRequest>().Items);
        Assert.Equal(RecruitmentRequestStatus.Approved, req.Status);
        Assert.Equal(_recruiterId, req.AssignedRecruiterId);
        Assert.Equal(_hrLeaderId, req.ReviewedByUserId);
        Assert.NotNull(req.ReviewedAt);
    }

    [Fact]
    public async Task Nguoi_duoc_phan_cong_phai_co_vai_tro_recruiter()
    {
        // Phân công nhầm cho một HM thì phiếu duyệt xong vẫn không ai dựng được tin —
        // `CreateJobCommand` chỉ nhận đúng Recruiter được giao.
        var uow = Seed();
        var id = (await Create(uow)).Value;

        var res = await Approve(uow, id, recruiter: _hmId);

        Assert.True(res.IsFailure);
        Assert.Contains("vai trò Recruiter", res.Error);
        Assert.Equal(RecruitmentRequestStatus.Pending, uow.Repo<RecruitmentRequest>().Items[0].Status);
    }

    // ---------- Ràng buộc 1: không tự duyệt phiếu của chính mình ----------

    [Fact]
    public async Task Khong_ai_tu_duyet_duoc_phieu_cua_chinh_minh()
    {
        // Chốt chặn này vẫn cần dù đường "HR Leader tự lập phiếu" đã bị đóng hẳn: một phiếu do
        // chính người duyệt đứng tên vẫn tồn tại được — dữ liệu lập trước lúc đóng, hoặc một tài khoản
        // đổi vai từ Hiring Manager sang HR Leader. Chặn theo NGƯỜI nên cả hai trường hợp đều kín.
        var uow = Seed();
        var own = SeedRequestOwnedBy(uow, _hrLeaderId);

        var res = await Approve(uow, own.Id, actor: _hrLeaderId);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("tự duyệt", res.Error);
        Assert.Equal(RecruitmentRequestStatus.Pending, own.Status);
    }

    [Fact]
    public async Task Khong_tu_tu_choi_duoc_phieu_cua_chinh_minh()
    {
        var uow = Seed();
        var own = SeedRequestOwnedBy(uow, _hrLeaderId);

        var res = await Reject(uow, own.Id, "Lý do đủ dài để qua ngưỡng", actor: _hrLeaderId);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Ràng buộc 2: từ chối phải kèm lý do ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ngắn")]
    public async Task Tu_choi_khong_co_ly_do_thi_bi_chan(string? reason)
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;

        var res = await Reject(uow, id, reason);

        Assert.True(res.IsFailure);
        Assert.Contains("lý do", res.Error);
        Assert.Equal(RecruitmentRequestStatus.Pending, uow.Repo<RecruitmentRequest>().Items[0].Status);
    }

    // ---------- Vòng sửa – gửi lại ----------

    [Fact]
    public async Task Bi_tra_lai_thi_hm_sua_roi_gui_lai_duoc()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;
        await Reject(uow, id, "Dải lương vượt khung của cấp bậc này");

        var req = uow.Repo<RecruitmentRequest>().Items[0];
        Assert.Equal(RecruitmentRequestStatus.Rejected, req.Status);
        Assert.Contains("Dải lương", req.ReviewReason);

        // HM chỉnh dải lương rồi gửi lại — đúng kịch bản thường gặp nhất.
        var edit = await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(id, Input(min: 18_000_000, max: 24_000_000), _hmId, RoleNames.HiringManager),
            CancellationToken.None);
        Assert.True(edit.IsSuccess);

        var resubmit = await new ResubmitRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ResubmitRecruitmentRequestCommand(id, _hmId), CancellationToken.None);

        Assert.True(resubmit.IsSuccess);
        Assert.Equal(RecruitmentRequestStatus.Pending, req.Status);
        Assert.Equal(2, req.SubmissionCount);
        Assert.Equal(24_000_000, req.SalaryMax);

        // Lý do bị trả về lần trước GIỮ NGUYÊN: đó là bối cảnh HR Leader cần khi xem lại vòng này.
        Assert.Contains("Dải lương", req.ReviewReason);
    }

    [Fact]
    public async Task Nguoi_khac_khong_sua_duoc_phieu()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;

        var res = await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(id, Input(), _hrLeaderId, RoleNames.HrAdmin), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Phieu_da_duyet_thi_khong_sua_duoc_nua()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;
        await Approve(uow, id);

        var res = await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(id, Input(), _hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Duyet_hai_lan_khong_duoc()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;
        await Approve(uow, id);

        var res = await Approve(uow, id);

        Assert.True(res.IsFailure);
        Assert.Contains("đang chờ duyệt", res.Error);
    }

    // ---------- Yêu cầu ứng viên (ô nhập bổ sung — nguyên liệu để Recruiter dựng JD) ----------

    [Fact]
    public async Task Yeu_cau_ung_vien_duoc_luu_va_sua_duoc()
    {
        var uow = Seed();
        var id = (await Create(uow)).Value;

        var req = uow.Repo<RecruitmentRequest>().Items[0];
        Assert.Contains("Thành thạo C#", req.Requirements);

        // HM sửa lại yêu cầu rồi lưu — cùng đường validate với lúc tạo.
        var edit = await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(
                id, Input(requirements: "Có kinh nghiệm ASP.NET MVC\nBiết viết unit test"),
                _hmId, RoleNames.HiringManager),
            CancellationToken.None);

        Assert.True(edit.IsSuccess);
        Assert.Contains("unit test", req.Requirements);
    }

    [Fact]
    public async Task Yeu_cau_ung_vien_de_trong_van_lap_duoc_phieu()
    {
        // ADR-065 đổi luật: yêu cầu ứng viên nay BẮT BUỘC. Đây là nguyên liệu chính để Recruiter
        // soạn JD — thiếu nó thì trình soạn mở ra gần như trang trắng, đúng thứ ADR-064 sinh ra để tránh.
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(Input(requirements: "   "), _hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Yêu cầu ứng viên", res.Error);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }

    // ---------- Ràng buộc bắt buộc mới (ADR-065) ----------

    [Fact]
    public async Task Thieu_ngay_du_kien_bat_dau_thi_bi_chan()
    {
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(
                Input(startDate: null) with { ExpectedStartDate = null }, _hmId, RoleNames.HiringManager),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Ngày dự kiến bắt đầu", res.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khan_cap")]
    public async Task Muc_do_uu_tien_khong_hop_le_thi_bi_chan(string? priority)
    {
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(Input(priority: priority), _hmId, RoleNames.HiringManager),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("ưu tiên", res.Error);
    }

    [Fact]
    public async Task Thieu_ly_do_hoac_mo_ta_thi_bi_chan()
    {
        var uow = Seed();
        var handler = new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService());

        var noReason = await handler.Handle(
            new CreateRecruitmentRequestCommand(Input(reason: "  "), _hmId, RoleNames.HiringManager), CancellationToken.None);
        var noDesc = await handler.Handle(
            new CreateRecruitmentRequestCommand(Input(description: null), _hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.Contains("Lý do tuyển", noReason.Error);
        Assert.Contains("Mô tả sơ bộ", noDesc.Error);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }

    // ---------- Dải lương: "thoả thuận" suy ra từ dữ liệu ----------

    [Fact]
    public async Task Khong_tich_thoa_thuan_va_khong_dien_so_nao_thi_bi_chan()
    {
        // Không có luật này thì "thoả thuận" và "quên điền" lẫn vào nhau — đúng thứ ô tích sinh ra
        // để phân biệt.
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(
                Input(min: null, max: null, negotiable: false), _hmId, RoleNames.HiringManager),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Thoả thuận", res.Error);
    }

    [Fact]
    public async Task Tich_thoa_thuan_thi_hai_o_luong_bi_XOA_TRANG()
    {
        // Giữ lại con số cũ là tạo đúng trạng thái mâu thuẫn mà cách suy-ra này sinh ra để loại bỏ.
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(
                Input(min: 20_000_000, max: 30_000_000, negotiable: true), _hmId, RoleNames.HiringManager),
                CancellationToken.None);

        Assert.True(res.IsSuccess);
        var req = Assert.Single(uow.Repo<RecruitmentRequest>().Items);
        Assert.Null(req.SalaryMin);
        Assert.Null(req.SalaryMax);
    }

    // ---------- Kiểm nội dung ----------

    [Fact]
    public async Task Luong_toi_thieu_lon_hon_toi_da_thi_bi_chan()
    {
        var uow = Seed();

        var res = await new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(Input(min: 40_000_000, max: 20_000_000), _hmId, RoleNames.HiringManager),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("lương tối thiểu", res.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }
}
