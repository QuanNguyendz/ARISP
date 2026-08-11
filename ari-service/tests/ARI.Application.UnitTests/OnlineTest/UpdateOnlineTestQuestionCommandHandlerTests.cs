using System;
using System.Collections.Generic;
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
/// Sửa câu hỏi trong ngân hàng (<see cref="UpdateOnlineTestQuestionCommandHandler"/>, test-plan B5):
/// validate request TRƯỚC khi tra câu hỏi, phân quyền theo chủ tin của câu hỏi, và chuẩn hoá
/// đáp án (trim nội dung, distinct + sort, đồng bộ <c>CorrectOption</c> legacy, chạm <c>UpdatedAt</c>).
/// </summary>
public class UpdateOnlineTestQuestionCommandHandlerTests
{
    private static UpsertOnlineTestQuestionRequest Req(
        string text = "Sửa?", List<string>? options = null, string type = "single", List<int>? correct = null) => new()
    {
        QuestionText = text,
        Options = options ?? new() { "A", "B", "C", "D" },
        QuestionType = type,
        CorrectOptions = correct ?? new() { 1 },
    };

    private static Task<Result<OnlineTestQuestionDto>> Run(InMemoryUnitOfWork uow, UpdateOnlineTestQuestionCommand cmd)
        => new UpdateOnlineTestQuestionCommandHandler(uow).Handle(cmd, CancellationToken.None);

    [Fact]
    public async Task Invalid_request_is_rejected_before_fetching_the_question()
    {
        // uow rỗng: nếu KHÔNG validate trước thì GetById trả null → "Không tìm thấy câu hỏi".
        var uow = new InMemoryUnitOfWork();
        var req = Req(type: "single", correct: new() { 0, 1 }); // single mà 2 đáp án đúng

        var res = await Run(uow, new UpdateOnlineTestQuestionCommand(Guid.NewGuid(), req, Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Contains("đúng 1 đáp án", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Non_owner_recruiter_cannot_edit_and_nothing_changes()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var question = OnlineTestData.Single(job.Id, correct: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(question);

        var res = await Run(uow, new UpdateOnlineTestQuestionCommand(question.Id, Req(), Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền sửa câu hỏi này", res.Error);
        Assert.Equal("Câu hỏi mẫu?", question.QuestionText); // giữ nguyên
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Owner_edit_trims_text_normalizes_correct_options_and_stamps_updated_at()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var question = OnlineTestData.Single(job.Id, correct: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(question);
        var before = DateTimeOffset.UtcNow;

        var req = Req(text: "  Edited?  ", type: "multiple", correct: new() { 2, 0, 2 }); // trùng + chưa sort
        var res = await Run(uow, new UpdateOnlineTestQuestionCommand(question.Id, req, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal("Edited?", res.Value.QuestionText);                 // trim
        Assert.Equal(new List<int> { 0, 2 }, res.Value.CorrectOptions);  // distinct + sort
        Assert.Equal("multiple", res.Value.QuestionType);

        Assert.Equal("Edited?", question.QuestionText);
        Assert.Equal("[0,2]", question.CorrectOptions);
        Assert.Equal(0, question.CorrectOption);                         // legacy = phần tử đầu
        Assert.True(question.UpdatedAt >= before);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
