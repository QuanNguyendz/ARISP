using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.CandidatePortal
{
    /// <summary>Helpers dùng chung của feature CandidatePortal — chuyển verbatim từ private helpers của controller cũ.</summary>
    internal static class PortalSupport
    {
        public static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public static string ComputeHash(byte[] bytes)
        {
            return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
        }

        public static List<string> DeserializeStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        /// <summary>CriterionScores lưu dạng {"technical":88,...} → list {Name, Score} cho FE render thanh điểm.</summary>
        public static List<object> ParseCriterionScores(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<object>();
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOpts);
                if (dict == null) return new List<object>();
                return dict.Select(kv => (object)new { Name = kv.Key, Score = kv.Value }).ToList();
            }
            catch { return new List<object>(); }
        }

        /// <summary>QuestionAnalyses lưu dạng mảng {Question, Answer, Score, Analysis, Feedback}.</summary>
        public static List<QuestionAnalysisDto> ParseQuestionAnalyses(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<QuestionAnalysisDto>();
            try { return JsonSerializer.Deserialize<List<QuestionAnalysisDto>>(json, JsonOpts) ?? new List<QuestionAnalysisDto>(); }
            catch { return new List<QuestionAnalysisDto>(); }
        }

        /// <summary>LanguageAssessment lưu dạng {fluency, grammar, vocabulary, comprehension, overall_score}.</summary>
        public static LanguageAssessmentDto? ParseLanguageAssessment(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<LanguageAssessmentDto>(json, JsonOpts); }
            catch { return null; }
        }

        /// <summary>
        /// Ứng viên đủ điều kiện phỏng vấn thử khi đã QUA vòng CV (HR chuyển khỏi
        /// <c>invited</c>/<c>cv_submitted</c>) và chưa ở trạng thái kết thúc. Tuân ADR-038.
        /// </summary>
        public static bool PracticeEligible(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s != "invited" && s != "cv_submitted" && s != "withdrawn"
                   && s != "pass" && s != "not_pass";
        }

        /// <summary>
        /// IDOR Protection + Auto-link: hồ sơ thuộc về ứng viên khi khớp <c>CandidateAccountId</c>, hoặc
        /// hồ sơ cũ chưa gắn tài khoản nhưng trùng email trong token → gắn luôn rồi coi là chủ sở hữu.
        /// </summary>
        public static async Task<bool> TryEnsureOwnerAsync(
            ARI.Domain.Entities.Application app, Guid candidateAccountId, string? emailClaim, IUnitOfWork unitOfWork)
        {
            if (app.CandidateAccountId == candidateAccountId)
                return true;

            if (!app.CandidateAccountId.HasValue && !string.IsNullOrEmpty(emailClaim) &&
                string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                app.CandidateAccountId = candidateAccountId;
                unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);
                await unitOfWork.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public static T DeserializeOrEmpty<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try { return JsonSerializer.Deserialize<T>(json, JsonOpts) ?? new T(); }
            catch { return new T(); }
        }

        public static CvReviewResponse? DeserializeReview(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<CvReviewResponse>(json, JsonOpts); }
            catch { return null; }
        }

        public static CandidateProfileResponse MapProfile(CandidateAccount acc)
        {
            var hasPw = !string.IsNullOrEmpty(acc.PasswordHash);
            return new CandidateProfileResponse
            {
                Id = acc.Id.ToString(),
                Email = acc.Email,
                FullName = acc.FullName,
                Headline = acc.Headline,
                Phone = acc.Phone,
                Location = acc.Location,
                ProvinceCode = acc.ProvinceCode,
                ProvinceName = acc.ProvinceName,
                WardCode = acc.WardCode,
                WardName = acc.WardName,
                DateOfBirth = acc.DateOfBirth,
                About = acc.About,
                LinkedinUrl = acc.LinkedinUrl,
                GithubUrl = acc.GithubUrl,
                PortfolioUrl = acc.PortfolioUrl,
                ProfileCvUrl = acc.ProfileCvUrl,
                CvFileName = acc.ProfileCvFileName,
                EmailVerified = acc.EmailVerified,
                CvReview = DeserializeReview(acc.CvReviewJson),
                Skills = DeserializeOrEmpty<List<string>>(acc.SkillsJson),
                Experience = DeserializeOrEmpty<List<CandidateExperienceItem>>(acc.ExperienceJson),
                Education = DeserializeOrEmpty<List<CandidateEducationItem>>(acc.EducationJson),
                HasPassword = hasPw
            };
        }

        /// <summary>Map hồ sơ + resolve CV storageKey thành URL hiển thị (presigned nếu dùng S3).</summary>
        public static async Task<CandidateProfileResponse> BuildProfileAsync(CandidateAccount acc, IFileStorageService fileStorage)
        {
            var resp = MapProfile(acc);
            if (!string.IsNullOrEmpty(acc.ProfileCvUrl))
            {
                var downloadName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName) ? "cv" + System.IO.Path.GetExtension(acc.ProfileCvUrl) : acc.ProfileCvFileName;
                resp.ProfileCvUrl = await fileStorage.GetUrlAsync(acc.ProfileCvUrl);
                resp.CvDownloadUrl = await fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, downloadName);
            }
            return resp;
        }
    }
}
