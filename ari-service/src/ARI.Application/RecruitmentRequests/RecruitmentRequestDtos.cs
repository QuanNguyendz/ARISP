using System;

namespace ARI.Application.RecruitmentRequests
{
    /// <summary>Nội dung phiếu do Hiring Manager nhập — dùng chung cho tạo mới và sửa (ADR-063).</summary>
    public record RecruitmentRequestInput(
        string Title,

        // CỐ Ý không có `DepartmentId`: đội của phiếu luôn lấy từ tài khoản người lập (ADR-065).
        // Một ô nhận từ client rồi bị bỏ qua trông như có tác dụng — đúng kiểu hiểu nhầm dẫn tới
        // "HM Team A nhưng phiếu ghi Team B" mà ADR-065 đi sửa.

        int Headcount,

        /// <summary>high | medium | low — quyết định thứ tự hàng chờ duyệt của HR Leader.</summary>
        string? Priority,

        string? Reason,
        string? Description,

        /// <summary>Kỹ năng và tiêu chí ứng viên phải đáp ứng — ghép thẳng vào bản nháp JD.</summary>
        string? Requirements,

        string? EmploymentType,
        string? WorkMode,
        string? Location,
        string? ExperienceLevel,
        DateTimeOffset? ExpectedStartDate,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,

        /// <summary>
        /// Người lập tích "Thoả thuận". KHÔNG có cột tương ứng trong DB — trạng thái này SUY RA từ
        /// việc cả hai ô lương trống. Cờ ở đây chỉ để phân biệt "cố ý thoả thuận" với "quên điền"
        /// ngay lúc kiểm dữ liệu; sau đó nó biến mất.
        /// </summary>
        bool SalaryNegotiable = false);

    /// <summary>Một dòng trong danh sách phiếu.</summary>
    public record RecruitmentRequestListItemDto(
        Guid Id,
        string Title,
        Guid? DepartmentId,

        /// <summary>Tên đội, tra bằng join lúc đọc — phiếu chỉ lưu khoá.</summary>
        string? Department,

        int Headcount,
        string Priority,
        string Status,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,
        string RequestedByName,
        Guid RequestedByUserId,
        string? AssignedRecruiterName,
        Guid? AssignedRecruiterId,
        string? ReviewReason,
        int SubmissionCount,

        /// <summary>
        /// Tin đã dựng từ phiếu — SUY RA từ <c>job_postings.recruitment_request_id</c>, không phải
        /// cột lưu sẵn trên phiếu. Xem chú thích "không khai hai chiều" ở entity.
        /// </summary>
        Guid? JobPostingId,

        DateTimeOffset CreatedAt,
        DateTimeOffset? ReviewedAt);

    /// <summary>Chi tiết phiếu — thêm phần mô tả dài mà danh sách không cần chở.</summary>
    public record RecruitmentRequestDetailDto(
        Guid Id,
        string Title,
        Guid? DepartmentId,
        string? Department,
        int Headcount,
        string Priority,
        string? Reason,
        string? Description,
        string? Requirements,
        string? EmploymentType,
        string? WorkMode,
        string? Location,
        string? ExperienceLevel,
        DateTimeOffset? ExpectedStartDate,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,
        string Status,
        string? ReviewReason,
        Guid RequestedByUserId,
        string RequestedByName,
        Guid? ReviewedByUserId,
        string? ReviewedByName,
        DateTimeOffset? ReviewedAt,
        Guid? AssignedRecruiterId,
        string? AssignedRecruiterName,
        Guid? JobPostingId,
        int SubmissionCount,

        /// <summary>Lý do thu hồi phê duyệt (ADR-066) — hiện cho cả phiếu đã đóng lẫn phiếu vừa mở lại.</summary>
        string? RevokedReason,
        string? RevokedByName,

        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,

        /// <summary>
        /// Người đang xem có sửa/gửi lại được phiếu này không. Tính ở SERVER — màn hình không được
        /// tự suy ra từ vai trò, vì cùng một vai trò nhưng khác chủ phiếu thì khác quyền (ADR-061).
        /// </summary>
        bool CanEdit,

        /// <summary>Người đang xem có duyệt/từ chối được không. Luôn <c>false</c> với chính chủ phiếu.</summary>
        bool CanReview,

        /// <summary>Người đang xem có dựng được tin từ phiếu này không (Recruiter được phân công, hoặc admin).</summary>
        bool CanCreateJob,

        /// <summary>
        /// Còn thu hồi được phê duyệt không (ADR-066) — mở lại để sửa hoặc đóng phiếu.
        ///
        /// Một cờ cho cả hai nút vì chúng cùng một điều kiện. Tách đôi là mở đường cho trạng thái "mở lại
        /// được nhưng không đóng được" — vô nghĩa, mà trạng thái nào biểu diễn được thì sẽ có lúc xảy ra.
        /// </summary>
        bool CanRevoke);
}
