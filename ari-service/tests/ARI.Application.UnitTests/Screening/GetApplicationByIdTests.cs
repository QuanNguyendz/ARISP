using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Chi tiết ứng viên khi sàng lọc (UC-54, <see cref="ApplicationService.GetApplicationByIdAsync"/>): trả CvText đầy đủ,
/// nạp CvJdAnalysis liên kết để lấy điểm match, suy vòng hiện tại, giờ hẹn + trạng thái xác nhận/lý do báo bận.
/// </summary>
public class GetApplicationByIdTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    [Fact]
    public async Task Not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork()).GetApplicationByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Application not found", res.Error);
    }

    [Fact]
    public async Task Detail_returns_cv_text_and_job_title()
    {
        var job = ScreeningData.Job(title: "Data Engineer");
        var app = ScreeningData.App(job.Id, cvText: "Kinh nghiệm ETL, Spark");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Svc(uow).GetApplicationByIdAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Kinh nghiệm ETL, Spark", res.Value!.CvText); // chi tiết có CvText (khác danh sách)
        Assert.Equal("Data Engineer", res.Value.JobTitle);
    }

    [Fact]
    public async Task Detail_loads_linked_analysis_for_match_score()
    {
        var job = ScreeningData.Job();
        var analysis = ScreeningData.Analysis(job.Id, score: 91, summary: "Ứng viên mạnh");
        var app = ScreeningData.App(job.Id, analysisId: analysis.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(analysis).Seed(app);

        var res = await Svc(uow).GetApplicationByIdAsync(app.Id, CancellationToken.None);

        Assert.Equal(91, res.Value!.MatchScore); // CvJdAnalysis null → được nạp thêm
        Assert.Equal("Ứng viên mạnh", res.Value.CvJdSummary);
    }

    [Fact]
    public async Task Detail_surfaces_scheduled_date_and_confirmation_status()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "screening"); // round 1
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var slot = ScreeningData.Slot(job.Id, start, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(ScreeningData.Booking(app.Id, slot.Id, round: 1, status: "scheduled", confirmationStatus: "confirmed"));

        var res = await Svc(uow).GetApplicationByIdAsync(app.Id, CancellationToken.None);

        Assert.True(res.Value!.HasScheduledInterview);
        Assert.Equal(start, res.Value.InterviewDate);
        Assert.Equal("confirmed", res.Value.ScheduleConfirmationStatus);
        Assert.Null(res.Value.ScheduleDeclineReason);
    }

    [Fact]
    public async Task Detail_surfaces_decline_reason_when_no_scheduled_booking()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "screening"); // round 1
        var slot = ScreeningData.Slot(job.Id, DateTimeOffset.UtcNow.AddDays(1), round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(ScreeningData.Booking(app.Id, slot.Id, round: 1, status: "declined",
                confirmationStatus: "declined", declineReason: "Bận công việc", respondedAt: DateTimeOffset.UtcNow));

        var res = await Svc(uow).GetApplicationByIdAsync(app.Id, CancellationToken.None);

        Assert.False(res.Value!.HasScheduledInterview);
        Assert.Null(res.Value.InterviewDate);
        Assert.Null(res.Value.ScheduleConfirmationStatus);
        Assert.Equal("Bận công việc", res.Value.ScheduleDeclineReason); // đưa lý do lên cho nhân sự xếp lại
    }

    [Fact]
    public async Task Detail_computes_round_from_invites_for_interview_status()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, status: "interview");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(ScreeningData.Invite(app.Id, 2));

        var res = await Svc(uow).GetApplicationByIdAsync(app.Id, CancellationToken.None);

        Assert.Equal(2, res.Value!.CurrentRound);
    }
}
