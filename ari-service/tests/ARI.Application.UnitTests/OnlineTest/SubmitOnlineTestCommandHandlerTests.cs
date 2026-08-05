using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Luồng CHÍNH của Online Test: nộp bài + tự chấm (<see cref="SubmitOnlineTestCommandHandler"/>).
/// Chốt cứng công thức chấm (khớp hoàn toàn tập đáp án, làm tròn 2 số, điểm sàn inclusive),
/// chỉ chấm đúng bộ đề đã bốc, cùng các cổng chặn: 1 lượt/vòng, CV chưa duyệt, đã rút, ngân hàng rỗng, phân quyền.
/// </summary>
public class SubmitOnlineTestCommandHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private const string Email = "cand@example.io";

    private SubmitOnlineTestCommand Cmd(Guid appId, Dictionary<Guid, List<int>> answers)
        => new(appId, _accountId, Email, answers);

    private static Task<Result<ARI.Application.DTOs.OnlineTestResultDto>> Run(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, SubmitOnlineTestCommand cmd)
        => new SubmitOnlineTestCommandHandler(uow, notif).Handle(cmd, CancellationToken.None);

    // ---------- Công thức chấm ----------

    [Fact]
    public async Task All_correct_scores_100_and_passes()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var job = OnlineTestData.Job(passScore: 70);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q1 = OnlineTestData.Single(job.Id, correct: 0);
        var q2 = OnlineTestData.Single(job.Id, correct: 1);
        uow.Seed(job).Seed(app).Seed(q1, q2);

        var res = await Run(uow, notif, Cmd(app.Id, new()
        {
            [q1.Id] = new() { 0 },
            [q2.Id] = new() { 1 },
        }));

        Assert.True(res.IsSuccess);
        Assert.Equal(100m, res.Value.Score);
        Assert.True(res.Value.IsPassed);
        Assert.Equal(2, res.Value.CorrectCount);
        Assert.Equal(2, res.Value.TotalQuestions);
        Assert.Equal(70, res.Value.PassScore);
        Assert.Single(uow.Repo<OnlineTestSubmission>().Items);
    }

    [Fact]
    public async Task Score_is_rounded_to_two_decimals_and_below_threshold_fails()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job(passScore: 70);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q1 = OnlineTestData.Single(job.Id, 0);
        var q2 = OnlineTestData.Single(job.Id, 0);
        var q3 = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q1, q2, q3);

        // 2/3 đúng → 66.67, dưới điểm sàn 70 → trượt.
        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, new()
        {
            [q1.Id] = new() { 0 },
            [q2.Id] = new() { 0 },
            [q3.Id] = new() { 1 },
        }));

        Assert.True(res.IsSuccess);
        Assert.Equal(66.67m, res.Value.Score);
        Assert.False(res.Value.IsPassed);
        Assert.Equal(2, res.Value.CorrectCount);
        Assert.Equal(3, res.Value.TotalQuestions);
    }

    [Fact]
    public async Task Pass_threshold_is_inclusive()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job(passScore: 50);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q1 = OnlineTestData.Single(job.Id, 0);
        var q2 = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q1, q2);

        // Đúng đúng 1/2 = 50.00 = điểm sàn → ĐẠT (>=).
        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, new()
        {
            [q1.Id] = new() { 0 },
            [q2.Id] = new() { 3 },
        }));

        Assert.Equal(50m, res.Value.Score);
        Assert.True(res.Value.IsPassed);
    }

    [Theory]
    [InlineData(new[] { 0, 2 }, true)]   // khớp hoàn toàn
    [InlineData(new[] { 0 }, false)]     // thiếu → sai
    [InlineData(new[] { 0, 1, 2 }, false)] // dư → sai
    [InlineData(new int[] { }, false)]   // bỏ trống → sai
    public async Task Multiple_answer_requires_exact_set_match(int[] picked, bool expectCorrect)
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job(passScore: 50);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q = OnlineTestData.Question(job.Id, "multiple", new[] { 0, 2 });
        uow.Seed(job).Seed(app).Seed(q);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q.Id] = picked.ToList() }));

        Assert.True(res.IsSuccess);
        Assert.Equal(expectCorrect ? 1 : 0, res.Value.CorrectCount);
        Assert.Equal(expectCorrect ? 100m : 0m, res.Value.Score);
    }

    [Fact]
    public async Task Unanswered_questions_count_as_wrong()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job(passScore: 70);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q1 = OnlineTestData.Single(job.Id, 0);
        var q2 = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q1, q2);

        // Chỉ trả lời q1, bỏ hẳn q2 (không có key) → q2 tính sai.
        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q1.Id] = new() { 0 } }));

        Assert.Equal(1, res.Value.CorrectCount);
        Assert.Equal(2, res.Value.TotalQuestions);
        Assert.Equal(50m, res.Value.Score);
        Assert.False(res.Value.IsPassed);
    }

    [Fact]
    public async Task Only_the_drawn_subset_is_graded()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job(passScore: 70, perTest: 2); // chỉ bốc 2/5 câu
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var bank = Enumerable.Range(0, 5).Select(_ => OnlineTestData.Single(job.Id, 0)).ToArray();
        uow.Seed(job).Seed(app).Seed(bank);

        // Trả lời đúng TẤT CẢ câu trong ngân hàng, nhưng chỉ 2 câu được bốc mới được chấm.
        var answers = bank.ToDictionary(q => q.Id, _ => new List<int> { 0 });
        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, answers));

        Assert.Equal(2, res.Value.TotalQuestions); // = perTest, không phải 5
        Assert.Equal(2, res.Value.CorrectCount);
        Assert.Equal(100m, res.Value.Score);
    }

    // ---------- Cổng chặn ----------

    [Fact]
    public async Task Second_submission_same_round_conflicts()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job();
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q)
           .Seed(new OnlineTestSubmission { ApplicationId = app.Id, RoundNumber = 1, Score = 80m, IsPassed = true });

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Single(uow.Repo<OnlineTestSubmission>().Items); // không thêm bản ghi mới
    }

    [Theory]
    [InlineData("cv_submitted")]
    [InlineData("cv_rejected")]
    public async Task Cv_not_passed_is_blocked(string status)
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job();
        var app = OnlineTestData.Application(job.Id, _accountId, status: status, email: Email);
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.True(res.IsFailure);
        Assert.Contains("CV", res.Error);
        Assert.Empty(uow.Repo<OnlineTestSubmission>().Items);
    }

    [Fact]
    public async Task Withdrawn_application_is_blocked()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job();
        var app = OnlineTestData.Application(job.Id, _accountId, status: "withdrawn", email: Email);
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.True(res.IsFailure);
        Assert.Contains("rút", res.Error);
    }

    [Fact]
    public async Task Empty_question_bank_returns_failure()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job();
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        uow.Seed(job).Seed(app); // không có câu hỏi

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, new()));

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<OnlineTestSubmission>().Items);
    }

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, new RecordingNotificationService(), Cmd(Guid.NewGuid(), new()));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Candidate_not_owning_application_is_forbidden()
    {
        var uow = new InMemoryUnitOfWork();
        var job = OnlineTestData.Job();
        // Hồ sơ thuộc account KHÁC + email khác → account đăng nhập không có quyền.
        var app = OnlineTestData.Application(job.Id, Guid.NewGuid(), email: "owner@example.io");
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Side-effects ----------

    [Fact]
    public async Task Successful_submit_notifies_candidate_and_staff()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var job = OnlineTestData.Job(passScore: 70);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q);

        await Run(uow, notif, Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId);            // chuông ứng viên
        Assert.Contains(notif.UserEvents, e => e.UserId == job.CreatedByUserId);   // recruiter chủ tin
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin");            // nhóm HR
    }

    [Fact]
    public async Task Notification_failure_does_not_break_submit()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService { ThrowOnPublish = true };
        var job = OnlineTestData.Job(passScore: 70);
        var app = OnlineTestData.Application(job.Id, _accountId, email: Email);
        var q = OnlineTestData.Single(job.Id, 0);
        uow.Seed(job).Seed(app).Seed(q);

        var res = await Run(uow, notif, Cmd(app.Id, new() { [q.Id] = new() { 0 } }));

        Assert.True(res.IsSuccess); // best-effort: lỗi SignalR không làm hỏng việc nộp bài
        Assert.Single(uow.Repo<OnlineTestSubmission>().Items);
    }
}
