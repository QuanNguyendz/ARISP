using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ARI.Application.CvScoring
{
    // ===================================================================================
    //  Xem trước tác động của một bản nháp bộ tiêu chí / công thức (ADR-075)
    //
    //  Tài liệu tuyển dụng đều khuyên HIỆU CHỈNH trước khi dùng: chấm thử bảng điểm trên các hồ sơ đã có rồi mới
    //  chốt trọng số / ngưỡng (ZYTHR, TicNote). Ở đây HM thấy ngay, trước khi bấm Lưu, mỗi hồ sơ của tin sẽ đổi
    //  điểm / khuyến nghị thế nào — chạy đúng bộ tính của lượt chấm thật (CvScoreCalculator), trong bộ nhớ:
    //  không ghi gì, không gọi AI. Hồ sơ nào bản nháp hỏi AI câu mới (thêm tiêu chí, sửa lời neo…) thì chỉ đếm —
    //  đó là số lượt gọi AI mà lần lưu sẽ tốn.
    // ===================================================================================

    public record PreviewJobCvRubricQuery(
        Guid JobPostingId,
        IReadOnlyList<CvRubricCriterionInput> Criteria,
        CvScoringPolicy? Policy,
        Guid? UserId,
        string? Role) : IRequest<Result<CvRubricPreviewDto>>;

    public record CvRubricPreviewItemDto(
        Guid ApplicationId,
        string CandidateName,
        int? OldScore,
        string? OldRecommendation,
        string? OldGateStatus,
        int? NewScore,
        string? NewRecommendation,
        string? NewGateStatus,
        /// <summary>Bản nháp hỏi AI câu mới cho hồ sơ này — điểm mới chỉ có sau khi AI chấm lại.</summary>
        bool RequiresAi);

    public record CvRubricPreviewDto(
        /// <summary>Số hồ sơ có CV của tin.</summary>
        int ApplicationCount,
        /// <summary>Tính lại được ngay từ câu trả lời cũ của AI.</summary>
        int RecomputeCount,
        /// <summary>Phải hỏi AI lại (tốn một lượt gọi mỗi hồ sơ).</summary>
        int AiRescoreCount,
        /// <summary>Chưa có kết quả chấm nào (đang chờ chấm lần đầu) — sẽ chấm theo bộ mới.</summary>
        int PendingCount,
        /// <summary>File không phải CV — công thức nào cũng vậy.</summary>
        int InvalidCvCount,
        int ScoreChangedCount,
        int RecommendationChangedCount,
        int GateFailCount,
        int GateReviewCount,
        IReadOnlyList<CvRubricPreviewItemDto> Items);

    public class PreviewJobCvRubricQueryHandler : IRequestHandler<PreviewJobCvRubricQuery, Result<CvRubricPreviewDto>>
    {
        /// <summary>Trần số dòng trả về — hồ sơ đổi khuyến nghị lên trước, rồi đổi điểm nhiều nhất.</summary>
        public const int MaxItems = 500;

        private readonly IUnitOfWork _unitOfWork;

        public PreviewJobCvRubricQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<CvRubricPreviewDto>> Handle(PreviewJobCvRubricQuery request, CancellationToken ct)
        {
            if (request.UserId is not { } actorId)
                return Result.Failure<CvRubricPreviewDto>("Không xác định được người dùng.", CommonErrorCodes.Forbidden);

            // Cùng quyền với LƯU: xem trước là một phần của việc soạn công thức, và nó đọc điểm của mọi hồ sơ trong tin.
            var (accessError, accessCode) = await PlaybookAccess.CheckWriteAsync(
                _unitOfWork, PlaybookScope.ScopeJobPosting, request.JobPostingId, actorId, request.Role, ct);
            if (accessError != null)
                return accessCode == null
                    ? Result.Failure<CvRubricPreviewDto>(accessError)
                    : Result.Failure<CvRubricPreviewDto>(accessError, accessCode);

            var normalized = CvRubricEditing.Normalize(request.Criteria, RubricPurpose.Cv);
            var errors = normalized.Errors.Concat(CvScoringPolicy.Validate(request.Policy)).ToList();
            if (errors.Count > 0)
                return Result.Failure<CvRubricPreviewDto>(string.Join(" · ", errors.Take(10)));

            var criteria = normalized.Criteria;
            var policy = (request.Policy ?? CvScoringPolicy.Default).Normalized();
            var liveSignature = CvObservationSignature.Of(criteria);

            var jobId = request.JobPostingId;
            var apps = await _unitOfWork.Repository<Domain.Entities.Application>().QueryAsync(
                q => q.Where(a => a.JobPostingId == jobId && a.CvFileUrl != null && a.CvFileUrl != "")
                      .Select(a => new { a.Id, a.CandidateName, a.CvJdAnalysisId }), ct);

            var analysisIds = apps.Where(a => a.CvJdAnalysisId != null).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var analyses = analysisIds.Count == 0
                ? new Dictionary<Guid, CvJdAnalysis>()
                : (await _unitOfWork.Repository<CvJdAnalysis>().QueryAsync(q => q.Where(x => analysisIds.Contains(x.Id)), ct))
                    .ToDictionary(x => x.Id);

            var rubricIds = analyses.Values.Where(a => a.RubricDocumentId != null)
                .Select(a => a.RubricDocumentId!.Value).Distinct().ToList();
            // Bộ tiêu chí cũ đã bị xoá mềm — phải bỏ bộ lọc toàn cục.
            var signatures = rubricIds.Count == 0
                ? new Dictionary<Guid, Dictionary<string, string>>()
                : (await _unitOfWork.Repository<PlaybookDocument>().QueryAsync(
                        q => q.IgnoreQueryFilters().Where(p => rubricIds.Contains(p.Id)).Select(p => new { p.Id, p.RubricJson }), ct))
                    .ToDictionary(p => p.Id, p => CvObservationSignature.Of(ScoringRubric.Deserialize(p.RubricJson)));

            int recompute = 0, ai = 0, pending = 0, invalid = 0, scoreChanged = 0, recChanged = 0, gateFail = 0, gateReview = 0;
            var items = new List<CvRubricPreviewItemDto>();

            foreach (var app in apps)
            {
                if (app.CvJdAnalysisId is not { } aid || !analyses.TryGetValue(aid, out var current))
                {
                    pending++;
                    continue;
                }
                if (current.Status == CvAnalysisStatuses.InvalidCv)
                {
                    invalid++;
                    continue;
                }

                var oldScore = current.MatchScore;
                var oldRec = string.IsNullOrWhiteSpace(current.OverallRecommendation) ? null : current.OverallRecommendation;

                var covered = current.RubricDocumentId is { } rid
                              && signatures.TryGetValue(rid, out var donorSig)
                              && CvObservationSignature.Covers(donorSig, liveSignature);
                var observations = covered
                    ? CvObservations.TryFromSnapshot(
                        CvScoreSnapshot.Parse(current.CriterionScores), CvScoringPolicy.FromStorage(current.ScoringPolicy), criteria)
                    : null;
                var result = observations == null ? null : CvScoreCalculator.Compute(criteria, policy, observations);

                if (result?.Score is not { } newScore)
                {
                    ai++;
                    items.Add(new CvRubricPreviewItemDto(app.Id, app.CandidateName, oldScore, oldRec, current.GateStatus,
                        null, null, null, true));
                    continue;
                }

                recompute++;
                if (newScore != oldScore) scoreChanged++;
                if (!string.Equals(result.Recommendation, oldRec, StringComparison.Ordinal)) recChanged++;
                if (result.GateStatus == CvGateStatuses.Fail) gateFail++;
                else if (result.GateStatus == CvGateStatuses.Review) gateReview++;

                items.Add(new CvRubricPreviewItemDto(app.Id, app.CandidateName, oldScore, oldRec, current.GateStatus,
                    newScore, result.Recommendation, result.GateStatus, false));
            }

            var ordered = items
                .OrderByDescending(i => !i.RequiresAi && !string.Equals(i.OldRecommendation, i.NewRecommendation, StringComparison.Ordinal))
                .ThenByDescending(i => i.RequiresAi ? -1 : Math.Abs((i.NewScore ?? 0) - (i.OldScore ?? 0)))
                .ThenBy(i => i.CandidateName, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxItems)
                .ToList();

            return Result.Success(new CvRubricPreviewDto(
                apps.Count, recompute, ai, pending, invalid, scoreChanged, recChanged, gateFail, gateReview, ordered));
        }
    }
}
