using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Ba luật chống xếp lịch hỏng của ADR-067, kiểm qua chính <see cref="AssignSlotCommandHandler"/>
/// (không gọi thẳng hàm nội bộ) vì đó là cửa mà nhân sự thật sự đi qua:
///
/// <list type="number">
/// <item>Một ca = một ứng viên — buổi thật có Hiring Manager ngồi cùng AI.</item>
/// <item>Ứng viên không dự hai buổi TRÙNG GIỜ, kể cả ở tin khác; khác giờ thì vẫn được.</item>
/// <item>Ca phải nằm TRỌN trong khung giờ Hiring Manager có mặt được.</item>
/// </list>
///
/// ADR-068: mọi tin đều có Hiring Manager, nên luật 3 LUÔN áp cho vòng hội thoại — tin thiếu HM (hay HM bị
/// khoá) là không xếp được. Test không nói về lịch HM thì <see cref="HiringManagerSeed.EnsureForAllJobs"/>
/// gán cho mỗi tin một HM có khung giờ rộng, để luật 3 không phải thứ làm nó hỏng.
/// </summary>
public class SlotConflictRulesTests
{
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private Task<Result<AssignSlotResultDto>> Assign(
        InMemoryUnitOfWork uow, Guid appId, Guid slotId, int round = 1, bool ensureHiringManagers = true)
    {
        if (ensureHiringManagers) HiringManagerSeed.EnsureForAllJobs(uow);
        return new AssignSlotCommandHandler(uow, new RecordingNotificationService(), EmptyConfig)
            .Handle(new AssignSlotCommand(appId, slotId, round, _staffId, AppRoles.Recruiter),
                CancellationToken.None);
    }

    /// <summary>Gán Hiring Manager cho tin + khai một khung giờ rảnh phủ trọn khoảng đã cho.</summary>
    private void WithHiringManager(
        InMemoryUnitOfWork uow, Guid jobId, DateTimeOffset from, DateTimeOffset to, int round = 1)
    {
        if (uow.Repo<User>().Items.All(u => u.Id != _hmId))
            uow.Seed(new User { Id = _hmId, Email = "hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = jobId, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = _staffId,
        });
        uow.Seed(new HiringManagerAvailability
        {
            JobPostingId = jobId, RoundNumber = round, HiringManagerUserId = _hmId,
            StartTime = from, EndTime = to,
        });
    }

    // ---------- Luật 1: một ca = một ứng viên ----------

    [Fact]
    public async Task Ca_da_co_nguoi_thi_khong_xep_them_ai()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, capacity: 2); // dữ liệu cũ còn sức chứa 2
        var first = SchedulingData.Application(job.Id, Guid.NewGuid(), email: "a@example.io");
        var second = SchedulingData.Application(job.Id, Guid.NewGuid(), email: "b@example.io");
        uow.Seed(job).Seed(slot).Seed(first).Seed(second)
            .Seed(SchedulingData.Booking(first.Id, slot.Id));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, second.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("một ứng viên", res.Error);
    }

    // ---------- Luật 2: không dự hai buổi trùng giờ ----------

    [Fact]
    public async Task Ung_vien_khong_du_hai_buoi_trung_gio_o_hai_tin_khac_nhau()
    {
        var uow = new InMemoryUnitOfWork();
        var accountId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var jobA = SchedulingData.Job(owner: _staffId);
        var slotA = SchedulingData.Slot(jobA.Id, start: start);
        var appA = SchedulingData.Application(jobA.Id, accountId);

        var jobB = SchedulingData.Job(owner: _staffId);
        // Chồng lấn 30 phút — vẫn là "cùng một lúc" với một con người.
        var slotB = SchedulingData.Slot(jobB.Id, start: start.AddMinutes(30));
        var appB = SchedulingData.Application(jobB.Id, accountId);

        uow.Seed(jobA).Seed(slotA).Seed(appA).Seed(jobB).Seed(slotB).Seed(appB)
            .Seed(SchedulingData.Booking(appA.Id, slotA.Id));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, appB.Id, slotB.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("trùng khung giờ", res.Error);
    }

    [Fact]
    public async Task Hai_tin_khac_GIO_thi_van_xep_duoc()
    {
        var uow = new InMemoryUnitOfWork();
        var accountId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var jobA = SchedulingData.Job(owner: _staffId);
        var slotA = SchedulingData.Slot(jobA.Id, start: start);
        var appA = SchedulingData.Application(jobA.Id, accountId);

        var jobB = SchedulingData.Job(owner: _staffId);
        var slotB = SchedulingData.Slot(jobB.Id, start: start.AddHours(3));
        var appB = SchedulingData.Application(jobB.Id, accountId);

        uow.Seed(jobA).Seed(slotA).Seed(appA).Seed(jobB).Seed(slotB).Seed(appB)
            .Seed(SchedulingData.Booking(appA.Id, slotA.Id));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, appB.Id, slotB.Id);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Ho_so_nop_khong_dang_nhap_doi_chieu_bang_EMAIL()
    {
        // Không có tài khoản ứng viên thì email là thứ duy nhất nối hai hồ sơ về cùng một người.
        var uow = new InMemoryUnitOfWork();
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var jobA = SchedulingData.Job(owner: _staffId);
        var slotA = SchedulingData.Slot(jobA.Id, start: start);
        var appA = SchedulingData.Application(jobA.Id, accountId: null, email: "same@example.io");

        var jobB = SchedulingData.Job(owner: _staffId);
        var slotB = SchedulingData.Slot(jobB.Id, start: start);
        var appB = SchedulingData.Application(jobB.Id, accountId: null, email: "same@example.io");

        uow.Seed(jobA).Seed(slotA).Seed(appA).Seed(jobB).Seed(slotB).Seed(appB)
            .Seed(SchedulingData.Booking(appA.Id, slotA.Id));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, appB.Id, slotB.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("trùng khung giờ", res.Error);
    }

    // ---------- Luật 3: khớp lịch rảnh của Hiring Manager ----------

    [Fact]
    public async Task Ca_nam_trong_khung_gio_ranh_thi_xep_duoc()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddHours(-1), start.AddHours(3));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Ca_nam_ngoai_khung_gio_ranh_thi_bi_chan_kem_danh_sach_gio()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddDays(1), start.AddDays(1).AddHours(3));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("ngoài lịch rảnh", res.Error);
        Assert.Contains("có mặt được", res.Error); // nói luôn giờ nào chọn được, không bắt đi hỏi
    }

    [Fact]
    public async Task Ca_chi_giao_MOT_PHAN_khung_gio_ranh_van_bi_chan()
    {
        // Ca 1 tiếng mà HM chỉ rảnh 15 phút đầu: về hình thức "có giao nhau", về thực tế thì họ phải
        // rời phòng giữa buổi phỏng vấn.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start, start.AddMinutes(15));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("ngoài lịch rảnh", res.Error);
    }

    [Fact]
    public async Task Tin_co_HM_ma_chua_khai_gio_nao_thi_khong_xep_duoc()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = job.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = _staffId,
        });
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa gửi khung giờ", res.Error);
    }

    [Fact]
    public async Task Khung_gio_da_TROI_QUA_khong_con_tinh_la_ranh()
    {
        // Giữ lại trong DB làm dấu vết vì sao ca cũ được xếp như vậy, nhưng không được phép làm
        // "chứng nhận" cho một ca mới.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow.AddDays(-4));
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa gửi khung giờ", res.Error);
    }

    [Fact]
    public async Task Tin_chua_co_HM_thi_KHONG_xep_duoc_ca_phong_van()
    {
        // ADR-068: trước đây tin chưa gán HM bỏ qua luật 3/4 và xếp ca vào lịch của không ai cả — tới hôm
        // phỏng vấn, phòng chờ đợi một người không tồn tại. Nay cổng ĐÓNG kèm câu nói rõ phải làm gì.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id, ensureHiringManagers: false);

        Assert.True(res.IsFailure);
        Assert.Equal(JobAccessErrors.HiringManagerMissing, res.Error);
        Assert.Empty(uow.Repo<InterviewBooking>().Items);
    }

    [Fact]
    public async Task HM_bi_khoa_thi_KHONG_xep_duoc_ca_phong_van()
    {
        var uow = new InMemoryUnitOfWork();
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddHours(-1), start.AddHours(3));
        uow.Repo<User>().Items.Single(u => u.Id == _hmId).IsActive = false;
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Equal(JobAccessErrors.HiringManagerInactive, res.Error);
    }

    // ---------- Luật 4: chính Hiring Manager không dự hai buổi cùng lúc ----------

    /// <summary>
    /// Tin thứ hai do CÙNG Hiring Manager phụ trách, đã hẹn một ứng viên khác ở ca bắt đầu lúc
    /// <paramref name="start"/>. <paramref name="roundType"/> là loại của vòng 1 ở tin đó.
    /// </summary>
    private void OtherJobOfSameHm(InMemoryUnitOfWork uow, DateTimeOffset start, string roundType)
    {
        var other = SchedulingData.Job(owner: _staffId);
        var otherSlot = SchedulingData.Slot(other.Id, round: 1, start: start);
        var otherApp = SchedulingData.Application(other.Id, Guid.NewGuid(), email: "other@example.io");
        uow.Seed(other).Seed(otherSlot).Seed(otherApp)
            .Seed(SchedulingData.Booking(otherApp.Id, otherSlot.Id))
            .Seed(new InterviewRoundConfig { JobPostingId = other.Id, RoundNumber = 1, RoundType = roundType })
            .Seed(new JobHiringTeamMember
            {
                JobPostingId = other.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
                IsPrimary = true, AddedByUserId = _staffId,
            });
    }

    [Fact]
    public async Task HM_khong_du_hai_buoi_phong_van_trung_gio_o_hai_tin()
    {
        var uow = new InMemoryUnitOfWork();
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddHours(-1), start.AddHours(3));
        OtherJobOfSameHm(uow, start, "technical");
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("Hiring Manager đã có buổi phỏng vấn khác", res.Error);
    }

    [Fact]
    public async Task Dot_thi_trac_nghiem_o_tin_khac_khong_chiem_mat_HM()
    {
        // Bài thi trực tuyến không ai ngồi cùng. Tính nó là "HM đang bận" thì một đợt thi cả ngày ở
        // tin kia chặn mọi ca phỏng vấn cùng ngày ở tin này — dù HM hoàn toàn rảnh.
        var uow = new InMemoryUnitOfWork();
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddHours(-1), start.AddHours(3));
        OtherJobOfSameHm(uow, start, "online_test");
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsSuccess);
    }

    // ---------- Ngoại lệ: vòng TRẮC NGHIỆM làm tại nhà ----------

    [Fact]
    public async Task Vong_trac_nghiem_khong_can_khop_lich_HM()
    {
        // Ứng viên làm bài trực tuyến tại nhà (ADR-049): không có Hiring Manager ngồi cùng, không có
        // phòng — nên ràng buộc "ca phải nằm trong giờ HM rảnh" không áp được. Đây là lỗi do chính
        // ADR-067 tạo ra: luật viết cho buổi hội thoại bị áp nhầm sang bài thi.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        uow.Seed(job).Seed(slot).Seed(app);
        uow.Seed(new InterviewRoundConfig
        {
            JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
        });
        // Có Hiring Manager nhưng KHÔNG khai khung giờ nào — với vòng hội thoại thì đây là lỗi.
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = job.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = _staffId,
        });
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Vong_trac_nghiem_nhieu_ung_vien_chung_mot_khung_gio()
    {
        // Không ai ngồi cùng thì một khung giờ phục vụ được nhiều người — "một ca một ứng viên" là
        // luật của PHÒNG phỏng vấn, không phải của bài thi trực tuyến.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 5);
        var first = SchedulingData.Application(job.Id, Guid.NewGuid(), email: "a@example.io");
        var second = SchedulingData.Application(job.Id, Guid.NewGuid(), email: "b@example.io");
        uow.Seed(job).Seed(slot).Seed(first).Seed(second)
            .Seed(SchedulingData.Booking(first.Id, slot.Id));
        uow.Seed(new InterviewRoundConfig
        {
            JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
        });
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, second.Id, slot.Id);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Vong_trac_nghiem_VAN_chan_ung_vien_trung_gio_o_tin_khac()
    {
        // Luật 2 giữ nguyên: một con người không thể vừa ngồi thi vừa phỏng vấn ở nơi khác.
        var uow = new InMemoryUnitOfWork();
        var accountId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var jobA = SchedulingData.Job(owner: _staffId);
        var slotA = SchedulingData.Slot(jobA.Id, start: start);
        var appA = SchedulingData.Application(jobA.Id, accountId);

        var jobB = SchedulingData.Job(owner: _staffId);
        var slotB = SchedulingData.Slot(jobB.Id, start: start);
        var appB = SchedulingData.Application(jobB.Id, accountId);

        uow.Seed(jobA).Seed(slotA).Seed(appA).Seed(jobB).Seed(slotB).Seed(appB)
            .Seed(SchedulingData.Booking(appA.Id, slotA.Id));
        uow.Seed(new InterviewRoundConfig
        {
            JobPostingId = jobB.Id, RoundNumber = 1, RoundType = "online_test",
        });
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, appB.Id, slotB.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("trùng khung giờ", res.Error);
    }

    /// <summary>Ca thi lúc <paramref name="start"/> (giờ kết thúc lưu trên ca = +1h) + buổi phỏng vấn ngay sau đó.</summary>
    private (InMemoryUnitOfWork Uow, ARI.Domain.Entities.Application App, AvailabilitySlot Test, AvailabilitySlot Interview)
        TestThenInterview(DateTimeOffset start, int testMinutes)
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());

        var testSlot = SchedulingData.Slot(job.Id, round: 3, capacity: 5, start: start);
        var interviewSlot = SchedulingData.Slot(job.Id, round: 1, start: start.AddHours(1));

        uow.Seed(job).Seed(app).Seed(testSlot).Seed(interviewSlot);
        uow.Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "screening" });
        uow.Seed(new InterviewRoundConfig
        {
            JobPostingId = job.Id, RoundNumber = 3, RoundType = "online_test", MaxDurationMinutes = testMinutes,
        });
        WithHiringManager(uow, job.Id, start, start.AddHours(6));
        _ = new SlotSqlEmulator(uow);
        return (uow, app, testSlot, interviewSlot);
    }

    [Fact]
    public async Task Ca_thi_ban_toi_gio_dong_bai_du_gio_ket_thuc_luu_tren_ca_som_hon()
    {
        // Bài thi đóng lúc giờ hẹn + thời lượng (ADR-072). Ca tạo trước ADR-072 có thể mang giờ kết thúc
        // gõ tay NGẮN hơn bài: ca 09:00–10:00, bài 90 phút → ứng viên làm bài tới 10:30, nên buổi phỏng
        // vấn 10:00 không xếp được dù hai khung ca chỉ chạm nhau.
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var s = TestThenInterview(start, testMinutes: 90);

        Assert.True((await Assign(s.Uow, s.App.Id, s.Test.Id, round: 3)).IsSuccess);

        var res = await Assign(s.Uow, s.App.Id, s.Interview.Id, round: 1);

        Assert.True(res.IsFailure);
        Assert.Contains("trùng khung giờ", res.Error);
        Assert.Contains("bài thi còn mở tới", res.Error); // nói rõ vì sao hai ca không chồng nhau lại xung đột
    }

    [Fact]
    public async Task Bai_thi_da_dong_thi_xep_duoc_buoi_phong_van_sau_do()
    {
        // Trước ADR-072 cửa vào mở một tiếng rồi mỗi người còn nguyên đồng hồ, nên ca thi 09:00 với bài
        // 30' chặn mọi buổi phỏng vấn tới ~10:35. Nay bài đóng đúng 09:30 — 10:00 là rảnh.
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var s = TestThenInterview(start, testMinutes: 30);

        Assert.True((await Assign(s.Uow, s.App.Id, s.Test.Id, round: 3)).IsSuccess);

        Assert.True((await Assign(s.Uow, s.App.Id, s.Interview.Id, round: 1)).IsSuccess);
    }

    [Fact]
    public async Task Buoi_phong_van_xong_han_thi_van_xep_duoc_ca_thi_sau_do()
    {
        // Mặt kia của cùng một luật: nới khoảng bận của bài thi không được biến thành "cấm cả ngày".
        // Ca phỏng vấn 09:00–10:00 rồi ca thi 11:00 thì không ai bận hai chỗ.
        var uow = new InMemoryUnitOfWork();
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());

        var interviewSlot = SchedulingData.Slot(job.Id, round: 1, start: start);
        var testSlot = SchedulingData.Slot(job.Id, round: 3, capacity: 5, start: start.AddHours(2));

        uow.Seed(job).Seed(app).Seed(interviewSlot).Seed(testSlot);
        uow.Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "screening" });
        uow.Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 3, RoundType = "online_test" });
        WithHiringManager(uow, job.Id, start, start.AddHours(6));
        _ = new SlotSqlEmulator(uow);

        Assert.True((await Assign(uow, app.Id, interviewSlot.Id, round: 1)).IsSuccess);

        Assert.True((await Assign(uow, app.Id, testSlot.Id, round: 3)).IsSuccess);
    }

    [Fact]
    public async Task Khung_gio_cua_VONG_KHAC_khong_dung_cho_vong_nay()
    {
        // HM dự buổi thật của từng vòng, mà các vòng cách nhau nhiều ngày — một danh sách rảnh dùng
        // chung cho mọi vòng thì hoặc quá chặt hoặc vô nghĩa.
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var slot = SchedulingData.Slot(job.Id, round: 2, start: start);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        uow.Seed(job).Seed(slot).Seed(app);
        WithHiringManager(uow, job.Id, start.AddHours(-1), start.AddHours(3), round: 1);
        _ = new SlotSqlEmulator(uow);

        var res = await Assign(uow, app.Id, slot.Id, round: 2);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa gửi khung giờ", res.Error);
    }
}
