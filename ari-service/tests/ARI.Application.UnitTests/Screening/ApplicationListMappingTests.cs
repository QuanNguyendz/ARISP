using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Làm giàu dữ liệu danh sách ứng viên (UC-53/54 — bảng sàng lọc): vòng hiện tại suy từ trạng thái + invite/session,
/// điểm phỏng vấn từ Evaluation "real", cờ đã đặt lịch + giờ hẹn từ InterviewBooking/AvailabilitySlot.
/// Chạy qua <see cref="ApplicationService.GetApplicationsByJobAsync"/> (đường dùng chung <c>MapApplications</c>).
/// </summary>
public class ApplicationListMappingTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    private static async Task<ARI.Application.DTOs.ApplicationResponse> MapOne(InMemoryUnitOfWork uow, Guid jobId)
        => Assert.Single((await Svc(uow).GetApplicationsByJobAsync(jobId, CancellationToken.None)).Value!);

    [Fact]
    public async Task Cv_submitted_has_no_current_round()
    {
        var job = ScreeningData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, status: "cv_submitted"));

        var dto = await MapOne(uow, job.Id);

        Assert.Null(dto.CurrentRound);
    }

    [Fact]
    public async Task Screening_status_sets_round_1()
    {
        var job = ScreeningData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, status: "screening"));

        var dto = await MapOne(uow, job.Id);

        Assert.Equal(1, dto.CurrentRound);
    }

    [Fact]
    public async Task Interview_status_uses_highest_invite_or_session_round()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(ScreeningData.Invite(app.Id, 1), ScreeningData.Invite(app.Id, 2))
            .Seed(ScreeningData.Session(app.Id, 3));

        var dto = await MapOne(uow, job.Id);

        Assert.Equal(3, dto.CurrentRound); // max(invite 2, session 3)
    }

    [Fact]
    public async Task Scheduled_booking_sets_flag_and_interview_date()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "screening"); // round 1
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var slot = ScreeningData.Slot(job.Id, start, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(ScreeningData.Booking(app.Id, slot.Id, round: 1, status: "scheduled"));

        var dto = await MapOne(uow, job.Id);

        Assert.True(dto.HasScheduledInterview);
        Assert.Equal(start, dto.InterviewDate);
    }

    [Fact]
    public async Task Cancelled_booking_does_not_set_scheduled_flag()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "screening");
        var slot = ScreeningData.Slot(job.Id, DateTimeOffset.UtcNow.AddDays(1));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(ScreeningData.Booking(app.Id, slot.Id, status: "cancelled"));

        var dto = await MapOne(uow, job.Id);

        Assert.False(dto.HasScheduledInterview); // chỉ tính booking "scheduled"
        Assert.Null(dto.InterviewDate);
    }

    [Fact]
    public async Task Real_evaluation_score_maps_to_interview_score()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(ScreeningData.Invite(app.Id, 1))
            .Seed(ScreeningData.Eval(app.Id, round: 1, score: 77m, sessionType: "real"));

        var dto = await MapOne(uow, job.Id);

        Assert.Equal(77m, dto.InterviewScore);
    }

    [Fact]
    public async Task Practice_evaluation_is_ignored_for_interview_score()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(ScreeningData.Invite(app.Id, 1))
            .Seed(ScreeningData.Eval(app.Id, round: 1, score: 99m, sessionType: "practice"));

        var dto = await MapOne(uow, job.Id);

        Assert.Null(dto.InterviewScore); // chỉ lấy điểm phiên "real"
    }
}
