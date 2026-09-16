using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJobDisplay;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Bật/tắt hiển thị tin (<see cref="UpdateJobDisplayCommandHandler"/>): HR Admin đổi được mọi lúc; chủ tin
/// (Recruiter) đổi <c>IsUrgent</c> mọi lúc nhưng chỉ đổi được <c>IsPublicListing</c> khi tin còn nháp; người
/// ngoài bị chặn. Tin đang <c>active</c> đổi thì phát realtime cho job board.
/// </summary>
public class UpdateJobDisplayCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static UpdateJobDisplayCommandHandler Handler(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => new(uow, NullLogger<UpdateJobDisplayCommandHandler>.Instance, notif);

    private static JobPosting Job(string status = "draft")
        => new() { Id = Guid.NewGuid(), Title = "Backend Developer", CreatedByUserId = OwnerId, Status = status };

    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingNotificationService())
            .Handle(new UpdateJobDisplayCommand(Guid.NewGuid(), new UpdateJobDisplayRequest { IsUrgent = true }, OwnerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Non_owner_non_admin_forbidden()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new UpdateJobDisplayCommand(job.Id, new UpdateJobDisplayRequest { IsUrgent = true }, Guid.NewGuid(), AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Owner_toggles_urgent()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif)
            .Handle(new UpdateJobDisplayCommand(job.Id, new UpdateJobDisplayRequest { IsUrgent = true }, OwnerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.True(job.IsUrgent);
        Assert.Empty(notif.AllEvents);   // tin nháp → không phát job board
    }

    [Fact]
    public async Task UTCID04_Recruiter_cannot_toggle_public_on_non_draft()
    {
        var job = Job(status: "active");
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new UpdateJobDisplayCommand(job.Id, new UpdateJobDisplayRequest { IsPublicListing = false }, OwnerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID05_Admin_toggles_public_on_active_and_publishes()
    {
        var job = Job(status: "active");
        var uow = new InMemoryUnitOfWork().Seed(job);
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif)
            .Handle(new UpdateJobDisplayCommand(job.Id, new UpdateJobDisplayRequest { IsPublicListing = true }, Guid.NewGuid(), AppRoles.HrAdmin),
                CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.True(job.IsPublicListing);
        Assert.Contains("ReceivePublicJobUpdate", notif.AllEvents);
    }
}
