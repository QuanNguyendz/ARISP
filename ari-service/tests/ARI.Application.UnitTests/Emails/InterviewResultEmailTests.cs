using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Emails;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Emails;

/// <summary>
/// Thư kết quả vòng phỏng vấn (ADR-074): bản xem trước trong trình soạn dựng bằng CÙNG builder + CÙNG hàm suy
/// biến thể với lệnh chốt, nên thư HM sửa đúng là thư ứng viên nhận; báo cáo phải thuộc hồ sơ đang xem.
/// </summary>
public class InterviewResultEmailTests
{
    private static (InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app, Evaluation eval) Seed(int round, int totalRounds)
    {
        var job = new JobPosting { Id = Guid.NewGuid(), Title = "Backend Developer", CreatedByUserId = Guid.NewGuid() };
        var app = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "cand@corp.io", CandidateName = "Nguyen Van A",
            Status = "interview",
        };
        var eval = new Evaluation { Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = round, SessionType = "real", AiVerdict = "pass" };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);
        for (var r = 1; r <= totalRounds; r++)
            uow.Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = r, RoundType = "technical" });
        return (uow, app, eval);
    }

    private static EmailTemplateRenderer Renderer(InMemoryUnitOfWork uow)
        => new(uow, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:CandidateBaseUrl"] = "https://jobs.corp.io/" })
            .Build());

    [Theory]
    [InlineData("pass", 1, 1, InterviewResultEmail.Variants.FinalPass)]
    [InlineData("pass", 2, 3, InterviewResultEmail.Variants.NextRound)]
    [InlineData("pass", 3, 3, InterviewResultEmail.Variants.FinalPass)]
    [InlineData("not_pass", 1, 3, InterviewResultEmail.Variants.NotPass)]
    [InlineData(null, 1, 1, InterviewResultEmail.Variants.NotPass)]
    public void Variant_follows_the_same_rule_as_the_application_status(
        string? verdict, int round, int total, string expected)
        => Assert.Equal(expected, InterviewResultEmail.ResolveVariant(verdict, round, total));

    [Fact]
    public async Task Preview_of_a_middle_round_pass_is_the_next_round_email_with_a_portal_link()
    {
        var (uow, app, eval) = Seed(round: 1, totalRounds: 2);

        var res = await Renderer(uow).RenderAsync(EmailTemplateKeys.InterviewResult, app.Id, eval.Id, "pass", CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Contains("qua vòng 1", res.Value.Subject);
        Assert.Contains("vòng phỏng vấn số 2", res.Value.Html);
        Assert.Contains($"https://jobs.corp.io/candidate/applications/{app.Id}", res.Value.Html);
        Assert.Equal("cand@corp.io", res.Value.ToEmail);
        Assert.DoesNotContain("{{", res.Value.Html);   // không còn placeholder nào trong bản người dùng sửa
    }

    [Fact]
    public async Task Preview_of_a_final_round_pass_announces_the_offer()
    {
        var (uow, app, eval) = Seed(round: 2, totalRounds: 2);

        var res = await Renderer(uow).RenderAsync(EmailTemplateKeys.InterviewResult, app.Id, eval.Id, "pass", CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Contains("Offer Letter", res.Value.Html);
    }

    [Fact]
    public async Task Evaluation_of_another_application_is_refused()
    {
        // Quyền được kiểm trên HỒ SƠ (contextId); báo cáo lấy từ hồ sơ khác thì không được dùng làm ngữ cảnh.
        var (uow, app, _) = Seed(round: 1, totalRounds: 1);
        var foreign = new Evaluation { Id = Guid.NewGuid(), ApplicationId = Guid.NewGuid(), RoundNumber = 1 };
        uow.Seed(foreign);

        var res = await Renderer(uow).RenderAsync(EmailTemplateKeys.InterviewResult, app.Id, foreign.Id, "pass", CancellationToken.None);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Unknown_verdict_is_refused()
    {
        var (uow, app, eval) = Seed(round: 1, totalRounds: 1);

        var res = await Renderer(uow).RenderAsync(EmailTemplateKeys.InterviewResult, app.Id, eval.Id, "maybe", CancellationToken.None);

        Assert.True(res.IsFailure);
    }
}
