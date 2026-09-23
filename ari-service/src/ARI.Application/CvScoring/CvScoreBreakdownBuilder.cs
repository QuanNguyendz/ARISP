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

            // Công thức đã áp cho CHÍNH bản chấm này (ảnh chụp). Bản chấm trước ADR-075 không có → công thức mặc định,
            // đúng là công thức đã sinh ra con số của nó.
            var legacyPolicy = string.IsNullOrWhiteSpace(analysis.ScoringPolicy);
            var policy = CvScoringPolicy.FromStorage(analysis.ScoringPolicy);
            dto.Policy = policy;

            var views = CvScoreSnapshot.Parse(analysis.CriterionScores);
            var scored = views.Where(v => !v.IsKnockout && v.Score.HasValue && (v.Weight ?? 0) > 0).ToList();
            var totalWeight = scored.Sum(v => v.Weight!.Value);
            var weightedSum = scored.Sum(v => Math.Clamp(v.Score!.Value, 0m, 100m) * v.Weight!.Value);

            dto.TotalWeight = totalWeight;
            dto.WeightedSum = weightedSum;
            // Bản chấm mới làm tròn MỘT lần từ số chưa làm tròn → hiện 2 chữ số bằng cách CẮT để luôn khớp số cuối
            // (79,495 → "79,49 → 79"). Bản cũ làm tròn 2 lần — giữ cách hiện cũ để khớp đúng số nó đã lưu.
            dto.ExactTotal = totalWeight <= 0 ? null
                : legacyPolicy ? Math.Round(weightedSum / totalWeight, 2, MidpointRounding.AwayFromZero)
                : CvScoreCalculator.DisplayExact(weightedSum / totalWeight);

            foreach (var v in views)
            {
                var label = string.IsNullOrWhiteSpace(v.Label) ? v.Key : v.Label!;

                if (v.IsKnockout)
                {
                    dto.Gates.Add(new CvScoreGateDto
                    {
                        Key = v.Key,
                        Label = label,
                        Type = "knockout",
                        Outcome = v.Gate ?? CvGateOutcomes.Unknown,
                        Reason = v.GateReason,
                        Unsupported = v.Unsupported == true,
                        Evidence = v.Evidence,
                        Reasoning = v.Reasoning,
                        Description = v.Description,
                    });
                    continue;
                }

                if (v.MinScore is { } min)
                {
                    dto.Gates.Add(new CvScoreGateDto
                    {
                        Key = v.Key,
                        Label = label,
                        Type = "min_score",
                        Outcome = v.Gate ?? CvGateOutcomes.Unknown,
                        Reason = v.GateReason,
                        Score = v.Score,
                        MinScore = min,
                        Description = v.Description,
                    });
                }

                var item = new CvScoreCriterionDto
                {
                    Key = v.Key,
                    Label = label,
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
                    Position = v.Position,
                    MinScore = v.MinScore,
                    Gate = v.Gate,
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
                        Weight = x.Weight ?? 1,
                    }).ToList();
                    item.ChecksMet = checks.Count(x => x.Met == true);
                    item.ChecksAnswered = checks.Count(x => x.Met != null);
                    item.ChecksMetWeight = checks.Where(x => x.Met == true).Sum(x => x.Weight ?? 1);
                    item.ChecksAnsweredWeight = checks.Where(x => x.Met != null).Sum(x => x.Weight ?? 1);
                }

                if (v.Score is { } s && (v.Weight ?? 0) > 0 && totalWeight > 0)
                {
                    item.Contribution = Math.Round(Math.Clamp(s, 0m, 100m) * v.Weight!.Value / totalWeight, 2, MidpointRounding.AwayFromZero);
                    item.Band = ScoringRubric.NormalizeBand(v.Band) ?? policy.Bands.BandOf(s);
                    if (policy.Bands.Range(item.Band) is { } range)
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
            dto.ScoreRecommendation = policy.Tier(analysis.MatchScore);
            dto.GateStatus = analysis.GateStatus;

            // Bản tính lại (ADR-075): AI đọc CV ở bản gốc — màn hình nói đúng "tính lại lúc … từ lượt AI chấm lúc …".
            dto.ObservedAt = analysis.CreatedAt;
            if (analysis.DerivedFromAnalysisId is { } rootId)
            {
                dto.Derived = true;
                var root = await uow.Repository<CvJdAnalysis>().GetByIdAsync(rootId, ct);
                if (root != null) dto.ObservedAt = root.CreatedAt;
            }
            return dto;
        }

        /// <summary>Dải điểm theo ngưỡng MẶC ĐỊNH — chỉ còn cho dữ liệu cũ không lưu công thức.</summary>
        public static string BandOf(decimal score) => ScoringRubric.BandOf(score);

        private static List<string> ReadList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch (JsonException) { return new List<string>(); }
        }
    }
}
