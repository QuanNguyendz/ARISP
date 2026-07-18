using System.Collections.Generic;

namespace ARISP.Application.DTOs
{
    public class CandidateExperienceItem
    {
        public string Title { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CandidateEducationItem
    {
        public string School { get; set; } = string.Empty;
        public string Degree { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    /// <summary>Bật/tắt một loại thông báo theo 2 kênh.</summary>
    public class NotificationChannelPref
    {
        public bool Email { get; set; } = true;
        public bool Push { get; set; } = true;
    }

    /// <summary>Tùy chọn cá nhân của ứng viên (thông báo, quyền riêng tư, ngôn ngữ giao diện).</summary>
    public class CandidateSettingsDto
    {
        // Ngôn ngữ giao diện ("vi" | "en") — không ảnh hưởng ngôn ngữ phỏng vấn.
        public string Language { get; set; } = "vi";

        // Thông báo theo loại × kênh (Email / Đẩy).
        public NotificationChannelPref InterviewInvite { get; set; } = new();
        public NotificationChannelPref Result { get; set; } = new();
        public NotificationChannelPref ApplicationUpdate { get; set; } = new();
        public NotificationChannelPref JobSuggestion { get; set; } = new() { Email = false, Push = false };

        // Quyền riêng tư.
        public bool AllowHrViewProfile { get; set; } = true;
        public bool AllowRecording { get; set; } = true;
        public bool MarketingEmail { get; set; } = false;
    }

    /// <summary>Kết quả phân tích độ phù hợp CV–JD (gọn) trả cho màn chi tiết tin tuyển dụng.</summary>
    public class CvMatchAnalysisDto
    {
        public int MatchScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> SkillsMatched { get; set; } = new();
        public List<string> SkillsGaps { get; set; } = new();
        public string ExperienceRelevance { get; set; } = string.Empty;
        public string OverallRecommendation { get; set; } = string.Empty;

        /// <summary>Nhà cung cấp AI đã tạo phân tích ("Gemini" | "GPT-4o-mini") — hiển thị trên UI.</summary>
        public string? ReviewedBy { get; set; }
    }

    /// <summary>
    /// Phản hồi cho <c>GET /api/portal/jobs/{id}/cv-match</c>: trạng thái CV của ứng viên +
    /// kết quả phân tích (nếu chạy được). <c>HasCv=false</c> → FE hiện nút tải CV.
    /// </summary>
    public class CvMatchResponse
    {
        public bool HasCv { get; set; }
        public string? CvFileName { get; set; }
        public string? CvUrl { get; set; }
        public string? CvDownloadUrl { get; set; }
        public bool AiAvailable { get; set; }
        public string? Message { get; set; }
        public CvMatchAnalysisDto? Analysis { get; set; }

        /// <summary>none | processing | completed | failed — FE poll khi "processing".</summary>
        public string Status { get; set; } = "none";
    }

    /// <summary>Hồ sơ ứng viên trả về cho trang Profile.</summary>
    public class CandidateProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Headline { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public int? ProvinceCode { get; set; }
        public string? ProvinceName { get; set; }
        public int? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? About { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? PortfolioUrl { get; set; }
        public string? ProfileCvUrl { get; set; }
        public string? CvFileName { get; set; }
        public string? CvDownloadUrl { get; set; }
        public bool EmailVerified { get; set; }
        public bool HasPassword { get; set; }
        public CvReviewResponse? CvReview { get; set; }
        public List<string> Skills { get; set; } = new();
        public List<CandidateExperienceItem> Experience { get; set; } = new();
        public List<CandidateEducationItem> Education { get; set; } = new();
    }

    /// <summary>Payload đổi/đặt mật khẩu ứng viên từ trang Profile.</summary>
    public class CandidateChangePasswordRequest
    {
        /// <summary>Mật khẩu hiện tại — không bắt buộc với tài khoản Google chưa từng đặt mật khẩu.</summary>
        public string? CurrentPassword { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>Payload cập nhật hồ sơ ứng viên.</summary>
    public class CandidateProfileUpdateRequest
    {
        public string? FullName { get; set; }
        public string? Headline { get; set; }
        public string? Phone { get; set; }
        public int? ProvinceCode { get; set; }
        public string? ProvinceName { get; set; }
        public int? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? About { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? PortfolioUrl { get; set; }
        public List<string>? Skills { get; set; }
        public List<CandidateExperienceItem>? Experience { get; set; }
        public List<CandidateEducationItem>? Education { get; set; }
    }
}
