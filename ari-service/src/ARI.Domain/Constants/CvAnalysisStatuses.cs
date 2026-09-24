namespace ARI.Domain.Constants
{
    /// <summary>
    /// Trạng thái một lần chấm CV (<c>cv_jd_analyses.status</c>) — ADR-070.
    ///
    /// Lỗi AI tạm thời KHÔNG có dòng nào: không lưu thì lượt quét sau chấm lại được. Chỉ lưu hai kết cục
    /// chắc chắn của một file: chấm xong, hoặc file đó không phải CV (chấm lại cũng ra như vậy).
    /// </summary>
    public static class CvAnalysisStatuses
    {
        public const string Completed = "completed";

        /// <summary>
        /// AI xác định file không phải CV. Trước ADR-070 ghi là <c>failed</c> kèm điểm 0, và màn nhân sự
        /// hiện "0 điểm" như thể ứng viên rất kém — migration đổi hết sang giá trị này.
        /// </summary>
        public const string InvalidCv = "invalid_cv";
    }

    /// <summary>
    /// Kết quả các CỔNG của công thức chấm CV (<c>cv_jd_analyses.gate_status</c>) — ADR-075: điều kiện bắt buộc
    /// và điểm tối thiểu của tiêu chí. <c>null</c> = bộ tiêu chí không có cổng nào.
    ///
    /// Cổng chỉ là NHÃN: không đạt thì khuyến nghị bị ép "Reject", hồ sơ không bao giờ bị chặn hay tự loại (ADR-053).
    /// </summary>
    public static class CvGateStatuses
    {
        /// <summary>Qua mọi cổng.</summary>
        public const string Pass = "pass";
        /// <summary>Trượt ít nhất một cổng — khuyến nghị bị ép "Reject".</summary>
        public const string Fail = "fail";
        /// <summary>
        /// Không trượt cổng nào nhưng có cổng chưa xác minh được (AI bỏ sót, hoặc đánh "đạt" mà không trích được
        /// bằng chứng) — cần người kiểm tra tay; KHÔNG ép khuyến nghị.
        /// </summary>
        public const string Review = "review";
    }

    /// <summary>
    /// Trạng thái điểm CV mà giao diện hiển thị cho một hồ sơ (suy ra, không lưu) — ADR-070.
    /// </summary>
    public static class CvScoreStates
    {
        /// <summary>Đã chấm theo bộ tiêu chí hiện hành.</summary>
        public const string Scored = "scored";
        /// <summary>Tin chưa có bộ tiêu chí — hồ sơ chờ HM khai.</summary>
        public const string PendingRubric = "pending_rubric";
        /// <summary>Chưa có kết quả, đang/ sắp chấm.</summary>
        public const string Queued = "queued";
        /// <summary>Có điểm theo bộ tiêu chí cũ; đang chấm lại theo bộ mới.</summary>
        public const string Rescoring = "rescoring";
        /// <summary>File không phải CV.</summary>
        public const string InvalidCv = "invalid_cv";
        /// <summary>Hồ sơ không có file CV để chấm.</summary>
        public const string NoCv = "no_cv";
        /// <summary>
        /// Lượt chấm gần nhất hỏng (AI lỗi, không đọc được file…) — hệ thống tự thử lại theo lịch giãn dần.
        /// Suy ra từ trạng thái lỗi trong bộ nhớ của tiến trình, không lưu DB: khởi động lại thì lượt quét
        /// chấm lại ngay và trạng thái quay về "đang chấm".
        /// </summary>
        public const string ScoringFailed = "scoring_failed";
    }
}
