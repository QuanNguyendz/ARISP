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
    /// Một Recruiter kèm khối lượng công việc — màn "Quản lý Recruiter" của HR Lead.
    /// Chỉ đọc: mọi thao tác vòng đời tài khoản (tạo/khoá/đổi vai trò) thuộc Super Admin
    /// (ADR-023/041), HR Lead xin cấp tài khoản qua luồng AccountRequest.
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
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Tin tuyển dụng do người này tạo (chưa xoá mềm).</summary>
        public int JobsTotal { get; set; }
        public int JobsActive { get; set; }
        /// <summary>Tin còn nháp — số này cao nghĩa là đang chờ HR Lead duyệt.</summary>
        public int JobsDraft { get; set; }
        /// <summary>Hồ sơ ứng tuyển vào các tin của người này.</summary>
        public int CandidatesTotal { get; set; }
        /// <summary>Hồ sơ đã có đánh giá THẬT nhưng chưa ai xác nhận — việc đang tồn đọng.</summary>
        public int PendingReviews { get; set; }
    }
}
