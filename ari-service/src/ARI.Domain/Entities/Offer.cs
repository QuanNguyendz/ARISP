using System;
using ARI.Domain.Constants;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Thư mời nhận việc (ADR-061, Phase 5) — đoạn kết của phễu tuyển dụng.
    ///
    /// Ràng buộc quan trọng nhất nằm ở DB chứ không ở mã: <b>một hồ sơ chỉ có MỘT offer sống</b>
    /// (unique index có lọc trên <c>application_id</c>, loại trừ các trạng thái đã khép). "Hai
    /// offer, hai mức lương, gửi cả hai" là hỏng nặng nhất mà tính năng này có thể gây ra, và nó
    /// không thể chặn đáng tin bằng một câu <c>if</c> trong lệnh.
    /// </summary>
    public class Offer : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ApplicationId { get; set; }

        /// <summary>Phi chuẩn hoá để định tuyến realtime + lọc phạm vi không phải join ngược qua hồ sơ.</summary>
        public Guid JobPostingId { get; set; }

        /// <summary>Xem <see cref="OfferStatus"/>.</summary>
        public string Status { get; set; } = OfferStatus.Draft;

        /// <summary>
        /// ẢNH CHỤP tên vị trí lúc ra offer. Tiêu đề tin vẫn bị sửa sau đó, mà thư mời nhận việc
        /// là văn bản cam kết — không được đổi nội dung theo dữ liệu sống.
        /// </summary>
        public string? Position { get; set; }

        public decimal? SalaryAmount { get; set; }
        public string? SalaryCurrency { get; set; } = "VND";
        /// <summary>month | year</summary>
        public string? SalaryPeriod { get; set; } = "month";

        public string? Bonus { get; set; }
        public string? Benefits { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkLocation { get; set; }

        /// <summary>Ngày đi làm dự kiến.</summary>
        public DateTimeOffset? StartDate { get; set; }

        /// <summary>Hạn ứng viên trả lời. Quá hạn → tác vụ nền đóng offer.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Ghi chú NỘI BỘ — không bao giờ trả ra Portal của ứng viên.</summary>
        public string? Notes { get; set; }

        /// <summary>storageKey của file thư mời (nếu có) — qua IFileStorageService như SignedJdFileUrl.</summary>
        public string? OfferLetterFileUrl { get; set; }

        public Guid CreatedByUserId { get; set; }

        // ===== Duyệt nội bộ =====
        public Guid? ApprovedByUserId { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        /// <summary>Lý do trả về bản nháp (bắt buộc khi từ chối duyệt).</summary>
        public string? RejectedReason { get; set; }

        // ===== Gửi & phản hồi =====
        public DateTimeOffset? SentAt { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        /// <summary>Lý do ứng viên từ chối (tự nhập) — dữ liệu quý để cải thiện mức đãi ngộ.</summary>
        public string? CandidateResponseNote { get; set; }

        public Guid? WithdrawnByUserId { get; set; }
        public string? WithdrawnReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
