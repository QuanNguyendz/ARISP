using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Ngân hàng câu hỏi + cấu hình cho staff (<see cref="GetOnlineTestBankQueryHandler"/>, test-plan B21):
/// phân quyền chủ tin, và DTO đầy đủ setting (điểm sàn/số câu/thời lượng) + câu hỏi sort theo CreatedAt,
/// loại rỗng → 'single', LỘ đáp án đúng cho staff.
/// </summary>
public class GetOnlineTestBankQueryHandlerTests
{
    private static Task<Result<OnlineTestBankDto>> Run(InMemoryUnitOfWork uow, GetOnlineTestBankQuery q)
        => new GetOnlineTestBankQueryHandler(uow).Handle(q, CancellationToken.None);

    [Fact]
    public async Task Missing_job_returns_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(),
            new GetOnlineTestBankQuery(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new GetOnlineTestBankQuery(job.Id, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền quản lý câu hỏi", res.Error);
    }

    [Fact]
    public async Task Bank_returns_settings_and_questions_sorted_with_answers_exposed()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(passScore: 70, perTest: 50, owner: owner); // DurationMinutes = 30

        var older = OnlineTestData.Question(job.Id, type: "single", correct: new[] { 1 });
        older.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var newerBlank = OnlineTestData.Question(job.Id, type: "", correct: new[] { 0, 2 }); // loại để trống
        newerBlank.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        // Seed đảo thứ tự để chứng minh sort theo CreatedAt (không phải thứ tự chèn).
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(newerBlank, older);

        var res = await Run(uow, new GetOnlineTestBankQuery(job.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        var dto = res.Value;
        Assert.Equal(job.Title, dto.JobTitle);
        Assert.Equal(70, dto.PassScore);
        Assert.Equal(50, dto.QuestionsPerTest);
        Assert.Equal(30, dto.DurationMinutes);

        Assert.Equal(2, dto.Questions.Count);
        Assert.Equal("single", dto.Questions[0].QuestionType);
        Assert.Equal(new List<int> { 1 }, dto.Questions[0].CorrectOptions);         // lộ đáp án cho staff
        Assert.Equal("single", dto.Questions[1].QuestionType);                     // loại rỗng → 'single'
        Assert.Equal(new List<int> { 0, 2 }, dto.Questions[1].CorrectOptions);
    }
}
