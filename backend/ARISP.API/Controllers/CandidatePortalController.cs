using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    /// <summary>Form nộp hồ sơ ứng tuyển qua Job Board (multipart). CvFile tùy chọn — không có thì dùng CV hồ sơ.</summary>
    public class PortalApplyFormRequest
    {
        public string? CandidateName { get; set; }
        public string? CandidatePhone { get; set; }
        public string? DesiredLocation { get; set; }
        public string? CoverLetter { get; set; }
        public string? NoticePeriod { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CvFile { get; set; }
    }

    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidatePortalController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGeminiProvider _geminiProvider;
        private readonly IDocumentParserService _documentParserService;
        private readonly IFileStorageService _fileStorage;
        private readonly CvJdAnalysisService _cvJdAnalysisService;
        private readonly ApplicationService _applicationService;
        private readonly IServiceScopeFactory _scopeFactory;

        // Trạng thái phân tích CV-JD đang chạy nền (key = "{jobId}:{cvHash}"). Dùng cho lỗi AI
        // (không ghi row vào DB) để poll biết được kết quả thất bại. Kết quả thành công nằm ở DB cache.
        private sealed class MatchJobState
        {
            public string Status = "processing";
            public string? Message;
        }
        private static readonly ConcurrentDictionary<string, MatchJobState> _matchJobs = new();

        public CandidatePortalController(
            IUnitOfWork unitOfWork,
            IGeminiProvider geminiProvider,
            IDocumentParserService documentParserService,
            IFileStorageService fileStorage,
            CvJdAnalysisService cvJdAnalysisService,
            ApplicationService applicationService,
            IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _geminiProvider = geminiProvider;
            _documentParserService = documentParserService;
            _fileStorage = fileStorage;
            _cvJdAnalysisService = cvJdAnalysisService;
            _applicationService = applicationService;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// GET /api/portal/jobs/{jobPostingId}/cv-match
        /// Phân tích độ phù hợp CV–JD dùng CHÍNH CV trong hồ sơ của ứng viên hiện tại (nếu có).
        /// Không có CV → trả <c>hasCv=false</c> để FE hiện nút tải CV. Kết quả được cache theo
        /// (job + CV hash) nên không chạy lại Gemini cho cùng một CV.
        /// </summary>
        [HttpGet("jobs/{jobPostingId:guid}/cv-match")]
        public async Task<IActionResult> GetCvMatch(Guid jobPostingId, CancellationToken ct)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản ứng viên." });

            // Chưa có CV trong hồ sơ → FE hiện nút "Tải CV lên".
            if (string.IsNullOrEmpty(acc.ProfileCvUrl))
                return Ok(new CvMatchResponse { HasCv = false, Status = "none" });

            var fileName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName)
                ? "cv" + System.IO.Path.GetExtension(acc.ProfileCvUrl)
                : acc.ProfileCvFileName;

            var resp = new CvMatchResponse
            {
                HasCv = true,
                CvFileName = fileName,
                CvUrl = await _fileStorage.GetUrlAsync(acc.ProfileCvUrl, ct),
                CvDownloadUrl = await _fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, fileName, ct)
            };

            var bytes = await _fileStorage.ReadAllBytesAsync(acc.ProfileCvUrl, ct);
            if (bytes == null || bytes.Length == 0)
            {
                resp.AiAvailable = false;
                resp.Status = "failed";
                resp.Message = "Không đọc được file CV đã lưu.";
                return Ok(resp);
            }

            var cvHash = ComputeHash(bytes);
            var key = $"{jobPostingId}:{cvHash}";

            // 1. Đã có kết quả cache trong DB (không chạy lại Gemini).
            var cached = (await _unitOfWork.Repository<CvJdAnalysis>()
                .FindAsync(x => x.JobPostingId == jobPostingId && x.CvHash == cvHash, ct)).FirstOrDefault();
            if (cached != null && cached.Status == "completed")
            {
                _matchJobs.TryRemove(key, out _);
                resp.AiAvailable = true;
                resp.Status = "completed";
                resp.Analysis = new CvMatchAnalysisDto
                {
                    MatchScore = cached.MatchScore,
                    Summary = cached.Summary,
                    SkillsMatched = DeserializeStringList(cached.SkillsMatched),
                    SkillsGaps = DeserializeStringList(cached.SkillsGaps),
                    ExperienceRelevance = cached.ExperienceRelevance,
                    OverallRecommendation = cached.OverallRecommendation,
                    ReviewedBy = cached.AiModel
                };
                return Ok(resp);
            }
            if (cached != null && cached.Status == "failed")
            {
                _matchJobs.TryRemove(key, out _);
                resp.AiAvailable = false;
                resp.Status = "failed";
                resp.Message = cached.ErrorMessage ?? "CV không hợp lệ.";
                return Ok(resp);
            }

            // 2. Có job nền đang/đã chạy. Lỗi AI (không ghi DB) được giữ ở bộ nhớ để poll đọc.
            if (_matchJobs.TryGetValue(key, out var state))
            {
                if (state.Status == "failed")
                {
                    _matchJobs.TryRemove(key, out _);
                    resp.AiAvailable = false;
                    resp.Status = "failed";
                    resp.Message = state.Message ?? "Phân tích CV thất bại.";
                    return Ok(resp);
                }
                resp.Status = "processing";
                return Ok(resp);
            }

            // 3. Chưa có gì → khởi chạy phân tích ở nền và trả "processing" để FE poll.
            if (_matchJobs.TryAdd(key, new MatchJobState { Status = "processing" }))
            {
                var bytesCopy = bytes;
                var fileNameCopy = fileName;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<CvJdAnalysisService>();
                        using var bgStream = new System.IO.MemoryStream(bytesCopy);
                        var r = await svc.AnalyzeAndCacheAsync(jobPostingId, bgStream, fileNameCopy, CancellationToken.None);
                        if (!r.IsFailure)
                        {
                            // Thành công → đã nằm trong DB cache, bỏ trạng thái nền.
                            _matchJobs.TryRemove(key, out _);
                        }
                        else if (_matchJobs.TryGetValue(key, out var s))
                        {
                            // Lỗi AI không ghi DB → giữ trạng thái failed cho poll kế tiếp.
                            s.Status = "failed";
                            s.Message = r.Error;
                        }
                    }
                    catch (Exception)
                    {
                        if (_matchJobs.TryGetValue(key, out var s))
                        {
                            s.Status = "failed";
                            s.Message = "Lỗi hệ thống khi phân tích CV.";
                        }
                    }
                });
            }

            resp.Status = "processing";
            return Ok(resp);
        }

        private static string ComputeHash(byte[] bytes)
        {
            return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
        }

        // ============================================================
        // SAVED JOBS (Việc đã lưu / bookmark)
        // ============================================================

        /// <summary>
        /// GET /api/portal/saved-jobs — danh sách việc làm ứng viên đã lưu (đầy đủ thông tin để render thẻ job),
        /// sắp xếp theo thời điểm lưu mới nhất. Bỏ qua các tin đã bị gỡ/đóng.
        /// </summary>
        [HttpGet("saved-jobs")]
        public async Task<IActionResult> GetSavedJobs(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var saved = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == candidateId, ct)).ToList();
            if (saved.Count == 0)
                return Ok(Array.Empty<object>());

            var savedAtByJob = saved
                .GroupBy(s => s.JobPostingId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.CreatedAt));

            var jobIds = savedAtByJob.Keys.ToList();
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct);

            // Chỉ hiển thị tin còn mở để ứng viên có thể ứng tuyển.
            var response = jobs
                .Where(j => j.Status == "active" && j.IsPublicListing)
                .Select(j =>
                {
                    var dto = JobPostingListItemResponse.FromEntity(j);
                    return new
                    {
                        dto.Id,
                        dto.Title,
                        dto.Department,
                        dto.Location,
                        dto.WorkMode,
                        dto.EmploymentType,
                        dto.ExperienceLevel,
                        dto.JobCategory,
                        dto.Skills,
                        dto.IsUrgent,
                        dto.SalaryMin,
                        dto.SalaryMax,
                        dto.SalaryCurrency,
                        dto.SalaryIsNegotiable,
                        dto.CreatedAt,
                        dto.PublishedAt,
                        SavedAt = savedAtByJob.TryGetValue(j.Id, out var at) ? at : j.CreatedAt
                    };
                })
                .OrderByDescending(j => j.SavedAt)
                .ToList();

            return Ok(response);
        }

        /// <summary>
        /// GET /api/portal/saved-jobs/ids — danh sách Id các job đã lưu (để Job Board tô đậm nút bookmark).
        /// </summary>
        [HttpGet("saved-jobs/ids")]
        public async Task<IActionResult> GetSavedJobIds(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var ids = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == candidateId, ct))
                .Select(s => s.JobPostingId)
                .Distinct()
                .ToList();

            return Ok(ids);
        }

        /// <summary>
        /// POST /api/portal/saved-jobs/{jobPostingId} — lưu (bookmark) một tin tuyển dụng. Idempotent.
        /// </summary>
        [HttpPost("saved-jobs/{jobPostingId:guid}")]
        public async Task<IActionResult> SaveJob(Guid jobPostingId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });

            var existing = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == candidateId && s.JobPostingId == jobPostingId, ct))
                .FirstOrDefault();

            if (existing == null)
            {
                await _unitOfWork.Repository<SavedJob>().AddAsync(new SavedJob
                {
                    CandidateAccountId = candidateId,
                    JobPostingId = jobPostingId
                }, ct);
                await _unitOfWork.SaveChangesAsync();
            }

            return Ok(new { saved = true });
        }

        /// <summary>
        /// DELETE /api/portal/saved-jobs/{jobPostingId} — bỏ lưu một tin tuyển dụng. Idempotent.
        /// </summary>
        [HttpDelete("saved-jobs/{jobPostingId:guid}")]
        public async Task<IActionResult> UnsaveJob(Guid jobPostingId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var existing = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == candidateId && s.JobPostingId == jobPostingId, ct))
                .ToList();

            if (existing.Count > 0)
            {
                foreach (var s in existing)
                    _unitOfWork.Repository<SavedJob>().Delete(s);
                await _unitOfWork.SaveChangesAsync();
            }

            return Ok(new { saved = false });
        }

        private static List<string> DeserializeStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        // ===== Parse các trường JSON thô của Evaluation thành cấu trúc gọn cho FE =====

        /// <summary>CriterionScores lưu dạng {"technical":88,...} → list {Name, Score} cho FE render thanh điểm.</summary>
        private static List<object> ParseCriterionScores(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<object>();
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, _jsonOpts);
                if (dict == null) return new List<object>();
                return dict.Select(kv => (object)new { Name = kv.Key, Score = kv.Value }).ToList();
            }
            catch { return new List<object>(); }
        }

        /// <summary>QuestionAnalyses lưu dạng mảng {Question, Answer, Score, Analysis, Feedback}.</summary>
        private static List<QuestionAnalysisDto> ParseQuestionAnalyses(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<QuestionAnalysisDto>();
            try { return JsonSerializer.Deserialize<List<QuestionAnalysisDto>>(json, _jsonOpts) ?? new List<QuestionAnalysisDto>(); }
            catch { return new List<QuestionAnalysisDto>(); }
        }

        /// <summary>LanguageAssessment lưu dạng {fluency, grammar, vocabulary, comprehension, overall_score}.</summary>
        private static LanguageAssessmentDto? ParseLanguageAssessment(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<LanguageAssessmentDto>(json, _jsonOpts); }
            catch { return null; }
        }

        /// <summary>
        /// POST /api/portal/applications/{jobPostingId}/apply
        /// Nộp hồ sơ ứng tuyển qua Job Board — gửi về cho bộ phận nhân sự.
        /// CV mặc định lấy từ hồ sơ; ứng viên có thể đính kèm CV khác cho riêng tin này (CvFile).
        /// Kèm xác nhận họ tên/SĐT, nơi làm việc mong muốn, thư giới thiệu + notice period.
        /// Tự đính kèm kết quả phân tích CV–JD đã cache (nếu có) và embed CV cho cá nhân hoá.
        /// </summary>
        [HttpPost("applications/{jobPostingId:guid}/apply")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> Apply(Guid jobPostingId, [FromForm] PortalApplyFormRequest form, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản ứng viên." });

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });

            // Các trường bắt buộc.
            if (string.IsNullOrWhiteSpace(form.CandidateName))
                return BadRequest(new { message = "Vui lòng nhập họ và tên." });
            if (string.IsNullOrWhiteSpace(form.CandidatePhone))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại." });
            if (string.IsNullOrWhiteSpace(form.DesiredLocation))
                return BadRequest(new { message = "Vui lòng nhập nơi làm việc mong muốn." });
            if (string.IsNullOrWhiteSpace(form.CoverLetter))
                return BadRequest(new { message = "Vui lòng trả lời câu hỏi giới thiệu bản thân." });
            if (string.IsNullOrWhiteSpace(form.NoticePeriod))
                return BadRequest(new { message = "Vui lòng nhập thời gian báo trước khi nghỉ việc." });

            // Chặn ứng tuyển trùng (đã có hồ sơ chưa rút cho tin này).
            var existing = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .FindAsync(a => a.JobPostingId == jobPostingId && a.CandidateAccountId == candidateId, ct))
                .Where(a => a.Status != "withdrawn")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
            if (existing != null)
                return Conflict(new { message = "Bạn đã ứng tuyển vị trí này rồi.", code = "already_applied", applicationId = existing.Id });

            // Nguồn CV: ưu tiên file ứng viên đính kèm cho tin này; nếu không có dùng CV hồ sơ.
            byte[] bytes;
            string fileName;
            string ext;
            if (form.CvFile != null && form.CvFile.Length > 0)
            {
                ext = System.IO.Path.GetExtension(form.CvFile.FileName).ToLowerInvariant();
                if (ext != ".pdf" && ext != ".docx")
                    return BadRequest(new { message = "Chỉ chấp nhận CV định dạng PDF hoặc DOCX.", code = "bad_format" });
                if (form.CvFile.Length > 10 * 1024 * 1024)
                    return BadRequest(new { message = "Kích thước CV tối đa 10MB.", code = "too_large" });
                using var ms = new System.IO.MemoryStream();
                await form.CvFile.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
                fileName = System.IO.Path.GetFileName(form.CvFile.FileName);
            }
            else
            {
                // Không đính kèm → bắt buộc phải có CV trong hồ sơ.
                if (string.IsNullOrEmpty(acc.ProfileCvUrl))
                    return BadRequest(new { message = "Bạn cần tải CV lên hồ sơ hoặc đính kèm CV cho tin này.", code = "no_cv" });
                bytes = await _fileStorage.ReadAllBytesAsync(acc.ProfileCvUrl, ct);
                if (bytes == null || bytes.Length == 0)
                    return BadRequest(new { message = "Không đọc được file CV trong hồ sơ. Vui lòng tải lại CV.", code = "cv_unreadable" });
                ext = System.IO.Path.GetExtension(acc.ProfileCvFileName ?? acc.ProfileCvUrl).ToLowerInvariant();
                fileName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName) ? $"cv{ext}" : acc.ProfileCvFileName;
            }

            var mime = ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };

            var cvHash = ComputeHash(bytes);

            string cvText = string.Empty;
            try
            {
                using var s = new System.IO.MemoryStream(bytes);
                cvText = (await _documentParserService.ParseDocumentAsync(s, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch { /* best-effort: vẫn ứng tuyển dù không parse được text */ }

            // Lưu một BẢN SAO CV riêng cho hồ sơ ứng tuyển (immutable — không phụ thuộc việc
            // ứng viên đổi/xoá CV hồ sơ sau này).
            string cvFileUrl;
            try
            {
                cvFileUrl = await _fileStorage.SaveAsync(bytes, fileName, mime);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Không thể lưu CV cho hồ sơ ứng tuyển: {ex.Message}" });
            }

            var serviceRequest = new SubmitApplicationRequest
            {
                JobPostingId = jobPostingId,
                CandidateAccountId = candidateId,
                CandidateEmail = acc.Email,
                CandidateName = form.CandidateName!.Trim(),
                CandidatePhone = form.CandidatePhone!.Trim(),
                CvFileUrl = cvFileUrl,
                CvText = cvText,
                CvFileHash = cvHash,
                DesiredLocation = form.DesiredLocation!.Trim(),
                CoverLetter = form.CoverLetter!.Trim(),
                NoticePeriod = form.NoticePeriod!.Trim()
            };

            var result = await _applicationService.SubmitApplicationAsync(serviceRequest, "job_board", ct);
            if (result.IsFailure)
            {
                await _fileStorage.DeleteAsync(cvFileUrl); // dọn file nếu tạo hồ sơ thất bại
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        // ============================================================
        // NOTIFICATIONS (Thông báo)
        // ============================================================

        /// <summary>
        /// GET /api/portal/notifications — đồng bộ thông báo từ sự kiện thực tế của ứng viên
        /// rồi trả danh sách (mới nhất trước) + số chưa đọc.
        /// </summary>
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            await SyncNotificationsAsync(candidateId, ct);

            var items = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == candidateId, ct))
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
                .ToList();

            return Ok(new { items, unreadCount = items.Count(i => !i.IsRead) });
        }

        /// <summary>POST /api/portal/notifications/read-all — đánh dấu tất cả đã đọc.</summary>
        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == candidateId && !n.IsRead, ct)).ToList();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
            }
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Ok(new { updated = list.Count });
        }

        /// <summary>POST /api/portal/notifications/{id}/read — đánh dấu một thông báo đã đọc.</summary>
        [HttpPost("notifications/{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(id, ct);
            if (n == null || n.CandidateAccountId != candidateId)
                return NotFound(new { message = "Không tìm thấy thông báo." });
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
                await _unitOfWork.SaveChangesAsync();
            }
            return Ok(new { read = true });
        }

        // ==================== CÀI ĐẶT (Settings) ====================

        /// <summary>GET /api/portal/settings — tùy chọn cá nhân (thông báo, quyền riêng tư, ngôn ngữ). Trả mặc định nếu chưa lưu.</summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản ứng viên." });

            return Ok(DeserializeOrEmpty<CandidateSettingsDto>(acc.SettingsJson));
        }

        /// <summary>PUT /api/portal/settings — lưu toàn bộ tùy chọn cá nhân (auto-save từ FE).</summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] CandidateSettingsDto settings, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản ứng viên." });

            settings ??= new CandidateSettingsDto();
            if (settings.Language != "en") settings.Language = "vi";

            acc.SettingsJson = JsonSerializer.Serialize(settings, _jsonOpts);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();
            return Ok(settings);
        }

        /// <summary>
        /// GET /api/portal/settings/export — xuất toàn bộ dữ liệu của ứng viên (hồ sơ + đơn ứng tuyển) dưới dạng JSON tải về.
        /// </summary>
        [HttpGet("settings/export")]
        public async Task<IActionResult> ExportData(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản ứng viên." });

            var apps = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .FindAsync(a => a.CandidateAccountId == candidateId, ct)).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = (await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct))
                .ToDictionary(j => j.Id, j => j.Title);

            var export = new
            {
                exportedAt = DateTimeOffset.UtcNow,
                profile = new
                {
                    acc.Email,
                    acc.FullName,
                    acc.Phone,
                    acc.Headline,
                    acc.Location,
                    acc.DateOfBirth,
                    acc.About,
                    acc.LinkedinUrl,
                    acc.GithubUrl,
                    acc.PortfolioUrl,
                    Skills = DeserializeOrEmpty<List<string>>(acc.SkillsJson),
                    Experience = DeserializeOrEmpty<List<CandidateExperienceItem>>(acc.ExperienceJson),
                    Education = DeserializeOrEmpty<List<CandidateEducationItem>>(acc.EducationJson),
                    CreatedAt = acc.CreatedAt,
                },
                applications = apps.OrderByDescending(a => a.CreatedAt).Select(a => new
                {
                    a.Id,
                    JobTitle = jobs.TryGetValue(a.JobPostingId, out var t) ? t : null,
                    a.Status,
                    a.DesiredLocation,
                    a.CoverLetter,
                    a.NoticePeriod,
                    a.CreatedAt,
                }),
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"arisp-data-{DateTime.UtcNow:yyyyMMdd}.json");
        }

        /// <summary>
        /// POST /api/portal/settings/logout-all — thu hồi toàn bộ refresh token của ứng viên,
        /// đăng xuất khỏi mọi thiết bị (phiên hiện tại sẽ hết hạn khi access token hết hiệu lực).
        /// </summary>
        [HttpPost("settings/logout-all")]
        public async Task<IActionResult> LogoutAllDevices(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var tokens = (await _unitOfWork.Repository<CandidateRefreshToken>()
                .FindAsync(t => t.CandidateAccountId == candidateId && t.RevokedAt == null, ct)).ToList();
            foreach (var t in tokens)
            {
                t.RevokedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<CandidateRefreshToken>().Update(t);
            }
            if (tokens.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Ok(new { revoked = tokens.Count });
        }

        /// <summary>
        /// Sinh thông báo từ các sự kiện thực tế của ứng viên (lời mời PV, kết quả đã chia sẻ,
        /// lịch sắp tới, nộp hồ sơ). Idempotent theo DedupKey — không tạo trùng, giữ trạng thái đã đọc.
        /// </summary>
        private async Task SyncNotificationsAsync(Guid candidateId, CancellationToken ct)
        {
            var apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .QueryAsync(q => q.Where(a => a.CandidateAccountId == candidateId)
                    .Select(a => new { a.Id, a.JobPostingId, a.CreatedAt }), ct);
            if (apps.Count == 0) return;

            var appIds = apps.Select(a => a.Id).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                .ToDictionary(j => j.Id, j => j.Title);
            string JobTitle(Guid jid) => jobs.TryGetValue(jid, out var t) ? t : "Vị trí tuyển dụng";

            var nowUtc = DateTimeOffset.UtcNow;
            var existing = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == candidateId, ct))
                .Select(n => n.DedupKey).ToHashSet();
            var toAdd = new List<Notification>();

            void Add(string key, string type, string title, string? body, string? link, DateTimeOffset createdAt)
            {
                if (existing.Contains(key)) return;
                existing.Add(key);
                toAdd.Add(new Notification
                {
                    CandidateAccountId = candidateId,
                    DedupKey = key,
                    Type = type,
                    Title = title,
                    Body = body,
                    Link = link,
                    CreatedAt = createdAt,
                    UpdatedAt = nowUtc
                });
            }

            // 1. Đã nộp hồ sơ
            foreach (var a in apps)
                Add($"applied:{a.Id}", "applied", "Đã nộp hồ sơ ứng tuyển",
                    JobTitle(a.JobPostingId), "/candidate/applications", a.CreatedAt);

            // 2. Lời mời phỏng vấn (mã On-site còn hiệu lực)
            var codes = (await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => appIds.Contains(c.ApplicationId) && c.UsedAt == null && c.ExpiresAt > nowUtc, ct)).ToList();
            foreach (var c in codes)
            {
                var app = apps.FirstOrDefault(a => a.Id == c.ApplicationId);
                Add($"invite:{c.Id}", "invite", $"Lời mời phỏng vấn vòng {c.RoundNumber}",
                    $"{(app != null ? JobTitle(app.JobPostingId) : "")} · On-site — mã phỏng vấn đã sẵn sàng tại Hồ sơ ứng tuyển.",
                    "/candidate/applications", c.CreatedAt);
            }

            // 3. Kết quả vòng đã được HR chia sẻ
            var sessionIds = await _unitOfWork.Repository<InterviewSession>()
                .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId)).Select(s => s.Id), ct);
            // Evaluation projection nhẹ (bỏ cột JSON lớn)
            var evals = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Where(e => sessionIds.Contains(e.SessionId))
                    .Select(e => new { e.Id, e.AiVerdict, e.OverallScore, e.RoundNumber, e.ApplicationId }), ct);
            var evalIds = evals.Select(e => e.Id).ToList();
            var reviews = evalIds.Any()
                ? (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct)).ToList()
                : new List<HrReview>();
            var reviewByEval = reviews.ToDictionary(r => r.EvaluationId, r => r);
            foreach (var ev in evals)
            {
                if (!reviewByEval.TryGetValue(ev.Id, out var rv) || !rv.ShareEvaluation) continue;
                var verdict = string.IsNullOrEmpty(rv.FinalVerdict) ? ev.AiVerdict : rv.FinalVerdict;
                var pass = verdict == "pass";
                var scoreText = ev.OverallScore.HasValue ? $" với điểm {Math.Round(ev.OverallScore.Value)}/100" : "";
                Add($"result:{ev.Id}", "result",
                    $"Kết quả vòng {ev.RoundNumber}: {(pass ? "Pass" : "Not Pass")}",
                    $"{JobTitle(apps.FirstOrDefault(a => a.Id == ev.ApplicationId)?.JobPostingId ?? Guid.Empty)}{scoreText}.",
                    $"/candidate/applications/{ev.ApplicationId}", rv.UpdatedAt);
            }

            // 4. Lịch phỏng vấn sắp tới
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => appIds.Contains(b.ApplicationId) && b.Status == "scheduled", ct)).ToList();
            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Any()
                ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s)
                : new Dictionary<Guid, AvailabilitySlot>();
            foreach (var b in bookings)
            {
                if (!slots.TryGetValue(b.AvailabilitySlotId, out var slot) || slot.StartTime <= nowUtc) continue;
                var app = apps.FirstOrDefault(a => a.Id == b.ApplicationId);
                Add($"schedule:{b.Id}", "schedule", $"Lịch phỏng vấn vòng {b.RoundNumber} sắp tới",
                    $"{(app != null ? JobTitle(app.JobPostingId) : "")} · {slot.StartTime:dd/MM HH:mm}",
                    "/candidate/applications", b.CreatedAt);
            }

            if (toAdd.Count > 0)
            {
                foreach (var n in toAdd) await _unitOfWork.Repository<Notification>().AddAsync(n, ct);
                await _unitOfWork.SaveChangesAsync();
            }
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

            // Liên kết các hồ sơ khớp email nhưng chưa gắn CandidateAccountId — làm thẳng bằng
            // SQL UPDATE (không nạp entity). Sau đó mọi hồ sơ của ứng viên đều có CandidateAccountId.
            if (!string.IsNullOrEmpty(emailClaim))
            {
                await _unitOfWork.ExecuteSqlRawAsync(
                    "UPDATE applications SET candidate_account_id = {0}, updated_at = {1} " +
                    "WHERE candidate_account_id IS NULL AND candidate_email = {2} AND deleted_at IS NULL",
                    new object[] { candidateAccountId, DateTimeOffset.UtcNow, emailClaim });
            }

            // Hồ sơ của ứng viên — projection nhẹ (KHÔNG kéo CvText/CoverLetter/DemographicData lớn).
            var appsList = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => a.CandidateAccountId == candidateAccountId)
                    .Select(a => new
                    {
                        a.Id, a.JobPostingId, a.CandidateEmail, a.CandidateName, a.Status,
                        a.CvJdAnalysisId, a.CvFileUrl, a.PracticeSessionUsed, a.Source, a.CreatedAt, a.UpdatedAt,
                    }));

            // Batch fetch jobs — chỉ cột cần (bỏ JobDescription/ScoringRubric...)
            var jobIds = appsList.Select(a => a.JobPostingId).Distinct().ToList();
            var jobsDict = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id))
                        .Select(j => new { j.Id, j.Title, j.Location, j.Department, j.InterviewMode })))
                .ToDictionary(j => j.Id, j => j);

            // Batch fetch CV–JD analyses (chỉ match score)
            var analysisIds = appsList.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var analysisDict = (await _unitOfWork.Repository<CvJdAnalysis>()
                    .QueryAsync(q => q.Where(x => analysisIds.Contains(x.Id)).Select(x => new { x.Id, x.MatchScore })))
                .ToDictionary(x => x.Id, x => x);

            // Sessions + evaluations (bỏ cột JSON lớn) + HR reviews để dựng tiến trình vòng phỏng vấn
            var appIds = appsList.Select(a => a.Id).ToList();
            var allSessions = await _unitOfWork.Repository<InterviewSession>()
                .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId))
                    .Select(s => new { s.Id, s.ApplicationId, s.RoundNumber, s.RoundType, s.SessionType, s.Status }));
            var sessionIds = allSessions.Select(s => s.Id).ToList();
            var allEvals = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Where(e => sessionIds.Contains(e.SessionId))
                    .Select(e => new { e.Id, e.SessionId, e.ApplicationId, e.AiVerdict, e.OverallScore }));
            var evalBySession = allEvals.ToDictionary(e => e.SessionId, e => e);
            var evalIds = allEvals.Select(e => e.Id).ToList();
            var allReviews = (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId))).ToList();
            var reviewByEval = allReviews.ToDictionary(r => r.EvaluationId, r => r);

            var nowUtc = DateTimeOffset.UtcNow;

            // Mã phỏng vấn On-site còn hiệu lực (chưa dùng, chưa hết hạn) — để hiển thị thẻ "Cần hành động".
            var activeCodes = (await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => appIds.Contains(c.ApplicationId) && c.UsedAt == null && c.ExpiresAt > nowUtc)).ToList();

            // Lịch phỏng vấn sắp tới (booking đã đặt + slot tương ứng).
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => appIds.Contains(b.ApplicationId) && b.Status == "scheduled")).ToList();
            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Any()
                ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList()
                : new List<AvailabilitySlot>();
            var slotById = slots.ToDictionary(s => s.Id, s => s);

            // Lời mời theo vòng — để xác định "vòng đang hoạt động" (vòng được mời mới nhất).
            var invites = (await _unitOfWork.Repository<InterviewInvite>()
                .FindAsync(i => appIds.Contains(i.ApplicationId))).ToList();

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

                    // Vòng đang hoạt động = vòng được mời mới nhất (mỗi vòng có 1 invite). Mặc định 1.
                    var inviteRounds = invites.Where(i => i.ApplicationId == a.Id).Select(i => i.RoundNumber).ToList();
                    int activeRound = inviteRounds.Count > 0 ? inviteRounds.Max() : 1;
                    // Phỏng vấn thử theo VÒNG: còn lượt nếu chưa có phiên practice của vòng này và
                    // chưa làm phỏng vấn thật của vòng này.
                    bool practiceUsedForRound = allSessions.Any(s => s.ApplicationId == a.Id && s.SessionType == "practice" && s.RoundNumber == activeRound);
                    bool realDoneForRound = allSessions.Any(s => s.ApplicationId == a.Id && s.SessionType == "real" && s.RoundNumber == activeRound);

                    bool pendingHrReview = false; // có vòng đã xong + AI đã chấm nhưng HR chưa xác nhận/chia sẻ
                    var rounds = allSessions
                        .Where(s => s.ApplicationId == a.Id)
                        .OrderBy(s => s.RoundNumber)
                        .Select(s =>
                        {
                            string? verdict = null;
                            decimal? overall = null;
                            bool hasEval = evalBySession.TryGetValue(s.Id, out var ev);
                            bool shared = hasEval && reviewByEval.TryGetValue(ev!.Id, out var rv) && rv.ShareEvaluation;
                            // Chỉ lộ verdict/điểm khi HR đã chia sẻ kết quả vòng đó (giữ nguyên mô hình bảo mật)
                            if (shared)
                            {
                                verdict = ev!.AiVerdict;
                                overall = ev.OverallScore;
                            }
                            // Vòng đã hoàn tất + AI đã có đánh giá nhưng HR chưa chia sẻ → đang chờ HR xác nhận
                            if (s.Status == "completed" && hasEval && !shared)
                                pendingHrReview = true;
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

                    // Mã phỏng vấn còn hiệu lực mới nhất (theo vòng).
                    var code = activeCodes
                        .Where(c => c.ApplicationId == a.Id)
                        .OrderByDescending(c => c.RoundNumber)
                        .ThenByDescending(c => c.CreatedAt)
                        .FirstOrDefault();

                    // Phản hồi của HR được chia sẻ cho ứng viên (nếu có).
                    var sharedFeedback = allReviews
                        .Where(r => r.ShareFeedback && !string.IsNullOrWhiteSpace(r.CandidateFeedback)
                            && allEvals.Any(e => e.Id == r.EvaluationId && e.ApplicationId == a.Id))
                        .OrderByDescending(r => r.UpdatedAt)
                        .FirstOrDefault();

                    // Lịch phỏng vấn sắp tới gần nhất.
                    var upcoming = bookings
                        .Where(b => b.ApplicationId == a.Id && slotById.ContainsKey(b.AvailabilitySlotId)
                            && slotById[b.AvailabilitySlotId].StartTime > nowUtc)
                        .OrderBy(b => slotById[b.AvailabilitySlotId].StartTime)
                        .Select(b => slotById[b.AvailabilitySlotId])
                        .FirstOrDefault();

                    return new
                    {
                        a.Id,
                        a.JobPostingId,
                        JobTitle = job?.Title,
                        Location = job?.Location,
                        Department = job?.Department,
                        InterviewMode = job?.InterviewMode,
                        a.CandidateEmail,
                        a.CandidateName,
                        a.Status,
                        MatchScore = matchScore,
                        CvFileUrl = cvUrlMap[a.Id],
                        a.PracticeSessionUsed,
                        // ADR-038: phỏng vấn thử mở cho ứng viên ĐÃ QUA vòng CV, tính theo TỪNG VÒNG
                        // (1 lượt/vòng) — vòng kế mở lại thử khi được mời lên vòng đó.
                        PracticeAvailable = PracticeEligible(a.Status) && !practiceUsedForRound && !realDoneForRound,
                        ActiveRound = activeRound,
                        PendingHrReview = pendingHrReview,
                        HrFeedback = sharedFeedback?.CandidateFeedback,
                        a.Source,
                        a.CreatedAt,
                        a.UpdatedAt,
                        Rounds = rounds,
                        InterviewCode = code == null ? null : new { code.Code, code.ExpiresAt, code.RoundNumber },
                        UpcomingInterview = upcoming == null ? null : new
                        {
                            upcoming.StartTime,
                            upcoming.Timezone,
                            RoundNumber = upcoming.RoundNumber
                        }
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

            // Lịch phỏng vấn sắp tới (booking đã đặt) cho hồ sơ này — để hiển thị "Lịch sắp tới".
            var nowUtc = DateTimeOffset.UtcNow;
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => b.ApplicationId == id && b.Status == "scheduled")).ToList();
            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Any()
                ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList()
                : new List<AvailabilitySlot>();
            var slotById = slots.ToDictionary(s => s.Id, s => s);
            var upcoming = bookings
                .Where(b => slotById.ContainsKey(b.AvailabilitySlotId) && slotById[b.AvailabilitySlotId].StartTime > nowUtc)
                .OrderBy(b => slotById[b.AvailabilitySlotId].StartTime)
                .Select(b => slotById[b.AvailabilitySlotId])
                .FirstOrDefault();

            // Mã phỏng vấn On-site còn hiệu lực (để liên kết qua trang Hồ sơ ứng tuyển).
            var activeCode = (await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => c.ApplicationId == id && c.UsedAt == null && c.ExpiresAt > nowUtc))
                .OrderByDescending(c => c.RoundNumber).ThenByDescending(c => c.CreatedAt)
                .FirstOrDefault();

            var sessionDetails = new List<object>();
            foreach (var s in sessions)
            {
                object? evalData = null;
                string? recordingUrl = null;
                bool transcriptShared = false;
                string? hrFeedback = null;
                string? hrFinalVerdict = null;
                bool pendingHrReview = false;

                if (evalDict.TryGetValue(s.Id, out var evaluation))
                {
                    reviewDict.TryGetValue(evaluation.Id, out var review);
                    bool sharedEval = review != null && review.ShareEvaluation;

                    // Vòng đã hoàn tất + AI đã chấm nhưng HR chưa chia sẻ → đang chờ HR xác nhận.
                    if (s.Status == "completed" && !sharedEval)
                        pendingHrReview = true;

                    if (review != null)
                    {
                        hrFinalVerdict = sharedEval ? review.FinalVerdict : null;
                        transcriptShared = review.ShareTranscript;
                        if (review.ShareRecording && !string.IsNullOrEmpty(s.RecordingUrl))
                            recordingUrl = await _fileStorage.GetUrlAsync(s.RecordingUrl);
                        if (review.ShareFeedback && !string.IsNullOrWhiteSpace(review.CandidateFeedback))
                            hrFeedback = review.CandidateFeedback;
                    }

                    if (sharedEval)
                    {
                        evalData = new
                        {
                            evaluation.Id,
                            evaluation.RoundNumber,
                            evaluation.AiVerdict,
                            evaluation.OverallScore,
                            evaluation.Reasoning,
                            evaluation.RecommendedNextStep,
                            CriterionScores = ParseCriterionScores(evaluation.CriterionScores),
                            QuestionAnalyses = ParseQuestionAnalyses(evaluation.QuestionAnalyses),
                            LanguageAssessment = ParseLanguageAssessment(evaluation.LanguageAssessment)
                        };
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
                    RecordingUrl = recordingUrl,
                    TranscriptShared = transcriptShared,
                    PendingHrReview = pendingHrReview,
                    HrFeedback = hrFeedback,
                    HrFinalVerdict = hrFinalVerdict,
                    Evaluation = evalData
                });
            }

            return Ok(new
            {
                app.Id,
                app.JobPostingId,
                JobTitle = job?.Title,
                JobDescription = job?.JobDescription,
                Location = job?.Location,
                Department = job?.Department,
                InterviewMode = job?.InterviewMode,
                DetectedLanguage = job?.DetectedLanguage,
                app.CandidateEmail,
                app.CandidateName,
                app.CandidatePhone,
                CvFileUrl = string.IsNullOrEmpty(app.CvFileUrl) ? app.CvFileUrl : await _fileStorage.GetUrlAsync(app.CvFileUrl),
                app.Status,
                app.CreatedAt,
                app.UpdatedAt,
                InterviewCode = activeCode == null ? null : new { activeCode.Code, activeCode.ExpiresAt, activeCode.RoundNumber },
                UpcomingInterview = upcoming == null ? null : new { upcoming.StartTime, upcoming.Timezone, RoundNumber = upcoming.RoundNumber },
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
                    ReviewedAt = DateTimeOffset.UtcNow.ToString("o"),
                    ReviewedBy = r.Provider
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

        /// <summary>
        /// Ứng viên đủ điều kiện phỏng vấn thử khi đã QUA vòng CV (HR chuyển khỏi
        /// <c>invited</c>/<c>cv_submitted</c>) và chưa ở trạng thái kết thúc. Tuân ADR-038:
        /// practice không mở khi HR còn đang xem hồ sơ.
        /// </summary>
        private static bool PracticeEligible(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s != "invited" && s != "cv_submitted" && s != "withdrawn"
                   && s != "pass" && s != "not_pass";
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
