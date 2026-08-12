using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Danh sách ứng viên trong một ca cho HR (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetCandidatesInSlotAsync</c>, test-plan B18): override hiển thị cho hồ sơ đã bị loại, và map
/// session THẬT + đánh giá (làm tròn điểm) + mã On-site còn hiệu lực (bỏ qua phiên thử).
/// </summary>
public class GetCandidatesInSlotTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task Rejected_application_booking_is_overridden_as_cancelled()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "not_pass"); // đã bị loại
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1, status: "scheduled", confirmation: "confirmed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);

        var result = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal("declined", dto.ConfirmationStatus);        // override
        Assert.Contains("Đã bị loại khỏi quy trình", dto.DeclineReason);
        Assert.Equal("cancelled", dto.BookingStatus);
    }

    [Fact]
    public async Task Real_session_evaluation_and_active_code_are_mapped_practice_excluded()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var real = new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "completed", DurationSeconds = 1800 };
        var practice = new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "practice", Status = "completed", DurationSeconds = 900 };
        var eval = new Evaluation { ApplicationId = app.Id, SessionId = real.Id, SessionType = "real", AiVerdict = "pass", OverallScore = 72.6m, RoundNumber = 1 };
        var code = new InterviewCode { ApplicationId = app.Id, Code = "ABC123", RoundNumber = 1, ExpiresAt = DateTimeOffset.UtcNow.AddHours(2), UsedAt = null };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking)
            .Seed(real, practice).Seed(eval).Seed(code);

        var result = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(real.Id, dto.SessionId);       // phiên THẬT, không phải practice
        Assert.Equal("completed", dto.SessionStatus);
        Assert.Equal(1800, dto.DurationSeconds);
        Assert.Equal("pass", dto.Verdict);
        Assert.Equal(73, dto.OverallScore);          // 72.6 → làm tròn
        Assert.Equal("ABC123", dto.InterviewCode);
        Assert.Equal(code.ExpiresAt, dto.CodeExpiresAt);
    }
}
