using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Đội/bộ phận của công ty (ADR-065) — danh sách do Super Admin quản lý.
    ///
    /// <b>Vì sao phải là bảng chứ không để chuỗi text như trước:</b> phòng ban vốn là text tự do ở
    /// mọi nơi, và tệ hơn, nhân viên **tự sửa được phòng ban của chính mình** ở trang Cài đặt. Nên
    /// một Hiring Manager của đội A có thể lập phiếu ghi đội B — chỉ cần đổi hồ sơ rồi quay ra lập
    /// phiếu. Khoá ô phòng ban trên biểu mẫu mà không bịt đường đó thì chỉ là hình thức.
    ///
    /// <b>Ranh giới với ADR-061 — đọc kỹ trước khi mở rộng:</b> đây là dữ liệu **tổ chức**, KHÔNG
    /// phải trục phân quyền. ADR-061 đã cân nhắc và loại phòng ban khỏi mô hình quyền ("thêm phòng
    /// ban là mở trục thứ hai phải giữ đồng bộ mãi mãi"); quyết định đó vẫn nguyên vẹn — ai được
    /// quyết định về một tin vẫn do <c>job_hiring_team_members</c> trả lời. Ở đây chỉ trả lời một
    /// câu khác: "phiếu này do đội nào xin".
    /// </summary>
    public class Department : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Tên đội, duy nhất (không phân biệt hoa thường).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mã ngắn tuỳ chọn để hiển thị gọn — vd <c>BE</c>, <c>QA</c>.</summary>
        public string? Code { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// Đội giải thể thì TẮT chứ không xoá: phiếu và tài khoản cũ vẫn phải tra được tên đội.
        /// Đội đã tắt không còn xuất hiện trong ô chọn của phiếu mới.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
