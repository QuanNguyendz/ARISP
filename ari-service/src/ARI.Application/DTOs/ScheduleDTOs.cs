using System;
using ARI.Domain.Entities;

namespace ARI.Application.DTOs
{
    /// <summary>Khung giờ phỏng vấn (slot) trả về cho client.</summary>
    public class AvailabilitySlotResponse
    {
        public Guid Id { get; set; }
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        /// <summary>Số ứng viên tối đa; <c>null</c> = không giới hạn (vòng trắc nghiệm).</summary>
        public int? Capacity { get; set; }

        public int BookedCount { get; set; }

        /// <summary>Còn chỗ trống để ứng viên đặt không. Không giới hạn thì luôn còn chỗ.</summary>
        public bool IsAvailable => Capacity == null || BookedCount < Capacity.Value;

        /// <summary>
        /// Ứng viên đang giữ chỗ ở ca này. Rỗng khi chưa ai đặt.
        ///
        /// Vì sao trả kèm chứ không để giao diện tự tra: màn cấu hình lịch trước đây chỉ hiện
        /// "Đã đặt 0/1" — một con số không nói được ca đó đang giữ chỗ cho AI, và người vận hành
        /// phải sang màn khác mới biết có được đụng vào ca này không.
        /// </summary>
        public List<SlotBookingBriefDto> Bookings { get; set; } = new();

        /// <summary>
        /// Số dòng booking ĐÃ ĐÓNG (báo bận / huỷ) vẫn trỏ vào ca này.
        ///
        /// Không chiếm chỗ (nen không vào <c>BookedCount</c>) nhưng làm ca <b>không xoá được</b>: dòng
        /// đó là bằng chứng "ứng viên này được mời vào đúng giờ đó rồi báo bận". Thiếu con số này thì
        /// giao diện hiện "Đã đặt 0/1" — trông như trống — và người dùng bấm xoá rồi mới biết là không.
        /// </summary>
        public int ClosedBookingCount { get; set; }

        /// <summary>
        /// Chính các dòng đã đóng đó — AI, và vì sao (từ chối tham dự / bị loại / huỷ).
        ///
        /// Một con số trần kiểu "2 lượt đã đóng" không trả lời được câu người vận hành hỏi ngay khi
        /// nhìn thấy nó: <i>lượt gì, của ai?</i> Có tên và lý do thì dòng đó tự giải thích được vì sao
        /// ca này không xoá được, và không ai phải sang màn khác để tra.
        /// </summary>
        public List<SlotClosedBookingDto> ClosedBookings { get; set; } = new();

        public static AvailabilitySlotResponse FromEntity(AvailabilitySlot s) => new()
        {
            Id = s.Id,
            JobPostingId = s.JobPostingId,
            RoundNumber = s.RoundNumber,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Timezone = s.Timezone,
            Capacity = s.Capacity,
            BookedCount = s.BookedCount,
        };
    }

    /// <summary>Ứng viên đang giữ một chỗ trong ca — vừa đủ để nhận ra người đó trên màn lịch.</summary>
    public class SlotBookingBriefDto
    {
        public Guid BookingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        /// <summary>pending | confirmed | declined — ứng viên đã xác nhận giờ hẹn chưa (ADR-048).</summary>
        public string ConfirmationStatus { get; set; } = "pending";
    }

    /// <summary>Một dòng booking đã đóng (không giữ chỗ) còn trỏ vào ca.</summary>
    public class SlotClosedBookingDto
    {
        public Guid BookingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }

        /// <summary>
        /// Vì sao dòng này đóng — một giá trị của <c>SlotCandidateState</c>
        /// (<c>declined_by_candidate</c>, <c>no_show</c>, <c>rejected_by_staff</c>, …). Suy từ
        /// (<c>Status</c>, <c>DeclinedBy</c>) ở server để nhãn không phụ thuộc vào chữ trong lý do.
        /// </summary>
        public string State { get; set; } = "cancelled";

        /// <summary>Lý do ứng viên tự nhập khi từ chối, hoặc ghi chú của hệ thống.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Tạo một khung giờ phỏng vấn cho job + vòng.</summary>
    public class CreateSlotRequest
    {
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

        /// <summary>
        /// Số ứng viên tối đa. Bỏ trống = để hệ thống quyết định theo LOẠI vòng: vòng trắc nghiệm
        /// thì không giới hạn, vòng hội thoại thì bằng 1.
        /// </summary>
        public int? Capacity { get; set; }
    }

    public class UpdateSlotCapacityRequest
    {
        /// <summary>Bỏ trống = không giới hạn (chỉ vòng trắc nghiệm chấp nhận).</summary>
        public int? Capacity { get; set; }
    }

    /// <summary>
    /// Một mục lịch phỏng vấn của ứng viên (kèm thông tin booking để xác nhận/từ chối) — ADR-048.
    /// </summary>
    public class CandidateScheduleItemDto
    {
        public Guid BookingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        /// <summary>pending | confirmed | declined — phản hồi của ứng viên với lịch được gán.</summary>
        public string ConfirmationStatus { get; set; } = "pending";
        /// <summary>Lý do đã từ chối (khi ConfirmationStatus = declined).</summary>
        public string? DeclineReason { get; set; }

        /// <summary>
        /// Loại vòng (<c>online_test</c> | <c>screening</c> | <c>technical</c>) — để giao diện gọi đúng
        /// tên: lịch của vòng trắc nghiệm là "lịch làm bài trắc nghiệm", không phải "phỏng vấn".
        /// </summary>
        public string? RoundType { get; set; }
    }

    /// <summary>Body ứng viên từ chối lịch (bận) kèm lý do để nhân sự xếp lại.</summary>
    public class DeclineScheduleRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
