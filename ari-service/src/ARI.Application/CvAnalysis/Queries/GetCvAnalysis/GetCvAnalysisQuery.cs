using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CvAnalysis.Queries.GetCvAnalysis
{
    /// <summary>Candidate xem bản phân tích CV của chính mình — check ownership trước khi trả.</summary>
    public record GetCvAnalysisQuery(Guid Id, Guid CandidateId) : IRequest<Result<CvJdAnalysis>>;

    public class GetCvAnalysisQueryHandler : IRequestHandler<GetCvAnalysisQuery, Result<CvJdAnalysis>>
    {
        private readonly ICvJdAnalysisService _analysisService;

        public GetCvAnalysisQueryHandler(ICvJdAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        public async Task<Result<CvJdAnalysis>> Handle(GetCvAnalysisQuery request, CancellationToken ct)
        {
            var hasPermission = await _analysisService.CheckCandidateOwnershipAsync(request.Id, request.CandidateId, ct);
            if (!hasPermission)
                return Result.Failure<CvJdAnalysis>(
                    "Bạn không có quyền xem bản đánh giá này, hoặc bạn chưa hoàn tất việc nộp đơn.", CommonErrorCodes.Forbidden);

            var result = await _analysisService.GetAnalysisByIdAsync(request.Id, ct);
            if (result.IsFailure)
                return Result.Failure<CvJdAnalysis>(result.Error, CommonErrorCodes.NotFound);
            return result;
        }
    }
}
