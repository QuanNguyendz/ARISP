using System;

namespace ARI.Domain.Entities
{
    public class OnlineTestSubmission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string SelectedAnswers { get; set; } = "{}"; // JSONB – e.g. {"q_uuid_1":[2], "q_uuid_2":[0,3]}
        public decimal Score { get; set; }
        public bool IsPassed { get; set; } = false;
        /// <summary>Số câu trả lời đúng trong lượt thi này.</summary>
        public int CorrectCount { get; set; }
        /// <summary>Tổng số câu của lượt thi (số câu đã bốc ngẫu nhiên).</summary>
        public int TotalQuestions { get; set; }
        /// <summary>
        /// Số lần ứng viên rời khỏi bài thi (chuyển tab / mất focus cửa sổ) trong lúc làm bài —
        /// tín hiệu chống gian lận nhẹ do FE đếm và gửi kèm khi nộp. &gt; 0 → HR thấy cờ nghi vấn.
        /// </summary>
        public int TabSwitchCount { get; set; }

        /// <summary>
        /// Ai nộp bài này — <see cref="Constants.OnlineTestSubmittedBy"/>. <c>system</c> = ứng viên
        /// không vào làm bài trong khung giờ đã hẹn, hệ thống tự nộp một bài trống khi hết hạn.
        ///
        /// Phải là cột riêng chứ không suy từ "bài trống": người vào làm rồi bỏ trắng hết cũng ra
        /// 0 điểm, nhưng hai trường hợp đó là hai câu chuyện khác nhau với người tuyển dụng.
        /// </summary>
        public string SubmittedBy { get; set; } = Constants.OnlineTestSubmittedBy.Candidate;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
