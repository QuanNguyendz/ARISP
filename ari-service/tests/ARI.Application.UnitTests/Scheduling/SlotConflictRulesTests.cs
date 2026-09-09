using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
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
/// Luật 3 chỉ áp khi tin ĐÃ gán Hiring Manager — tin chưa có ai thì không có ràng buộc nào để áp,
/// và đó cũng là lý do phần lớn test cũ của luồng xếp lịch không phải đổi.
/// </summary>
public class SlotConflictRulesTests
{
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private Task<Result<AssignSlotResultDto>> Assign(
        InMemoryUnitOfWork uow, Guid appId, Guid slotId, int round = 1)
        => new AssignSlotCommandHandler(uow, new RecordingNotificationService(), EmptyConfig)
            .Handle(new AssignSlotCommand(appId, slotId, round, _staffId, AppRoles.Recruiter),
                CancellationToken.None);

    /// <summary>Gán Hiring Manager cho tin + khai một khung giờ rảnh phủ trọn khoảng đã cho.</summary>
    private void WithHiringManager(
        InMemoryUnitOfWork uow, Guid jobId, DateTimeOffset from, DateTimeOffset to, int round = 1)
    {
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
