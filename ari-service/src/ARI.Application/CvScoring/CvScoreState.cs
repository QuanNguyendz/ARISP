using System;
using ARI.Domain.Constants;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Luật DUY NHẤT cho câu hỏi "hồ sơ này đang ở trạng thái điểm CV nào, và có được hiện con số không"
    /// (ADR-070). Mọi màn đọc điểm CV — danh sách, chi tiết, dashboard, Portal — đi qua đây, để không màn
    /// nào lỡ hiện lại điểm AI tự cho (bản chấm trước ADR-070) hay "0 điểm" của một file không phải CV.
    /// </summary>
    public static class CvScoreState
    {
        /// <summary>Tóm tắt bản chấm đang gắn với hồ sơ — đủ để phân loại, không cần nạp cả dòng.</summary>
        public readonly record struct AnalysisInfo(string Status, Guid? RubricDocumentId, int MatchScore);

        /// <summary>Bản chấm này có được hiện điểm không: chỉ khi chấm xong theo MỘT bộ tiêu chí.</summary>
        public static bool IsDisplayable(string? status, Guid? rubricDocumentId)
            => rubricDocumentId != null && string.Equals(status, CvAnalysisStatuses.Completed, StringComparison.OrdinalIgnoreCase);

        /// <param name="hasCvFile">Hồ sơ có file CV để chấm không.</param>
        /// <param name="analysis">Bản chấm đang gắn với hồ sơ (nếu có).</param>
        /// <param name="liveRubricId">Bộ tiêu chí đang sống của tin (nếu có).</param>
        /// <returns>Trạng thái + điểm được phép hiện (null nếu không hiện).</returns>
        public static (string State, int? Score) Resolve(bool hasCvFile, AnalysisInfo? analysis, Guid? liveRubricId)
        {
            if (!hasCvFile && analysis == null) return (CvScoreStates.NoCv, null);

            var a = analysis;
            var status = a?.Status;
            // "failed" là tên cũ của invalid_cv — migration đổi hết, giữ ở đây cho dữ liệu sao lưu cũ.
            var isInvalid = string.Equals(status, CvAnalysisStatuses.InvalidCv, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

            if (liveRubricId == null)
                return isInvalid ? (CvScoreStates.InvalidCv, null) : (CvScoreStates.PendingRubric, null);

            if (a == null) return (CvScoreStates.Queued, null);

            var current = a.Value.RubricDocumentId == liveRubricId;
            if (isInvalid) return (CvScoreStates.InvalidCv, null);
            if (current) return (CvScoreStates.Scored, a.Value.MatchScore);

            // Có điểm theo bộ tiêu chí CŨ → vẫn hiện (là điểm thật theo tiêu chí) kèm nhãn "đang chấm lại".
            // Điểm AI tự cho trước ADR-070 (không có bộ tiêu chí) thì không hiện.
            return IsDisplayable(a.Value.Status, a.Value.RubricDocumentId)
                ? (CvScoreStates.Rescoring, a.Value.MatchScore)
                : (CvScoreStates.Queued, null);
        }

        /// <summary>
        /// Như <see cref="Resolve(bool, AnalysisInfo?, Guid?)"/>, cộng thêm lượt chấm hỏng gần nhất của hồ sơ theo bộ tiêu
        /// chí hiện hành. Chỉ đổi hai trạng thái "đang chờ": hồ sơ đã có điểm đúng bộ thì lỗi cũ không còn ý nghĩa.
        /// Đang chấm lại mà hỏng thì vẫn giữ điểm theo bộ cũ — đó là điểm thật, chỉ chưa cập nhật.
        /// </summary>
        public static (string State, int? Score) Resolve(
            bool hasCvFile, AnalysisInfo? analysis, Guid? liveRubricId, CvScoringInFlight.FailureState? failure)
        {
            var (state, score) = Resolve(hasCvFile, analysis, liveRubricId);
            if (failure != null && (state == CvScoreStates.Queued || state == CvScoreStates.Rescoring))
                return (CvScoreStates.ScoringFailed, score);
            return (state, score);
        }

        /// <summary>Lỗi gần nhất của hồ sơ theo bộ tiêu chí đang sống (null nếu không có hoặc không tra được).</summary>
        public static CvScoringInFlight.FailureState? FailureOf(CvScoringInFlight? inFlight, Guid applicationId, Guid? liveRubricId)
            => inFlight == null || liveRubricId is not { } rubricId
                ? null
                : inFlight.LastFailure(CvScoringInFlight.ApplicationKey(applicationId, rubricId));
    }
}
