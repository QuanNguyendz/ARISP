using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Đổi trạng thái hồ sơ (<see cref="ApplicationService.UpdateApplicationStatusAsync"/>): chỉ cho phép các
/// bước chuyển hợp lệ theo bảng trạng thái (invited→cv_submitted→screening→interview→pass/not_pass, withdrawn
/// là điểm cuối, not_pass mở lại được), không đổi khi trùng, và báo realtime cho ứng viên khi thành công.
/// </summary>
public class UpdateApplicationStatusTests
{
    private readonly Guid _accountId = Guid.NewGuid();

    private (InMemoryUnitOfWork uow, RecordingNotificationService notif, ARI.Domain.Entities.Application app)
        Seed(string status, Guid? accountId = null)
    {
        var job = ApplicationData.Job();
        var app = ApplicationData.Application(job.Id, accountId, status: status);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);
        return (uow, new RecordingNotificationService(), app);
    }

    private static Task<Result<ApplicationResponse>> Run(InMemoryUnitOfWork uow, RecordingNotificationService notif, Guid id, string newStatus)
        => ApplicationServiceFactory.Create(uow, notif, new RecordingEmailService(), new RecordingRagIngestionService())
            .UpdateApplicationStatusAsync(id, newStatus, CancellationToken.None);

    [Fact]
    public async Task Empty_status_fails()
    {
        var (uow, notif, app) = Seed("screening");

        var res = await Run(uow, notif, app.Id, "   ");

        Assert.True(res.IsFailure);
        Assert.Contains("cannot be empty", res.Error);
    }

    [Fact]
    public async Task Unknown_status_fails()
    {
        var (uow, notif, app) = Seed("screening");

        var res = await Run(uow, notif, app.Id, "foobar");

        Assert.True(res.IsFailure);
        Assert.Contains("is invalid", res.Error);
        Assert.Equal("screening", app.Status);
    }

    [Fact]
    public async Task Unchanged_status_fails()
    {
        var (uow, notif, app) = Seed("screening");

        var res = await Run(uow, notif, app.Id, "screening");

        Assert.True(res.IsFailure);
        Assert.Contains("already in", res.Error);
    }

    [Fact]
    public async Task Disallowed_transition_fails()
    {
        var (uow, notif, app) = Seed("cv_submitted");

        var res = await Run(uow, notif, app.Id, "pass"); // cv_submitted không thể nhảy thẳng sang pass

        Assert.True(res.IsFailure);
        Assert.Contains("Cannot transition", res.Error);
        Assert.Equal("cv_submitted", app.Status);
    }

    [Fact]
    public async Task Withdrawn_is_terminal()
    {
        var (uow, notif, app) = Seed("withdrawn");

        var res = await Run(uow, notif, app.Id, "screening");

        Assert.True(res.IsFailure);
        Assert.Contains("Cannot transition", res.Error);
    }

    [Fact]
    public async Task Not_pass_can_be_reopened_to_screening()
    {
        var (uow, notif, app) = Seed("not_pass");

        var res = await Run(uow, notif, app.Id, "screening");

        Assert.True(res.IsSuccess);
        Assert.Equal("screening", app.Status);
    }

    [Fact]
    public async Task Allowed_transition_succeeds_and_is_case_insensitive()
    {
        var (uow, notif, app) = Seed("screening", accountId: _accountId);

        var res = await Run(uow, notif, app.Id, "INTERVIEW");

        Assert.True(res.IsSuccess);
        Assert.Equal("interview", app.Status); // lưu ở dạng chữ thường
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task App_not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingNotificationService(), Guid.NewGuid(), "screening");

        Assert.True(res.IsFailure);
        Assert.Contains("Application not found", res.Error);
    }
}
