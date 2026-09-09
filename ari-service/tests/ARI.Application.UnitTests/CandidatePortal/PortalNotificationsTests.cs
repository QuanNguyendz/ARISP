using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.StaffNotifications;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

internal static class PortalNotifData
{
    public static readonly Guid CandidateA = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static ARI.Domain.Entities.Application App(Guid id, Guid job, DateTimeOffset createdAt)
        => new() { Id = id, CandidateAccountId = CandidateA, JobPostingId = job, CandidateName = "N", CandidateEmail = "c@x.io", Status = "cv_submitted", CreatedAt = createdAt };

    public static JobPosting Job(Guid id, string title)
        => new() { Id = id, Title = title, CreatedByUserId = Guid.NewGuid() };

    public static Notification Notif(Guid candidate, string dedup, string type = "applied", bool isRead = false, DateTimeOffset? createdAt = null, DateTimeOffset? deletedAt = null)
        => new() { Id = Guid.NewGuid(), CandidateAccountId = candidate, DedupKey = dedup, Type = type, Title = "T", Body = "B", IsRead = isRead, CreatedAt = createdAt ?? DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, DeletedAt = deletedAt };
}

/// <summary>
/// Đồng bộ + đọc thông báo portal (<see cref="GetPortalNotificationsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetPortalNotifications" (UTCID01–16): sinh idempotent theo DedupKey từ sự kiện thực (applied/invite/result/
/// schedule), sắp CreatedAt desc + đếm chưa đọc; và lỗi query/save.
/// </summary>
public class GetPortalNotificationsQueryHandlerTests
{
    private static Task<Result<StaffNotificationListDto>> Run(InMemoryUnitOfWork uow)
        => new GetPortalNotificationsQueryHandler(uow).Handle(new GetPortalNotificationsQuery(PortalNotifData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_No_applications_or_notifications()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Items);
        Assert.Equal(0, res.Value.UnreadCount);
    }

    [Fact]
    public async Task UTCID02_Existing_notifications_ordered_desc()
    {
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(
            PortalNotifData.Notif(PortalNotifData.CandidateA, "a", isRead: true, createdAt: now.AddHours(-2)),
            PortalNotifData.Notif(PortalNotifData.CandidateA, "b", isRead: false, createdAt: now));
        var res = await Run(uow);
        Assert.True(res.Value.Items[0].CreatedAt > res.Value.Items[1].CreatedAt);
        Assert.Equal(1, res.Value.UnreadCount);
    }

    [Fact]
    public async Task UTCID03_Applied_notification_with_job_title()
    {
        var app = Guid.NewGuid(); var job = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, job, DateTimeOffset.UtcNow)).Seed(PortalNotifData.Job(job, "Backend Dev"));
        var res = await Run(uow);
        var applied = res.Value.Items.Single(i => i.Type == "applied");
        Assert.Equal("Đã nộp hồ sơ ứng tuyển", applied.Title);
        Assert.Equal("Backend Dev", applied.Body);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID04_Missing_job_uses_fallback_body()
    {
        var app = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow));
        var res = await Run(uow);
        Assert.Equal("Vị trí tuyển dụng", res.Value.Items.Single(i => i.Type == "applied").Body);
    }

    [Fact]
    public async Task UTCID05_Duplicate_dedupkey_not_recreated_read_preserved()
    {
        var app = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, $"applied:{app}", isRead: true));
        var res = await Run(uow);
        Assert.Single(res.Value.Items, i => i.Type == "applied");
        Assert.True(res.Value.Items.Single(i => i.Type == "applied").IsRead);
        Assert.Equal(0, uow.SaveChangesCount);   // không thêm mới → không save
    }

    [Fact]
    public async Task UTCID06_Soft_deleted_dedupkey_not_recreated()
    {
        // In-memory không có global soft-delete filter; handler dùng IgnoreQueryFilters (no-op tại đây)
        // để DedupKey của bản đã xoá vẫn nằm trong tập "existing" → không sinh lại.
        var app = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, $"applied:{app}", deletedAt: DateTimeOffset.UtcNow));
        await Run(uow);
        Assert.Single(uow.Repo<Notification>().Items);   // không tạo bản applied thứ 2
    }

    [Fact]
    public async Task UTCID07_Active_code_creates_invite()
    {
        var app = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app, RoundNumber = 1, Code = "ABC123", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() });
        var res = await Run(uow);
        Assert.Contains(res.Value.Items, i => i.Type == "invite");
    }

    [Fact]
    public async Task UTCID08_Expired_code_no_invite()
    {
        var app = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app, RoundNumber = 1, Code = "OLD123", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), CreatedByUserId = Guid.NewGuid() });
        var res = await Run(uow);
        Assert.DoesNotContain(res.Value.Items, i => i.Type == "invite");
    }

    private static InMemoryUnitOfWork WithSharedEval(Guid app, string aiVerdict, string finalVerdict, bool share)
    {
        var session = Guid.NewGuid(); var eval = Guid.NewGuid();
        return new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(new InterviewSession { Id = session, ApplicationId = app, RoundNumber = 1, RoundType = "technical", SessionType = "real", InterviewLanguage = "vi", Status = "completed" })
            .Seed(new Evaluation { Id = eval, SessionId = session, ApplicationId = app, RoundNumber = 1, SessionType = "real", AiVerdict = aiVerdict, OverallScore = 85.4m })
            .Seed(new ARI.Domain.Entities.HrReview { Id = Guid.NewGuid(), EvaluationId = eval, ReviewedByUserId = Guid.NewGuid(), FinalVerdict = finalVerdict, ShareEvaluation = share });
    }

    [Fact]
    public async Task UTCID09_Shared_pass_eval_result_with_score()
    {
        var res = await Run(WithSharedEval(Guid.NewGuid(), aiVerdict: "pass", finalVerdict: "", share: true));
        var result = res.Value.Items.Single(i => i.Type == "result");
        Assert.Contains("Pass", result.Title);
        Assert.Contains("85", result.Body);
    }

    [Fact]
    public async Task UTCID10_Unshared_eval_no_result()
    {
        var res = await Run(WithSharedEval(Guid.NewGuid(), aiVerdict: "pass", finalVerdict: "", share: false));
        Assert.DoesNotContain(res.Value.Items, i => i.Type == "result");
    }

    [Fact]
    public async Task UTCID11_Final_verdict_overrides_ai()
    {
        var res = await Run(WithSharedEval(Guid.NewGuid(), aiVerdict: "not_pass", finalVerdict: "pass", share: true));
        Assert.Contains("Pass", res.Value.Items.Single(i => i.Type == "result").Title);
    }

    private static InMemoryUnitOfWork WithBooking(Guid app, DateTimeOffset slotStart, string status)
    {
        var slot = Guid.NewGuid();
        return new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, Guid.NewGuid(), DateTimeOffset.UtcNow))
            .Seed(new InterviewBooking { Id = Guid.NewGuid(), ApplicationId = app, AvailabilitySlotId = slot, RoundNumber = 1, Status = status })
            .Seed(new AvailabilitySlot { Id = slot, JobPostingId = Guid.NewGuid(), RoundNumber = 1, StartTime = slotStart, EndTime = slotStart.AddHours(1), Timezone = "UTC", Capacity = 5 });
    }

    [Fact]
    public async Task UTCID12_Future_booking_creates_schedule()
    {
        var res = await Run(WithBooking(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), "scheduled"));
        Assert.Contains(res.Value.Items, i => i.Type == "schedule");
    }

    [Fact]
    public async Task UTCID13_Past_booking_no_schedule()
    {
        var res = await Run(WithBooking(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), "scheduled"));
        Assert.DoesNotContain(res.Value.Items, i => i.Type == "schedule");
    }

    [Fact]
    public async Task UTCID14_Multiple_events_desc_with_unread()
    {
        var app = Guid.NewGuid(); var job = Guid.NewGuid(); var slot = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(app, job, now.AddHours(-3))).Seed(PortalNotifData.Job(job, "Dev"))
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app, RoundNumber = 1, Code = "AAA111", ExpiresAt = now.AddHours(2), CreatedByUserId = Guid.NewGuid(), CreatedAt = now.AddHours(-2) })
            .Seed(new InterviewBooking { Id = Guid.NewGuid(), ApplicationId = app, AvailabilitySlotId = slot, RoundNumber = 1, Status = "scheduled", CreatedAt = now.AddHours(-1) })
            .Seed(new AvailabilitySlot { Id = slot, JobPostingId = job, RoundNumber = 1, StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(1), Timezone = "UTC", Capacity = 5 })
            .Seed(new OnlineTestSubmission { Id = Guid.NewGuid(), ApplicationId = app, RoundNumber = 1, SelectedAnswers = "[]", Score = 90m, IsPassed = true, CorrectCount = 9, TotalQuestions = 10, CreatedAt = now });

        var res = await Run(uow);

        Assert.Equal(4, res.Value.Items.Count);   // applied + invite + schedule + online_test
        Assert.Equal(res.Value.Items.Count, res.Value.UnreadCount);
        for (var i = 1; i < res.Value.Items.Count; i++)
            Assert.True(res.Value.Items[i - 1].CreatedAt >= res.Value.Items[i].CreatedAt);
    }

    [Fact]
    public async Task UTCID15_Application_query_error()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<ARI.Domain.Entities.Application>("Application DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Application DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID16_Save_error()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.App(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow))
            .FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Đánh dấu tất cả đã đọc (<see cref="MarkAllPortalNotificationsReadCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "MarkAllPortalNotificationsRead" (UTCID01–07).
/// </summary>
public class MarkAllPortalNotificationsReadCommandHandlerTests
{
    private static Task<Result<int>> Run(InMemoryUnitOfWork uow)
        => new MarkAllPortalNotificationsReadCommandHandler(uow).Handle(new MarkAllPortalNotificationsReadCommand(PortalNotifData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_No_unread()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID02_One_unread()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a", isRead: false);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await Run(uow);
        Assert.Equal(1, res.Value);
        Assert.True(n.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Three_unread()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            PortalNotifData.Notif(PortalNotifData.CandidateA, "a"),
            PortalNotifData.Notif(PortalNotifData.CandidateA, "b"),
            PortalNotifData.Notif(PortalNotifData.CandidateA, "c"));
        var res = await Run(uow);
        Assert.Equal(3, res.Value);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID04_Only_read()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, "a", isRead: true));
        var res = await Run(uow);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_Find_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<Notification>("Notification DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Notification DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID06_Update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, "a")).FailUpdateFor<Notification>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Update Error", ex.Message);
    }

    [Fact]
    public async Task UTCID07_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, "a")).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Xoá một thông báo portal (<see cref="DeletePortalNotificationCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "MarkPortalNotification" (nhãn báo cáo sai — nội dung là handler Delete; UTCID01–06): IDOR + not_found + lỗi phụ thuộc.
/// </summary>
public class DeletePortalNotificationCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow, Guid notificationId)
        => new DeletePortalNotificationCommandHandler(uow).Handle(new DeletePortalNotificationCommand(PortalNotifData.CandidateA, notificationId), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy thông báo.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Other_candidate_is_not_found()
    {
        var n = PortalNotifData.Notif(Guid.NewGuid(), "a");   // thuộc ứng viên khác
        var res = await Run(new InMemoryUnitOfWork().Seed(n), n.Id);
        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Owned_notification_deleted()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a");
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await Run(uow, n.Id);
        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Notification>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID04_GetById_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<Notification>("Notification DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, Guid.NewGuid()));
        Assert.Equal("Notification DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID05_Delete_error()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a");
        var uow = new InMemoryUnitOfWork().Seed(n).FailDeleteFor<Notification>("Delete Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, n.Id));
        Assert.Equal("Delete Error", ex.Message);
    }

    [Fact]
    public async Task UTCID06_Save_error()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a");
        var uow = new InMemoryUnitOfWork().Seed(n).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, n.Id));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>Đánh dấu một thông báo đã đọc (<see cref="MarkPortalNotificationReadCommandHandler"/>) — ngoài 87 hàm test-plan,
/// giữ để phủ handler: IDOR + not_found + idempotent (đã đọc thì không save lại).</summary>
public class MarkPortalNotificationReadCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow, Guid notificationId)
        => new MarkPortalNotificationReadCommandHandler(uow).Handle(new MarkPortalNotificationReadCommand(PortalNotifData.CandidateA, notificationId), CancellationToken.None);

    [Fact]
    public async Task Not_found_when_missing_or_other_candidate()
    {
        var n = PortalNotifData.Notif(Guid.NewGuid(), "a");
        var res = await Run(new InMemoryUnitOfWork().Seed(n), n.Id);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Marks_unread_and_saves()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a", isRead: false);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await Run(uow, n.Id);
        Assert.True(res.IsSuccess);
        Assert.True(n.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Already_read_is_idempotent_no_save()
    {
        var n = PortalNotifData.Notif(PortalNotifData.CandidateA, "a", isRead: true);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await Run(uow, n.Id);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
