using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Dashboard.Queries.GetHrDashboard;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.ApplicationFlow;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Dashboard;

/// <summary>
/// Tổng quan tuyển dụng cho HR (<see cref="GetHrDashboardQueryHandler"/>, test-plan B23): KPI + phễu 5 bước +
/// xu hướng 14 ngày khi rỗng; đếm active/draft/pending + project 'closed' cho tin quá hạn; Hired
/// (case-insensitive) & PendingReviews; ứng viên gần đây (6, mới nhất trước) kèm match score + vòng/verdict.
/// </summary>
public class GetHrDashboardQueryHandlerTests
{
    private static Task<Result<HrDashboardResponse>> Run(InMemoryUnitOfWork uow)
        => new GetHrDashboardQueryHandler(uow).Handle(new GetHrDashboardQuery(), CancellationToken.None);

    private static Evaluation Eval(Guid appId, int round, string verdict)
        => new() { ApplicationId = appId, SessionId = Guid.NewGuid(), RoundNumber = round, AiVerdict = verdict };

    [Fact]
    public async Task Empty_repositories_yield_all_zero_metrics()
    {
        var res = await Run(new InMemoryUnitOfWork());

        Assert.True(res.IsSuccess);
        var d = res.Value;
        Assert.Equal(0, d.ActiveJobs);
        Assert.Equal(0, d.DraftJobs);
        Assert.Equal(0, d.TotalApplications);
        Assert.Equal(0, d.AiInterviews);
        Assert.Equal(0, d.PendingReviews);
        Assert.Equal(0, d.Hired);
        Assert.Equal(5, d.Funnel.Count);                          // phễu 5 bước
        Assert.All(d.Funnel, f => Assert.Equal(0, f.Value));
        Assert.Equal(14, d.Analytics.Trend.Count);                // xu hướng 14 ngày
        Assert.All(d.Analytics.Trend, t => Assert.Equal(0, t.Count));
        Assert.Empty(d.TopJobs);
        Assert.Empty(d.RecentCandidates);
        Assert.Equal(0, d.PendingJobsCount);
        Assert.Empty(d.PendingJobs);
    }

    [Fact]
    public async Task Job_states_are_counted_and_expired_active_projects_as_closed()
    {
        var now = DateTimeOffset.UtcNow;
        var jobA = ApplicationData.Job(status: "active", deadline: now.AddDays(1));   // còn hạn
        var jobB = ApplicationData.Job(status: "active", deadline: now.AddDays(-1));  // quá hạn → closed
        var jobC = ApplicationData.Job(status: "draft");
        var jobD = ApplicationData.Job(status: "pending");
        var uow = new InMemoryUnitOfWork().Seed(jobA, jobB, jobC, jobD);

        var d = (await Run(uow)).Value;

        Assert.Equal(1, d.ActiveJobs);          // chỉ A (B quá hạn không tính)
        Assert.Equal(1, d.DraftJobs);
        Assert.Equal(1, d.PendingJobsCount);
        Assert.Equal(jobD.Id, Assert.Single(d.PendingJobs).Id);

        Assert.Equal("closed", d.TopJobs.Single(t => t.Id == jobB.Id).Status); // project closed
        Assert.Equal("active", d.TopJobs.Single(t => t.Id == jobA.Id).Status);
    }

    [Fact]
    public async Task Hired_is_case_insensitive_and_pending_reviews_excludes_reviewed()
    {
        var e1 = Eval(Guid.NewGuid(), 1, "pass");
        var e2 = Eval(Guid.NewGuid(), 1, "not_pass");
        var e3 = Eval(Guid.NewGuid(), 1, "pass");
        var review = new ARI.Domain.Entities.HrReview { EvaluationId = e1.Id, FinalVerdict = "Pass" }; // hoa
        var uow = new InMemoryUnitOfWork().Seed(e1, e2, e3).Seed(review);

        var d = (await Run(uow)).Value;

        Assert.Equal(1, d.Hired);            // "Pass" khớp "pass" (case-insensitive)
        Assert.Equal(2, d.PendingReviews);   // 3 eval − 1 đã review
    }

    [Fact]
    public async Task Recent_candidates_take_latest_six_with_match_score_and_latest_round()
    {
        var job = ApplicationData.Job();
        var now = DateTimeOffset.UtcNow;
        var apps = new List<ARI.Domain.Entities.Application>();
        for (int i = 0; i < 8; i++)
        {
            var app = ApplicationData.Application(job.Id);
            app.CreatedAt = now.AddMinutes(-i); // i=0 mới nhất
            apps.Add(app);
        }

        // Hồ sơ mới nhất (i=0): có phân tích CV–JD điểm 88, không có evaluation.
        var analysis = ApplicationData.Analysis(job.Id, cvHash: "h", score: 88);
        apps[0].CvJdAnalysisId = analysis.Id;

        // Hồ sơ i=1: 2 vòng eval, vòng 2 pass; không có phân tích.
        var ev1 = Eval(apps[1].Id, 1, "not_pass");
        var ev2 = Eval(apps[1].Id, 2, "pass");

        var uow = new InMemoryUnitOfWork().Seed(job).Seed(analysis).Seed(apps.ToArray()).Seed(ev1, ev2);

        var d = (await Run(uow)).Value;

        Assert.Equal(6, d.RecentCandidates.Count);                 // Take(6)
        Assert.Equal(apps[0].Id, d.RecentCandidates[0].Id);        // mới nhất trước

        var withAnalysis = d.RecentCandidates.Single(c => c.Id == apps[0].Id);
        Assert.Equal(88, withAnalysis.MatchScore);
        Assert.Null(withAnalysis.LatestRound);                     // không có eval

        var withEvals = d.RecentCandidates.Single(c => c.Id == apps[1].Id);
        Assert.Equal(2, withEvals.LatestRound);                    // vòng cao nhất
        Assert.Equal("pass", withEvals.LatestVerdict);
        Assert.Null(withEvals.MatchScore);                         // không phân tích → null
    }
}
