using System;
using ARI.Domain.Constants;

namespace ARI.Application.Applications
{
    /// <summary>
    /// Vòng mà hồ sơ đang ở — con số quyết định hồ sơ nằm ở cột "Vòng N" nào trên bảng ứng viên,
    /// và vòng nào được xếp lịch tiếp.
    ///
    /// <b>Ba nguồn, lấy lớn nhất:</b> lời mời vòng (sinh ra khi HM chốt ĐẠT vòng trước), phiên phỏng
    /// vấn, và <b>lịch đang giữ chỗ</b>. Nguồn thứ ba từng bị bỏ sót: vòng TRẮC NGHIỆM không có bước HM
    /// chốt kết quả nên không bao giờ sinh lời mời vòng kế — Recruiter nhìn điểm rồi xếp thẳng lịch
    /// vòng 2, và hồ sơ đã có lịch vòng 2 vẫn nằm ở cột "Vòng 1" với giờ hẹn của vòng 1. Lịch đang
    /// giữ chỗ là tín hiệu mạnh nhất rằng ứng viên đã sang vòng đó (ADR-058: dòng booking là sự thật).
    /// </summary>
    public static class ApplicationCurrentRound
    {
        /// <returns><c>null</c> khi hồ sơ còn ở giai đoạn CV (chưa vào vòng nào).</returns>
        public static int? Resolve(string? status, int maxInviteRound, int maxSessionRound, int maxScheduledBookingRound)
        {
            if (ApplicationStatuses.Is(status, ApplicationStatuses.CvSubmitted)
                || ApplicationStatuses.Is(status, ApplicationStatuses.Invited)
                || ApplicationStatuses.Is(status, ApplicationStatuses.CvRejected))
                return null;

            // Đã duyệt nhưng chưa có lịch: chờ xếp vòng 1.
            if (ApplicationStatuses.Is(status, ApplicationStatuses.Screening)) return 1;

            var round = Math.Max(Math.Max(maxInviteRound, maxSessionRound), maxScheduledBookingRound);
            return round > 0 ? round : 1;
        }
    }
}
