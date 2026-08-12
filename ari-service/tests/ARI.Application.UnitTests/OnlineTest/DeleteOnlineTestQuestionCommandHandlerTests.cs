using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Xoá câu hỏi khỏi ngân hàng (<see cref="DeleteOnlineTestQuestionCommandHandler"/>, test-plan B22):
/// id lạ → NotFound; không phải chủ tin → Forbidden (giữ câu hỏi); chủ tin hoặc HrAdmin → xoá thật.
/// </summary>
public class DeleteOnlineTestQuestionCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow, DeleteOnlineTestQuestionCommand cmd)
        => new DeleteOnlineTestQuestionCommandHandler(uow).Handle(cmd, CancellationToken.None);

    [Fact]
    public async Task Unknown_question_returns_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(),
            new DeleteOnlineTestQuestionCommand(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden_and_question_stays()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var question = OnlineTestData.Single(job.Id, correct: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(question);

        var res = await Run(uow, new DeleteOnlineTestQuestionCommand(question.Id, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền xoá câu hỏi này", res.Error);
        Assert.Single(uow.Repo<OnlineTestQuestion>().Items);  // vẫn còn
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Owner_can_delete()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var question = OnlineTestData.Single(job.Id, correct: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(question);

        var res = await Run(uow, new DeleteOnlineTestQuestionCommand(question.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<OnlineTestQuestion>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Admin_can_delete_any_job_question()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var question = OnlineTestData.Single(job.Id, correct: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(question);

        var res = await Run(uow, new DeleteOnlineTestQuestionCommand(question.Id, Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<OnlineTestQuestion>().Items);
    }
}
