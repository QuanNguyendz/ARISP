using System;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Nội dung thư do nhân sự SỬA TAY, truyền kèm chính lệnh nghiệp vụ sẽ gửi thư đó.
    ///
    /// VÌ SAO KHÔNG LÀM ENTITY "BẢN NHÁP" HAI PHA: nếu hành động miền (chốt chỗ, đổi trạng thái)
    /// chạy trước rồi mới mở trình soạn, người dùng bỏ dở là <b>ứng viên bị xếp lịch mà không thư
    /// nào rời hệ thống</b> — đúng lỗi ADR-059 sinh ra để chữa. Soạn TRƯỚC, gửi KÈM hành động, nên
    /// tính nguyên tử giữ nguyên: huỷ trình soạn = không có gì xảy ra cả.
    ///
    /// Bỏ trống (null) → lệnh render mẫu như cũ, hành vi y hệt trước Phase 4.
    /// </summary>
    public class EmailOverride
    {
        public string? Subject { get; set; }
        public string? BodyHtml { get; set; }

        /// <summary>
        /// Có nội dung thật để dùng thay mẫu không — CHỈ CẦN MỘT trong hai trường có nội dung.
        ///
        /// Trước đây yêu cầu CẢ HAI (`&amp;&amp;`), nên người dùng xoá trắng dòng tiêu đề là **toàn bộ
        /// thân thư họ vừa viết bị vứt**, ứng viên nhận đúng mẫu gốc, và `EmailLog.WasEdited` ghi
        /// `false` — tức nhật ký nói không ai sửa gì. `CandidateEmailSender` đã có sẵn hai dòng bù
        /// từng trường ngay bên dưới (`if (IsNullOrWhiteSpace(subject)) subject = template.Subject;`),
        /// nhưng cờ này chặn trước nên hai dòng đó không bao giờ chạy tới.
        /// </summary>
        public bool HasContent =>
            !string.IsNullOrWhiteSpace(Subject) || !string.IsNullOrWhiteSpace(BodyHtml);
    }

    /// <summary>Thư đã dựng xong, sẵn sàng gửi (hoặc trả về cho trình soạn).</summary>
    public record RenderedEmail(string Subject, string Html, string ToEmail, string? ToName);
}
