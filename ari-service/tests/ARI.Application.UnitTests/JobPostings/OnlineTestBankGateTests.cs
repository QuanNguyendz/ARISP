using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Cổng chặn ngân hàng đề: tin có vòng <c>online_test</c> không được rời bản nháp (gửi duyệt hoặc
/// đăng thẳng) khi ngân hàng đề chưa đủ số câu mỗi lượt thi — nếu không ứng viên sẽ bấm vào một
/// bài thi rỗng. Tin KHÔNG có vòng trắc nghiệm phải đi qua bình thường. Sau khi rời bản nháp, luật được
/// giữ ở lệnh sửa cấu hình và lệnh xoá câu (<see cref="OnlineTestBankGate"/>).
/// </summary>
public class OnlineTestBankGateTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, Guid jobId, string status, Guid userId, string? role)
    {
        ARI.Application.UnitTests.CvScoring.CvScoringKit.EnsureRubricsForAllJobs(uow);
        return new UpdateJobStatusCommandHandler(
                uow, new RecordingFileStorage(), new RecordingJdStampService(), new StubDocumentParser(),
                new RecordingNotificationService(), new RecordingEmailService(),
                TestConfig.Frontend(), NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, JobPostingData.StatusRequest(status), userId, role), CancellationToken.None);
    }

    private static OnlineTestQuestion Question(Guid jobId) => new()
    {
        JobPostingId = jobId,
        QuestionText = "1 + 1 = ?",
        Options = "[\"1\",\"2\"]",
        CorrectOption = 1,
    };

    private static (InMemoryUnitOfWork Uow, JobPosting Job, Guid Owner) Seed(
        string roundType, int questionCount, int questionsPerTest = 20, string status = "draft")
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner, status: status);
        job.OnlineTestQuestionsPerTest = questionsPerTest;

        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, 1, roundType));
        for (var i = 0; i < questionCount; i++) uow.Seed(Question(job.Id));
        // ADR-068: mọi tin đều có Hiring Manager chính — gửi duyệt là gửi cho người đó.
        HiringManagerSeed.Primary(uow, job.Id, addedBy: owner);

        return (uow, job, owner);
    }

    [Fact]
    public async Task Submit_for_approval_blocked_when_bank_empty()
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 0);

        var res = await Run(uow, job.Id, "pending", owner, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("ngân hàng đề đang trống", res.Error);
        Assert.Equal("draft", uow.Repo<JobPosting>().Items[0].Status); // không đổi trạng thái
    }

    [Fact]
    public async Task Submit_for_approval_blocked_when_bank_smaller_than_questions_per_test()
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 5, questionsPerTest: 20);

        var res = await Run(uow, job.Id, "pending", owner, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("mới có 5 câu", res.Error);
        Assert.Contains("chưa đủ 20 câu", res.Error);
        // Có mã riêng: thiếu mã thì giao diện chỉ hiện "Không thể cập nhật trạng thái tin" (lỗi người dùng báo).
        Assert.Equal(OnlineTestBankGate.InsufficientCode, res.ErrorCode);
        Assert.Equal("draft", uow.Repo<JobPosting>().Items[0].Status);
    }

    [Fact]
    public async Task Submit_for_approval_passes_when_bank_has_enough_questions()
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 3, questionsPerTest: 3);

        var res = await Run(uow, job.Id, "pending", owner, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("pending", uow.Repo<JobPosting>().Items[0].Status);
    }

    [Fact]
    public async Task Job_without_online_test_round_is_not_blocked()
    {
        var (uow, job, owner) = Seed("technical", questionCount: 0);

        var res = await Run(uow, job.Id, "pending", owner, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
    }

    /// <summary>HR Leader publish thẳng từ nháp cũng phải qua cổng — nếu không sẽ lách được.</summary>
    [Fact]
    public async Task Publishing_straight_from_draft_is_blocked_too()
    {
        var (uow, job, _) = Seed("online_test", questionCount: 0);

        var res = await Run(uow, job.Id, "active", Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("ngân hàng đề đang trống", res.Error);
        Assert.Equal("draft", uow.Repo<JobPosting>().Items[0].Status);
    }

    // ===== Sau khi rời bản nháp: luật giữ ở chính các lệnh sửa ngân hàng / cấu hình =====

    private static Task<Result<OnlineTestBankDto>> SaveSettings(
        InMemoryUnitOfWork uow, JobPosting job, Guid owner, int questionsPerTest, int passScore = 70)
        => new UpdateOnlineTestSettingsCommandHandler(uow).Handle(
            new UpdateOnlineTestSettingsCommand(job.Id, passScore, questionsPerTest, null, owner, AppRoles.Recruiter),
            CancellationToken.None);

    private static Task<Result> DeleteFirstQuestion(InMemoryUnitOfWork uow, Guid owner)
        => new DeleteOnlineTestQuestionCommandHandler(uow).Handle(
            new DeleteOnlineTestQuestionCommand(uow.Repo<OnlineTestQuestion>().Items[0].Id, owner, AppRoles.Recruiter),
            CancellationToken.None);

    [Theory]
    [InlineData("pending")]
    [InlineData("active")]
    [InlineData("closed")]
    public async Task Live_job_cannot_raise_questions_per_test_above_bank(string status)
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 10, questionsPerTest: 10, status: status);

        var res = await SaveSettings(uow, job, owner, questionsPerTest: 20);

        Assert.True(res.IsFailure);
        Assert.Equal(OnlineTestBankGate.InsufficientCode, res.ErrorCode);
        Assert.Contains("mới có 10 câu", res.Error);
        Assert.Equal(10, job.OnlineTestQuestionsPerTest);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Live_job_can_raise_questions_per_test_up_to_bank_size()
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 12, questionsPerTest: 10, status: "active");

        var res = await SaveSettings(uow, job, owner, questionsPerTest: 12);

        Assert.True(res.IsSuccess);
        Assert.Equal(12, job.OnlineTestQuestionsPerTest);
    }

    /// <summary>
    /// Ở bản nháp chỉ cảnh báo: khai số câu mỗi bài trước rồi mới nhập câu hỏi là thứ tự hợp lệ —
    /// cổng gửi duyệt mới chặn.
    /// </summary>
    [Theory]
    [InlineData("draft")]
    [InlineData("rejected")]
    public async Task Draft_job_may_configure_more_questions_than_bank(string status)
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 10, questionsPerTest: 10, status: status);

        var res = await SaveSettings(uow, job, owner, questionsPerTest: 20);

        Assert.True(res.IsSuccess);
        Assert.Equal(20, job.OnlineTestQuestionsPerTest);
    }

    /// <summary>
    /// Tin cũ đã lệch sẵn (20 câu/bài, ngân hàng 10) không bị khoá cứng: sửa điểm sàn hay giảm dần số câu
    /// vẫn được, chỉ TĂNG vượt ngân hàng mới bị chặn.
    /// </summary>
    [Fact]
    public async Task Live_job_already_short_can_still_edit_other_settings_and_lower_count()
    {
        var (uow, job, owner) = Seed("online_test", questionCount: 10, questionsPerTest: 20, status: "active");

        var passScoreOnly = await SaveSettings(uow, job, owner, questionsPerTest: 20, passScore: 80);
        var lowered = await SaveSettings(uow, job, owner, questionsPerTest: 15);

        Assert.True(passScoreOnly.IsSuccess);
        Assert.True(lowered.IsSuccess);
        Assert.Equal(15, job.OnlineTestQuestionsPerTest);
    }

    [Fact]
    public async Task Live_job_without_online_test_round_is_not_gated()
    {
        var (uow, job, owner) = Seed("technical", questionCount: 0, questionsPerTest: 10, status: "active");

        var res = await SaveSettings(uow, job, owner, questionsPerTest: 20);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Live_job_cannot_delete_below_questions_per_test()
    {
        var (uow, _, owner) = Seed("online_test", questionCount: 10, questionsPerTest: 10, status: "active");

        var res = await DeleteFirstQuestion(uow, owner);

        Assert.True(res.IsFailure);
        Assert.Equal(OnlineTestBankGate.InsufficientCode, res.ErrorCode);
        Assert.Equal(10, uow.Repo<OnlineTestQuestion>().Items.Count);
    }

    [Fact]
    public async Task Live_job_can_delete_surplus_question()
    {
        var (uow, _, owner) = Seed("online_test", questionCount: 11, questionsPerTest: 10, status: "active");

        var res = await DeleteFirstQuestion(uow, owner);

        Assert.True(res.IsSuccess);
        Assert.Equal(10, uow.Repo<OnlineTestQuestion>().Items.Count);
    }

    [Fact]
    public async Task Draft_job_can_delete_below_questions_per_test()
    {
        var (uow, _, owner) = Seed("online_test", questionCount: 10, questionsPerTest: 10, status: "draft");

        var res = await DeleteFirstQuestion(uow, owner);

        Assert.True(res.IsSuccess);
    }
}
