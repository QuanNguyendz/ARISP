using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CvAnalysis.Commands.AnalyzeCv
{
    /// <summary>Phân tích CV-JD (Gemini) + cache theo (JobPostingId, CvHash) — delegate sang ICvJdAnalysisService.</summary>
    public record AnalyzeCvCommand(Guid JobPostingId, Stream CvStream, string FileName) : IRequest<Result<CvJdAnalysis>>;

    public class AnalyzeCvCommandHandler : IRequestHandler<AnalyzeCvCommand, Result<CvJdAnalysis>>
    {
        private readonly ICvJdAnalysisService _analysisService;

        public AnalyzeCvCommandHandler(ICvJdAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        public Task<Result<CvJdAnalysis>> Handle(AnalyzeCvCommand request, CancellationToken ct)
            => _analysisService.AnalyzeAndCacheAsync(request.JobPostingId, request.CvStream, request.FileName, ct);
    }
}
