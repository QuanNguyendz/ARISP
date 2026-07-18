using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARISP.Application.DTOs
{
    /// <summary>
    /// Kết quả Gemini trích xuất thông tin Job Posting từ file JD (PDF/DOCX) — output JSON thô.
    /// Dùng để tự động điền (auto-fill) các trường trống trong form tạo tin; người dùng vẫn sửa được.
    /// </summary>
    public class JdExtractionResultDto
    {
        [JsonPropertyName("is_valid_jd")]
        public bool IsValidJd { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("department")]
        public string? Department { get; set; }

        /// <summary>Mô tả công việc đã được làm sạch/định dạng lại từ file JD.</summary>
        [JsonPropertyName("job_description")]
        public string? JobDescription { get; set; }

        /// <summary>Một trong: backend|frontend|devops|qa|data|ai_ml|mobile|pm|designer|other</summary>
        [JsonPropertyName("job_category")]
        public string? JobCategory { get; set; }

        /// <summary>Một trong: intern|fresher|junior|middle|senior|lead|manager</summary>
        [JsonPropertyName("experience_level")]
        public string? ExperienceLevel { get; set; }

        /// <summary>Một trong: full_time|part_time|contract|internship|freelance</summary>
        [JsonPropertyName("employment_type")]
        public string? EmploymentType { get; set; }

        /// <summary>Một trong: onsite|hybrid|remote</summary>
        [JsonPropertyName("work_mode")]
        public string? WorkMode { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new();

        /// <summary>Yêu cầu ngôn ngữ phỏng vấn nếu JD có nêu (vd "English (TOEIC > 700)"). Null nếu tiếng Việt.</summary>
        [JsonPropertyName("language_requirement")]
        public string? LanguageRequirement { get; set; }

        [JsonPropertyName("salary_min")]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salary_max")]
        public decimal? SalaryMax { get; set; }

        [JsonIgnore] public int PromptTokens { get; set; }
        [JsonIgnore] public int CompletionTokens { get; set; }
    }

    /// <summary>
    /// Response trả về FE sau khi upload + phân tích JD: vừa có dữ liệu auto-fill,
    /// vừa có storageKey + metadata file JD đã lưu để đính kèm vào job khi submit.
    /// </summary>
    public class AnalyzeJdResponse
    {
        public bool IsValidJd { get; set; }

        // Thông tin file JD đã lưu (để gửi lại trong CreateJobPostingRequest)
        public string JdFileUrl { get; set; } = string.Empty;   // storageKey
        public string JdFileName { get; set; } = string.Empty;
        public string JdFileFormat { get; set; } = string.Empty; // pdf | docx

        // Dữ liệu auto-fill
        public string? Title { get; set; }
        public string? Department { get; set; }
        public string? JobDescription { get; set; }
        public string? JobCategory { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkMode { get; set; }
        public string? Location { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? LanguageRequirement { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
    }
}
