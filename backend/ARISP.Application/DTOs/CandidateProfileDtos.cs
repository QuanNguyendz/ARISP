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
