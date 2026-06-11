using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Application.Services
{
    /// <summary>
    /// Service quản lý sinh mã (Interview Code) và xác thực mã để bắt đầu Session phỏng vấn thật.
    /// </summary>
    public class InterviewCodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly InterviewService _interviewService;

        public InterviewCodeService(IUnitOfWork unitOfWork, InterviewService interviewService)
        {
            _unitOfWork = unitOfWork;
            _interviewService = interviewService;
        }

        public async Task<Result<InterviewCode>> GenerateCodeAsync(Guid applicationId, int? roundNumber, Guid createdByUserId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
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
                var sessions = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => s.ApplicationId == applicationId && string.Equals(s.Status, "completed", StringComparison.OrdinalIgnoreCase), ct);
                finalRoundNumber = sessions.Any() ? sessions.Max(s => s.RoundNumber) + 1 : 1;
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
            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Loại bỏ ký tự dễ nhầm lẫn (I, O, 0, 1) theo đúng mẫu hướng dẫn
            var prefixChars = new char[3];
            var suffixChars = new char[4];

            for (int i = 0; i < 3; i++)
            {
                prefixChars[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }
            for (int i = 0; i < 4; i++)
            {
                suffixChars[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }

            return $"{new string(prefixChars)}-{new string(suffixChars)}";
        }
    }
}