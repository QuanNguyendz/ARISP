using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARI.Application.DTOs;

namespace ARI.Application.Jobs
{
    /// <summary>
    /// Helpers dùng chung của feature Jobs — validation tạo/sửa tin, parse filter CSV,
    /// facet label maps + builder, tiện ích tên file. Chuyển verbatim từ JobsController cũ.
    /// </summary>
    internal static class JobsSupport
    {
        /// <summary>
        /// Validate request tạo/sửa tin — trả message lỗi đầu tiên hoặc null nếu hợp lệ.
        /// <paramref name="existingDeadline"/> != null (update-mode): deadline quá khứ được
        /// chấp nhận nếu KHÔNG đổi so với giá trị hiện tại.
        /// </summary>
        public static string? ValidateJobRequest(CreateJobPostingRequest request, DateTimeOffset? existingDeadline, bool isUpdate)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return "Title is required.";

            if (string.IsNullOrWhiteSpace(request.JobDescription))
                return "JobDescription is required.";

            if (request.Title?.Length > 200)
                return "Title cannot exceed 200 characters.";

            var allowedModes = new[] { "remote", "onsite", "both" };
            if (!allowedModes.Contains(request.InterviewMode))
                return "InterviewMode must be 'remote', 'onsite', or 'both'.";

            if (request.InterviewMode != "remote" && string.IsNullOrWhiteSpace(request.Location))
                return "Location is required when InterviewMode is not 'remote'.";

            if (request.SalaryMin < 0 || request.SalaryMax < 0)
                return "Salary cannot be negative.";

            if (request.SalaryMin.HasValue && request.SalaryMax.HasValue && request.SalaryMax < request.SalaryMin)
                return "SalaryMax cannot be less than SalaryMin.";

            if (request.SalaryIsNegotiable && (request.SalaryMin.HasValue || request.SalaryMax.HasValue))
                return "SalaryMin and SalaryMax must be null when SalaryIsNegotiable is true.";

            var allowedCategories = new[] { "backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other" };
            if (!string.IsNullOrWhiteSpace(request.JobCategory) && !allowedCategories.Contains(request.JobCategory.ToLower()))
                return $"JobCategory is invalid. Must be one of: {string.Join(", ", allowedCategories)}";

            if (request.ApplicationDeadline.HasValue &&
                request.ApplicationDeadline.Value <= DateTimeOffset.UtcNow &&
                (!isUpdate || request.ApplicationDeadline.Value != existingDeadline))
                return "ApplicationDeadline must be in the future.";

            if (request.RescheduleDeadlineHours < 0)
                return "RescheduleDeadlineHours cannot be negative.";

            if (request.InviteTokenTtlHours <= 0)
                return "InviteTokenTtlHours must be greater than 0.";

            if (request.RoundConfigs == null || request.RoundConfigs.Count == 0)
                return "At least one interview round configuration is required.";

            foreach (var round in request.RoundConfigs)
            {
                if (round.RoundNumber <= 0) return "RoundNumber must be > 0.";
                if (round.MaxDurationMinutes <= 0) return "MaxDurationMinutes must be > 0.";
                if (round.InterviewCodeTtlHours <= 0) return "InterviewCodeTtlHours must be > 0.";
            }

            return null;
        }

        public static List<string> ParseCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .ToList();
        }

        /// <summary>Chèn hậu tố vào tên file trước phần mở rộng. VD: "JD.pdf" + "-da-duyet" => "JD-da-duyet.pdf".</summary>
        public static string AppendSuffix(string fileName, string suffix)
        {
            var ext = Path.GetExtension(fileName);
            var name = Path.GetFileNameWithoutExtension(fileName);
            return $"{name}{suffix}{ext}";
        }

        // ===== Label maps & ordering cho facets (giá trị thô -> nhãn hiển thị) =====
        public static readonly Dictionary<string, string> CategoryLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["backend"] = "Backend", ["frontend"] = "Frontend", ["devops"] = "DevOps / Infra",
            ["qa"] = "QA / Testing", ["data"] = "Data", ["ai_ml"] = "AI / ML",
            ["mobile"] = "Mobile", ["pm"] = "Project Manager", ["designer"] = "Designer", ["other"] = "Khác"
        };
        public static readonly List<string> CategoryOrder = new()
        { "backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other" };

        public static readonly Dictionary<string, string> EmploymentTypeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["full_time"] = "Full-time", ["part_time"] = "Part-time",
            ["contract"] = "Hợp đồng", ["internship"] = "Thực tập", ["freelance"] = "Freelance"
        };
        public static readonly List<string> EmploymentTypeOrder = new()
        { "full_time", "part_time", "contract", "internship", "freelance" };

        public static readonly Dictionary<string, string> ExperienceLevelLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["intern"] = "Intern / Fresher", ["fresher"] = "Intern / Fresher", ["junior"] = "Junior",
            ["middle"] = "Middle", ["senior"] = "Senior", ["lead"] = "Lead / Manager", ["manager"] = "Lead / Manager"
        };
        public static readonly List<string> ExperienceLevelOrder = new()
        { "intern", "fresher", "junior", "middle", "senior", "lead", "manager" };

        public static readonly Dictionary<string, string> WorkModeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["onsite"] = "Onsite", ["hybrid"] = "Hybrid", ["remote"] = "Remote"
        };
        public static readonly List<string> WorkModeOrder = new() { "onsite", "hybrid", "remote" };

        public static readonly Dictionary<string, string> LanguageLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["vi"] = "Tiếng Việt", ["en"] = "English"
        };

        /// <summary>
        /// Gom nhóm + đếm theo giá trị thô (bỏ rỗng/null), gắn nhãn hiển thị và sắp xếp.
        /// Nếu có <paramref name="order"/> thì sắp theo thứ tự đó; nếu không thì sắp giảm dần theo count.
        /// </summary>
        public static List<JobFacetItem> BuildFacet(
            IEnumerable<string?> values,
            Dictionary<string, string>? labels,
            List<string>? order)
        {
            var grouped = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new JobFacetItem
                {
                    // Khi có bảng nhãn (enum thô) -> chuẩn hoá value về lowercase để FE so khớp ổn định.
                    // Không có nhãn (location, skill) -> giữ nguyên giá trị gốc.
                    Value = labels != null ? g.Key.ToLowerInvariant() : g.Key,
                    Label = labels != null && labels.TryGetValue(g.Key, out var lbl) ? lbl : g.Key,
                    Count = g.Count()
                });

            // Gom nhóm theo Label hiển thị để tránh bị lặp (vd: "Intern / Fresher" cho cả 'intern' và 'fresher')
            if (labels != null)
            {
                grouped = grouped
                    .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new JobFacetItem
                    {
                        Value = string.Join(",", g.Select(x => x.Value).Distinct()),
                        Label = g.Key,
                        Count = g.Sum(x => x.Count)
                    });
            }

            var list = grouped.ToList();

            if (order != null)
            {
                return list
                    .OrderBy(item => {
                        var firstVal = item.Value.Split(',')[0];
                        var idx = order.IndexOf(firstVal);
                        return idx < 0 ? int.MaxValue : idx;
                    })
                    .ThenByDescending(item => item.Count)
                    .ToList();
            }

            return list
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
