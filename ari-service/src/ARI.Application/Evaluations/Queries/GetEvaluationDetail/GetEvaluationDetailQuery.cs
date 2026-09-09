using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations.Queries.GetEvaluationDetail
{
    /// <summary>
    /// Tra cứu chi tiết đánh giá theo EvaluationId, fallback theo SessionId (dùng chung cho 2 endpoint).
    /// Trả về transcript, bảng điểm từng tiêu chí và <b>link video buổi phỏng vấn</b>, nên bắt buộc
    /// kiểm quyền trên tin mà hồ sơ thuộc về.
    /// </summary>
    public record GetEvaluationDetailQuery(Guid Id, Guid? UserId, string? Role)
        : IRequest<Result<EvaluationDetailResponse>>;

    public class GetEvaluationDetailQueryHandler
        : IRequestHandler<GetEvaluationDetailQuery, Result<EvaluationDetailResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetEvaluationDetailQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<EvaluationDetailResponse>> Handle(GetEvaluationDetailQuery request, CancellationToken ct)
        {
            // First check by evaluation ID
            var evaluation = await _unitOfWork.Repository<Evaluation>().GetByIdAsync(request.Id, ct);

            // If not found, try search by SessionId
            if (evaluation == null)
            {
                var evals = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == request.Id, ct);
                evaluation = evals.FirstOrDefault();
            }

            if (evaluation == null)
                return Result.Failure<EvaluationDetailResponse>("Evaluation not found.");

            // Buổi thử chỉ thuộc về ứng viên — với nhân sự nội bộ thì coi như không tồn tại (ADR-051).
            if (evaluation.SessionType == "practice")
                return Result.Failure<EvaluationDetailResponse>("Evaluation not found.");

            var (application, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, evaluation.ApplicationId, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<EvaluationDetailResponse>("Application associated with this evaluation was not found.");
            if (job == null)
                return Result.Failure<EvaluationDetailResponse>("Job posting associated with this evaluation was not found.");
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<EvaluationDetailResponse>(JobAccessErrors.EvaluationForbidden, CommonErrorCodes.Forbidden);

            var hrReviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id, ct);
            var hrReview = hrReviews.FirstOrDefault();

            var response = EvaluationDetailResponse.FromEntity(evaluation, application, job, hrReview);

            // Video buổi phỏng vấn thật (ADR-052) — resolve storageKey thành URL xem được, kèm hạn lưu.
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(evaluation.SessionId, ct);
            if (session != null)
            {
                response.RecordingExpiresAt = session.RecordingExpiresAt;
                response.RecordingDeletedAt = session.RecordingDeletedAt;
                if (!string.IsNullOrEmpty(session.RecordingUrl))
                    response.RecordingUrl = await _fileStorage.GetUrlAsync(session.RecordingUrl, ct);
            }

            // Ai là người có thẩm quyền chốt kết quả này (ADR-061). Giao diện cần biết để hiện đúng
            // một trong hai thứ: nút chốt, hay banner "đang chờ Hiring Manager" kèm nút chốt thay.
            var primaryHm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
            if (primaryHm != null)
            {
                response.RequiresHmApproval = true;
                response.HiringManagerUserId = primaryHm.UserId;
                var hmUser = await _unitOfWork.Repository<User>().GetByIdAsync(primaryHm.UserId, ct);
                if (hmUser != null)
                    response.HiringManagerName =
                        string.IsNullOrWhiteSpace(hmUser.FullName) ? hmUser.Email : hmUser.FullName;
            }

            // Điểm khớp CV-JD đã chấm sẵn lúc ứng tuyển (ADR-030) — chỉ đọc lại, KHÔNG gọi Gemini.
            if (application.CvJdAnalysisId.HasValue)
            {
                var analysis = await _unitOfWork.Repository<CvJdAnalysis>()
                    .GetByIdAsync(application.CvJdAnalysisId.Value, ct);
                if (analysis != null)
                {
                    response.CvMatchScore = analysis.MatchScore;
                    response.CvMatchSummary = analysis.Summary;
                }
            }

            return Result.Success(response);
        }
    }
}
