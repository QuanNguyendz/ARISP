using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Application.Interviews;
using ARI.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/interview")] // Đồng bộ chuẩn prefix số ít theo đúng yêu cầu đồng bộ hệ thống backend
    public class InterviewController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public InterviewController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Map lỗi nghiệp vụ ra đúng mã HTTP. Trước đây khối management/* trả BadRequest cho mọi
        /// loại lỗi, nên "không có quyền" và "không tìm thấy" đều về 400 — giao diện không phân biệt
        /// được để xử lý khác nhau.
        /// </summary>
        private IActionResult MapFailure(Result result)
        {
            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
                CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>
        /// POST /api/interview/generate-code
        /// </summary>
        [HttpPost("generate-code")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCode([FromBody] GenerateCodeRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new GenerateInterviewCodeCommand(request.ApplicationId, request.RoundNumber, GetCurrentUserId()), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new
            {
                code = result.Value.Code,
                expiresAt = result.Value.ExpiresAt,
                applicationId = result.Value.ApplicationId
            });
        }

        /// <summary>
        /// POST /api/interview/generate-code-batch
        /// </summary>
        [HttpPost("generate-code-batch")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCodeBatch([FromBody] GenerateBatchRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new GenerateInterviewCodeBatchCommand(request.ApplicationIds, request.RoundNumber, GetCurrentUserId()), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            // Map định dạng trả về mảng danh sách: [ { code, applicationId }, ... ]
            var response = result.Value.Select(c => new
            {
                code = c.Code,
                applicationId = c.ApplicationId
            });

            return Ok(response);
        }

        /// <summary>
        /// POST /api/interview/validate-code
        /// </summary>
        [HttpPost("validate-code")]
        [AllowAnonymous] // Kiosk công cộng không cần Token Đăng nhập
        public async Task<IActionResult> ValidateInterviewCode([FromBody] ValidateCodeRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Mã phỏng vấn không được để trống.", valid = false });
            }

            var result = await _sender.Send(new ValidateInterviewCodeCommand(request.Code), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error, valid = false });
            }

            // Mã sai/đã dùng/hết hạn → 200 kèm reason để Kiosk hiện đúng hướng dẫn (ADR-052).
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/codes?jobPostingId={id}
        /// </summary>
        [HttpGet("codes")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetCodesByJob([FromQuery] Guid jobPostingId, CancellationToken ct)
        {
            if (jobPostingId == Guid.Empty)
            {
                return BadRequest(new { message = "JobPostingId không hợp lệ." });
            }

            var result = await _sender.Send(new GetInterviewCodesByJobQuery(jobPostingId), ct);
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/sessions
        /// Danh sách phiên phỏng vấn cho HR (kèm ứng viên, vị trí, verdict).
        /// </summary>
        [HttpGet("sessions")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetSessions([FromQuery] Guid? applicationId, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetHrInterviewSessionsQuery(applicationId, _currentUser.UserId, _currentUser.Role), ct);
            return Ok(result.Value);
        }

        // ─────────── Interview Management: Job → Slot → Candidate ───────────

        /// <summary>
        /// GET /api/interview/management/jobs
        /// Danh sách vị trí tuyển dụng có ca phỏng vấn, kèm thống kê tổng quan.
        /// </summary>
        [HttpGet("management/jobs")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetInterviewJobs(CancellationToken ct)
        {
            var result = await _sender.Send(new GetInterviewJobsQuery(_currentUser.UserId, _currentUser.Role), ct);
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/management/jobs/{jobId}/slots
        /// Danh sách ca phỏng vấn (AvailabilitySlot) của một vị trí, kèm thống kê đặt lịch.
        /// </summary>
        [HttpGet("management/jobs/{jobId:guid}/slots")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetSlotsForJob(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetSlotsForJobQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/management/slots/{slotId}/candidates
        /// Danh sách ứng viên trong một ca, kèm trạng thái xác nhận, phiên AI, điểm đánh giá.
        /// </summary>
        [HttpGet("management/slots/{slotId:guid}/candidates")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetCandidatesInSlot(Guid slotId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetCandidatesInSlotQuery(slotId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/interview/management/booking/{bookingId}/remind
        /// Gửi email + notification nhắc lịch phỏng vấn cho ứng viên.
        /// </summary>
        [HttpPost("management/booking/{bookingId:guid}/remind")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> SendBookingReminder(Guid bookingId, CancellationToken ct)
        {
            var result = await _sender.Send(new SendBookingReminderCommand(bookingId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { success = true, message = "Đã gửi nhắc nhở tới ứng viên." });
        }

        /// <summary>
        /// POST /api/interview/management/booking/{bookingId}/reschedule
        /// Dời một ứng viên sang ca phỏng vấn mới.
        /// </summary>
        [HttpPost("management/booking/{bookingId:guid}/reschedule")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> RescheduleBooking(Guid bookingId, [FromBody] RescheduleRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new RescheduleBookingCommand(bookingId, request.TargetSlotId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { success = true, message = "Đã dời lịch thành công." });
        }

        /// <summary>
        /// POST /api/interview/management/bookings/reschedule
        /// Dời NHIỀU ứng viên sang cùng một ca, được ăn cả ngã về không: hoặc mọi người hợp lệ cùng
        /// chuyển, hoặc không ai chuyển. Thay cho việc giao diện gửi N request tuần tự — cách cũ để
        /// lại trạng thái dở dang khi ca đích không đủ chỗ cho cả nhóm.
        /// </summary>
        [HttpPost("management/bookings/reschedule")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> RescheduleBookings([FromBody] RescheduleBatchRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new RescheduleBookingsCommand(request.BookingIds, request.TargetSlotId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        // ─────────── Phòng chờ buổi phỏng vấn thật (ADR-067) ───────────

        /// <summary>
        /// GET /api/interview/waiting-rooms
        /// Buổi phỏng vấn thật đang mở trong phạm vi người gọi — Hiring Manager mở màn này để vào phòng.
        /// </summary>
        [HttpGet("waiting-rooms")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetWaitingRooms(CancellationToken ct)
        {
            var result = await _sender.Send(new GetWaitingRoomsQuery(_currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/interview/session/{id}/hm-join
        /// Hiring Manager vào phòng cùng AI — điều kiện để buổi phỏng vấn bắt đầu được.
        /// </summary>
        [HttpPost("session/{id:guid}/hm-join")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> JoinInterviewRoom(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new JoinInterviewRoomCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/interview/session/{id}/admit
        /// Cho ứng viên vào phòng — phiên chuyển sang đang diễn ra và AI bắt đầu hỏi.
        /// </summary>
        [HttpPost("session/{id:guid}/admit")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> AdmitCandidate(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new AdmitCandidateCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        // ────────────────────────────────────────────────────────────────────

        #region ================= EXISTED INTERVIEW SESSION ENDPOINTS =================

        [HttpPost("session/start")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
        {
            var result = await _sender.Send(new StartInterviewSessionCommand(request));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/session/{id}/media-config
        /// Token Deepgram (STT) + HeyGen (avatar) + ngôn ngữ/voice để FE vào phòng phỏng vấn.
        /// </summary>
        [HttpGet("session/{id}/media-config")]
        [Authorize(Policy = "InterviewParticipant")]
        public async Task<IActionResult> GetMediaConfig(Guid id, CancellationToken ct)
        {
            if (!IsAuthorizedForSession(id)) return Forbid();
            var (accountId, email) = GetCandidateIdentity();
            var result = await _sender.Send(new GetMediaConfigQuery(id, accountId, email, IsKioskSession(id)), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/interview/session/{id}/tts  body { text }
        /// TTS câu hỏi → base64 PCM 24k cho FE đẩy vào LiveAvatar repeatAudio (ADR-044).
        /// </summary>
        [HttpPost("session/{id}/tts")]
        [Authorize(Policy = "InterviewParticipant")]
        public async Task<IActionResult> GetSpeechAudio(Guid id, [FromBody] TtsRequest request, CancellationToken ct)
        {
            if (!IsAuthorizedForSession(id)) return Forbid();
            var (accountId, email) = GetCandidateIdentity();
            var result = await _sender.Send(new SynthesizeSpeechCommand(id, request?.Text ?? string.Empty, accountId, email, IsKioskSession(id)), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { audio = result.Value });
        }

        [HttpPost("session/{id}/answer")]
        [Authorize(Policy = "InterviewParticipant")]
        public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerRequest request)
        {
            if (!IsAuthorizedForSession(id)) return Forbid();
            var result = await _sender.Send(new SubmitAnswerCommand(id, request.QuestionId, request.Transcript, request.ResponseTimeMs));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPost("session/{id}/end")]
        [Authorize(Policy = "InterviewParticipant")]
        public async Task<IActionResult> EndSession(Guid id, [FromQuery] string status = "completed")
        {
            if (!IsAuthorizedForSession(id)) return Forbid();
            var result = await _sender.Send(new EndInterviewSessionCommand(id, status));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { success = true });
        }

        /// <summary>
        /// POST /api/interview/session/{id}/signals — ghi nhận tín hiệu nghi vấn (thoát toàn màn hình,
        /// chuyển tab, đóng trang giữa buổi…). Có bản REST bên cạnh SignalR vì lúc trang đang đóng
        /// chỉ `sendBeacon`/`fetch keepalive` là chắc chắn gửi được (ADR-054).
        /// </summary>
        [HttpPost("session/{id}/signals")]
        [Authorize(Policy = "InterviewParticipant")]
        public async Task<IActionResult> ReportSignal(Guid id, [FromBody] ReportSignalRequest request, CancellationToken ct)
        {
            if (!IsAuthorizedForSession(id)) return Forbid();

            var result = await _sender.Send(new ReportCheatSignalCommand(
                id, request?.SignalType ?? string.Empty, request?.Payload), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { count = result.Value });
        }

        /// <summary>
        /// POST /api/interview/session/{id}/recording — Kiosk tải video buổi phỏng vấn THẬT lên
        /// storage sau khi kết thúc (ADR-052). File tự xoá sau <c>Interview:RecordingRetentionDays</c>.
        /// </summary>
        [HttpPost("session/{id}/recording")]
        [Authorize(Policy = "InterviewParticipant")]
        [RequestSizeLimit(400L * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadRecording(Guid id, IFormFile? file, CancellationToken ct)
        {
            if (!IsAuthorizedForSession(id)) return Forbid();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Chưa có dữ liệu ghi hình." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var result = await _sender.Send(new UploadRecordingCommand(
                id, ms.ToArray(), file.FileName, file.ContentType ?? "video/webm"), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>
        /// Chốt kết quả phỏng vấn (xác nhận hoặc ghi đè verdict của AI).
        ///
        /// Danh tính người duyệt lấy từ TOKEN, không phải từ header do client gửi. Trước đây tham số
        /// này là <c>[FromHeader("X-User-Id")]</c> kèm GUID dự phòng viết cứng — nghĩa là bất kỳ ai
        /// qua được policy đều gán được quyết định của mình cho người khác, và request không kèm
        /// header thì quyết định bị ghi cho một tài khoản không tồn tại. <c>HrReview.ReviewedByUserId</c>
        /// là dấu vết DUY NHẤT trả lời "ai đã chốt tuyển người này".
        /// </summary>
        [HttpPost("review/confirm")]
        [Authorize(Policy = "HiringDecision")] // ADR-061: người chốt là Hiring Manager, HR Admin dự phòng
        public async Task<IActionResult> ConfirmReview([FromBody] ConfirmReviewRequest request)
        {
            if (_currentUser.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            var result = await _sender.Send(new ConfirmHrReviewCommand(userId, request));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { success = true });
        }

        #endregion

        /// <summary>Token Kiosk (role Kiosk_session) chỉ hợp lệ với ĐÚNG phiên ghi trong claim.</summary>
        private bool IsKioskSession(Guid sessionId)
        {
            var role = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!string.Equals(role, AppRoles.KioskSession, StringComparison.OrdinalIgnoreCase)) return false;
            var claim = User.FindFirst("session_id")?.Value;
            return Guid.TryParse(claim, out var sid) && sid == sessionId;
        }

        /// <summary>Ứng viên đăng nhập (kiểm quyền sở hữu ở service) HOẶC Kiosk đúng phiên.</summary>
        private bool IsAuthorizedForSession(Guid sessionId)
        {
            var role = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (string.Equals(role, AppRoles.KioskSession, StringComparison.OrdinalIgnoreCase))
                return IsKioskSession(sessionId);
            return true;
        }

        private (Guid? accountId, string? email) GetCandidateIdentity()
        {
            var subClaim = User.FindFirst("sub")?.Value
                           ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst("email")?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            return (Guid.TryParse(subClaim, out var aid) ? aid : null, email);
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedId))
            {
                return parsedId;
            }
            return Guid.Parse("22222222-2222-2222-2222-222222222222"); // Mặc định phòng hờ
        }
    }
}
