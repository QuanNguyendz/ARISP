using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Gửi thư cho ứng viên + ghi nhật ký, trong MỘT lối đi (ADR-061, Phase 4).
    ///
    /// Mọi thư đi qua đây đều được lọc HTML và ghi <see cref="EmailLog"/>, nên "gửi mà không có
    /// dấu vết" không còn là trạng thái có thể xảy ra do quên. Nhận <paramref name="over"/> để chỗ
    /// gọi không phải tự viết lại nhánh "dùng bản sửa tay hay dùng mẫu" ở từng lệnh.
    ///
    /// Gửi thư là BEST-EFFORT ở toàn bộ hệ thống này: SMTP hỏng không được phép làm hỏng việc
    /// chốt chỗ hay đổi trạng thái hồ sơ. Nên lỗi được ghi vào nhật ký (<c>status=failed</c>) rồi
    /// nuốt, đúng như các call site cũ vẫn làm — khác là nay có chỗ để nhìn thấy nó.
    /// </summary>
    public static class CandidateEmailSender
    {
        public record SendResult(bool Sent, string? MessageId);

        public static async Task<SendResult> SendAsync(
            IUnitOfWork unitOfWork,
            INotificationService notifications,
            string templateKey,
            RenderedEmail template,
            EmailOverride? over,
            Guid? applicationId,
            Guid? jobPostingId,
            Guid? sentByUserId,
            CancellationToken ct,
            string? inReplyToMessageId = null)
        {
            var wasEdited = over is { HasContent: true };

            // Nội dung do người dùng gõ LUÔN đi qua bộ lọc; nội dung từ mẫu thì không cần (do
            // chính hệ thống dựng) — lọc nó chỉ tổ làm hỏng layout thư có sẵn.
            var subject = wasEdited ? EmailHtmlSanitizer.SanitizeSubject(over!.Subject) : template.Subject;
            var body = wasEdited ? EmailHtmlSanitizer.Sanitize(over!.BodyHtml) : template.Html;

            if (string.IsNullOrWhiteSpace(subject)) subject = template.Subject;
            if (string.IsNullOrWhiteSpace(body)) body = template.Html;

            var log = new EmailLog
            {
                TemplateKey = templateKey,
                ApplicationId = applicationId,
                JobPostingId = jobPostingId,
                ToEmail = template.ToEmail,
                Subject = subject,
                BodyHtml = body, // bản ĐÃ LỌC — đúng bằng thứ được gửi đi
                WasEdited = wasEdited,
                SentByUserId = sentByUserId,
                InReplyTo = inReplyToMessageId,
            };

            string? messageId = null;
            try
            {
                messageId = await notifications.SendThreadedEmailAsync(
                    template.ToEmail, subject, body, inReplyToMessageId, ct);
                log.Status = "sent";
                log.MessageId = messageId;
            }
            catch (Exception ex)
            {
                log.Status = "failed";
                log.ErrorMessage = ex.Message;
            }

            await unitOfWork.Repository<EmailLog>().AddAsync(log, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return new SendResult(log.Status == "sent", messageId);
        }
    }
}
