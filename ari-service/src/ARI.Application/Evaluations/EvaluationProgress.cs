using ARI.Domain.Constants;

namespace ARI.Application.Evaluations
{
    /// <summary>
    /// Trạng thái báo cáo đánh giá để HIỂN THỊ (ADR-073) — một bản luật cho mọi màn đọc, để phía ứng viên và phía
    /// nhân sự không tự suy mỗi nơi một kiểu. Lỗi AI còn lượt thử tự động vẫn là "đang chấm": người xem không
    /// làm được gì với nó, hệ thống đang tự xử lý.
    /// </summary>
    public static class EvaluationProgress
    {
        public const string Done = "done";
        public const string Pending = "pending";
        public const string NeedsRubric = "needs_rubric";
        public const string NoAnswers = "no_answers";
        public const string Failed = "failed";

        /// <returns>Null với phiên chưa đóng (chưa có gì để chấm).</returns>
        public static string? ForDisplay(string? sessionStatus, string? evaluationStatus, int attempts, bool hasEvaluation)
        {
            if (hasEvaluation) return Done;
            if (!InterviewSessionStatuses.Is(sessionStatus, InterviewSessionStatuses.Completed)) return null;

            return evaluationStatus switch
            {
                EvaluationStatuses.BlockedNoRubric => NeedsRubric,
                EvaluationStatuses.NoAnswers => NoAnswers,
                EvaluationStatuses.Failed when attempts >= EvaluationStatuses.MaxAttempts => Failed,
                _ => Pending,
            };
        }
    }
}
