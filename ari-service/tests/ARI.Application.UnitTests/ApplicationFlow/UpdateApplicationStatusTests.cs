using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
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

    /// <summary>
    /// MỌI trạng thái được ghi ở bất kỳ đâu phải có mặt làm KHOÁ trong bảng chuyển trạng thái.
    ///
    /// `hm_review` (ADR-061) từng bị bỏ sót: hồ sơ đang chờ Hiring Manager duyệt rơi vào nhánh
    /// "Transition mapping … is not configured" — không rút được, không mở lại được, và thông báo
    /// lỗi không nói được vì sao. Đúng lỗi mà `cv_rejected` đã mắc trước đó.
    /// </summary>
    [Theory]
    [InlineData(ApplicationStatuses.Invited)]
    [InlineData(ApplicationStatuses.CvSubmitted)]
    [InlineData(ApplicationStatuses.HmReview)]
    [InlineData(ApplicationStatuses.CvRejected)]
    [InlineData(ApplicationStatuses.Screening)]
    [InlineData(ApplicationStatuses.Interview)]
    [InlineData(ApplicationStatuses.Pass)]
    [InlineData(ApplicationStatuses.NotPass)]
    [InlineData(ApplicationStatuses.Offer)]
    [InlineData(ApplicationStatuses.Hired)]
    [InlineData(ApplicationStatuses.OfferDeclined)]
    [InlineData(ApplicationStatuses.Withdrawn)]
    public async Task Moi_trang_thai_deu_phai_la_khoa_trong_bang_chuyen(string status)
    {
        var (uow, notif, app) = Seed(status);

        // Gửi một đích chắc chắn không hợp lệ: thông báo lỗi phải là "không đi được tới đó",
        // KHÔNG được là "trạng thái hiện tại chưa được cấu hình".
        var res = await Run(uow, notif, app.Id, ApplicationStatuses.Invited);

        Assert.DoesNotContain("is not configured", res.Error ?? string.Empty);
    }

    /// <summary>
    /// Cổng duyệt shortlist của Hiring Manager phải có đường ra cả hai phía: duyệt xong đi tiếp
    /// (`screening`), và rút lại việc gửi duyệt (`cv_submitted`) khi gửi nhầm.
    /// </summary>
    [Theory]
    [InlineData(ApplicationStatuses.Screening)]
    [InlineData(ApplicationStatuses.CvSubmitted)]
    [InlineData(ApplicationStatuses.CvRejected)]
    [InlineData(ApplicationStatuses.Withdrawn)]
    public async Task Hm_review_co_duong_ra(string target)
    {
        var (uow, notif, app) = Seed(ApplicationStatuses.HmReview, _accountId);

        var res = await Run(uow, notif, app.Id, target);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(target, app.Status);
    }

    [Fact]
    public async Task Cv_submitted_gui_duoc_sang_cong_duyet_hm()
    {
        var (uow, notif, app) = Seed(ApplicationStatuses.CvSubmitted, _accountId);

        var res = await Run(uow, notif, app.Id, ApplicationStatuses.HmReview);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(ApplicationStatuses.HmReview, app.Status);
    }

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
    public async Task Cv_rejected_is_a_configured_state_not_a_dead_end()
    {
        // "cv_rejected" được RejectApplicationAsync ghi ra nhưng trước đây KHÔNG phải khoá trong
        // bảng chuyển trạng thái, nên mọi hồ sơ bị loại ở vòng CV rơi vào nhánh "Transition mapping
        // … is not configured": không thao tác lại được, và thông báo lỗi không nói được vì sao.
        var (uow, notif, app) = Seed(ApplicationStatuses.CvRejected);

        var res = await Run(uow, notif, app.Id, ApplicationStatuses.CvSubmitted);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.CvSubmitted, app.Status);
    }

    [Fact]
    public async Task Cv_rejected_still_refuses_illegal_jumps()
    {
        // Mở lại được KHÔNG có nghĩa là đi đâu cũng được: hồ sơ bị loại CV phải quay về vòng CV,
        // không nhảy thẳng vào phỏng vấn.
        var (uow, notif, app) = Seed(ApplicationStatuses.CvRejected);

        var res = await Run(uow, notif, app.Id, ApplicationStatuses.Interview);

        Assert.True(res.IsFailure);
        Assert.Contains("Cannot transition", res.Error);
        Assert.Equal(ApplicationStatuses.CvRejected, app.Status);
    }

    [Fact]
    public async Task Cv_submitted_can_be_rejected_outright()
    {
        var (uow, notif, app) = Seed(ApplicationStatuses.CvSubmitted);

        var res = await Run(uow, notif, app.Id, ApplicationStatuses.CvRejected);

        Assert.True(res.IsSuccess);
        Assert.Equal(ApplicationStatuses.CvRejected, app.Status);
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
