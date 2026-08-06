using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations.Queries.GetEvaluationDetail
{
    /// <summary>Tra cứu chi tiết đánh giá theo EvaluationId, fallback theo SessionId (dùng chung cho 2 endpoint).</summary>
    public record GetEvaluationDetailQuery(Guid Id) : IRequest<Result<EvaluationDetailResponse>>;

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

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(evaluation.ApplicationId, ct);
            if (application == null)
                return Result.Failure<EvaluationDetailResponse>("Application associated with this evaluation was not found.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (job == null)
                return Result.Failure<EvaluationDetailResponse>("Job posting associated with this evaluation was not found.");

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

            return Result.Success(response);
        }
    }
}
