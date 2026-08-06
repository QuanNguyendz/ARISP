using System;
using System.Collections.Generic;

namespace ARI.Application.DTOs
{
    // ============================================================
    // DTOs — Ngân hàng câu hỏi trắc nghiệm (Online Test) per Job Posting
    // ============================================================

    /// <summary>Câu hỏi trắc nghiệm — góc nhìn HR/Recruiter (kèm đáp án đúng).</summary>
    public record OnlineTestQuestionDto(
        Guid Id, string QuestionText, List<string> Options, string QuestionType, List<int> CorrectOptions);

    /// <summary>Toàn bộ ngân hàng câu hỏi + cấu hình bài thi của một job — góc nhìn HR/Recruiter.</summary>
    public record OnlineTestBankDto(
        Guid JobPostingId,
        string JobTitle,
        int PassScore,
        int QuestionsPerTest,
        int DurationMinutes,
        List<OnlineTestQuestionDto> Questions);

    /// <summary>Câu hỏi hiển thị cho ứng viên khi làm bài — KHÔNG lộ đáp án đúng.</summary>
    public record CandidateTestQuestionDto(Guid Id, string QuestionText, List<string> Options, string QuestionType);

    /// <summary>Đề thi cho ứng viên (đã bốc ngẫu nhiên) + trạng thái đã nộp (nếu có).</summary>
    public record CandidateOnlineTestDto(
        Guid ApplicationId,
        Guid JobPostingId,
        string JobTitle,
        int RoundNumber,
        int PassScore,
        int DurationMinutes,
        int TotalQuestions,
        List<CandidateTestQuestionDto> Questions,
        bool AlreadySubmitted,
        decimal? Score,
        bool? IsPassed,
        DateTimeOffset? SubmittedAt,
        bool CvPassed);

    /// <summary>Kết quả chấm bài trắc nghiệm.</summary>
    public record OnlineTestResultDto(
        decimal Score,
        bool IsPassed,
        int PassScore,
        int CorrectCount,
        int TotalQuestions,
        DateTimeOffset SubmittedAt);

    /// <summary>Một dòng điểm của ứng viên trong bảng tổng hợp theo job.</summary>
    public record OnlineTestScoreRowDto(
        Guid ApplicationId,
        string CandidateName,
        string CandidateEmail,
        int RoundNumber,
        decimal Score,
        bool IsPassed,
        int CorrectCount,
        int TotalQuestions,
        DateTimeOffset SubmittedAt,
        int TabSwitchCount);

    /// <summary>Bảng tổng hợp điểm bài trắc nghiệm của toàn bộ ứng viên đã thi trong một job.</summary>
    public record OnlineTestJobResultsDto(
        Guid JobPostingId,
        string JobTitle,
        int PassScore,
        int TotalQuestions,
        int SubmissionCount,
        int PassedCount,
        int NotPassedCount,
        decimal AverageScore,
        decimal HighestScore,
        decimal LowestScore,
        List<OnlineTestScoreRowDto> Rows);

    // ===== Request bodies (model binding từ controller) =====

    public class UpsertOnlineTestQuestionRequest
    {
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        /// <summary>single | multiple.</summary>
        public string QuestionType { get; set; } = "single";
        /// <summary>Chỉ số các đáp án đúng (0-based). single = đúng 1 phần tử; multiple = ≥1 phần tử.</summary>
        public List<int> CorrectOptions { get; set; } = new();
    }

    public class UpdateOnlineTestSettingsRequest
    {
        public int PassScore { get; set; }
        public int QuestionsPerTest { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class SubmitOnlineTestRequest
    {
        /// <summary>Map questionId → danh sách chỉ số đáp án ứng viên chọn (0-based, có thể nhiều).</summary>
        public Dictionary<Guid, List<int>> Answers { get; set; } = new();

        /// <summary>
        /// Số lần ứng viên rời khỏi bài thi (chuyển tab / mất focus cửa sổ) trong lúc làm bài —
        /// FE đếm và gửi kèm. Chống gian lận nhẹ (client-side, có thể spoof) — dùng để răn đe + lưu vết.
        /// </summary>
        public int TabSwitchCount { get; set; }
    }
}
