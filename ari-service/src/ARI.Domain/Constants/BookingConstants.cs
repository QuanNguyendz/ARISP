namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vòng đời của <see cref="Entities.InterviewBooking"/>.
    ///
    /// QUY TẮC NỀN TẢNG: một booking CHIẾM CHỖ trong khung giờ khi và chỉ khi
    /// <c>Status == Scheduled</c>. <c>ConfirmationStatus</c> KHÔNG tham gia — lịch đang
    /// chờ ứng viên xác nhận vẫn giữ chỗ (ADR-048). Vị từ này phải giống hệt nhau ở mọi
    /// nơi đếm chỗ: read model, SQL đối soát, và cột <c>availability_slots.booked_count</c>.
    /// </summary>
    public static class BookingStatus
    {
        /// <summary>Đang giữ chỗ — trạng thái DUY NHẤT được tính vào booked_count.</summary>
        public const string Scheduled = "scheduled";

        /// <summary>Ứng viên báo bận hoặc hệ thống tự huỷ do quá hạn xác nhận — đã TRẢ chỗ.</summary>
        public const string Declined = "declined";

        /// <summary>Nhân sự loại hồ sơ khỏi quy trình — đã TRẢ chỗ.</summary>
        public const string Cancelled = "cancelled";
    }

    /// <summary>Phản hồi của ứng viên với lịch được gán.</summary>
    public static class BookingConfirmationStatus
    {
        public const string Pending = "pending";
        public const string Confirmed = "confirmed";
        public const string Declined = "declined";
    }

    /// <summary>
    /// AI đã đóng lịch này. Tồn tại vì <c>Status</c> + <c>ConfirmationStatus</c> KHÔNG phân biệt
    /// được "ứng viên báo bận" với "hệ thống tự huỷ do quá hạn xác nhận" — cả hai đều ghi
    /// <c>declined</c>. Trước khi có cột này, giao diện phải dò chuỗi tiếng Việt trong
    /// <c>DeclineReason</c> (<c>.includes('loại')</c>) nên gắn nhãn sai, và ứng viên chỉ cần gõ
    /// đúng từ khoá vào lý do báo bận là đổi được trạng thái hiển thị của chính mình.
    /// </summary>
    public static class BookingDeclinedBy
    {
        /// <summary>Ứng viên tự báo bận (kèm lý do tự nhập).</summary>
        public const string Candidate = "candidate";

        /// <summary>
        /// Hệ thống tự đóng lịch: ứng viên không tham dự buổi phỏng vấn đã hẹn
        /// (<c>InterviewScheduleFollowUpHostedService</c>). Dữ liệu cũ còn giá trị này cho trường
        /// hợp "quá hạn xác nhận" của cơ chế auto-huỷ 48h đã bỏ.
        /// </summary>
        public const string System = "system";

        /// <summary>Nhân sự loại hồ sơ khỏi quy trình tuyển dụng.</summary>
        public const string Staff = "staff";
    }

    /// <summary>
    /// Trạng thái một ứng viên trong ca phỏng vấn, dưới góc nhìn của nhân sự. Suy ra từ
    /// (<c>Status</c>, <c>ConfirmationStatus</c>, <c>DeclinedBy</c>) — xem
    /// <c>InterviewService.ResolveCandidateState</c>. Giao diện đọc thẳng giá trị này thay vì
    /// tự suy luận, nên nhãn không còn phụ thuộc vào nội dung văn bản lý do.
    /// </summary>
    public static class SlotCandidateState
    {
        /// <summary>Đang giữ chỗ, chờ ứng viên xác nhận.</summary>
        public const string Pending = "pending";

        /// <summary>Đang giữ chỗ, ứng viên đã xác nhận tham dự.</summary>
        public const string Confirmed = "confirmed";

        /// <summary>Ứng viên chủ động báo bận — đã trả chỗ, vẫn có thể dời sang ca khác.</summary>
        public const string DeclinedByCandidate = "declined_by_candidate";

        /// <summary>Hệ thống tự huỷ do quá hạn xác nhận — đã trả chỗ, vẫn có thể dời sang ca khác.</summary>
        public const string ExpiredNoResponse = "expired_no_response";

        /// <summary>
        /// Ứng viên KHÔNG tham dự buổi phỏng vấn đã hẹn (phớt lờ thư mời hoặc xác nhận rồi không
        /// đến) → hệ thống tự đánh trượt. Khác <see cref="DeclinedByCandidate"/> ở chỗ KHÔNG xếp
        /// lại lịch được: người báo bận thì được xếp ca khác, người không đến thì hồ sơ dừng lại.
        /// </summary>
        public const string NoShow = "no_show";

        /// <summary>Nhân sự đã loại hồ sơ — đã trả chỗ, KHÔNG thao tác gì thêm được.</summary>
        public const string RejectedByStaff = "rejected_by_staff";

        /// <summary>Huỷ vì lý do khác (không rõ nguồn) — đã trả chỗ.</summary>
        public const string Cancelled = "cancelled";
    }
}
