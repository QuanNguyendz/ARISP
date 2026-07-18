using System;

namespace ARISP.Domain.Entities
{
    public class CandidateAccount : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Headline { get; set; }
        public string? ProfileCvUrl { get; set; }
        public string? ProfileCvFileName { get; set; }   // Tên file CV gốc (hiển thị + gợi ý tên khi tải về)

        // Hồ sơ cá nhân mở rộng (Candidate Profile)
        public string? Location { get; set; }                // Chuỗi hiển thị gộp "Phường X, Tỉnh Y" (denormalized)
        // Địa giới hành chính theo Provinces Open API v2 (sau sáp nhập 07/2025): 2 cấp Tỉnh → Phường/Xã
        public int? ProvinceCode { get; set; }
        public string? ProvinceName { get; set; }
        public int? WardCode { get; set; }
        public string? WardName { get; set; }
        public string? DateOfBirth { get; set; }       // ISO date string "yyyy-MM-dd"
        public string? About { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? PortfolioUrl { get; set; }
        public string SkillsJson { get; set; } = "[]";       // JSON array of string
        public string ExperienceJson { get; set; } = "[]";   // JSON array of {title, organization, period, description}
        public string EducationJson { get; set; } = "[]";    // JSON array of {school, degree, period, note}
        public string? CvReviewJson { get; set; }            // Kết quả Gemini đánh giá CV hồ sơ (CvReviewResponse)
        public string? SettingsJson { get; set; }            // Tùy chọn cá nhân: thông báo, quyền riêng tư, ngôn ngữ (CandidateSettingsDto)

        public bool IsActive { get; set; } = true;
        public bool EmailVerified { get; set; } = false;
        public DateTimeOffset? LastLoginAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
