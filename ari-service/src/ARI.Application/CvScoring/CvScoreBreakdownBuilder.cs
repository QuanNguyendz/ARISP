using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Dựng phần "điểm này ra từ đâu" cho màn hồ sơ (ADR-070): từng tiêu chí, trọng số, điểm, số điểm góp
    /// vào tổng, bằng chứng trích từ CV, lý do theo chuẩn chấm — và chính phép tính bằng số thật.
    ///
    /// Mọi con số đọc từ ẢNH CHỤP lưu cùng bản chấm, không từ bộ tiêu chí đang sống: HM sửa bộ tiêu chí
    /// sau đó thì bản chấm cũ vẫn giải thích đúng con số của nó.
    /// </summary>
    public static class CvScoreBreakdownBuilder
    {
        public static async Task<CvScoreBreakdownDto> BuildAsync(
            IUnitOfWork uow, Domain.Entities.Application app, CvJdAnalysis? analysis, CancellationToken ct,
            CvScoringInFlight? inFlight = null)
        {
            var live = await CvRubricStore.LiveAsync(uow, app.JobPostingId, ct);
            var info = analysis == null
                ? (CvScoreState.AnalysisInfo?)null
                : new CvScoreState.AnalysisInfo(analysis.Status, analysis.RubricDocumentId, analysis.MatchScore);
            var failure = CvScoreState.FailureOf(inFlight, app.Id, live?.Id);
            var (state, score) = CvScoreState.Resolve(!string.IsNullOrWhiteSpace(app.CvFileUrl), info, live?.Id, failure);

            var dto = new CvScoreBreakdownDto
            {
                State = state,
                Total = score,
                IsCurrentRubric = live != null && analysis?.RubricDocumentId == live.Id,
                RubricSavedAt = live?.CreatedAt,
            };

            if (state == CvScoreStates.ScoringFailed && failure != null)
            {
                dto.FailureReason = failure.Code ?? CvScoringErrors.AiUnavailable;
                dto.FailedAttempts = failure.Attempts;
                dto.RetryAt = failure.RetryAfter;
            }

            if (live != null)
            {
                var author = await uow.Repository<User>().GetByIdAsync(live.UploadedByUserId, ct);
                dto.RubricSavedBy = author == null ? null : (string.IsNullOrWhiteSpace(author.FullName) ? author.Email : author.FullName);
            }

            if (analysis == null) return dto;

            if (state == CvScoreStates.InvalidCv)
            {
                dto.InvalidReason = analysis.ErrorMessage ?? "File tải lên không phải là CV.";
                dto.ScoredAt = analysis.CreatedAt;
                dto.Model = analysis.AiModel;
                return dto;
            }

            // Điểm AI tự cho trước ADR-070 không được giải thích như điểm theo tiêu chí — không hiện gì.
            if (!CvScoreState.IsDisplayable(analysis.Status, analysis.RubricDocumentId)) return dto;

            var views = CvScoreSnapshot.Parse(analysis.CriterionScores);
            var scored = views.Where(v => v.Score.HasValue && (v.Weight ?? 0) > 0).ToList();
            var totalWeight = scored.Sum(v => v.Weight!.Value);
            var weightedSum = scored.Sum(v => Math.Clamp(v.Score!.Value, 0m, 100m) * v.Weight!.Value);

            dto.TotalWeight = totalWeight;
            dto.WeightedSum = weightedSum;
            dto.ExactTotal = totalWeight > 0 ? Math.Round(weightedSum / totalWeight, 2, MidpointRounding.AwayFromZero) : null;

            foreach (var v in views)
            {
                var item = new CvScoreCriterionDto
                {
                    Key = v.Key,
                    Label = string.IsNullOrWhiteSpace(v.Label) ? v.Key : v.Label!,
                    Weight = v.Weight,
                    Score = v.Score,
                    Evidence = v.Evidence,
                    Reasoning = v.Reasoning,
                    Description = v.Description,
                    Levels = v.Levels == null ? null : new CvRubricLevelsDto
                    {
                        Excellent = v.Levels.Excellent,
                        Good = v.Levels.Good,
                        Fair = v.Levels.Fair,
                        Poor = v.Levels.Poor,
                    },
                };

                if (v.Checks is { Count: > 0 } checks)
                {
                    item.Checks = checks.Select(x => new CvScoreCheckDto
                    {
                        Key = x.Key,
                        Text = x.Text,
                        Met = x.Met,
                        Evidence = x.Evidence,
                        Unsupported = x.Unsupported == true,
                    }).ToList();
                    item.ChecksMet = checks.Count(x => x.Met == true);
                    item.ChecksAnswered = checks.Count(x => x.Met != null);
                }

                if (v.Score is { } s && (v.Weight ?? 0) > 0 && totalWeight > 0)
                {
                    item.Contribution = Math.Round(Math.Clamp(s, 0m, 100m) * v.Weight!.Value / totalWeight, 2, MidpointRounding.AwayFromZero);
                    item.Band = ScoringRubric.NormalizeBand(v.Band) ?? BandOf(s);
                    if (ScoringRubric.BandRange(item.Band) is { } range)
                    {
                        item.BandMin = range.Min;
                        item.BandMax = range.Max;
                    }
                    // Bản chấm trước khi có ý kiểm không ghi nguồn — khi đó điểm là AI ước lượng.
                    item.ScoreSource = v.ScoreSource ?? CvCriterionScoring.SourceAi;
                    dto.Criteria.Add(item);
                }
                else
                {
                    dto.Excluded.Add(item);
                }
            }

            dto.ScoredAt = analysis.CreatedAt;
            dto.Model = analysis.AiModel;
            dto.Summary = analysis.Summary;
            dto.SkillsMatched = ReadList(analysis.SkillsMatched);
            dto.SkillsGaps = ReadList(analysis.SkillsGaps);
            dto.RedFlags = ReadList(analysis.RedFlags);
            dto.SeniorityAlignment = analysis.SeniorityAlignment;
            dto.ExperienceRelevance = analysis.ExperienceRelevance;
            dto.Recommendation = analysis.OverallRecommendation;
            return dto;
        }

        /// <summary>Dải điểm cố định của mức neo (<see cref="Playbooks.RubricLevels"/>).</summary>
        public static string BandOf(decimal score) => ScoringRubric.BandOf(score);

        private static List<string> ReadList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch (JsonException) { return new List<string>(); }
        }
    }
}
