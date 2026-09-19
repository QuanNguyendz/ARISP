using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations
{
    // ============================================================
    // POST /api/evaluations/sessions/{sessionId}/retry
    // ============================================================

    /// <summary>
    /// Nhân sự bấm "Chấm lại" một buổi phỏng vấn THẬT chưa có báo cáo (ADR-073): AI đã hỏng hết số lượt thử tự
    /// động, hoặc muốn chấm ngay sau khi khai bộ tiêu chí thay vì đợi lượt quét. Chỉ đặt lại trạng thái và đưa
    /// vào hàng — việc chấm chạy nền, màn hình tự cập nhật qua realtime khi xong.
    ///
    /// Không chấm lại buổi ĐÃ có báo cáo: báo cáo có thể đã được đọc, đã làm căn cứ chốt kết quả.
    /// </summary>
    public record RetryInterviewEvaluationCommand(Guid SessionId, Guid? UserId, string? Role) : IRequest<Result<bool>>;

    public class RetryInterviewEvaluationCommandHandler : IRequestHandler<RetryInterviewEvaluationCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEvaluationQueue _queue;

        public RetryInterviewEvaluationCommandHandler(IUnitOfWork unitOfWork, IEvaluationQueue queue)
        {
            _unitOfWork = unitOfWork;
            _queue = queue;
        }

        public async Task<Result<bool>> Handle(RetryInterviewEvaluationCommand request, CancellationToken ct)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(request.SessionId, ct);
            // Buổi thử là không gian riêng của ứng viên (ADR-051) — với nhân sự, nó không tồn tại.
            if (session == null || session.SessionType != "real")
                return Result<bool>.Failure("Không tìm thấy buổi phỏng vấn.", CommonErrorCodes.NotFound);

            var (application, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, session.ApplicationId, request.UserId, request.Role, ct);
            if (application == null || job == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            // Người vận hành phễu (chủ tin, quản trị viên) và người chốt kết quả (HM chính) — mỗi lần là một lượt AI.
            var allowed = level >= JobAccessLevel.Owner
                          || await JobAccess.IsPrimaryHiringManagerAsync(_unitOfWork, job.Id, request.UserId, ct);
            if (!allowed)
                return Result<bool>.Failure("Bạn không có quyền chấm lại buổi phỏng vấn này.", CommonErrorCodes.Forbidden);

            if (!InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Completed))
                return Result<bool>.Failure("Buổi phỏng vấn chưa kết thúc nên chưa chấm được.", CommonErrorCodes.Conflict);

            if (await _unitOfWork.Repository<Evaluation>().CountAsync(e => e.SessionId == session.Id, ct) > 0)
                return Result<bool>.Failure("Buổi phỏng vấn này đã có báo cáo.", CommonErrorCodes.Conflict);

            if (EvaluationStatuses.Is(session.EvaluationStatus, EvaluationStatuses.Processing))
                return Result.Success(true);

            session.EvaluationStatus = EvaluationStatuses.Pending;
            session.EvaluationAttempts = 0;
            session.EvaluationError = null;
            session.EvaluationUpdatedAt = DateTimeOffset.UtcNow;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewSession>().Update(session);
            await Admin.AdminSupport.WriteAuditAsync(_unitOfWork, request.UserId, "interview_evaluation_retried",
                nameof(InterviewSession), session.Id,
                AuditMetadata.Serialize(new { candidate = application.CandidateName, jobTitle = job.Title, session.RoundNumber }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _queue.Enqueue(session.Id);
            return Result.Success(true);
        }
    }
}
