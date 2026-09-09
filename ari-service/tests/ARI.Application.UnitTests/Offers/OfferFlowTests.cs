using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.Offers;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Offers;

/// <summary>
/// Thư mời nhận việc (ADR-061, Phase 5) — đoạn kết mà phễu tuyển dụng trước đây không có:
/// <c>pass</c> là trạng thái cuối và thư chúc mừng nói "HR sẽ liên hệ để gửi Offer Letter",
/// tức là quy trình rời khỏi hệ thống đúng ở bước quan trọng nhất.
/// </summary>
public class OfferFlowTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    /// <summary>ADR-063: người CHỐT thư mời là HR Leader, không phải Hiring Manager.</summary>
    private readonly Guid _hrLeaderId = Guid.NewGuid();
    private readonly Guid _candidateAccountId = Guid.NewGuid();

    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app) Seed(
        string appStatus = "pass", bool withHiringManager = true)
    {
        var job = new JobPosting
        {
            Id = Guid.NewGuid(), Title = "Backend Developer", JobDescription = "JD",
            Status = "active", CreatedByUserId = _ownerId,
        };
        var app = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "cand@example.io",
            CandidateName = "Nguyen Van A", Status = appStatus, CandidateAccountId = _candidateAccountId,
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

    private Task<Result<OfferDto>> Create(InMemoryUnitOfWork uow, Guid appId, Guid? actor = null)
        => new CreateOfferCommandHandler(uow).Handle(
            new CreateOfferCommand(new UpsertOfferRequest { ApplicationId = appId }, actor ?? _ownerId, AppRoles.Recruiter),
            CancellationToken.None);

    private static Offer Ready(InMemoryUnitOfWork uow)
    {
        var offer = uow.Repo<Offer>().Items.Single();
        offer.SalaryAmount = 25_000_000m;
        offer.StartDate = DateTimeOffset.UtcNow.AddDays(30);
        offer.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        return offer;
    }

    private Task<Result<bool>> Submit(InMemoryUnitOfWork uow, Guid offerId)
        => new SubmitOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new SubmitOfferCommand(offerId, _ownerId, AppRoles.Recruiter), CancellationToken.None);

    private Task<Result<bool>> Decide(InMemoryUnitOfWork uow, Guid offerId, string decision, string? note = null, Guid? actor = null, string? role = null)
        => new DecideOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DecideOfferCommand(offerId, decision, note, actor ?? _hrLeaderId, role ?? AppRoles.HrAdmin),
                CancellationToken.None);

    private Task<Result<bool>> Send(InMemoryUnitOfWork uow, Guid offerId, RecordingNotificationService? notif = null)
        => new SendOfferCommandHandler(uow, notif ?? new RecordingNotificationService(), new EmptyConfiguration())
            .Handle(new SendOfferCommand(offerId, _ownerId, AppRoles.Recruiter), CancellationToken.None);

    private Task<Result<bool>> Respond(InMemoryUnitOfWork uow, Guid offerId, string decision, string? note = null)
        => new RespondToOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RespondToOfferCommand(offerId, decision, note, _candidateAccountId, "cand@example.io"),
                CancellationToken.None);

    // ===== Tạo =====

    [Fact]
    public async Task Chi_ra_offer_cho_ung_vien_da_qua_het_cac_vong()
    {
        var (uow, _, app) = Seed(appStatus: "interview");

        var res = await Create(uow, app.Id);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Offer>().Items);
    }

    [Fact]
    public async Task Tao_offer_dien_san_luong_tu_de_xuat_cua_nguoi_chot_ket_qua()
    {
        // Công sức Hiring Manager bỏ ra khi chốt kết quả không nên phải gõ lại.
        var (uow, _, app) = Seed();
        var eval = new Evaluation
        {
            Id = Guid.NewGuid(), ApplicationId = app.Id, SessionId = Guid.NewGuid(),
            RoundNumber = 1, SessionType = "real", AiVerdict = "pass",
        };
        uow.Seed(eval).Seed(new ARI.Domain.Entities.HrReview
        {
            EvaluationId = eval.Id, ReviewedByUserId = _hmId, FinalVerdict = "pass",
            SuggestedSalaryMin = 22_000_000m, SuggestedSalaryMax = 28_000_000m, SuggestedSalaryCurrency = "VND",
        });

        var res = await Create(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(28_000_000m, res.Value!.SalaryAmount);
        Assert.Equal(OfferStatus.Draft, res.Value.Status);
    }

    [Fact]
    public async Task Khong_tao_duoc_offer_thu_hai_khi_con_mot_cai_dang_song()
    {
        // "Hai offer, hai mức lương, gửi cả hai" là hỏng nặng nhất mà tính năng này gây ra được.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);

        var second = await Create(uow, app.Id);

        Assert.True(second.IsFailure);
        Assert.Single(uow.Repo<Offer>().Items);
    }

    [Fact]
    public async Task Nguoi_ngoai_khong_tao_duoc_offer_cho_tin_nguoi_khac()
    {
        var (uow, _, app) = Seed();

        var res = await Create(uow, app.Id, actor: Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ===== Gửi duyệt =====

    [Fact]
    public async Task Gui_duyet_doi_du_luong_ngay_bat_dau_va_han_phan_hoi()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = uow.Repo<Offer>().Items.Single();

        var res = await Submit(uow, offer.Id);

        Assert.True(res.IsFailure); // thiếu cả ba → người duyệt không có gì để duyệt
        Assert.Equal(OfferStatus.Draft, offer.Status);
    }

    [Fact]
    public async Task Gui_duyet_bao_cho_hiring_manager_cua_tin()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);

        var res = await Submit(uow, offer.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.PendingApproval, offer.Status);
        Assert.Contains(uow.Repo<Notification>().Items, n => n.RecipientUserId == _hmId);
    }

    // ===== Duyệt =====

    [Fact]
    public async Task Hr_leader_chot_offer()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "approved");

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Approved, offer.Status);
        Assert.Equal(_hrLeaderId, offer.ApprovedByUserId);
    }

    /// <summary>
    /// ADR-063 — Hiring Manager ĐỀ XUẤT mức lương nhưng không tự chốt đề xuất của chính mình.
    /// Trước đây chỗ này nhận `isTheHm || isAdmin`, tức người có nhu cầu tuyển cũng là người duyệt
    /// ngân sách lương. Test khoá lại ở TẦNG NGHIỆP VỤ, không chỉ ở policy của controller: policy
    /// nới ra một dòng là quyền cũ lặng lẽ sống lại.
    /// </summary>
    [Fact]
    public async Task Hiring_manager_khong_tu_chot_duoc_offer()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "approved", actor: _hmId, role: AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("HR Leader là người quyết định cuối cùng", res.Error);
        Assert.Equal(OfferStatus.PendingApproval, offer.Status);   // vẫn nằm chờ, không bị chốt
    }

    [Fact]
    public async Task Recruiter_cung_khong_chot_duoc_offer()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "approved", actor: _ownerId, role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal(OfferStatus.PendingApproval, offer.Status);
    }

    [Fact]
    public async Task Tu_choi_duyet_tra_ve_ban_nhap_chu_khong_khep_offer()
    {
        // Chủ tin sửa rồi gửi duyệt lại — khép luôn thì phải tạo offer mới từ đầu.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "rejected", note: "Mức lương vượt ngân sách phòng");

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Draft, offer.Status);
        Assert.Equal("Mức lương vượt ngân sách phòng", offer.RejectedReason);
    }

    [Fact]
    public async Task Nguoi_khong_phai_hm_cua_tin_khong_duyet_duoc()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "approved", actor: Guid.NewGuid(), role: AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Tin_chua_gan_hm_thi_quan_tri_vien_duyet()
    {
        var (uow, _, app) = Seed(withHiringManager: false);
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);

        var res = await Decide(uow, offer.Id, "approved", actor: Guid.NewGuid(), role: AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Approved, offer.Status);
    }

    // ===== Gửi cho ứng viên =====

    [Fact]
    public async Task Ho_so_chi_chuyen_sang_offer_khi_thu_DUOC_GUI()
    {
        // Bản nháp không phải lời hứa.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        Assert.Equal(ApplicationStatuses.Pass, app.Status);

        await Submit(uow, offer.Id);
        await Decide(uow, offer.Id, "approved");
        Assert.Equal(ApplicationStatuses.Pass, app.Status); // duyệt xong vẫn chưa hứa gì

        var res = await Send(uow, offer.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Sent, offer.Status);
        Assert.Equal(ApplicationStatuses.Offer, app.Status);
        Assert.Single(uow.Repo<EmailLog>().Items); // thư đi kèm dấu vết (Phase 4)
    }

    [Fact]
    public async Task Khong_gui_duoc_offer_chua_qua_duyet()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);

        var res = await Send(uow, offer.Id);

        Assert.True(res.IsFailure);
        Assert.Equal(ApplicationStatuses.Pass, app.Status);
    }

    // ===== Ứng viên phản hồi =====

    private async Task<(InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app, Offer offer)> SentOffer()
    {
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);
        await Submit(uow, offer.Id);
        await Decide(uow, offer.Id, "approved");
        await Send(uow, offer.Id);
        return (uow, app, offer);
    }

    [Fact]
    public async Task Ung_vien_nhan_offer_thi_ho_so_thanh_hired()
    {
        var (uow, app, offer) = await SentOffer();

        var res = await Respond(uow, offer.Id, "accept");

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Equal(ApplicationStatuses.Hired, app.Status); // điểm kết thúc thành công của phễu
    }

    [Fact]
    public async Task Ung_vien_tu_choi_thi_ho_so_thanh_offer_declined()
    {
        var (uow, app, offer) = await SentOffer();

        var res = await Respond(uow, offer.Id, "decline", note: "Đã nhận lời nơi khác");

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.OfferDeclined, app.Status);
        Assert.Equal("Đã nhận lời nơi khác", offer.CandidateResponseNote);
    }

    [Fact]
    public async Task Ung_vien_khac_khong_tra_loi_duoc_offer_nay()
    {
        var (uow, _, offer) = await SentOffer();

        var res = await new RespondToOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RespondToOfferCommand(offer.Id, "accept", null, Guid.NewGuid(), "khac@example.io"),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal(OfferStatus.Sent, offer.Status);
    }

    [Fact]
    public async Task Offer_qua_han_thi_khong_tra_loi_duoc_nua()
    {
        var (uow, _, offer) = await SentOffer();
        offer.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var res = await Respond(uow, offer.Id, "accept");

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    [Fact]
    public async Task Khong_tra_loi_hai_lan_duoc()
    {
        var (uow, _, offer) = await SentOffer();
        await Respond(uow, offer.Id, "accept");

        var again = await Respond(uow, offer.Id, "decline");

        Assert.True(again.IsFailure);
        Assert.Equal(OfferStatus.Accepted, offer.Status);
    }

    // ===== Phạm vi dữ liệu của ứng viên =====

    [Fact]
    public async Task Ung_vien_khong_thay_offer_khi_chua_duoc_gui()
    {
        // Bản nháp và bản đang duyệt là đàm phán nội bộ: lộ ra là ứng viên thấy mức lương công ty
        // còn đang cân nhắc, trước cả khi công ty quyết.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        Ready(uow);

        var res = await new GetCandidateOfferQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetCandidateOfferQuery(app.Id, _candidateAccountId, "cand@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Dto_cua_ung_vien_khong_co_truong_ghi_chu_noi_bo()
    {
        var (uow, app, offer) = await SentOffer();
        offer.Notes = "Ngân sách còn dư, có thể nâng thêm 3 triệu nếu ứng viên mặc cả.";

        var res = await new GetCandidateOfferQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetCandidateOfferQuery(app.Id, _candidateAccountId, "cand@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        // Lớp CandidateOfferDto KHÔNG khai trường Notes — không có gì để quên xoá.
        Assert.DoesNotContain("Notes", typeof(CandidateOfferDto).GetProperties().Select(p => p.Name));
    }

    // ===== Thu hồi =====

    [Fact]
    public async Task Thu_hoi_offer_da_gui_thi_dong_ho_so()
    {
        var (uow, app, offer) = await SentOffer();

        var res = await new WithdrawOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new WithdrawOfferCommand(offer.Id, "Phòng ban tạm dừng tuyển", Guid.NewGuid(), AppRoles.HrAdmin),
                CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.Withdrawn, offer.Status);
        Assert.Equal(ApplicationStatuses.NotPass, app.Status);
    }

    [Fact]
    public async Task Thu_hoi_offer_CHUA_gui_thi_ho_so_quay_ve_pass()
    {
        // Ứng viên chưa biết gì cả → còn ra được thư mời khác.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = uow.Repo<Offer>().Items.Single();

        await new WithdrawOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new WithdrawOfferCommand(offer.Id, "Đổi mức lương", Guid.NewGuid(), AppRoles.HrAdmin),
                CancellationToken.None);

        Assert.Equal(ApplicationStatuses.Pass, app.Status);

        // Và offer đã khép không còn chiếm "một offer sống".
        var again = await Create(uow, app.Id);
        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task Chi_quan_tri_vien_thu_hoi_duoc_offer()
    {
        var (uow, _, offer) = await SentOffer();

        var res = await new WithdrawOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new WithdrawOfferCommand(offer.Id, "Lý do", _ownerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }
}

/// <summary>IConfiguration rỗng — thư offer không cần cấu hình nào để dựng được nội dung.</summary>
internal sealed class EmptyConfiguration : Microsoft.Extensions.Configuration.IConfiguration
{
    public string? this[string key] { get => null; set { } }
    public System.Collections.Generic.IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren()
        => Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();
    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
        => new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);
    public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => null!;
}
