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
/// Cấu hình bài thi trắc nghiệm theo job (<see cref="UpdateOnlineTestSettingsCommandHandler"/>, test-plan B6):
/// biên các giá trị (điểm sàn 0–100, số câu 1–200, thời lượng 1–300') được kiểm TRƯỚC khi tra tin,
/// phân quyền chủ tin, và ghi 3 field + <c>UpdatedAt</c> khi hợp lệ.
/// </summary>
public class UpdateOnlineTestSettingsCommandHandlerTests
{
    private static Task<Result<OnlineTestBankDto>> Run(InMemoryUnitOfWork uow, UpdateOnlineTestSettingsCommand cmd)
        => new UpdateOnlineTestSettingsCommandHandler(uow).Handle(cmd, CancellationToken.None);

    public static IEnumerable<object[]> OutOfRange()
    {
        yield return new object[] { 101, 50, 30, "Điểm sàn" };
        yield return new object[] { -1, 50, 30, "Điểm sàn" };
        yield return new object[] { 70, 0, 30, "Số câu mỗi bài" };
        yield return new object[] { 70, 201, 30, "Số câu mỗi bài" };
        yield return new object[] { 70, 50, 0, "Thời lượng" };
        yield return new object[] { 70, 50, 301, "Thời lượng" };
    }

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public async Task Out_of_range_settings_are_rejected_before_lookup(int pass, int perTest, int duration, string fragment)
    {
        var uow = new InMemoryUnitOfWork(); // rỗng: validate chặn trước khi tra tin

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(
            Guid.NewGuid(), pass, perTest, duration, Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Contains(fragment, res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData(100, 200, 300)]
    [InlineData(0, 1, 1)]
    public async Task Owner_can_save_boundary_values(int pass, int perTest, int duration)
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(passScore: 70, perTest: 50, owner: owner); // job.DurationMinutes mặc định 30
        var uow = new InMemoryUnitOfWork().Seed(job);
        var before = DateTimeOffset.UtcNow;

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, pass, perTest, duration, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(pass, res.Value.PassScore);
        Assert.Equal(perTest, res.Value.QuestionsPerTest);
        Assert.Equal(duration, res.Value.DurationMinutes);
        Assert.Equal(pass, job.OnlineTestPassScore);
        Assert.Equal(perTest, job.OnlineTestQuestionsPerTest);
        Assert.Equal(duration, job.OnlineTestDurationMinutes);
        Assert.True(job.UpdatedAt >= before);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, 80, 30, 45, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền cấu hình bài thi", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
