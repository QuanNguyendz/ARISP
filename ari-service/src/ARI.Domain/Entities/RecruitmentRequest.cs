using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Phiếu yêu cầu tuyển dụng do Hiring Manager lập cho đội của mình (ADR-063).
    ///
    /// Đây là ĐIỂM BẮT ĐẦU BẮT BUỘC của mọi tin tuyển dụng: HM nêu nhu cầu + đề xuất dải lương →
    /// HR Leader duyệt (kèm chọn Recruiter phụ trách) hoặc từ chối kèm lý do và trả lại cho HM sửa.
    /// Tin tuyển dụng chỉ được tạo từ một phiếu đã <c>approved</c>, bởi đúng Recruiter được phân công.
    ///
    /// Vì sao tách khỏi <see cref="JobPosting"/> chứ không thêm cột trạng thái vào tin: phiếu tồn tại
    /// TRƯỚC khi có tin, sống qua nhiều vòng sửa–gửi lại, và có thể bị từ chối hẳn mà không sinh ra
    /// tin nào. Nhét vào <c>JobPosting</c> thì mọi truy vấn job board phải nhớ lọc bỏ những dòng
    /// chưa từng là tin thật — đúng loại bộ lọc mà quên một chỗ là rò tin nháp ra ngoài.
    ///
    /// Tên trường soi gương <see cref="JobPosting"/> (<c>SalaryMin</c>, <c>EmploymentType</c>,
    /// <c>WorkMode</c>…) để bước điền sẵn JD là phép gán thẳng, không phải bảng dịch tên.
    /// </summary>
    public class RecruitmentRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Hiring Manager lập phiếu. Người này thành HM của tin sinh ra từ phiếu.</summary>
        public Guid RequestedByUserId { get; set; }

        // ===== Nhu cầu tuyển =====
        /// <summary>Vị trí cần tuyển — thành tiêu đề tin khi Recruiter dựng JD.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Đội/bộ phận xin tuyển (ADR-065). Với Hiring Manager, giá trị này LUÔN lấy từ tài khoản của
        /// họ và bỏ qua bất cứ thứ gì client gửi lên — đó là toàn bộ điểm của thay đổi này. Quản trị
        /// viên lập phiếu hộ thì chọn được đội, vì họ lập cho đội khác.
        ///
        /// Vẫn KHÔNG dùng cho phân quyền (ADR-061 giữ nguyên) — chỉ để biết phiếu thuộc đội nào và
        /// xếp gợi ý Recruiter.
        /// </summary>
        public Guid? DepartmentId { get; set; }

        /// <summary>Số lượng cần tuyển.</summary>
        public int Headcount { get; set; } = 1;

        /// <summary>
        /// Mức độ ưu tiên — <c>high</c> | <c>medium</c> | <c>low</c>, xem
        /// <see cref="Constants.RecruitmentPriority"/>.
        ///
        /// Không chỉ để hiển thị: hàng chờ duyệt của HR Leader xếp theo cột này. Một ô bắt buộc nhập
        /// mà không điều khiển gì thì chỉ là thêm việc cho người lập phiếu.
        /// </summary>
        public string Priority { get; set; } = "medium";

        /// <summary>Lý do phát sinh nhu cầu (thay người nghỉ, mở rộng đội, dự án mới…).</summary>
        public string? Reason { get; set; }

        /// <summary>Mô tả sơ bộ công việc — nguyên liệu để Recruiter viết JD.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Kỹ năng và tiêu chí ứng viên phải đáp ứng, do chính HM nêu.
        ///
        /// Tách khỏi <see cref="Description"/> vì hai thứ này trả lời hai câu hỏi khác nhau — "làm
        /// gì" và "cần gì để làm được" — và chỉ có trưởng bộ phận biết vế thứ hai. Gộp chung một ô
        /// thì phần yêu cầu kỹ thuật hay bị viết qua loa, và Recruiter phải tự đoán khi dựng JD.
        /// Nội dung này được ghép thẳng vào bản nháp JD ở bước dựng tin.
        /// </summary>
        public string? Requirements { get; set; }

        public string? EmploymentType { get; set; }
        public string? WorkMode { get; set; }
        public string? Location { get; set; }
        public string? ExperienceLevel { get; set; }

        /// <summary>Thời điểm mong muốn người mới vào làm.</summary>
        public DateTimeOffset? ExpectedStartDate { get; set; }

        // ===== Dải lương HM đề xuất =====
        // HR Leader từ chối phiếu thường là để chỉnh đúng hai con số này, nên chúng nằm ngay trên
        // phiếu chứ không đợi tới lúc dựng tin.
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; } = "VND";

        // ===== Cổng duyệt của HR Leader =====
        /// <summary>pending | approved | rejected | cancelled — xem <see cref="Constants.RecruitmentRequestStatus"/>.</summary>
        public string Status { get; set; } = "pending";

        /// <summary>Lý do từ chối (BẮT BUỘC khi reject) hoặc ghi chú khi duyệt.</summary>
        public string? ReviewReason { get; set; }

        /// <summary>HR Leader đã xử lý phiếu. Không bao giờ trùng <see cref="RequestedByUserId"/>.</summary>
        public Guid? ReviewedByUserId { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>
        /// Recruiter được HR Leader phân công ngay trong thao tác duyệt. Chỉ người này (và admin)
        /// được dựng tin từ phiếu — phân công rỗng thì phiếu duyệt xong vẫn không ai làm.
        /// </summary>
        public Guid? AssignedRecruiterId { get; set; }

        // CỐ Ý KHÔNG có cột `JobPostingId` ở đây. Liên kết phiếu ↔ tin chỉ tồn tại MỘT chiều, ở
        // `JobPosting.RecruitmentRequestId`. Khai cả hai chiều là hai nguồn sự thật cho cùng một sự
        // kiện, và ADR-058 đã trả giá đúng cho kiểu đó (`booked_count` trôi lệch khỏi chính các dòng
        // booking mà nó đếm). "Phiếu đã dựng tin chưa" suy ra bằng LEFT JOIN — không thể sai lệch.

        // ===== Thu hồi phê duyệt (ADR-066) =====
        // Hai thao tác cùng một bản chất — huỷ hiệu lực chữ ký của HR Leader — chỉ khác đích đến:
        // mở lại để sửa (→ pending) hay đóng hẳn vì hết nhu cầu (→ cancelled).

        /// <summary>
        /// Lý do thu hồi phê duyệt. BẮT BUỘC, vì thao tác này lấy mất việc đã giao cho Recruiter và
        /// huỷ một quyết định ngân sách của HR Leader — cả hai người đó phải đọc được vì sao.
        ///
        /// Cố ý KHÔNG dùng lại <see cref="ReviewReason"/>: ô đó là ghi chú của người DUYỆT, ghi đè
        /// lên là xoá mất bản ghi phê duyệt ngay lúc cần đối chiếu nhất.
        /// </summary>
        public string? RevokedReason { get; set; }

        /// <summary>
        /// Người thu hồi — HM chủ phiếu hoặc quản trị viên.
        ///
        /// Không dùng lại <see cref="ReviewedByUserId"/> vì cột đó mang bất biến "không bao giờ trùng
        /// <see cref="RequestedByUserId"/>", mà HM đóng phiếu của chính mình thì trùng ngay.
        /// </summary>
        public Guid? RevokedByUserId { get; set; }

        /// <summary>Số lần HM gửi lại sau khi bị từ chối — để HR Leader thấy phiếu đã đi mấy vòng.</summary>
        public int SubmissionCount { get; set; } = 1;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
