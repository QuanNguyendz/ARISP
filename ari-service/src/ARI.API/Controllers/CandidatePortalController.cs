using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>Form nộp hồ sơ ứng tuyển qua Job Board (multipart). CvFile tùy chọn — không có thì dùng CV hồ sơ.</summary>
    public class PortalApplyFormRequest
    {
        public string? CandidateName { get; set; }
        public string? CandidatePhone { get; set; }
        public string? CoverLetter { get; set; }
        public string? NoticePeriod { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CvFile { get; set; }
    }

    public class VerifyCvInfoRequest
    {
        public string? CandidateName { get; set; }
        public string? CandidatePhone { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CvFile { get; set; }
    }

    /// <summary>
    /// Cổng ứng viên — controller thin: nghiệp vụ nằm trong ARI.Application/CandidatePortal (CQRS),
    /// controller giữ trích claims, guard IFormFile và mapping Result → HTTP y hệt shape cũ.
    /// </summary>
    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidatePortalController : ControllerBase
    {
        private readonly ISender _sender;

        public CandidatePortalController(ISender sender)
        {
            _sender = sender;
        }

        private bool TryGetCandidateId(out Guid candidateId)
        {
            candidateId = Guid.Empty;
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(subClaim) && Guid.TryParse(subClaim, out candidateId);
        }

        private string? GetEmailClaim() =>
            User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

        /// <summary>
        /// GET /api/portal/jobs/{jobPostingId}/cv-match
        /// Phân tích độ phù hợp CV–JD dùng CHÍNH CV trong hồ sơ của ứng viên hiện tại (nếu có).
        /// </summary>
        [HttpGet("jobs/{jobPostingId:guid}/cv-match")]
        public async Task<IActionResult> GetCvMatch(Guid jobPostingId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetCvMatchQuery(jobPostingId, candidateId), ct);
            if (result.IsFailure)
                return Unauthorized(new { message = result.Error });
            return Ok(result.Value);
        }

        // ============================================================
        // SAVED JOBS (Việc đã lưu / bookmark)
        // ============================================================

        [HttpGet("saved-jobs")]
        public async Task<IActionResult> GetSavedJobs(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetSavedJobsQuery(candidateId), ct);
            return Ok(result.Value);
        }

        [HttpGet("saved-jobs/ids")]
        public async Task<IActionResult> GetSavedJobIds(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetSavedJobIdsQuery(candidateId), ct);
            return Ok(result.Value);
        }

        [HttpPost("saved-jobs/{jobPostingId:guid}")]
        public async Task<IActionResult> SaveJob(Guid jobPostingId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new SaveJobCommand(candidateId, jobPostingId), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(new { saved = true });
        }

        [HttpDelete("saved-jobs/{jobPostingId:guid}")]
        public async Task<IActionResult> UnsaveJob(Guid jobPostingId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            await _sender.Send(new UnsaveJobCommand(candidateId, jobPostingId), ct);
            return Ok(new { saved = false });
        }

        // ============================================================
        // APPLY / VERIFY CV
        // ============================================================

        /// <summary>POST /api/portal/applications/verify-cv-info — so sánh thông tin liên hệ và nội dung CV bằng AI.</summary>
        [HttpPost("applications/verify-cv-info")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> VerifyCvInfo([FromForm] VerifyCvInfoRequest form, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            if (string.IsNullOrWhiteSpace(form.CandidateName))
                return BadRequest(new { message = "Vui lòng nhập họ và tên." });
            if (string.IsNullOrWhiteSpace(form.CandidatePhone))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại." });

            byte[]? bytes = null;
            string? fileName = null;
            if (form.CvFile != null && form.CvFile.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(form.CvFile.FileName).ToLowerInvariant();
                if (ext != ".pdf" && ext != ".docx")
                    return BadRequest(new { message = "Chỉ chấp nhận CV định dạng PDF hoặc DOCX.", code = "bad_format" });
                if (form.CvFile.Length > 10 * 1024 * 1024)
                    return BadRequest(new { message = "Kích thước CV tối đa 10MB.", code = "too_large" });
                using var ms = new System.IO.MemoryStream();
                await form.CvFile.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
                fileName = System.IO.Path.GetFileName(form.CvFile.FileName);
            }

            var result = await _sender.Send(new VerifyCvInfoCommand(candidateId, form.CandidateName, form.CandidatePhone, bytes, fileName), ct);
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.Unauthorized => Unauthorized(new { message = result.Error }),
                    "no_cv" or "cv_unreadable" => BadRequest(new { message = result.Error, code = result.ErrorCode }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }

            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/portal/applications/{jobPostingId}/apply — nộp hồ sơ ứng tuyển qua Job Board.
        /// CV mặc định lấy từ hồ sơ; ứng viên có thể đính kèm CV khác cho riêng tin này (CvFile).
        /// </summary>
        [HttpPost("applications/{jobPostingId:guid}/apply")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> Apply(Guid jobPostingId, [FromForm] PortalApplyFormRequest form, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            // Các trường bắt buộc.
            if (string.IsNullOrWhiteSpace(form.CandidateName))
                return BadRequest(new { message = "Vui lòng nhập họ và tên." });
            if (string.IsNullOrWhiteSpace(form.CandidatePhone))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại." });
            if (string.IsNullOrWhiteSpace(form.NoticePeriod))
                return BadRequest(new { message = "Vui lòng nhập thời gian báo trước khi nghỉ việc." });

            byte[]? bytes = null;
            string? fileName = null;
            if (form.CvFile != null && form.CvFile.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(form.CvFile.FileName).ToLowerInvariant();
                if (ext != ".pdf" && ext != ".docx")
                    return BadRequest(new { message = "Chỉ chấp nhận CV định dạng PDF hoặc DOCX.", code = "bad_format" });
                if (form.CvFile.Length > 10 * 1024 * 1024)
                    return BadRequest(new { message = "Kích thước CV tối đa 10MB.", code = "too_large" });
                using var ms = new System.IO.MemoryStream();
                await form.CvFile.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
                fileName = System.IO.Path.GetFileName(form.CvFile.FileName);
            }

            var result = await _sender.Send(new ApplyToJobCommand(
                jobPostingId, candidateId, form.CandidateName!, form.CandidatePhone!, form.CoverLetter, form.NoticePeriod!,
                bytes, fileName), ct);

            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.Unauthorized => Unauthorized(new { message = result.Error }),
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    CommonErrorCodes.ServerError => StatusCode(500, new { message = result.Error }),
                    "no_cv" or "cv_unreadable" => BadRequest(new { message = result.Error, code = result.ErrorCode }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }

            var outcome = result.Value;
            if (outcome.AlreadyApplied)
                return Conflict(new { message = "Bạn đã ứng tuyển vị trí này rồi.", code = "already_applied", applicationId = outcome.ExistingApplicationId });

            return Ok(outcome.Application);
        }

        // ============================================================
        // NOTIFICATIONS (Thông báo)
        // ============================================================

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetPortalNotificationsQuery(candidateId), ct);
            return Ok(result.Value);
        }

        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new MarkAllPortalNotificationsReadCommand(candidateId), ct);
            return Ok(new { updated = result.Value });
        }

        [HttpPost("notifications/{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new MarkPortalNotificationReadCommand(candidateId, id), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(new { read = true });
        }

        [HttpDelete("notifications/{id:guid}")]
        public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new DeletePortalNotificationCommand(candidateId, id), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(new { deleted = true });
        }

        [HttpDelete("notifications")]
        public async Task<IActionResult> DeleteAllNotifications(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new DeleteAllPortalNotificationsCommand(candidateId), ct);
            return Ok(new { deleted = result.Value });
        }

        // ==================== CÀI ĐẶT (Settings) ====================

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetPortalSettingsQuery(candidateId), ct);
            if (result.IsFailure)
                return Unauthorized(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] CandidateSettingsDto settings, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new UpdatePortalSettingsCommand(candidateId, settings), ct);
            if (result.IsFailure)
                return Unauthorized(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpGet("settings/export")]
        public async Task<IActionResult> ExportData(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new ExportMyDataQuery(candidateId), ct);
            if (result.IsFailure)
                return Unauthorized(new { message = result.Error });

            var file = result.Value;
            return File(file.Bytes, file.ContentType, file.FileName);
        }

        [HttpPost("settings/logout-all")]
        public async Task<IActionResult> LogoutAllDevices(CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new LogoutAllDevicesCommand(candidateId), ct);
            return Ok(new { revoked = result.Value });
        }

        // ============================================================
        // APPLICATIONS (Hồ sơ ứng tuyển của tôi)
        // ============================================================

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            if (!TryGetCandidateId(out var candidateAccountId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyApplicationsQuery(candidateAccountId, GetEmailClaim()));
            return Ok(result.Value);
        }

        [HttpGet("applications/{id:guid}")]
        public async Task<IActionResult> GetApplicationDetail(Guid id)
        {
            if (!TryGetCandidateId(out var candidateAccountId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyApplicationDetailQuery(id, candidateAccountId, GetEmailClaim()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.Forbidden
                    ? Forbid()
                    : NotFound(new { message = result.Error });
            }
            return Ok(result.Value);
        }

        [HttpGet("evaluations/{sessionId:guid}")]
        public async Task<IActionResult> GetSessionEvaluation(Guid sessionId)
        {
            if (!TryGetCandidateId(out var candidateAccountId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyEvaluationQuery(sessionId, candidateAccountId, GetEmailClaim()));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.Forbidden => Forbid(),
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }
            return Ok(result.Value);
        }

        // ============================================================
        // PRACTICE (Phỏng vấn thử — transcript + nhận xét AI, riêng tư của ứng viên, ADR-051)
        // ============================================================

        /// <summary>GET /api/portal/practice/sessions — các buổi thử đã làm (lọc theo hồ sơ nếu truyền applicationId).</summary>
        [HttpGet("practice/sessions")]
        public async Task<IActionResult> GetPracticeSessions([FromQuery] Guid? applicationId)
        {
            if (!TryGetCandidateId(out var candidateAccountId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyPracticeSessionsQuery(applicationId, candidateAccountId, GetEmailClaim()));
            return Ok(result.Value);
        }

        /// <summary>GET /api/portal/practice/sessions/{sessionId} — transcript đầy đủ + nhận xét AI của buổi thử.</summary>
        [HttpGet("practice/sessions/{sessionId:guid}")]
        public async Task<IActionResult> GetPracticeReview(Guid sessionId)
        {
            if (!TryGetCandidateId(out var candidateAccountId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyPracticeReviewQuery(sessionId, candidateAccountId, GetEmailClaim()));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.Forbidden => Forbid(),
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }
            return Ok(result.Value);
        }

        // ============================================================
        // CANDIDATE PROFILE
        // ============================================================

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetMyProfileQuery(candidateId));
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] CandidateProfileUpdateRequest req)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new UpdateMyProfileCommand(candidateId, req));
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>POST /api/portal/profile/cv — tải lên CV hồ sơ (PDF/DOCX), Gemini đánh giá CV và lưu kết quả.</summary>
        [HttpPost("profile/cv")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCv(Microsoft.AspNetCore.Http.IFormFile? cvFile)
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

            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            {
                await cvFile.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            var result = await _sender.Send(new UploadProfileCvCommand(candidateId, bytes, cvFile.FileName, ext));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    "invalid_cv" => BadRequest(new { message = result.Error, code = "invalid_cv" }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }

            var v = result.Value;
            return Ok(new
            {
                profileCvUrl = v.ProfileCvUrl,
                cvFileName = v.CvFileName,
                cvDownloadUrl = v.CvDownloadUrl,
                review = v.Review,
                aiAvailable = v.AiAvailable,
                aiMessage = v.AiMessage
            });
        }

        /// <summary>POST /api/portal/profile/change-password — đổi (hoặc đặt lần đầu cho tài khoản Google) mật khẩu.</summary>
        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] CandidateChangePasswordRequest request)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new ChangeCandidatePasswordCommand(candidateId, request.CurrentPassword, request.NewPassword));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    "wrong_current_password" => BadRequest(new { message = result.Error, code = "wrong_current_password" }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }

            return Ok(new
            {
                message = result.Value.Message,
                hasPassword = true
            });
        }
    }
}
