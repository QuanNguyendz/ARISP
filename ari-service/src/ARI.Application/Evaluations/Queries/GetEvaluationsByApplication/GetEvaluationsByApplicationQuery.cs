using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations.Queries.GetEvaluationsByApplication
{
    public record GetEvaluationsByApplicationQuery(Guid ApplicationId) : IRequest<Result<List<EvaluationListItemResponse>>>;

    public class GetEvaluationsByApplicationQueryHandler
        : IRequestHandler<GetEvaluationsByApplicationQuery, Result<List<EvaluationListItemResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetEvaluationsByApplicationQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<EvaluationListItemResponse>>> Handle(GetEvaluationsByApplicationQuery request, CancellationToken ct)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(request.ApplicationId, ct);
            if (application == null)
                return Result.Failure<List<EvaluationListItemResponse>>("Application not found.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (job == null)
                return Result.Failure<List<EvaluationListItemResponse>>("Job posting associated with this application was not found.");

            var evaluations = (await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.ApplicationId == request.ApplicationId, ct)).ToList();
            var evalIds = evaluations.Select(e => e.Id).ToList();
            // Chỉ lấy HrReview của các đánh giá thuộc hồ sơ này (không quét toàn bảng).
            var hrDict = (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct))
                .ToDictionary(r => r.EvaluationId);

            var responseList = evaluations.Select(e =>
            {
                hrDict.TryGetValue(e.Id, out var hr);
                return EvaluationListItemResponse.FromEntity(e, application, job, hr);
            }).ToList();

            return Result.Success(responseList);
        }
    }
}
