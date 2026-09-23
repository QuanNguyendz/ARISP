using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Phần công thức chấm CV ở cấp TIN mà Hiring Manager quyết định (ADR-075): ngưỡng của bốn dải điểm và
    /// ngưỡng của nhãn khuyến nghị. Phần công thức ở cấp TIÊU CHÍ (điều kiện bắt buộc, điểm tối thiểu, trọng
    /// số ý kiểm) nằm ngay trên <see cref="RubricCriterion"/>.
    ///
    /// Mặc định = đúng hành vi trước ADR-075 (90/70/40 và 80/65/50). Công thức mặc định lưu là <c>null</c> —
    /// bộ tiêu chí cũ không phải đổi gì, và "lưu y hệt thì không tạo phiên bản" vẫn đúng.
    ///
    /// Cơ sở: mỗi đơn vị "được tự chọn thang phù hợp" (ma trận tuyển dụng ĐH Wyoming); ngưỡng đi tiếp khác
    /// nhau theo vị trí (4 Corner Resources: 4.0+/3.5–3.9/&lt;3.5; TicNote: 80/70/60).
    /// </summary>
    public sealed class CvScoringPolicy
    {
        public CvBandCuts Bands { get; set; } = new();
        public CvTierCuts Tiers { get; set; } = new();

        public static CvScoringPolicy Default => new();

        [JsonIgnore]
        public bool IsDefault => (Bands ?? new()).IsDefault && (Tiers ?? new()).IsDefault;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Trả HẾT lỗi một lượt (rỗng = hợp lệ).</summary>
        public static List<string> Validate(CvScoringPolicy? policy)
        {
            var errors = new List<string>();
            if (policy == null) return errors;

            var b = policy.Bands ?? new CvBandCuts();
            if (!(100 > b.ExcellentFrom && b.ExcellentFrom > b.GoodFrom && b.GoodFrom > b.FairFrom && b.FairFrom > 0))
                errors.Add("Ngưỡng dải phải giảm dần: Xuất sắc > Tốt > Đạt một phần > 0.");
            else if (100 - b.ExcellentFrom < CvBandCuts.MinWidth
                     || b.ExcellentFrom - 1 - b.GoodFrom < CvBandCuts.MinWidth
                     || b.GoodFrom - 1 - b.FairFrom < CvBandCuts.MinWidth
                     || b.FairFrom - 1 < CvBandCuts.MinWidth)
                errors.Add($"Mỗi dải điểm phải rộng ít nhất {CvBandCuts.MinWidth} điểm.");

            var t = policy.Tiers ?? new CvTierCuts();
            if (!(100 >= t.StrongHireFrom && t.StrongHireFrom > t.HireFrom && t.HireFrom > t.CautionFrom && t.CautionFrom >= 1))
                errors.Add("Ngưỡng khuyến nghị phải giảm dần trong khoảng 1–100: Rất phù hợp > Phù hợp > Cân nhắc.");

            return errors;
        }

        /// <summary>Giá trị lưu cột <c>scoring_policy_json</c>: <c>null</c> khi là mặc định.</summary>
        public static string? ToStorage(CvScoringPolicy? policy)
            => policy == null || policy.IsDefault ? null : JsonSerializer.Serialize(policy.Normalized(), JsonOpts);

        /// <summary>Đọc dễ dãi: rỗng / hỏng / thiếu phần nào thì phần đó về mặc định.</summary>
        public static CvScoringPolicy FromStorage(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Default;
            try
            {
                var p = JsonSerializer.Deserialize<CvScoringPolicy>(json, JsonOpts);
                return p?.Normalized() ?? Default;
            }
            catch (JsonException)
            {
                return Default;
            }
        }

        /// <summary>Ảnh chụp công thức đã áp cho MỘT bản chấm — luôn ghi đủ, kể cả khi là mặc định (ADR-060: bản chấm tự giải thích được).</summary>
        public string ToSnapshotJson() => JsonSerializer.Serialize(Normalized(), JsonOpts);

        /// <summary>Nhãn khuyến nghị theo điểm (chưa xét cổng).</summary>
        public string Tier(int score) => (Tiers ?? new CvTierCuts()).Tier(score);

        public CvScoringPolicy Normalized() => new()
        {
            Bands = Bands ?? new CvBandCuts(),
            Tiers = Tiers ?? new CvTierCuts(),
        };

        public bool SameAs(CvScoringPolicy? other)
        {
            var a = Normalized();
            var b = (other ?? Default).Normalized();
            return a.Bands.ExcellentFrom == b.Bands.ExcellentFrom && a.Bands.GoodFrom == b.Bands.GoodFrom
                   && a.Bands.FairFrom == b.Bands.FairFrom
                   && a.Tiers.StrongHireFrom == b.Tiers.StrongHireFrom && a.Tiers.HireFrom == b.Tiers.HireFrom
                   && a.Tiers.CautionFrom == b.Tiers.CautionFrom;
        }
    }

    /// <summary>
    /// Ngưỡng dưới của ba dải trên; dải thấp nhất luôn bắt đầu từ 0, dải cao nhất luôn kết thúc ở 100.
    /// excellent = [ExcellentFrom, 100] · good = [GoodFrom, ExcellentFrom − 1] · fair = [FairFrom, GoodFrom − 1] · poor = [0, FairFrom − 1].
    /// </summary>
    public sealed class CvBandCuts
    {
        public const int DefaultExcellentFrom = 90;
        public const int DefaultGoodFrom = 70;
        public const int DefaultFairFrom = 40;

        /// <summary>Độ rộng tối thiểu (đỉnh − đáy) của một dải — dải hẹp hơn thì ý kiểm không còn chỗ tạo khác biệt.</summary>
        public const int MinWidth = 5;

        public int ExcellentFrom { get; set; } = DefaultExcellentFrom;
        public int GoodFrom { get; set; } = DefaultGoodFrom;
        public int FairFrom { get; set; } = DefaultFairFrom;

        [JsonIgnore]
        public bool IsDefault => ExcellentFrom == DefaultExcellentFrom && GoodFrom == DefaultGoodFrom && FairFrom == DefaultFairFrom;

        public (decimal Min, decimal Max)? Range(string? band) => band switch
        {
            ScoringRubric.BandExcellent => (ExcellentFrom, 100m),
            ScoringRubric.BandGood => (GoodFrom, ExcellentFrom - 1m),
            ScoringRubric.BandFair => (FairFrom, GoodFrom - 1m),
            ScoringRubric.BandPoor => (0m, FairFrom - 1m),
            _ => null,
        };

        public string BandOf(decimal score)
        {
            if (score >= ExcellentFrom) return ScoringRubric.BandExcellent;
            if (score >= GoodFrom) return ScoringRubric.BandGood;
            if (score >= FairFrom) return ScoringRubric.BandFair;
            return ScoringRubric.BandPoor;
        }
    }

    /// <summary>Ngưỡng của nhãn khuyến nghị (so với điểm tổng đã làm tròn).</summary>
    public sealed class CvTierCuts
    {
        public const int DefaultStrongHireFrom = 80;
        public const int DefaultHireFrom = 65;
        public const int DefaultCautionFrom = 50;

        public int StrongHireFrom { get; set; } = DefaultStrongHireFrom;
        public int HireFrom { get; set; } = DefaultHireFrom;
        public int CautionFrom { get; set; } = DefaultCautionFrom;

        [JsonIgnore]
        public bool IsDefault => StrongHireFrom == DefaultStrongHireFrom && HireFrom == DefaultHireFrom && CautionFrom == DefaultCautionFrom;

        public string Tier(int score)
        {
            if (score >= StrongHireFrom) return CvRecommendations.StrongHire;
            if (score >= HireFrom) return CvRecommendations.Hire;
            if (score >= CautionFrom) return CvRecommendations.Caution;
            return CvRecommendations.Reject;
        }
    }

    /// <summary>
    /// Nhãn khuyến nghị lưu ở <c>cv_jd_analyses.overall_recommendation</c>. Giữ đúng chuỗi cũ — i18n và dữ liệu
    /// đã lưu dựa vào chúng.
    /// </summary>
    public static class CvRecommendations
    {
        public const string StrongHire = "Strong Hire";
        public const string Hire = "Hire";
        public const string Caution = "Proceed with caution";
        public const string Reject = "Reject";
    }
}
