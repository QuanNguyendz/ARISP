using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidatePortalController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGeminiProvider _geminiProvider;
        private readonly IDocumentParserService _documentParserService;
        private readonly IFileStorageService _fileStorage;

        public CandidatePortalController(
            IUnitOfWork unitOfWork,
            IGeminiProvider geminiProvider,
            IDocumentParserService documentParserService,
            IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _geminiProvider = geminiProvider;
            _documentParserService = documentParserService;
            _fileStorage = fileStorage;
        }

        /// <summary>
        /// GET /api/portal/applications
        /// Lấy danh sách hồ sơ ứng tuyển của ứng viên hiện tại (Lọc theo CandidateAccountId hoặc Email chưa liên kết)
        /// </summary>
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            // Fetch applications matching by CandidateAccountId or CandidateEmail (if CandidateAccountId is null)
            IEnumerable<ARISP.Domain.Entities.Application> apps;
            if (!string.IsNullOrEmpty(emailClaim))
            {
                apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => 
                    a.CandidateAccountId == candidateAccountId || 
                    (a.CandidateAccountId == null && a.CandidateEmail == emailClaim));
            }
            else
            {
                apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => 
                    a.CandidateAccountId == candidateAccountId);
            }

            var appsList = apps.ToList();

            // Link applications that are matched by email but don't have CandidateAccountId set
            bool hasUpdates = false;
            foreach (var app in appsList)
            {
                if (app.CandidateAccountId == null)
                {
                    app.CandidateAccountId = candidateAccountId;
                    _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                    hasUpdates = true;
                }
            }

            if (hasUpdates)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            // Batch fetch jobs (tránh N+1)
            var jobIds = appsList.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id));
            var jobsDict = jobs.ToDictionary(j => j.Id, j => j);

            // Batch fetch CV–JD analyses (match score)
            var analysisIds = appsList.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var analyses = analysisIds.Any()
                ? await _unitOfWork.Repository<CvJdAnalysis>().FindAsync(x => analysisIds.Contains(x.Id))
                : Enumerable.Empty<CvJdAnalysis>();
            var analysisDict = analyses.ToDictionary(x => x.Id, x => x);

            // Batch fetch sessions + evaluations + HR reviews để dựng tiến trình vòng phỏng vấn
            var appIds = appsList.Select(a => a.Id).ToList();
            var allSessions = (await _unitOfWork.Repository<InterviewSession>().FindAsync(s => appIds.Contains(s.ApplicationId))).ToList();
            var sessionIds = allSessions.Select(s => s.Id).ToList();
            var allEvals = sessionIds.Any()
                ? (await _unitOfWork.Repository<Evaluation>().FindAsync(e => sessionIds.Contains(e.SessionId))).ToList()
                : new List<Evaluation>();
            var evalBySession = allEvals.ToDictionary(e => e.SessionId, e => e);
            var evalIds = allEvals.Select(e => e.Id).ToList();
            var allReviews = evalIds.Any()
                ? (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId))).ToList()
                : new List<HrReview>();
            var reviewByEval = allReviews.ToDictionary(r => r.EvaluationId, r => r);

            // Resolve CV storageKey -> URL hiển thị (presigned nếu dùng S3) trước khi project (LINQ sync).
            var cvUrlMap = new Dictionary<Guid, string?>();
            foreach (var a in appsList)
                cvUrlMap[a.Id] = string.IsNullOrEmpty(a.CvFileUrl) ? a.CvFileUrl : await _fileStorage.GetUrlAsync(a.CvFileUrl);

            var response = appsList
                .OrderByDescending(a => a.UpdatedAt)
                .Select(a =>
                {
                    jobsDict.TryGetValue(a.JobPostingId, out var job);
                    int? matchScore = (a.CvJdAnalysisId.HasValue && analysisDict.TryGetValue(a.CvJdAnalysisId.Value, out var an)) ? an.MatchScore : (int?)null;

                    var rounds = allSessions
                        .Where(s => s.ApplicationId == a.Id)
                        .OrderBy(s => s.RoundNumber)
                        .Select(s =>
                        {
                            string? verdict = null;
                            decimal? overall = null;
                            // Chỉ lộ verdict/điểm khi HR đã chia sẻ kết quả vòng đó
                            if (evalBySession.TryGetValue(s.Id, out var ev)
                                && reviewByEval.TryGetValue(ev.Id, out var rv) && rv.ShareEvaluation)
                            {
                                verdict = ev.AiVerdict;
                                overall = ev.OverallScore;
                            }
                            return new
                            {
                                s.RoundNumber,
                                s.RoundType,
                                s.SessionType,
                                s.Status,
                                Verdict = verdict,
                                OverallScore = overall
                            };
                        })
                        .ToList();

                    return new
                    {
                        a.Id,
                        a.JobPostingId,
                        JobTitle = job?.Title,
                        Location = job?.Location,
                        Department = job?.Department,
                        a.CandidateEmail,
                        a.CandidateName,
                        a.Status,
                        MatchScore = matchScore,
                        CvFileUrl = cvUrlMap[a.Id],
                        a.PracticeSessionUsed,
                        a.Source,
                        a.CreatedAt,
                        a.UpdatedAt,
                        Rounds = rounds
                    };
                })
                .ToList();

            return Ok(response);
        }

        /// <summary>
        /// GET /api/portal/applications/{id}
        /// Chi tiết hồ sơ ứng tuyển + Danh sách buổi phỏng vấn & Kết quả đánh giá được share (Bảo mật IDOR)
        /// </summary>
        [HttpGet("applications/{id:guid}")]
        public async Task<IActionResult> GetApplicationDetail(Guid id)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            var app = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(id);
            if (app == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng tuyển." });
            }

            // IDOR Protection + Auto-link
            bool isOwner = app.CandidateAccountId == candidateAccountId;
            if (!isOwner && !app.CandidateAccountId.HasValue && !string.IsNullOrEmpty(emailClaim) &&
                string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                app.CandidateAccountId = candidateAccountId;
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                await _unitOfWork.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId);

            var sessionsResult = await _unitOfWork.Repository<InterviewSession>().FindAsync(s => s.ApplicationId == id);
            var sessions = sessionsResult.OrderBy(s => s.RoundNumber).ToList();

            // Optimize query: Fetch all evaluations and HR reviews in batch
            var sessionIds = sessions.Select(s => s.Id).ToList();
            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => sessionIds.Contains(e.SessionId));
            var evalDict = evaluations.ToDictionary(e => e.SessionId, e => e);

            var evalIds = evaluations.Select(e => e.Id).ToList();
            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId));
            var reviewDict = reviews.ToDictionary(r => r.EvaluationId, r => r);

            var sessionDetails = new List<object>();
            foreach (var s in sessions)
            {
                object? evalData = null;

                if (evalDict.TryGetValue(s.Id, out var evaluation))
                {
                    if (reviewDict.TryGetValue(evaluation.Id, out var review))
                    {
                        if (review.ShareEvaluation)
                        {
                            evalData = new
                            {
                                evaluation.Id,
                                evaluation.RoundNumber,
                                evaluation.AiVerdict,
                                evaluation.OverallScore,
                                evaluation.Reasoning,
                                evaluation.RecommendedNextStep,
                                evaluation.LanguageAssessment
                            };
                        }
                    }
                }

                sessionDetails.Add(new
                {
                    s.Id,
                    s.RoundNumber,
                    s.RoundType,
                    s.SessionType,
                    s.Status,
                    s.StartedAt,
                    s.EndedAt,
                    s.DurationSeconds,
                    s.RecordingUrl,
                    Evaluation = evalData
                });
            }

            return Ok(new
            {
                app.Id,
                app.JobPostingId,
                JobTitle = job?.Title,
                JobDescription = job?.JobDescription,
                app.CandidateEmail,
                app.CandidateName,
                app.CandidatePhone,
                CvFileUrl = string.IsNullOrEmpty(app.CvFileUrl) ? app.CvFileUrl : await _fileStorage.GetUrlAsync(app.CvFileUrl),
                app.Status,
                app.CreatedAt,
                Sessions = sessionDetails
            });
        }

        /// <summary>
        /// GET /api/portal/evaluations/{sessionId}
        /// Xem chi tiết đánh giá của vòng phỏng vấn nếu được HR cấu hình Share (Bảo mật IDOR)
        /// </summary>
        [HttpGet("evaluations/{sessionId:guid}")]
        public async Task<IActionResult> GetSessionEvaluation(Guid sessionId)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { message = "Không tìm thấy buổi phỏng vấn." });
            }

            var app = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId);
            if (app == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng tuyển liên quan." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            // IDOR Protection + Auto-link
            bool isOwner = app.CandidateAccountId == candidateAccountId;
            if (!isOwner && !app.CandidateAccountId.HasValue && !string.IsNullOrEmpty(emailClaim) &&
                string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                app.CandidateAccountId = candidateAccountId;
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                await _unitOfWork.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == sessionId);
            var evaluation = evaluations.FirstOrDefault();
            if (evaluation == null)
            {
                return NotFound(new { message = "Báo cáo đánh giá chưa được khởi tạo." });
            }

            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id);
            var review = reviews.FirstOrDefault();
            if (review == null || !review.ShareEvaluation)
            {
                return BadRequest(new { message = "Kết quả đánh giá chi tiết chưa được chia sẻ cho vòng phỏng vấn này." });
            }

            return Ok(new
            {
                evaluation.Id,
                evaluation.SessionId,
                evaluation.ApplicationId,
                evaluation.RoundNumber,
                evaluation.SessionType,
                evaluation.AiVerdict,
                evaluation.OverallScore,
                evaluation.CriterionScores,
                evaluation.Reasoning,
                evaluation.RecommendedNextStep,
                evaluation.QuestionAnalyses,
                evaluation.LanguageAssessment,
                evaluation.CreatedAt
            });
        }

        // ============================================================
        // CANDIDATE PROFILE
        // ============================================================

        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

        /// <summary>GET /api/portal/profile — hồ sơ cá nhân của ứng viên hiện tại.</summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId);
            if (acc == null)
                return NotFound(new { message = "Không tìm thấy tài khoản ứng viên." });

            return Ok(await BuildProfileAsync(acc));
        }

        /// <summary>PUT /api/portal/profile — cập nhật hồ sơ cá nhân.</summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] CandidateProfileUpdateRequest req)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId);
            if (acc == null)
                return NotFound(new { message = "Không tìm thấy tài khoản ứng viên." });

            if (!string.IsNullOrWhiteSpace(req.FullName)) acc.FullName = req.FullName.Trim();
            acc.Headline = req.Headline?.Trim();
            acc.Phone = req.Phone?.Trim();

            // Địa giới hành chính (Provinces Open API v2). Lưu code+name có cấu trúc và
            // tự suy ra chuỗi Location "Phường X, Tỉnh Y" để hiển thị/tương thích cũ.
            acc.ProvinceCode = req.ProvinceCode;
            acc.ProvinceName = req.ProvinceName?.Trim();
            acc.WardCode = req.WardCode;
            acc.WardName = req.WardName?.Trim();
            acc.Location = string.Join(", ", new[] { acc.WardName, acc.ProvinceName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(acc.Location)) acc.Location = null;

            acc.DateOfBirth = string.IsNullOrWhiteSpace(req.DateOfBirth) ? null : req.DateOfBirth.Trim();
            acc.About = req.About;
            acc.LinkedinUrl = req.LinkedinUrl?.Trim();
            acc.GithubUrl = req.GithubUrl?.Trim();
            acc.PortfolioUrl = req.PortfolioUrl?.Trim();

            if (req.Skills != null)
                acc.SkillsJson = JsonSerializer.Serialize(req.Skills.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList());
            if (req.Experience != null)
                acc.ExperienceJson = JsonSerializer.Serialize(req.Experience);
            if (req.Education != null)
                acc.EducationJson = JsonSerializer.Serialize(req.Education);

            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Ok(await BuildProfileAsync(acc));
        }

        /// <summary>
        /// POST /api/portal/profile/cv — tải lên CV hồ sơ (PDF/DOCX), Gemini đánh giá CV và lưu kết quả.
        /// </summary>
        [HttpPost("profile/cv")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCv(IFormFile? cvFile)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            if (cvFile == null || cvFile.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file CV." });

            var ext = System.IO.Path.GetExtension(cvFile.FileName).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".docx")
                return BadRequest(new { message = "Chỉ chấp nhận định dạng PDF hoặc DOCX." });
            if (cvFile.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file tối đa 5MB." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId);
            if (acc == null)
                return NotFound(new { message = "Không tìm thấy tài khoản ứng viên." });

            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            {
                await cvFile.CopyToAsync(ms);
                bytes = ms.ToArray();
            }
            var mime = ext == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            string fallbackText = string.Empty;
            try
            {
                using var s = new System.IO.MemoryStream(bytes);
                fallbackText = await _documentParserService.ParseDocumentAsync(s, ext);
            }
            catch { /* fallback text best-effort */ }

            // Gemini đánh giá CV
            var reviewResult = await _geminiProvider.ReviewCvAsync(bytes, mime, fallbackText);
            CvReviewResponse? reviewResp = null;
            if (reviewResult.IsSuccess)
            {
                var r = reviewResult.Value;
                if (!r.IsValidCv)
                    return BadRequest(new { message = "Tài liệu tải lên không phải là CV hợp lệ. Vui lòng tải lên CV (PDF/DOCX).", code = "invalid_cv" });

                reviewResp = new CvReviewResponse
                {
                    IsValidCv = true,
                    OverallScore = r.OverallScore,
                    Verdict = r.Verdict,
                    Summary = r.Summary,
                    Strengths = r.Strengths,
                    Improvements = r.Improvements,
                    MissingSections = r.MissingSections,
                    ReviewedAt = DateTimeOffset.UtcNow.ToString("o")
                };
            }
            // Nếu AI không khả dụng (vd thiếu API key) vẫn lưu CV, chỉ không có đánh giá.

            // Xoá CV cũ (nếu có) để không tích tụ rác trên storage.
            if (!string.IsNullOrEmpty(acc.ProfileCvUrl))
                await _fileStorage.DeleteAsync(acc.ProfileCvUrl);

            var storageKey = await _fileStorage.SaveAsync(bytes, cvFile.FileName, mime);
            var originalFileName = System.IO.Path.GetFileName(cvFile.FileName);

            acc.ProfileCvUrl = storageKey;
            acc.ProfileCvFileName = originalFileName;
            if (reviewResp != null)
                acc.CvReviewJson = JsonSerializer.Serialize(reviewResp);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                profileCvUrl = await _fileStorage.GetUrlAsync(acc.ProfileCvUrl),
                cvFileName = originalFileName,
                cvDownloadUrl = await _fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, originalFileName),
                review = reviewResp,
                aiAvailable = reviewResult.IsSuccess,
                aiMessage = reviewResult.IsFailure ? reviewResult.Error : null
            });
        }

        /// <summary>
        /// POST /api/portal/profile/change-password — đổi (hoặc đặt lần đầu cho tài khoản Google) mật khẩu đăng nhập.
        /// </summary>
        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] CandidateChangePasswordRequest request)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId);
            if (acc == null)
                return NotFound(new { message = "Không tìm thấy tài khoản ứng viên." });

            var hasPassword = !string.IsNullOrEmpty(acc.PasswordHash);

            // Tài khoản đã có mật khẩu → bắt buộc xác minh mật khẩu hiện tại trước khi đổi.
            if (hasPassword)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                    return BadRequest(new { message = "Vui lòng nhập mật khẩu hiện tại." });

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, acc.PasswordHash))
                    return BadRequest(new { message = "Mật khẩu hiện tại không đúng.", code = "wrong_current_password" });
            }

            if (!IsStrongPassword(request.NewPassword, out var validationError))
                return BadRequest(new { message = validationError });

            // Không cho đặt mật khẩu mới trùng mật khẩu cũ.
            if (hasPassword && BCrypt.Net.BCrypt.Verify(request.NewPassword, acc.PasswordHash))
                return BadRequest(new { message = "Mật khẩu mới không được trùng mật khẩu hiện tại." });

            acc.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                message = hasPassword ? "Đổi mật khẩu thành công." : "Đặt mật khẩu thành công.",
                hasPassword = true
            });
        }

        // Quy tắc mật khẩu mạnh — đồng bộ với AuthController.IsStrongPassword.
        private static bool IsStrongPassword(string password, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                errorMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
                return false;
            }
            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ hoa.";
                return false;
            }
            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ số.";
                return false;
            }
            const string specialCharacters = "!@#$%^&*";
            if (!password.Any(c => specialCharacters.Contains(c)))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.";
                return false;
            }
            return true;
        }

        private bool TryGetCandidateId(out Guid candidateId)
        {
            candidateId = Guid.Empty;
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(subClaim) && Guid.TryParse(subClaim, out candidateId);
        }

        private static CandidateProfileResponse MapProfile(CandidateAccount acc)
        {
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
                HasPassword = !string.IsNullOrEmpty(acc.PasswordHash),
                CvReview = DeserializeReview(acc.CvReviewJson),
                Skills = DeserializeOrEmpty<List<string>>(acc.SkillsJson),
                Experience = DeserializeOrEmpty<List<CandidateExperienceItem>>(acc.ExperienceJson),
                Education = DeserializeOrEmpty<List<CandidateEducationItem>>(acc.EducationJson),
            };
        }

        /// <summary>Map hồ sơ + resolve CV storageKey thành URL hiển thị (presigned nếu dùng S3).</summary>
        private async Task<CandidateProfileResponse> BuildProfileAsync(CandidateAccount acc)
        {
            var resp = MapProfile(acc);
            if (!string.IsNullOrEmpty(acc.ProfileCvUrl))
            {
                var downloadName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName) ? "cv" + System.IO.Path.GetExtension(acc.ProfileCvUrl) : acc.ProfileCvFileName;
                resp.ProfileCvUrl = await _fileStorage.GetUrlAsync(acc.ProfileCvUrl);
                resp.CvDownloadUrl = await _fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, downloadName);
            }
            return resp;
        }

        private static T DeserializeOrEmpty<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try { return JsonSerializer.Deserialize<T>(json, _jsonOpts) ?? new T(); }
            catch { return new T(); }
        }

        private static CvReviewResponse? DeserializeReview(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<CvReviewResponse>(json, _jsonOpts); }
            catch { return null; }
        }
    }
}
