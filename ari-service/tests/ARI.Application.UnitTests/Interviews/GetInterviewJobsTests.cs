using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.ApplicationFlow;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Bảng tổng quan phỏng vấn theo job cho HR (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetInterviewJobsAsync</c>, test-plan B17): không job → rỗng; thống kê ca/booking/xác nhận/session +
/// status "closed" khi quá hạn nộp + ca kế tiếp trong tương lai.
/// </summary>
public class GetInterviewJobsTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task No_jobs_returns_empty_list()
    {
        var result = await Svc(new InMemoryUnitOfWork()).GetInterviewJobsAsync(Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Aggregates_slots_bookings_sessions_and_closes_expired_job()
    {
        var appId1 = Guid.NewGuid();
        var appId2 = Guid.NewGuid();
        var job = ApplicationData.Job(status: "active", deadline: SchedulingData.Past); // quá hạn nộp → closed
        var slot1 = SchedulingData.Slot(job.Id, round: 1, start: SchedulingData.Future);
        var slot2 = SchedulingData.Slot(job.Id, round: 2, start: SchedulingData.Future.AddDays(1));
        var uow = new InMemoryUnitOfWork()
            .Seed(job)
            .Seed(slot1, slot2)
            .Seed(SchedulingData.Booking(appId1, slot1.Id, round: 1, confirmation: "confirmed"),
                  SchedulingData.Booking(appId2, slot2.Id, round: 2, confirmation: "pending"))
            .Seed(new InterviewSession { ApplicationId = appId1, RoundNumber = 1, SessionType = "real", Status = "completed" });

        var result = await Svc(uow).GetInterviewJobsAsync(Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(job.Id, dto.JobId);
        Assert.Equal("closed", dto.JobStatus);      // active + deadline quá khứ
        Assert.Equal(2, dto.TotalSlots);
        Assert.Equal(2, dto.TotalBooked);
        Assert.Equal(1, dto.TotalConfirmed);         // chỉ 1 confirmed
        Assert.Equal(2, dto.MaxRound);
        Assert.Equal(1, dto.TotalSessions);
        Assert.Equal(1, dto.CompletedSessions);
        Assert.Equal(slot1.StartTime, dto.NextSlotTime); // ca tương lai gần nhất
    }

    /// <summary>
    /// Thẻ tổng quan phải dùng CÙNG vị từ chiếm chỗ với danh sách ca bên dưới, nếu không đầu trang
    /// nói một đằng, phân số từng ca nói một nẻo. Và "đã xác nhận" không được đếm lịch sau đó đã đóng.
    /// </summary>
    [Fact]
    public async Task Chi_dem_booking_dang_giu_cho()
    {
        var job = ApplicationData.Job(status: "active");
        var slot = SchedulingData.Slot(job.Id, round: 1, start: SchedulingData.Future);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot)
            .Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "confirmed"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "pending"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Declined, confirmation: "declined"),
                  // Đã xác nhận rồi mới bị loại — không còn giữ chỗ, không được tính là "đã xác nhận".
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Cancelled, confirmation: "confirmed"));

        var result = await Svc(uow).GetInterviewJobsAsync(Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(2, dto.TotalBooked);
        Assert.Equal(1, dto.TotalConfirmed);
    }

    /// <summary>Recruiter chỉ thấy tin của mình — trước đây nhánh dự phòng ở giao diện cho thấy TẤT CẢ.</summary>
    [Fact]
    public async Task Recruiter_chi_thay_tin_minh_tao()
    {
        var me = Guid.NewGuid();
        var mine = SchedulingData.Job(me);
        var theirs = SchedulingData.Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(mine, theirs);

        var result = await Svc(uow).GetInterviewJobsAsync(me, AppRoles.Recruiter, CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(result).JobId);
    }

    [Fact]
    public async Task Admin_thay_moi_tin()
    {
        var uow = new InMemoryUnitOfWork().Seed(SchedulingData.Job(), SchedulingData.Job());

        var result = await Svc(uow).GetInterviewJobsAsync(Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }
}
