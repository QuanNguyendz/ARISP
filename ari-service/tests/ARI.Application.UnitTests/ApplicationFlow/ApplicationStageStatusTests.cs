using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Trạng thái CHI TIẾT của vòng đang diễn ra (<see cref="ApplicationStageStatus"/>, ADR-067).
///
/// Vì sao cần: <c>applications.status</c> chỉ nói hồ sơ ở KHÚC nào của phễu, nên suốt cả một vòng nó
/// đứng yên ở <c>interview</c> — bảng ứng viên hiện đúng một chữ "Đang phỏng vấn" từ lúc xếp lịch
/// tới lúc chờ chốt kết quả, và người vận hành không biết việc tiếp theo của mình là gì.
/// </summary>
public class ApplicationStageStatusTests
{
    private static Task<Dictionary<Guid, string>> Run(
        InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app, int? round) =>
        ApplicationStageStatus.ComputeAsync(
            uow,
            new List<(Guid, Guid, string, int?)> { (app.Id, app.JobPostingId, app.Status, round) },
            CancellationToken.None);

    private static async Task<string> Stage(
        InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app, int? round = 1) =>
        (await Run(uow, app, round))[app.Id];

    // ---------- Trước khi vào vòng ----------

    [Fact]
    public async Task Ho_so_moi_nop_thi_cho_sang_CV()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "cv_submitted");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        Assert.Equal(ApplicationStageStatus.CvPending, await Stage(uow, app, null));
    }

    [Fact]
    public async Task Dang_o_ban_cua_HM()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "hm_review");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        Assert.Equal(ApplicationStageStatus.HmReview, await Stage(uow, app, null));
    }

    [Fact]
    public async Task Da_duyet_nhung_chua_co_lich_thi_cho_xep_lich()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "screening");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        Assert.Equal(ApplicationStageStatus.AwaitingSchedule, await Stage(uow, app));
    }

    // ---------- Lịch của vòng ----------

    [Fact]
    public async Task Da_xep_lich_nhung_ung_vien_chua_tra_loi()
    {
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id));

        Assert.Equal(ApplicationStageStatus.SchedulePending, await Stage(uow, app));
    }

    [Fact]
    public async Task Ung_vien_vua_xac_nhan_lich()
    {
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id, confirmation: "confirmed"));

        Assert.Equal(ApplicationStageStatus.ScheduleConfirmed, await Stage(uow, app));
    }

    [Fact]
    public async Task Ung_vien_bao_ban_thi_cho_xep_lai()
    {
        // Khác hẳn "chưa từng xếp lịch" — và đó là phân biệt quyết định việc tiếp theo của Recruiter.
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.DeclinedBooking(app.Id, slot.Id));

        Assert.Equal(ApplicationStageStatus.ScheduleDeclined, await Stage(uow, app));
    }

    [Fact]
    public async Task Qua_gio_ma_khong_co_phien_nao_thi_la_khong_du()
    {
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(-2));
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id));

        Assert.Equal(ApplicationStageStatus.Missed, await Stage(uow, app));
    }

    [Fact]
    public async Task Vong_trac_nghiem_nhung_chua_xep_lich_thi_van_la_cho_xep_lich()
    {
        // Lỗi đã gặp: hồ sơ nằm ở "Chờ xếp lịch" nhưng bảng ghi "Chưa nộp bài thi" — đổ lỗi nhầm
        // người. Bài thi chỉ mở cùng thư mời kèm giờ hẹn (ADR-059), nên ứng viên chưa được mời làm
        // bài; việc đang chờ là việc của Recruiter.
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "screening");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
            });

        Assert.Equal(ApplicationStageStatus.AwaitingSchedule, await Stage(uow, app));
    }

    // ---------- Vòng trắc nghiệm ----------

    [Fact]
    public async Task Vong_trac_nghiem_chua_nop_bai()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
            });

        Assert.Equal(ApplicationStageStatus.TestOpen, await Stage(uow, app));
    }

    [Fact]
    public async Task Vong_trac_nghiem_da_nop_bai()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
            })
            .Seed(new OnlineTestSubmission { ApplicationId = app.Id, RoundNumber = 1 });

        Assert.Equal(ApplicationStageStatus.TestSubmitted, await Stage(uow, app));
    }

    [Fact]
    public async Task Vong_trac_nghiem_he_thong_nop_thay_thi_la_het_han_khong_phai_da_nop()
    {
        // "0 điểm do không làm" và "0 điểm do làm sai hết" là hai câu chuyện khác nhau với người
        // quyết định loại hay giữ — bảng không được gộp chúng vào "Đã nộp bài thi".
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
            })
            .Seed(new OnlineTestSubmission
            {
                ApplicationId = app.Id, RoundNumber = 1, SubmittedBy = OnlineTestSubmittedBy.System,
            });

        Assert.Equal(ApplicationStageStatus.TestExpired, await Stage(uow, app));
    }

    [Theory]
    [InlineData(-30, "test_open")]     // trong cửa vào (giờ hẹn → +1h)
    [InlineData(-61, "test_expired")]  // cửa đã đóng, chưa có bài — hệ thống chưa kịp nộp thay
    [InlineData(120, "test_open")]     // chưa tới giờ
    public async Task Vong_trac_nghiem_cua_da_dong_ma_chua_co_bai_thi_bao_het_han_ngay(
        int startOffsetMinutes, string expected)
    {
        // Hệ thống chỉ nộp thay sau hạn chót (đóng cửa + thời lượng bài); trong khoảng giữa đó ứng
        // viên đã không còn vào được, và bảng phải nói ngay chứ không đợi tác vụ nền.
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddMinutes(startOffsetMinutes));
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id))
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test",
            });

        Assert.Equal(expected, await Stage(uow, app));
    }

    // ---------- Phiên phỏng vấn nói to nhất ----------

    [Fact]
    public async Task Ung_vien_dang_o_phong_cho()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewSession
            {
                ApplicationId = app.Id, RoundNumber = 1, SessionType = "real",
                Status = InterviewSessionStatuses.Waiting,
            });

        Assert.Equal(ApplicationStageStatus.InterviewWaiting, await Stage(uow, app));
    }

    [Fact]
    public async Task Dang_phong_van_that()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewSession
            {
                ApplicationId = app.Id, RoundNumber = 1, SessionType = "real",
                Status = InterviewSessionStatuses.Active,
            });

        Assert.Equal(ApplicationStageStatus.InterviewActive, await Stage(uow, app));
    }

    [Fact]
    public async Task Phien_da_dong_thi_cho_chot_ket_qua()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewSession
            {
                ApplicationId = app.Id, RoundNumber = 1, SessionType = "real",
                Status = InterviewSessionStatuses.Completed,
            });

        Assert.Equal(ApplicationStageStatus.InterviewDone, await Stage(uow, app));
    }

    [Fact]
    public async Task Phien_dang_chay_thang_ca_lich_da_qua_gio()
    {
        // Có phiên nghĩa là ứng viên ĐÃ có mặt — không được gọi là "không dự".
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddHours(-1));
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id))
            .Seed(new InterviewSession
            {
                ApplicationId = app.Id, RoundNumber = 1, SessionType = "real",
                Status = InterviewSessionStatuses.Active,
            });

        Assert.Equal(ApplicationStageStatus.InterviewActive, await Stage(uow, app));
    }

    // ---------- Hồ sơ đã đóng ----------

    [Theory]
    [InlineData("hired")]
    [InlineData("not_pass")]
    [InlineData("cv_rejected")]
    public async Task Ho_so_da_dong_thi_giu_nguyen_trang_thai_phieu(string status)
    {
        // Không còn "việc đang diễn ra" nào để mô tả — trả lại chính trạng thái phễu.
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: status);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        Assert.Equal(status, await Stage(uow, app));
    }
}
