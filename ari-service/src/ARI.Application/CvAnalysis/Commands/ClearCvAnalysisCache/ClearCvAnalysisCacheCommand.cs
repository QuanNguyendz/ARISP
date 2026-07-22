using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.CvAnalysis.Commands.ClearCvAnalysisCache
{
    public record ClearCvAnalysisCacheCommand : IRequest<Result>;

    public class ClearCvAnalysisCacheCommandHandler : IRequestHandler<ClearCvAnalysisCacheCommand, Result>
    {
        private readonly ICvJdAnalysisService _analysisService;

        public ClearCvAnalysisCacheCommandHandler(ICvJdAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        public async Task<Result> Handle(ClearCvAnalysisCacheCommand request, CancellationToken ct)
        {
            await _analysisService.ClearAllCacheAsync(ct);
            return Result.Success();
        }
    }
}
