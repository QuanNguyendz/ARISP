using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CvAnalysis.Queries.GetCvAnalysisByApplication
{
    /// <summary>Staff xem bản phân tích CV gắn với một Application.</summary>
    public record GetCvAnalysisByApplicationQuery(Guid ApplicationId) : IRequest<Result<CvJdAnalysis>>;

    public class GetCvAnalysisByApplicationQueryHandler : IRequestHandler<GetCvAnalysisByApplicationQuery, Result<CvJdAnalysis>>
    {
        private readonly ICvJdAnalysisService _analysisService;

        public GetCvAnalysisByApplicationQueryHandler(ICvJdAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        public async Task<Result<CvJdAnalysis>> Handle(GetCvAnalysisByApplicationQuery request, CancellationToken ct)
        {
            var result = await _analysisService.GetAnalysisByApplicationIdAsync(request.ApplicationId, ct);
            if (result.IsFailure)
                return Result.Failure<CvJdAnalysis>(result.Error, CommonErrorCodes.NotFound);
            return result;
        }
    }
}
