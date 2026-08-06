using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// Bảng giám sát phiên phỏng vấn cho HR (UC-84/90, <c>InterviewService.GetSessionsForHrAsync</c>): ẩn buổi thử,
/// sắp mới nhất trước, join tên ứng viên/vị trí + verdict AI mới nhất theo (hồ sơ, vòng) + cờ có video.
/// </summary>
public class HrSessionsTests
{
    private static Task<System.Collections.Generic.List<ARI.Application.DTOs.HrInterviewSessionItem>> Run(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService()).GetSessionsForHrAsync(CancellationToken.None);

    [Fact]
    public async Task Excludes_practice_sessions()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(EvaluationData.Session(app.Id, type: "real"))
            .Seed(EvaluationData.Session(app.Id, type: "practice"));

        var list = await Run(uow);

        Assert.Equal("real", Assert.Single(list).SessionType); // buổi thử ẩn khỏi HR (ADR-051)
    }

    [Fact]
    public async Task Ordered_newest_first()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var older = EvaluationData.Session(app.Id, round: 1, createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = EvaluationData.Session(app.Id, round: 2, createdAt: DateTimeOffset.UtcNow);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(older, newer);

        var list = await Run(uow);

        Assert.Equal(newer.Id, list[0].Id);
    }

    [Fact]
    public async Task Joins_candidate_job_and_latest_verdict()
    {
        var job = EvaluationData.Job("Backend Developer");
        var app = EvaluationData.App(job.Id, name: "Phạm D");
        var session = EvaluationData.Session(app.Id, round: 1);
        var eval = EvaluationData.Eval(app.Id, round: 1, verdict: "pass");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(eval);

        var item = Assert.Single(await Run(uow));

        Assert.Equal("Phạm D", item.CandidateName);
        Assert.Equal("Backend Developer", item.JobTitle);
        Assert.Equal(eval.Id, item.EvaluationId);
        Assert.Equal("pass", item.Verdict);
    }

    [Fact]
    public async Task Has_recording_flag_reflects_stored_video()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(EvaluationData.Session(app.Id, recordingUrl: "rec/x.webm"));

        var item = Assert.Single(await Run(uow));

        Assert.True(item.HasRecording);
    }

    [Fact]
    public async Task Empty_when_no_sessions()
    {
        var list = await Run(new InMemoryUnitOfWork());

        Assert.Empty(list);
    }
}
