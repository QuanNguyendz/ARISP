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
            .Seed(new User { Id = _ownerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true })
            .Seed(new User { Id = _hrLeaderId, Email = "hr-leader@corp.io", Role = RoleNames.HrAdmin, IsActive = true });

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

    private Task<Result<OfferDto>> Create(InMemoryUnitOfWork uow, Guid appId, Guid? actor = null, string role = AppRoles.Recruiter)
        => new CreateOfferCommandHandler(uow).Handle(
            new CreateOfferCommand(new UpsertOfferRequest { ApplicationId = appId }, actor ?? _ownerId, role),
            CancellationToken.None);

    private static Offer Ready(InMemoryUnitOfWork uow)
    {
        var offer = uow.Repo<Offer>().Items.Single();
        offer.SalaryAmount = 25_000_000m;
        offer.StartDate = DateTimeOffset.UtcNow.AddDays(30);
        offer.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        return offer;
    }

    private Task<Result<bool>> Submit(InMemoryUnitOfWork uow, Guid offerId, Guid? actor = null, string role = AppRoles.Recruiter)
        => new SubmitOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new SubmitOfferCommand(offerId, actor ?? _ownerId, role), CancellationToken.None);

    private Task<Result<bool>> Decide(InMemoryUnitOfWork uow, Guid offerId, string decision, string? note = null, Guid? actor = null, string? role = null)
        => new DecideOfferCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DecideOfferCommand(offerId, decision, note, actor ?? _hrLeaderId, role ?? AppRoles.HrAdmin),
                CancellationToken.None);

    private Task<Result<bool>> Send(
        InMemoryUnitOfWork uow, Guid offerId, RecordingNotificationService? notif = null, Guid? actor = null, string role = AppRoles.Recruiter)
        => new SendOfferCommandHandler(uow, notif ?? new RecordingNotificationService(), new EmptyConfiguration())
            .Handle(new SendOfferCommand(offerId, actor ?? _ownerId, role), CancellationToken.None);

    private Task<Result<bool>> Respond(InMemoryUnitOfWork uow, Guid offerId, string decision, string? note = null)
        => new RespondToOfferCommandHandler(uow, new RecordingNotificationService(), new EmptyConfiguration())
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
    public async Task Dien_san_chi_lay_de_xuat_cua_luot_chot_DAT_o_vong_cao_nhat()
    {
        // Thư mời là hệ quả của quyết định tuyển CUỐI CÙNG. Trước đây lấy đề xuất mới nhất của bất kỳ lượt
        // chốt nào — kể cả lượt "không đạt", hay đề xuất sớm ở vòng 1 mà HM đã điều chỉnh ở vòng cuối.
        var (uow, _, app) = Seed();
        Evaluation Eval(int round) => new()
        {
            Id = Guid.NewGuid(), ApplicationId = app.Id, SessionId = Guid.NewGuid(),
            RoundNumber = round, SessionType = "real", AiVerdict = "pass",
        };
        var r1 = Eval(1);
        var r2 = Eval(2);
        var practice = Eval(2);
        practice.SessionType = "practice";
        uow.Seed(r1).Seed(r2).Seed(practice)
            .Seed(new ARI.Domain.Entities.HrReview
            {
                EvaluationId = r2.Id, ReviewedByUserId = _hmId, FinalVerdict = "pass",
                SuggestedSalaryMax = 30_000_000m, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            })
            .Seed(new ARI.Domain.Entities.HrReview
            {
                // Mới hơn nhưng ở vòng THẤP hơn.
                EvaluationId = r1.Id, ReviewedByUserId = _hmId, FinalVerdict = "pass",
                SuggestedSalaryMax = 20_000_000m, CreatedAt = DateTimeOffset.UtcNow,
            })
            .Seed(new ARI.Domain.Entities.HrReview
            {
                // Buổi thử không bao giờ là căn cứ tuyển.
                EvaluationId = practice.Id, ReviewedByUserId = _hmId, FinalVerdict = "pass",
                SuggestedSalaryMax = 90_000_000m, CreatedAt = DateTimeOffset.UtcNow,
            });

        var res = await Create(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(30_000_000m, res.Value!.SalaryAmount);
    }

    // ===== ADR-063: Hiring Manager SOẠN và GỬI DUYỆT, không tự chốt =====

    [Fact]
    public async Task Hm_chinh_cua_tin_soan_sua_va_gui_duyet_duoc_thu_moi()
    {
        // Trước đây ba lệnh này đòi mức chủ tin, nên HM (thành viên đội) nhận 403 dù ADR-063 viết rõ họ soạn được.
        var (uow, _, app) = Seed();

        var created = await Create(uow, app.Id, actor: _hmId, role: AppRoles.HiringManager);
        Assert.True(created.IsSuccess, created.Error);
        var offer = Ready(uow);
        Assert.Equal(_hmId, offer.CreatedByUserId);

        var updated = await new UpdateOfferCommandHandler(uow).Handle(
            new UpdateOfferCommand(offer.Id, new UpsertOfferRequest { ApplicationId = app.Id, SalaryAmount = 27_000_000m },
                _hmId, AppRoles.HiringManager), CancellationToken.None);
        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal(27_000_000m, offer.SalaryAmount);

        var submitted = await Submit(uow, offer.Id, actor: _hmId, role: AppRoles.HiringManager);
        Assert.True(submitted.IsSuccess, submitted.Error);
        Assert.Equal(OfferStatus.PendingApproval, offer.Status);
    }

    [Fact]
    public async Task Thanh_vien_phu_cua_doi_khong_soan_duoc_thu_moi()
    {
        // Mở đúng một lối vào có tên cho HM CHÍNH — không nới cho mọi thành viên đội.
        var (uow, job, app) = Seed();
        var interviewer = Guid.NewGuid();
        uow.Seed(new User { Id = interviewer, Email = "iv@corp.io", Role = RoleNames.HiringManager, IsActive = true })
            .Seed(new JobHiringTeamMember
            {
                JobPostingId = job.Id, UserId = interviewer, RoleOnJob = JobTeamRoles.Interviewer,
                IsPrimary = false, AddedByUserId = _ownerId,
            });

        var res = await Create(uow, app.Id, actor: interviewer, role: AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Repo<Offer>().Items);
    }

    [Fact]
    public async Task Hm_khong_gui_thu_cho_ung_vien_duoc()
    {
        // Gửi cho ứng viên (qua trình soạn thư) vẫn là việc của chủ tin / quản trị viên.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id, actor: _hmId, role: AppRoles.HiringManager);
        var offer = Ready(uow);
        await Submit(uow, offer.Id, actor: _hmId, role: AppRoles.HiringManager);
        await Decide(uow, offer.Id, "approved");

        var res = await Send(uow, offer.Id, actor: _hmId, role: AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(OfferStatus.Approved, offer.Status);
        Assert.Equal(ApplicationStatuses.Pass, app.Status);
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
    public async Task Gui_duyet_bao_cho_HR_Leader_nguoi_chot_va_bao_HM_de_biet()
    {
        // ADR-063: người CHỐT là HR Leader, nên họ phải nhận việc. Trước đây tin có HM thì chỉ HM được báo
        // "Thư mời chờ bạn duyệt" (sót từ ADR-061) — HM bấm duyệt thì 403, còn HR Leader không nhận gì.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id);
        var offer = Ready(uow);

        var res = await Submit(uow, offer.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(OfferStatus.PendingApproval, offer.Status);
        var notices = uow.Repo<Notification>().Items;
        var toLeader = Assert.Single(notices, n => n.RecipientUserId == _hrLeaderId);
        Assert.Equal("/hr/offers", toLeader.Link);
        var toHm = Assert.Single(notices, n => n.RecipientUserId == _hmId);
        Assert.Equal("/hm/offers", toHm.Link);
        Assert.DoesNotContain("chờ bạn duyệt", toHm.Title);
    }

    [Fact]
    public async Task Chot_thu_bao_nguoi_soan_chu_tin_va_hm_moi_nguoi_link_dung_workspace()
    {
        // Trước đây chỉ người soạn được báo, link cứng `/recruiter/offers` — HM soạn thư thì bấm vào gặp 403,
        // còn chủ tin (người phải GỬI thư) không biết thư đã được chốt.
        var (uow, _, app) = Seed();
        await Create(uow, app.Id, actor: _hmId, role: AppRoles.HiringManager);
        var offer = Ready(uow);
        await Submit(uow, offer.Id, actor: _hmId, role: AppRoles.HiringManager);

        var res = await Decide(uow, offer.Id, "approved");

        Assert.True(res.IsSuccess);
        var decided = uow.Repo<Notification>().Items.Where(n => n.DedupKey!.StartsWith($"offer_decided:{offer.Id}:")).ToList();
        Assert.Contains(decided, n => n.RecipientUserId == _hmId && n.Link == "/hm/offers");
        Assert.Contains(decided, n => n.RecipientUserId == _ownerId && n.Link == "/recruiter/offers");
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
        Assert.Contains("HR Admin là người quyết định cuối cùng", res.Error);
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

    // ===== Nhận việc: thư xác nhận + tự đóng tin khi đủ người (ADR-074) =====

    private static JobPosting JobOf(InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app)
        => uow.Repo<JobPosting>().Items.Single(j => j.Id == app.JobPostingId);

    private static RecruitmentRequest LinkRequest(InMemoryUnitOfWork uow, JobPosting job, int headcount)
    {
        var request = new RecruitmentRequest
        {
            Id = Guid.NewGuid(), Title = job.Title, Headcount = headcount, Status = "approved",
            RequestedByUserId = Guid.NewGuid(),
        };
        uow.Seed(request);
        job.RecruitmentRequestId = request.Id;
        return request;
    }

    [Fact]
    public async Task Nhan_viec_gui_thu_xac_nhan_noi_dung_luong_thu_moi_va_co_dau_moi_lien_he()
    {
        var (uow, app, offer) = await SentOffer();
        var offerLog = uow.Repo<EmailLog>().Items.Single(e => e.TemplateKey == "offer_sent");

        var res = await Respond(uow, offer.Id, "accept");

        Assert.True(res.IsSuccess, res.Error);
        var log = uow.Repo<EmailLog>().Items.Single(e => e.TemplateKey == OfferEmail.AcceptedTemplateKey);
        Assert.Equal(app.Id, log.ApplicationId);
        Assert.Null(log.SentByUserId);                         // máy gửi, không ai bấm nút
        Assert.Equal(offerLog.MessageId, log.InReplyTo);       // nằm cùng luồng với thư mời
        Assert.Contains("Xác nhận nhận việc", log.Subject);
        Assert.Contains("25.000.000 VND", log.BodyHtml);        // điều kiện đã chốt được ghi lại
        Assert.Contains("owner@corp.io", log.BodyHtml);        // biết hỏi ai
    }

    [Fact]
    public async Task Tu_choi_thi_khong_co_thu_xac_nhan_va_tin_van_mo()
    {
        var (uow, app, offer) = await SentOffer();
        LinkRequest(uow, JobOf(uow, app), headcount: 1);

        await Respond(uow, offer.Id, "decline");

        Assert.DoesNotContain(uow.Repo<EmailLog>().Items, e => e.TemplateKey == OfferEmail.AcceptedTemplateKey);
        Assert.Equal("active", JobOf(uow, app).Status);
    }

    [Fact]
    public async Task Du_nguoi_thi_tin_tu_dong_va_bao_HM_Recruiter_HR_Leader()
    {
        var (uow, app, offer) = await SentOffer();
        var job = JobOf(uow, app);
        LinkRequest(uow, job, headcount: 1);

        var res = await Respond(uow, offer.Id, "accept");

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal("closed", job.Status);
        Assert.Contains(uow.Repo<AuditLog>().Items,
            a => a.Action == JobHeadcountCloser.AuditAction && a.EntityId == job.Id && a.ActorUserId == null);
        var notices = uow.Repo<Notification>().Items.Where(n => n.DedupKey!.StartsWith($"job_filled:{job.Id}:")).ToList();
        Assert.Contains(notices, n => n.RecipientUserId == _hmId && n.Link == $"/hm/jobs/{job.Id}");
        Assert.Contains(notices, n => n.RecipientUserId == _ownerId && n.Link == $"/recruiter/my-jobs/{job.Id}");
        Assert.Contains(notices, n => n.RecipientUserId == _hrLeaderId && n.Link == $"/hr/jobs/{job.Id}");
    }

    [Fact]
    public async Task Chua_du_nguoi_thi_tin_van_mo()
    {
        var (uow, app, offer) = await SentOffer();
        LinkRequest(uow, JobOf(uow, app), headcount: 2);

        await Respond(uow, offer.Id, "accept");

        Assert.Equal("active", JobOf(uow, app).Status);
        Assert.DoesNotContain(uow.Repo<AuditLog>().Items, a => a.Action == JobHeadcountCloser.AuditAction);
    }

    [Fact]
    public async Task Tin_khong_co_phieu_thi_khong_doan_so_luong()
    {
        var (uow, app, offer) = await SentOffer();   // tin cũ trước ADR-063: không có phiếu

        await Respond(uow, offer.Id, "accept");

        Assert.Equal("active", JobOf(uow, app).Status);
    }

    [Fact]
    public async Task Dong_tin_khong_dung_toi_ho_so_va_thu_moi_khac_chi_bao_so_luong()
    {
        var (uow, app, offer) = await SentOffer();
        var job = JobOf(uow, app);
        LinkRequest(uow, job, headcount: 1);
        var midFunnel = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "b@example.io", CandidateName = "B",
            Status = ApplicationStatuses.Interview,
        };
        var otherOffered = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "c@example.io", CandidateName = "C",
            Status = ApplicationStatuses.Offer,
        };
        var otherOffer = new Offer
        {
            Id = Guid.NewGuid(), ApplicationId = otherOffered.Id, JobPostingId = job.Id, Status = OfferStatus.Sent,
            CreatedByUserId = _ownerId,
        };
        uow.Seed(midFunnel).Seed(otherOffered).Seed(otherOffer);

        await Respond(uow, offer.Id, "accept");

        Assert.Equal("closed", job.Status);
        Assert.Equal(ApplicationStatuses.Interview, midFunnel.Status);   // con người quyết, không phải máy
        Assert.Equal(OfferStatus.Sent, otherOffer.Status);                // không tự thu hồi thư mời
        var notice = uow.Repo<Notification>().Items.First(n => n.DedupKey!.StartsWith($"job_filled:{job.Id}:"));
        Assert.Contains("Còn 2 hồ sơ", notice.Body);
        Assert.Contains("1 thư mời khác", notice.Body);
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

        var res = await new RespondToOfferCommandHandler(uow, new RecordingNotificationService(), new EmptyConfiguration())
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
