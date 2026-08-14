using System.Collections.Generic;

namespace ARI.Application.DTOs
{
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
    
    public class StaffSettingsDto
    {
        public bool ReceiveEmail { get; set; } = true;
        public bool ReceivePush { get; set; } = true;
    }

    /// <summary>Hồ sơ cá nhân của nhân sự nội bộ (màn Cài đặt → tab Hồ sơ).</summary>
    public class StaffProfileDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Department { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>False = tài khoản chỉ đăng nhập Google, chưa từng đặt mật khẩu.</summary>
        public bool HasPassword { get; set; }
    }

    public class UpdateStaffProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? Department { get; set; }
    }

    public class ChangeStaffPasswordRequest
    {
        public string? CurrentPassword { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Một Recruiter kèm tải tuyển dụng và các nút thắt đang tồn — màn "Phân công & tải tuyển
    /// dụng" của HR Lead.
    ///
    /// Cấu trúc bám nghiệp vụ ATS chứ không phải CRUD tài khoản: người quản lý tuyển dụng theo
    /// dõi *req load* (số tin đang gánh), *pipeline theo giai đoạn* và *tuổi chờ* để phát hiện
    /// quá tải và nút thắt trước khi vỡ SLA, rồi cân tải bằng cách chuyển giao tin. Vòng đời tài
    /// khoản (tạo/khoá/đổi vai trò) vẫn thuộc Super Admin (ADR-023/041) và không có ở đây.
    /// </summary>
    public class RecruiterOverviewDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Department { get; set; }
        public bool IsActive { get; set; }
        public string? LockReason { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }

        // --- Req load ---
        /// <summary>Tin đang phụ trách (chưa xoá mềm).</summary>
        public int JobsTotal { get; set; }
        /// <summary>Tin đang tuyển — đây mới là tải thực sự, tin đóng không tốn công nữa.</summary>
        public int JobsActive { get; set; }

        // --- Nút thắt theo giai đoạn, kèm tuổi chờ của mục cũ nhất (ngày) ---
        /// <summary>Tin nháp đang chờ HR Lead duyệt. Nút thắt này nằm ở phía HR Lead.</summary>
        public int DraftsAwaitingApproval { get; set; }
        public int DraftsOldestDays { get; set; }

        /// <summary>Hồ sơ mới nộp chưa được sàng (chưa chuyển khỏi trạng thái nộp).</summary>
        public int ApplicationsUnscreened { get; set; }
        public int UnscreenedOldestDays { get; set; }

        /// <summary>Đã qua sàng nhưng chưa được gán lịch phỏng vấn — việc của recruiter.</summary>
        public int AwaitingScheduling { get; set; }
        public int AwaitingSchedulingOldestDays { get; set; }

        /// <summary>Ứng viên đã báo bận, chỗ đã trả lại kho và đang chờ gán lịch khác.</summary>
        public int DeclinedNeedRebooking { get; set; }

        /// <summary>Đánh giá buổi THẬT chưa ai xác nhận. Nút thắt nằm ở phía HR Lead.</summary>
        public int PendingReviews { get; set; }
        public int PendingReviewsOldestDays { get; set; }

        // --- Tổng hợp ---
        /// <summary>Tổng hồ sơ đang chạy trong pipeline của người này (chưa đóng).</summary>
        public int ActivePipeline { get; set; }
        /// <summary>Tổng hồ sơ đã tuyển được (qua hết vòng).</summary>
        public int Hired { get; set; }
        /// <summary>
        /// Tuổi chờ lớn nhất trong mọi nút thắt (ngày) — một con số để xếp hạng ai đang tắc nhất.
        /// </summary>
        public int OldestBottleneckDays { get; set; }
    }

    /// <summary>Một tin đang phụ trách — dùng ở hộp thoại chuyển giao của HR Lead.</summary>
    public class RecruiterJobBriefDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Candidates { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class ReassignJobRequest
    {
        public Guid ToRecruiterId { get; set; }
        public string? Reason { get; set; }
    }
}
