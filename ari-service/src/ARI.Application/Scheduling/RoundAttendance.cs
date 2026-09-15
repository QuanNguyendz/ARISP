using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Domain.Entities;

namespace ARI.Application.Scheduling
{
    /// <summary>
    /// "Ứng viên có dự vòng này không" — dấu vết tham dự tuỳ theo LOẠI vòng.
    ///
    /// <b>Vì sao phải hỏi theo loại vòng.</b> Tác vụ nền tự đánh trượt người vắng mặt (ADR-059) vốn
    /// chỉ tìm một <see cref="InterviewSession"/> loại <c>real</c>. Vòng TRẮC NGHIỆM không bao giờ
    /// sinh ra phiên nào — ứng viên làm bài tại nhà, không phòng, không Kiosk — nên mọi người thi
    /// trắc nghiệm đều trượt phép kiểm đó khi khung giờ trôi qua, <b>kể cả người đã nộp bài</b>. Họ
    /// bị ghi <c>not_pass</c> rồi rơi khỏi vòng sang "Không phù hợp" mà không ai bấm gì.
    ///
    /// <b>ĐIỂM SỐ không tham gia phép kiểm này.</b> Nộp bài là đã dự thi, dù được bao nhiêu điểm.
    /// Dưới điểm sàn thì hồ sơ ở nguyên vòng trắc nghiệm cho tới khi Recruiter quyết định loại — một
    /// tác vụ nền không phải chỗ ra quyết định tuyển dụng (cùng lý lẽ ADR-053 gỡ quyền ghi
    /// <c>Application.Status</c> khỏi AI).
    ///
    /// Tách khỏi hosted service để luật này test được: <c>ARI.Infrastructure</c> chưa có bộ test nào.
    /// </summary>
    public static class RoundAttendance
    {
        /// <summary>
        /// Vòng này có đi đường "không tham dự → đánh trượt + trả chỗ" (ADR-059) không.
        ///
        /// <b>Vòng trắc nghiệm thì KHÔNG.</b> Không vào làm bài vẫn là CÓ kết quả: hết hạn thì hệ
        /// thống nộp thay một bài trống (<see cref="OnlineTest.OnlineTestExpiry"/>), hồ sơ ở nguyên
        /// vòng với 0 điểm, và Recruiter quyết định loại hay giữ. Đường no-show thì ngược lại ở cả
        /// ba chỗ: nó huỷ lịch, TRẢ CHỖ trong ca (chỉ ứng viên từ chối mới được trả chỗ), và tự ghi
        /// <c>not_pass</c> — sau đó Portal còn báo "Chưa có giờ làm bài" với một người đã được hẹn.
        /// </summary>
        public static bool CanBeNoShow(string? roundType) => !InterviewInviteEmail.IsOnlineTest(roundType);

        /// <summary>
        /// Ứng viên đã để lại dấu vết tham dự vòng <paramref name="roundNumber"/> chưa.
        ///
        /// <paramref name="roundType"/> là <c>RoundType</c> của <see cref="InterviewRoundConfig"/>
        /// tương ứng; <c>null</c> (không tra được cấu hình vòng) được coi như vòng hội thoại — giữ
        /// đúng hành vi cũ thay vì âm thầm nới lỏng phép kiểm.
        /// </summary>
        public static bool HasAttended(
            Guid applicationId,
            int roundNumber,
            string? roundType,
            IEnumerable<InterviewSession> realSessions,
            IEnumerable<OnlineTestSubmission> submissions)
        {
            if (InterviewInviteEmail.IsOnlineTest(roundType))
                return submissions.Any(s => s.ApplicationId == applicationId && s.RoundNumber == roundNumber);

            return realSessions.Any(s => s.ApplicationId == applicationId && s.RoundNumber == roundNumber);
        }
    }
}
