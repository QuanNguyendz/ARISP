using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Cổng chặn ngân hàng đề: tin có vòng <c>online_test</c> không được rời bản nháp (gửi duyệt hoặc
/// đăng thẳng) khi ngân hàng đề chưa đủ số câu mỗi lượt thi — nếu không ứng viên sẽ bấm vào một
/// bài thi rỗng. Tin KHÔNG có vòng trắc nghiệm phải đi qua bình thường.
/// </summary>
public class OnlineTestBankGateTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, Guid jobId, string status, Guid userId, string? role)
        => new UpdateJobStatusCommandHandler(
                uow, new RecordingFileStorage(), new RecordingJdStampService(), new StubDocumentParser(),
                new RecordingNotificationService(), new RecordingEmailService(),
                NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, JobPostingData.StatusRequest(status), userId, role), CancellationToken.None);

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
}
