using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interviews;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.InterviewCodes;

/// <summary>
/// Cấp mã vào phòng phỏng vấn NGAY TRÊN DANH SÁCH ỨNG VIÊN của tin
/// (<see cref="GetApplicationInterviewCodeQueryHandler"/>, <see cref="IssueApplicationInterviewCodeCommandHandler"/>).
///
/// Ba điều bộ test này khoá:
/// 1. Chỉ chủ tin / quản trị viên phát mã — mã là chìa khoá mở phòng phỏng vấn thật của một người.
/// 2. Bấm lại để XEM mã không được làm mất mã ứng viên đang cầm; chỉ "Cấp mã mới" mới thay mã.
/// 3. Vòng sơ loại (làm từ nhà) có link vào phòng kèm mã để gửi ứng viên; vòng chuyên môn thì không.
/// </summary>
public class ApplicationInterviewCodeTests
{
    private readonly Guid _ownerId = Guid.NewGuid();

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:CandidateBaseUrl"] = "https://jobs.example.io/",
        })
        .Build();

    private (InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app, JobPosting job) Seed(
        string roundType = "screening", bool withBooking = true)
    {
        var job = InterviewCodeData.Job(owner: _ownerId);
        var app = InterviewCodeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = roundType, InterviewCodeTtlHours = 2,
            });
        if (withBooking) uow.Seed(InterviewCodeData.Booking(app.Id, round: 1));
        return (uow, app, job);
    }

    private Task<Result<ApplicationInterviewCodeDto>> Get(InMemoryUnitOfWork uow, Guid appId, Guid? actor = null,
        string role = AppRoles.Recruiter)
        => new GetApplicationInterviewCodeQueryHandler(uow, Config)
            .Handle(new GetApplicationInterviewCodeQuery(appId, actor ?? _ownerId, role), CancellationToken.None);

    private Task<Result<ApplicationInterviewCodeDto>> Issue(InMemoryUnitOfWork uow, Guid appId, bool regenerate = false,
        Guid? actor = null, string role = AppRoles.Recruiter)
        => new IssueApplicationInterviewCodeCommandHandler(uow, Config,
                InterviewCodeData.Service(uow, new RecordingNotificationService(), new FakeTokenService()))
            .Handle(new IssueApplicationInterviewCodeCommand(appId, regenerate, actor ?? _ownerId, role),
                CancellationToken.None);

    private static List<InterviewCode> LiveCodes(InMemoryUnitOfWork uow) =>
        uow.Repo<InterviewCode>().Items.Where(c => c.UsedAt == null && c.ExpiresAt > DateTimeOffset.UtcNow).ToList();

    // ---------- Quyền ----------

    [Fact]
    public async Task Nguoi_ngoai_khong_xem_cung_khong_cap_duoc_ma()
    {
        var (uow, app, _) = Seed();

        var get = await Get(uow, app.Id, actor: Guid.NewGuid());
        var issue = await Issue(uow, app.Id, actor: Guid.NewGuid());

        Assert.Equal(CommonErrorCodes.Forbidden, get.ErrorCode);
        Assert.Equal(CommonErrorCodes.Forbidden, issue.ErrorCode);
        Assert.Empty(uow.Repo<InterviewCode>().Items);
    }

    [Fact]
    public async Task Hiring_Manager_cua_tin_khong_phat_ma()
    {
        // HM xem được hồ sơ (thành viên đội) nhưng cấp mã là việc vận hành của Recruiter.
        var (uow, app, job) = Seed();
        var hmId = Guid.NewGuid();
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = job.Id, UserId = hmId, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = _ownerId,
        });

        var res = await Issue(uow, app.Id, actor: hmId, role: AppRoles.HiringManager);

        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Làm từ nhà vs tại văn phòng ----------

    [Fact]
    public async Task Vong_so_loai_lam_tu_nha_co_link_vao_phong_kem_ma()
    {
        var (uow, app, _) = Seed("screening");

        var res = await Issue(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsRemote);
        Assert.Equal(6, res.Value.Code!.Length);
        Assert.Equal("https://jobs.example.io/kiosk", res.Value.KioskUrl);
        Assert.Equal($"https://jobs.example.io/kiosk?code={res.Value.Code}", res.Value.EntryUrl);
    }

    [Fact]
    public async Task Vong_chuyen_mon_tai_van_phong_la_ma_dua_tan_tay()
    {
        var (uow, app, _) = Seed("technical");

        var res = await Issue(uow, app.Id);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsRemote);
        Assert.NotNull(res.Value.Code);
        // Link Kiosk trống vẫn có — để mở sẵn trên máy tại văn phòng.
        Assert.Equal("https://jobs.example.io/kiosk", res.Value.KioskUrl);
    }

    // ---------- Xem lại không làm mất mã; cấp mới thì thay mã ----------

    [Fact]
    public async Task Bam_lai_khong_cap_ma_moi_ma_tra_dung_ma_dang_co()
    {
        // Ứng viên làm từ nhà có thể đã nhận mã qua Zalo — bấm lại để xem mà thay mã là mã họ cầm
        // thành vô dụng đúng lúc sắp vào phòng.
        var (uow, app, _) = Seed();

        var first = await Issue(uow, app.Id);
        var again = await Issue(uow, app.Id);

        Assert.Equal(first.Value.Code, again.Value.Code);
        Assert.Single(uow.Repo<InterviewCode>().Items);
    }

    [Fact]
    public async Task Cap_ma_moi_thi_ma_cu_het_hieu_luc_ngay()
    {
        var (uow, app, _) = Seed();

        var first = await Issue(uow, app.Id);
        var renewed = await Issue(uow, app.Id, regenerate: true);

        Assert.NotEqual(first.Value.Code, renewed.Value.Code);
        var live = Assert.Single(LiveCodes(uow));
        Assert.Equal(renewed.Value.Code, live.Code);
    }

    [Fact]
    public async Task Xem_trang_thai_tra_ve_ma_dang_con_hieu_luc()
    {
        var (uow, app, _) = Seed();
        var issued = await Issue(uow, app.Id);

        var res = await Get(uow, app.Id);

        Assert.True(res.Value.CanIssue);
        Assert.Equal(issued.Value.Code, res.Value.Code);
        Assert.Equal(1, res.Value.RoundNumber);
    }

    // ---------- Khi nào KHÔNG cấp ----------

    [Fact]
    public async Task Chua_co_lich_thi_chua_cap_duoc()
    {
        var (uow, app, _) = Seed(withBooking: false);

        var get = await Get(uow, app.Id);
        var issue = await Issue(uow, app.Id);

        Assert.False(get.Value.CanIssue);
        Assert.Contains("chưa có lịch", get.Value.BlockedReason);
        Assert.True(issue.IsFailure);
        Assert.Empty(uow.Repo<InterviewCode>().Items);
    }

    [Fact]
    public async Task Vong_trac_nghiem_khong_dung_ma()
    {
        var (uow, app, _) = Seed("online_test");

        var get = await Get(uow, app.Id);
        var issue = await Issue(uow, app.Id);

        Assert.False(get.Value.CanIssue);
        Assert.True(issue.IsFailure);
        Assert.Contains("trắc nghiệm", issue.Error);
    }

    [Fact]
    public async Task Qua_vong_trac_nghiem_roi_xep_lich_vong_2_thi_cap_ma_vong_2()
    {
        // Lịch vòng 1 (trắc nghiệm) vẫn "scheduled" sau khi thi xong — vòng cần mã là vòng của lịch
        // CAO NHẤT, không phải vòng trắc nghiệm còn nằm đó.
        var (uow, app, job) = Seed("online_test");
        uow.Seed(InterviewCodeData.RoundConfig(job.Id, round: 2))
            .Seed(InterviewCodeData.Booking(app.Id, round: 2));

        var res = await Issue(uow, app.Id);

        Assert.True(res.IsSuccess, res.IsFailure ? res.Error : null);
        Assert.Equal(2, res.Value.RoundNumber);
        Assert.Equal(2, Assert.Single(LiveCodes(uow)).RoundNumber);
    }

    [Fact]
    public async Task Qua_vong_trac_nghiem_ma_chua_xep_lich_vong_2_thi_chi_ra_buoc_tiep_theo()
    {
        // Lịch cao nhất vẫn là vòng trắc nghiệm — chỉ nói "không dùng mã" nghe như hệ thống cấp nhầm vòng.
        var (uow, app, job) = Seed("online_test");
        uow.Seed(InterviewCodeData.RoundConfig(job.Id, round: 2));

        var get = await Get(uow, app.Id);

        Assert.False(get.Value.CanIssue);
        Assert.Contains("xếp lịch vòng 2", get.Value.BlockedReason);
    }

    [Fact]
    public async Task Ung_vien_da_vao_phong_thi_khong_cap_them()
    {
        // Đã nhập mã và đang chờ HM cho vào — mã mới là phòng chờ thứ hai cho cùng một người.
        var (uow, app, _) = Seed();
        uow.Seed(new InterviewSession
        {
            ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = InterviewSessionStatuses.Waiting,
        });

        var get = await Get(uow, app.Id);
        var issue = await Issue(uow, app.Id, regenerate: true);

        Assert.False(get.Value.CanIssue);
        Assert.Contains("đang ở phòng", get.Value.BlockedReason);
        Assert.True(issue.IsFailure);
    }

    [Fact]
    public async Task Ho_so_da_dong_thi_khong_cap()
    {
        var (uow, app, _) = Seed();
        app.Status = ApplicationStatuses.NotPass;

        var issue = await Issue(uow, app.Id);

        Assert.True(issue.IsFailure);
        Assert.Empty(uow.Repo<InterviewCode>().Items);
    }
}
