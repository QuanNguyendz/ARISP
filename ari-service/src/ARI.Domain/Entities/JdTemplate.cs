using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Mẫu bản mô tả công việc của công ty (ADR-064) — do HR Leader cấu hình một lần, mọi JD dựng
    /// từ trình soạn đều theo mẫu này.
    ///
    /// <b>Là CẤU HÌNH CÓ CẤU TRÚC, không phải một file .docx tải lên.</b> Hệ thống dựng bố cục cố
    /// định từ các trường ở đây, nên file xuất ra luôn hợp lệ và luôn có logo. Đổi lại: HR Leader
    /// không tự vẽ được bố cục tuỳ ý — đây là đánh đổi đã chốt, vì hai hướng còn lại (soạn tự do có
    /// chỗ điền / mail-merge file Word) đều hỏng âm thầm: gõ sai tên chỗ điền thì file ra chữ
    /// <c>{{…}}</c> thô, còn thao tác XML của Word thì vỡ bố cục với bảng và text-box.
    ///
    /// Single-tenant nên chỉ có MỘT mẫu đang dùng — đọc bản <see cref="UpdatedAt"/> mới nhất. Cố ý
    /// không có cột <c>is_active</c>: hai bản cùng bật là một trạng thái không ai muốn mà lại biểu
    /// diễn được, tức là sẽ có lúc xảy ra.
    /// </summary>
    public class JdTemplate
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // ===== Nhận diện công ty (phần đầu trang của file JD) =====
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyAddress { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? CompanyEmail { get; set; }

        /// <summary>
        /// storageKey của logo (<see cref="Constants.StorageFolders"/> → branding). Null = chưa tải
        /// logo; file vẫn xuất được, chỉ khuyết phần ảnh — một file ảnh hỏng không được phép làm
        /// chết cả việc dựng JD.
        /// </summary>
        public string? LogoStorageKey { get; set; }

        /// <summary>
        /// Tiêu đề in giữa đầu văn bản, phía trên bảng thông tin — vd "THÔNG TIN TUYỂN DỤNG".
        /// Là tiêu đề của LOẠI văn bản, không phải tên vị trí; tên vị trí nằm trong bảng thông tin.
        /// </summary>
        public string DocumentTitle { get; set; } = "THÔNG TIN TUYỂN DỤNG";

        // ===== Trình bày =====
        /// <summary>Màu nhấn dạng hex <c>#RRGGBB</c> — dùng cho tiêu đề mục và đường kẻ.</summary>
        public string AccentColor { get; set; } = "#4F46E5";

        /// <summary>Tên phông chữ. Phải là phông có trên máy chủ render PDF, nếu không sẽ rơi về mặc định.</summary>
        public string FontFamily { get; set; } = "Arial";

        /// <summary>Dòng chân trang (điều khoản, lời cảm ơn…).</summary>
        public string? FooterNote { get; set; }

        /// <summary>
        /// Danh sách mục của JD, có thứ tự — JSON mảng
        /// <c>[{"key","title","hint","enabled"}]</c>, xem <see cref="Constants.JdSectionKeys"/>.
        ///
        /// <b>`key` là BẤT BIẾN</b>: renderer và nội dung đã soạn trong <see cref="JdDocument"/>
        /// đều tham chiếu theo key. HR Leader chỉ đổi được <c>title</c>, <c>hint</c>, thứ tự và
        /// bật/tắt — đổi key thì mọi bản JD đã soạn mất nội dung mà không có lỗi nào.
        /// </summary>
        public string SectionsJson { get; set; } = "[]";

        public Guid? UpdatedByUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
