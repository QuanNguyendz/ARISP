using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Điều kiện phỏng vấn thử (<see cref="ApplicationService.CheckPracticeEligibilityAsync"/>, ADR-050):
/// 1 lượt / VÒNG — còn quyền khi chưa có phiên practice nào của đúng vòng đó.
/// </summary>
public class PracticeEligibilityTests
{
    private static Task<Result<bool>> Run(InMemoryUnitOfWork uow, Guid appId, int round)
        => ApplicationServiceFactory.Create(uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService())
            .CheckPracticeEligibilityAsync(appId, round, CancellationToken.None);

    [Fact]
    public async Task App_not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid(), 1);

        Assert.True(res.IsFailure);
        Assert.Contains("Application not found", res.Error);
    }

    [Fact]
    public async Task Eligible_when_no_practice_session_yet()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value); // còn lượt
    }

    [Fact]
    public async Task Not_eligible_when_round_already_practiced()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(ApplicationData.PracticeSession(app.Id, round: 1));

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value); // đã dùng lượt của vòng 1
    }

    /// <summary>
    /// Lỡ buổi phỏng vấn THẬT của vòng = trượt vòng đó → không còn phỏng vấn thử. Thử là để chuẩn bị
    /// cho buổi thật, mà buổi thật đã trôi qua.
    /// </summary>
    [Fact]
    public async Task Not_eligible_when_real_interview_slot_already_passed()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id,
            RoundNumber = 1,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            EndTime = DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(new InterviewBooking
            {
                ApplicationId = app.Id,
                AvailabilitySlotId = slot.Id,
                RoundNumber = 1,
                Status = "scheduled",
            });

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value);
    }

    /// <summary>Lịch còn ở tương lai thì vẫn được thử — đó chính là lúc cần luyện tập.</summary>
    [Fact]
    public async Task Eligible_when_slot_is_still_upcoming()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id,
            RoundNumber = 1,
            StartTime = DateTimeOffset.UtcNow.AddDays(2),
            EndTime = DateTimeOffset.UtcNow.AddDays(2).AddHours(1),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(new InterviewBooking
            {
                ApplicationId = app.Id,
                AvailabilitySlotId = slot.Id,
                RoundNumber = 1,
                Status = "scheduled",
            });

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value);
    }

    /// <summary>
    /// Ứng viên BÁO BẬN (booking đã trả chỗ) không phải là lỡ buổi: nhân sự sẽ xếp ca khác,
    /// nên vẫn còn quyền thử (ADR-048/058).
    /// </summary>
    [Fact]
    public async Task Declined_booking_in_the_past_does_not_block_practice()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id,
            RoundNumber = 1,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            EndTime = DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(new InterviewBooking
            {
                ApplicationId = app.Id,
                AvailabilitySlotId = slot.Id,
                RoundNumber = 1,
                Status = "declined",
            });

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value);
    }

    /// <summary>
    /// Vòng trắc nghiệm không có phỏng vấn thử: buổi thử là hội thoại với AI, không có gì để "thử"
    /// với bài chọn đáp án — và cho thử sẽ lộ chính ngân hàng đề.
    /// </summary>
    [Fact]
    public async Task Not_eligible_when_round_is_online_test()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" });

        var res = await Run(uow, app.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value);
    }

    [Fact]
    public async Task Eligible_when_round_is_conversational_even_if_another_round_is_online_test()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" })
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2, RoundType = "technical" });

        var res = await Run(uow, app.Id, 2);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value);
    }

    [Fact]
    public async Task Other_round_practice_does_not_block()
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(ApplicationData.PracticeSession(app.Id, round: 1));

        var res = await Run(uow, app.Id, 2); // vòng 2 vẫn còn lượt

        Assert.True(res.Value);
    }
}
