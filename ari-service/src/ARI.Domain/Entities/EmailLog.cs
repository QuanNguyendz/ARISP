using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Bản ghi mỗi thư đã rời hệ thống tới ứng viên (ADR-061, Phase 4).
    ///
    /// Ba lý do đây không phải phần thừa:
    ///   1. <b>Dấu vết</b> — ai đã gửi gì cho ứng viên, và có sửa khác mẫu không. Với thư mời nhận
    ///      việc thì đây là bằng chứng nội dung đã cam kết.
    ///   2. <b>Nối luồng thư</b> — trước đây <c>InterviewBooking.InviteEmailMessageId</c> là một
    ///      cột dành riêng cho MỘT loại thư (ADR-059). Có <see cref="MessageId"/> ở đây thì mọi thư
    ///      nhắc đều nối được vào đúng luồng gốc mà không phải thêm cột cho từng loại.
    ///   3. <b>Tab "Lịch sử email"</b> trên trang chi tiết ứng viên — Greenhouse và Lever đều có.
    ///
    /// KHÔNG soft-delete: đây là nhật ký, xoá đi thì mất chính thứ nó tồn tại để giữ.
    /// </summary>
    public class EmailLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Khoá mẫu thư (<c>EmailTemplateKeys</c>), hoặc tên định danh cho thư hệ thống.</summary>
        public string TemplateKey { get; set; } = string.Empty;

        public Guid? ApplicationId { get; set; }
        public Guid? JobPostingId { get; set; }

        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;

        /// <summary>Nội dung ĐÃ LỌC, đúng bằng thứ được gửi đi — không phải bản người dùng gõ vào.</summary>
        public string BodyHtml { get; set; } = string.Empty;

        /// <summary>Nhân sự có sửa khác mẫu không. Cột này là câu trả lời cho "thư này ai viết".</summary>
        public bool WasEdited { get; set; }

        /// <summary>Người bấm gửi. <c>null</c> = hệ thống tự gửi (nhắc lịch, quét hết hạn…).</summary>
        public Guid? SentByUserId { get; set; }

        public string? MessageId { get; set; }
        public string? InReplyTo { get; set; }

        /// <summary>queued | sent | failed</summary>
        public string Status { get; set; } = "sent";
        public string? ErrorMessage { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
