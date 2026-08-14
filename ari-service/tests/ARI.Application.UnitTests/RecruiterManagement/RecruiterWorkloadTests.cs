using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Queries.GetRecruiters;
using ARI.Application.Jobs.Commands.ReassignJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.RecruiterManagement;

/// <summary>
/// Màn "Phân công & tải tuyển dụng" của HR Lead: đo tải theo mô hình ATS (req load, nút thắt
/// theo giai đoạn, tuổi chờ) và chuyển giao tin để cân tải.
/// </summary>
public class RecruiterWorkloadTests
{
    private static User Recruiter(string name = "Recruiter A", bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        FullName = name,
        Email = $"{Guid.NewGuid():N}@corp.io",
        Role = AppRoles.Recruiter,
        IsActive = active,
    };

    private static JobPosting Job(Guid ownerId, string status = "active", int ageDays = 0) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Fresher .NET",
        CreatedByUserId = ownerId,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-ageDays),
    };

    private static ARI.Domain.Entities.Application App(Guid jobId, string status, int ageDays = 0) => new()
    {
        Id = Guid.NewGuid(),
        JobPostingId = jobId,
        CandidateName = "Ứng viên",
        CandidateEmail = "cand@example.io",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-ageDays),
    };

    // ===== Đo tải =====

    [Fact]
    public async Task No_recruiters_returns_empty()
    {
        var res = await new GetRecruitersQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetRecruitersQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value!);
    }

    [Fact]
    public async Task Counts_req_load_and_stage_bottlenecks_with_age()
    {
        var user = Recruiter();
        var activeJob = Job(user.Id);
        var draft = Job(user.Id, "draft", ageDays: 9);

        var uow = new InMemoryUnitOfWork().Seed(user).Seed(activeJob).Seed(draft)
            .Seed(App(activeJob.Id, "applied", ageDays: 4))     // chưa sàng
            .Seed(App(activeJob.Id, "screening", ageDays: 2))   // đã sàng, chưa có lịch
            .Seed(App(activeJob.Id, "pass"));                   // đã đóng

        var res = await new GetRecruitersQueryHandler(uow).Handle(new GetRecruitersQuery(), CancellationToken.None);
        var row = Assert.Single(res.Value!);

        Assert.Equal(2, row.JobsTotal);
        Assert.Equal(1, row.JobsActive);          // tin nháp không tính là tải đang tuyển
        Assert.Equal(1, row.DraftsAwaitingApproval);
        Assert.Equal(9, row.DraftsOldestDays);
        Assert.Equal(1, row.ApplicationsUnscreened);
        Assert.Equal(4, row.UnscreenedOldestDays);
        Assert.Equal(1, row.AwaitingScheduling);
        Assert.Equal(2, row.AwaitingSchedulingOldestDays);
        Assert.Equal(2, row.ActivePipeline);       // hồ sơ "pass" đã đóng, không còn tốn công
        Assert.Equal(1, row.Hired);
        Assert.Equal(9, row.OldestBottleneckDays); // lấy nút thắt cũ nhất trong tất cả
    }

    [Fact]
    public async Task Scheduled_application_leaves_awaiting_scheduling_bucket()
    {
        var user = Recruiter();
        var job = Job(user.Id);
        var app = App(job.Id, "screening", ageDays: 5);

        var uow = new InMemoryUnitOfWork().Seed(user).Seed(job).Seed(app)
            .Seed(new InterviewBooking
            {
                Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 1, Status = "scheduled",
            });

        var res = await new GetRecruitersQueryHandler(uow).Handle(new GetRecruitersQuery(), CancellationToken.None);

        Assert.Equal(0, res.Value!.Single().AwaitingScheduling);
    }

    [Fact]
    public async Task Declined_booking_counts_until_rebooked()
    {
        var user = Recruiter();
        var job = Job(user.Id);
        var app = App(job.Id, "interview");

        var uow = new InMemoryUnitOfWork().Seed(user).Seed(job).Seed(app)
            .Seed(new InterviewBooking
            {
                Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 1,
                Status = "declined", ConfirmationStatus = "declined",
            });

        var res = await new GetRecruitersQueryHandler(uow).Handle(new GetRecruitersQuery(), CancellationToken.None);

        Assert.Equal(1, res.Value!.Single().DeclinedNeedRebooking);
    }

    [Fact]
    public async Task Practice_evaluations_never_count_as_pending_review()
    {
        var user = Recruiter();
        var job = Job(user.Id);
        var app = App(job.Id, "interview");

        var uow = new InMemoryUnitOfWork().Seed(user).Seed(job).Seed(app)
            // Buổi thử là không gian riêng của ứng viên, nhân sự không thấy và không duyệt (ADR-051).
            .Seed(new Evaluation
            {
                Id = Guid.NewGuid(), ApplicationId = app.Id, SessionId = Guid.NewGuid(),
                RoundNumber = 1, SessionType = "practice", AiVerdict = "pass",
            })
            .Seed(new Evaluation
            {
                Id = Guid.NewGuid(), ApplicationId = app.Id, SessionId = Guid.NewGuid(),
                RoundNumber = 1, SessionType = "real", AiVerdict = "pass",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
            });

        var res = await new GetRecruitersQueryHandler(uow).Handle(new GetRecruitersQuery(), CancellationToken.None);
        var row = res.Value!.Single();

        Assert.Equal(1, row.PendingReviews);
        Assert.Equal(3, row.PendingReviewsOldestDays);
    }

    [Fact]
    public async Task Most_stalled_recruiter_sorts_first()
    {
        var calm = Recruiter("Bình thường");
        var stalled = Recruiter("Đang tắc");
        var uow = new InMemoryUnitOfWork().Seed(calm).Seed(stalled)
            .Seed(Job(calm.Id, "draft", ageDays: 1))
            .Seed(Job(stalled.Id, "draft", ageDays: 12));

        var res = await new GetRecruitersQueryHandler(uow).Handle(new GetRecruitersQuery(), CancellationToken.None);

        Assert.Equal("Đang tắc", res.Value![0].FullName);
    }

    // ===== Chuyển giao =====

    private static ReassignJobCommandHandler Handler(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => new(uow, notif);

    [Fact]
    public async Task Reassign_moves_ownership_and_notifies_both_sides()
    {
        var from = Recruiter("Người cũ");
        var to = Recruiter("Người mới");
        var job = Job(from.Id);
        var actor = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(from).Seed(to).Seed(job).Seed(App(job.Id, "screening"));
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif)
            .Handle(new ReassignJobCommand(job.Id, to.Id, actor, "cân lại tải"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        // Ghi thẳng vào CreatedByUserId vì mọi cổng kiểm quyền của hệ thống đọc đúng trường này.
        Assert.Equal(to.Id, job.CreatedByUserId);

        var notifications = uow.Repo<Notification>().Items;
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.RecipientUserId == to.Id);
        Assert.Contains(notifications, n => n.RecipientUserId == from.Id);
        Assert.Equal(2, notif.UserEvents.Count(e => e.EventType == "JobReassigned"));

        // Sau khi ghi đè chủ sở hữu, audit log là nơi DUY NHẤT còn dấu vết người phụ trách cũ.
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("job_reassigned", audit.Action);
        Assert.Contains(from.Id.ToString(), audit.Metadata);
        Assert.Contains("cân lại tải", audit.Metadata);
    }

    [Fact]
    public async Task Reassign_rejects_non_recruiter_target()
    {
        var from = Recruiter();
        var hrLead = new User
        {
            Id = Guid.NewGuid(), FullName = "HR Lead", Email = "lead@corp.io",
            Role = AppRoles.HrAdmin, IsActive = true,
        };
        var job = Job(from.Id);
        var uow = new InMemoryUnitOfWork().Seed(from).Seed(hrLead).Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new ReassignJobCommand(job.Id, hrLead.Id, Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(from.Id, job.CreatedByUserId);
    }

    [Fact]
    public async Task Reassign_rejects_locked_target()
    {
        var from = Recruiter();
        var locked = Recruiter("Đang khoá", active: false);
        var job = Job(from.Id);
        var uow = new InMemoryUnitOfWork().Seed(from).Seed(locked).Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new ReassignJobCommand(job.Id, locked.Id, Guid.NewGuid(), null), CancellationToken.None);

        // Chuyển vào tài khoản khoá = tin rơi vào trạng thái không ai xử lý được.
        Assert.True(res.IsFailure);
        Assert.Equal(from.Id, job.CreatedByUserId);
    }

    [Fact]
    public async Task Reassign_rejects_same_owner()
    {
        var owner = Recruiter();
        var job = Job(owner.Id);
        var uow = new InMemoryUnitOfWork().Seed(owner).Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new ReassignJobCommand(job.Id, owner.Id, Guid.NewGuid(), null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<AuditLog>().Items);
    }
}
