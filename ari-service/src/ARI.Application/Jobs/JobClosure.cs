using System;
using ARI.Domain.Entities;

namespace ARI.Application.Jobs
{
    /// <summary>
    /// Khi nào một tin coi như đã KẾT THÚC TUYỂN — khác với "vừa đóng".
    ///
    /// <b>Vì sao cần phân biệt.</b> Tin đóng lúc hết hạn nộp hồ sơ, nhưng phễu chưa xong: những ứng
    /// viên đã nộp vẫn đang phỏng vấn, HM còn phải chốt kết quả, HR còn phải gửi thư mời. Báo ngay
    /// cho họ "tin đã đóng, bạn không phù hợp" ở thời điểm đó là sai — họ vẫn đang trong cuộc.
    ///
    /// <b>Mốc kết thúc là NGÀY ĐI LÀM dự kiến trên phiếu yêu cầu.</b> Qua ngày đó mà chưa ai được
    /// nhận thì vị trí ấy thực sự khép lại. Đây là mốc nghiệp vụ có sẵn, không phải một hằng số bịa ra.
    ///
    /// <b>Dữ liệu cũ (trước ADR-063) không có phiếu</b>, nên không có ngày đi làm. Với chúng, mốc là
    /// <see cref="GraceDaysWithoutRequest"/> ngày sau hạn nộp hồ sơ — đủ cho một vòng chốt kết quả.
    ///
    /// Không có mốc nào (tin cũ không hạn nộp) thì KHÔNG bao giờ kết thúc: thà để một tin treo còn
    /// hơn tự đánh trượt người mà không có căn cứ thời gian nào.
    /// </summary>
    public static class JobClosure
    {
        /// <summary>Số ngày ân hạn sau hạn nộp, chỉ dùng cho tin không gắn phiếu yêu cầu.</summary>
        public const int GraceDaysWithoutRequest = 5;

        /// <summary>Trạng thái tin coi là đã đóng cửa nhận hồ sơ.</summary>
        public static bool IsClosedStatus(string? status) =>
            string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Tin còn <c>active</c> nhưng đã quá hạn nộp — đóng trên thực tế dù cột trạng thái chưa đổi.
        /// Đây là luật hiển thị vốn có của hệ thống, chỉ được gom về đây thay vì chép ở ba nơi.
        /// </summary>
        public static bool DeadlinePassed(string? status, DateTimeOffset? applicationDeadline, DateTimeOffset now) =>
            string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
            && applicationDeadline.HasValue && applicationDeadline.Value <= now;

        /// <summary>
        /// Mốc thời điểm tin hết ân hạn. <c>null</c> = không xác định được ⇒ không bao giờ kết thúc.
        /// </summary>
        public static DateTimeOffset? FinishesAt(DateTimeOffset? applicationDeadline, DateTimeOffset? expectedStartDate)
            => expectedStartDate ?? applicationDeadline?.AddDays(GraceDaysWithoutRequest);

        /// <summary>
        /// Tin đã kết thúc tuyển chưa: vừa phải ĐÓNG, vừa phải QUA mốc ân hạn. Thiếu một trong hai
        /// thì vẫn coi như đang chạy — tin mới đóng còn nguyên thời gian cho HM/HR chốt nốt kết quả.
        ///
        /// Nhận từng trường rời chứ không chỉ nhận entity: các màn danh sách chiếu cột ra kiểu ẩn danh
        /// để khỏi kéo những cột JSON nặng, và nếu ở đó phải tự ghép lại luật thì sớm muộn hai màn sẽ
        /// nói hai kiểu về cùng một tin.
        /// </summary>
        public static bool IsFinished(
            string? status, DateTimeOffset? applicationDeadline, DateTimeOffset? expectedStartDate, DateTimeOffset now)
            => (IsClosedStatus(status) || DeadlinePassed(status, applicationDeadline, now))
               && FinishesAt(applicationDeadline, expectedStartDate) is { } finishesAt
               && now > finishesAt;

        /// <summary>
        /// Trạng thái để HIỂN THỊ cho nhân sự. Trước đây ba nơi cùng viết
        /// <c>active + quá hạn nộp ⇒ "closed"</c>, nên tin vừa hết hạn đã bị xếp vào nhóm đã đóng
        /// ngay lập tức — trong khi HM/HR vẫn còn phải chốt kết quả cho những người đã nộp.
        /// </summary>
        public static string DisplayStatus(
            string? status, DateTimeOffset? applicationDeadline, DateTimeOffset? expectedStartDate, DateTimeOffset now)
            => IsFinished(status, applicationDeadline, expectedStartDate, now) ? "closed" : status ?? "draft";

        /// <summary>Bản tiện dụng khi đã có sẵn entity.</summary>
        public static bool IsFinished(JobPosting? job, RecruitmentRequest? request, DateTimeOffset now)
            => job != null
               && IsFinished(job.Status, job.ApplicationDeadline, request?.ExpectedStartDate, now);
    }
}
