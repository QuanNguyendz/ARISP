using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Thêm câu hỏi vào ngân hàng (<see cref="CreateOnlineTestQuestionCommandHandler"/>): chốt cứng các luật
/// validate (nội dung, 2–6 phương án, ≥1 đáp án đúng nằm trong danh sách, single = đúng 1),
/// chuẩn hoá đáp án (distinct + sort, đồng bộ CorrectOption legacy), và phân quyền chủ tin.
/// </summary>
public class CreateOnlineTestQuestionCommandHandlerTests
{
    private static UpsertOnlineTestQuestionRequest Req(
        string text = "1 + 1 = ?",
        List<string>? options = null,
        string type = "single",
        List<int>? correct = null) => new()
    {
        QuestionText = text,
        Options = options ?? new() { "1", "2", "3", "4" },
        QuestionType = type,
        CorrectOptions = correct ?? new() { 1 },
    };

    private static Task<Result<OnlineTestQuestionDto>> Run(InMemoryUnitOfWork uow, CreateOnlineTestQuestionCommand cmd)
        => new CreateOnlineTestQuestionCommandHandler(uow).Handle(cmd, CancellationToken.None);

    // ---------- Validate ----------

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return new object[] { Req(text: "   "), "trống" };
        yield return new object[] { Req(options: new() { "chỉ một" }), "ít nhất 2" };
        yield return new object[] { Req(options: Enumerable.Range(1, 7).Select(i => i.ToString()).ToList(), correct: new() { 0 }), "tối đa 6" };
        yield return new object[] { Req(correct: new List<int>()), "ít nhất 1 đáp án" };
        yield return new object[] { Req(correct: new() { 9 }), "ngoài danh sách" };
        yield return new object[] { Req(type: "single", correct: new() { 0, 1 }), "đúng 1 đáp án" };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_question_is_rejected_before_touching_repo(UpsertOnlineTestQuestionRequest req, string expectedFragment)
    {
        var uow = new InMemoryUnitOfWork(); // rỗng: validate chặn trước khi chạm repo
        var res = await Run(uow, new CreateOnlineTestQuestionCommand(Guid.NewGuid(), req, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Contains(expectedFragment, res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ---------- Chuẩn hoá + persist ----------

    [Fact]
    public async Task Valid_multiple_question_is_normalized_and_persisted()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var req = Req(type: "multiple", correct: new() { 2, 0, 2 }); // trùng + chưa sort
        var res = await Run(uow, new CreateOnlineTestQuestionCommand(job.Id, req, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(new List<int> { 0, 2 }, res.Value.CorrectOptions); // distinct + sort
        Assert.Equal("multiple", res.Value.QuestionType);

        var saved = Assert.Single(uow.Repo<OnlineTestQuestion>().Items);
        Assert.Equal("[0,2]", saved.CorrectOptions);
        Assert.Equal(0, saved.CorrectOption); // legacy = phần tử đầu
        Assert.Equal(job.Id, saved.JobPostingId);
    }

    [Fact]
    public async Task Admin_can_add_to_any_job()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new CreateOnlineTestQuestionCommand(job.Id, Req(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new CreateOnlineTestQuestionCommand(job.Id, Req(), Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Repo<OnlineTestQuestion>().Items);
    }

    [Fact]
    public async Task Missing_job_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, new CreateOnlineTestQuestionCommand(Guid.NewGuid(), Req(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }
}
