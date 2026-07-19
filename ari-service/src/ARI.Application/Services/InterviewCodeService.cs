using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.Services
{
    /// <summary>
    /// Service quản lý sinh mã (Interview Code) và xác thực mã để bắt đầu Session phỏng vấn thật.
    /// </summary>
    public class InterviewCodeService : IInterviewCodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly InterviewService _interviewService;
        private readonly INotificationService _notificationService;

        public InterviewCodeService(IUnitOfWork unitOfWork, InterviewService interviewService, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _interviewService = interviewService;
            _notificationService = notificationService;
        }

        public async Task<Result<InterviewCode>> GenerateCodeAsync(Guid applicationId, int? roundNumber, Guid createdByUserId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
            {
                return Result.Failure<InterviewCode>("Hồ sơ ứng tuyển không tồn tại.");
            }

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (jobPosting == null)
            {
                return Result.Failure<InterviewCode>("Tin tuyển dụng liên kết không tồn tại.");
            }

            int finalRoundNumber = roundNumber ?? 1;
            if (!roundNumber.HasValue)
            {
                // Dùng ToLower() thay vì string.Equals(..., StringComparison) — EF Core/Npgsql
                // KHÔNG dịch được overload có StringComparison sang SQL (gây lỗi 500 khi cấp mã).
                var sessions = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => s.ApplicationId == applicationId && s.Status != null && s.Status.ToLower() == "completed", ct);
                finalRoundNumber = sessions.Any() ? sessions.Max(s => s.RoundNumber) + 1 : 1;
            }

            // ADR-015/016: mã On-site chỉ cấp khi ứng viên ĐÃ ĐẶT LỊCH buổi phỏng vấn thật của vòng
            // (InterviewBooking "scheduled"). Ứng viên đang sàng lọc / chưa đặt lịch thì CHƯA được cấp mã.
            var bookings = await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId
                     && b.RoundNumber == finalRoundNumber
                     && b.Status != null && b.Status.ToLower() == "scheduled", ct);
            if (!bookings.Any())
            {
                return Result.Failure<InterviewCode>(
                    $"Ứng viên chưa đặt lịch phỏng vấn thật cho vòng {finalRoundNumber} — chưa thể cấp mã. Mã On-site chỉ cấp sau khi ứng viên đã đặt lịch buổi phỏng vấn thật.");
            }

            var roundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(
                r => r.JobPostingId == jobPosting.Id && r.RoundNumber == finalRoundNumber, ct);
            var roundConfig = roundConfigs.FirstOrDefault();
            int ttlHours = roundConfig?.InterviewCodeTtlHours ?? 2;

            string generatedCode = string.Empty;
            bool isUnique = false;
            int attempts = 0;

            while (!isUnique && attempts < 15)
            {
                generatedCode = GenerateSecureRandomCode();
                var existingCodes = await _unitOfWork.Repository<InterviewCode>().FindAsync(c => c.Code == generatedCode, ct);
                if (!existingCodes.Any())
                {
                    isUnique = true;
                }
                attempts++;
            }

            if (!isUnique)
            {
                return Result.Failure<InterviewCode>("Không thể khởi tạo mã phỏng vấn duy nhất do phân tách dải mã bị trùng lặp.");
            }

            var interviewCode = new InterviewCode
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                RoundNumber = finalRoundNumber,
                Code = generatedCode,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<InterviewCode>().AddAsync(interviewCode, ct);

            // Đã cấp mã = ứng viên chắc chắn vào phỏng vấn thật → không còn "sàng lọc".
            // Đảm bảo bất biến: hồ sơ có mã thì status không phải "screening" (đồng bộ với việc
            // đặt lịch cũng nâng screening→interview). Tự chữa dữ liệu cũ ở lần cấp mã kế tiếp.
            if (string.Equals(application.Status, "screening", StringComparison.OrdinalIgnoreCase))
            {
                application.Status = "interview";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = createdByUserId,
                Action = "interview_code_generated",
                EntityType = "InterviewCode",
                EntityId = interviewCode.Id,
                Metadata = $"{{\"application_id\":\"{applicationId}\",\"round_number\":{finalRoundNumber},\"code\":\"{generatedCode}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<AuditLog>().AddAsync(auditLog, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Đẩy SignalR để chuông thông báo của candidate cập nhật TỨC THÌ (real-time). FE
            // (useAppNotifications) nhận "ReceiveUserNotification" → invalidate ['notifications']
            // → refetch /portal/notifications → SyncNotificationsAsync tạo notification "invite:{codeId}".
            // Chỉ push khi ứng viên có tài khoản (candidate on-site không tài khoản thì bỏ qua).
            if (application.CandidateAccountId.HasValue)
            {
                try
                {
                    await _notificationService.PublishUserEventAsync(
                        application.CandidateAccountId.Value,
                        "ReceiveUserNotification",
                        new { Type = "InterviewCodeIssued", applicationId = applicationId, roundNumber = finalRoundNumber },
                        ct);
                }
                catch
                {
                    // Push real-time là best-effort — lỗi SignalR không được làm hỏng việc cấp mã (đã lưu DB).
                }
            }

            return Result.Success(interviewCode);
        }

        /// <summary>
        /// YÊU CẦU MỚI: Sinh mã hàng loạt (Batch) cho danh sách hồ sơ ứng tuyển
        /// </summary>
        public async Task<Result<List<InterviewCode>>> GenerateBatchAsync(List<Guid> applicationIds, int? roundNumber, Guid createdByUserId, CancellationToken ct = default)
        {
            if (applicationIds == null || !applicationIds.Any())
            {
                return Result.Failure<List<InterviewCode>>("Danh sách ApplicationId không được để trống.");
            }

            var generatedCodes = new List<InterviewCode>();

            foreach (var appId in applicationIds)
            {
                var result = await GenerateCodeAsync(appId, roundNumber, createdByUserId, ct);
                if (result.IsSuccess)
                {
                    generatedCodes.Add(result.Value);
                }
            }

            return Result.Success(generatedCodes);
        }

        public async Task<Result<(bool Valid, Guid? SessionId)>> ValidateCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Result.Failure<(bool Valid, Guid? SessionId)>("Mã phỏng vấn không được để trống.");
            }

            var upperCode = code.Trim().ToUpper();
            var interviewCodes = await _unitOfWork.Repository<InterviewCode>().FindAsync(c => c.Code == upperCode, ct);
            var interviewCode = interviewCodes.FirstOrDefault();

            if (interviewCode == null || interviewCode.UsedAt.HasValue || interviewCode.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return Result.Success<(bool Valid, Guid? SessionId)>((false, null));
            }

            interviewCode.UsedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewCode>().Update(interviewCode);
            await _unitOfWork.SaveChangesAsync(ct);

            var startSessionRequest = new StartSessionRequest
            {
                ApplicationId = interviewCode.ApplicationId,
                RoundNumber = interviewCode.RoundNumber,
                SessionType = "real"
            };

            var sessionResult = await _interviewService.StartSessionAsync(startSessionRequest, ct);
            if (sessionResult.IsFailure)
            {
                interviewCode.UsedAt = null;
                _unitOfWork.Repository<InterviewCode>().Update(interviewCode);
                await _unitOfWork.SaveChangesAsync(ct);

                return Result.Failure<(bool Valid, Guid? SessionId)>(sessionResult.Error);
            }

            var sessionId = sessionResult.Value.SessionId;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = null,
                Action = "interview_code_used",
                EntityType = "InterviewCode",
                EntityId = interviewCode.Id,
                Metadata = $"{{\"code\":\"{upperCode}\",\"session_id\":\"{sessionId}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<AuditLog>().AddAsync(auditLog, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success<(bool Valid, Guid? SessionId)>((true, sessionId));
        }

        /// <summary>
        /// YÊU CẦU MỚI: Lấy danh sách mã kèm thông tin Ứng viên phục vụ HR
        /// </summary>
        public async Task<List<InterviewCodeSummaryDto>> GetCodesByJobAsync(Guid jobPostingId, CancellationToken ct = default)
        {
            // Lấy tất cả hồ sơ thuộc Tin tuyển dụng này
            var applications = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => a.JobPostingId == jobPostingId, ct);

            var applicationIds = applications.Select(a => a.Id).ToList();

            // Lấy toàn bộ mã Code tương ứng với các hồ sơ trên
            var codes = await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => applicationIds.Contains(c.ApplicationId), ct);

            var resultList = new List<InterviewCodeSummaryDto>();

            foreach (var code in codes)
            {
                var app = applications.FirstOrDefault(a => a.Id == code.ApplicationId);

                string status = "Active";
                if (code.UsedAt.HasValue) status = "Used";
                else if (code.ExpiresAt <= DateTimeOffset.UtcNow) status = "Expired";

                resultList.Add(new InterviewCodeSummaryDto
                {
                    Code = code.Code,
                    RoundNumber = code.RoundNumber,
                    ExpiresAt = code.ExpiresAt,
                    UsedAt = code.UsedAt,
                    Status = status,
                    CandidateName = app?.CandidateName ?? "Ẩn danh / Hệ thống" // Map đúng trường tên ứng viên từ Application entity của bạn
                });
            }

            return resultList;
        }

        private string GenerateSecureRandomCode()
        {
            // ADR-016: mã 6 ký tự alphanumeric, loại ký tự dễ nhầm (I, O, 0, 1). Không dấu gạch.
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var codeChars = new char[6];
            for (int i = 0; i < codeChars.Length; i++)
            {
                codeChars[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }
            return new string(codeChars);
        }
    }
}