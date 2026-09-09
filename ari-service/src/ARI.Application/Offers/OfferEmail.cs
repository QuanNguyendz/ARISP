using System;
using System.Globalization;
using ARI.Domain.Entities;

namespace ARI.Application.Offers
{
    /// <summary>
    /// Thư mời nhận việc gửi ứng viên (ADR-061, Phase 5).
    ///
    /// Theo khuôn <c>InterviewInviteEmail</c> — builder tách riêng, KHÔNG nội suy HTML tại call
    /// site, để trình soạn thảo của Phase 4 dựng bản xem trước bằng đúng nội dung sẽ gửi.
    /// </summary>
    public static class OfferEmail
    {
        public record Content(string Subject, string Html);

        private static string Money(decimal? amount, string? currency)
        {
            if (amount is not { } value) return "Thoả thuận";
            var text = value.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.');
            return $"{text} {currency ?? "VND"}";
        }

        private static string Date(DateTimeOffset? value) =>
            // Múi giờ VN cố định (+7), KHÔNG dùng ToLocalTime(): trên container production TZ=UTC nên
            // ToLocalTime() là lệnh rỗng, và ngày bắt đầu 05/09 do HR chọn (lưu 04/09T17:00Z) in ra
            // thành 04/09 — thư mời ghi sai ngày so với thứ HR chọn và ứng viên thấy trong Portal.
            value is { } d
                ? d.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "Sẽ trao đổi thêm";

        /// <summary>
        /// Chu kỳ lương → hậu tố hiển thị. Câu nhị phân cũ (`== "year" ? " / năm" : " / tháng"`) in ra
        /// "/ tháng" cho lương THEO GIỜ — mà trình soạn thư mời có sẵn lựa chọn "Giờ", nên ứng viên
        /// nhận được một văn bản cam kết ghi sai đơn vị. So khớp không phân biệt hoa thường vì đây là
        /// cột văn bản tự do, giống mọi so sánh trạng thái khác trong tính năng này.
        /// </summary>
        private static string SalaryPeriodLabel(string? period) =>
            (period ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "year" => " / năm",
                "hour" => " / giờ",
                _ => " / tháng",
            };

        private static string Row(string label, string value) => $@"
                <tr>
                    <td style='padding: 8px 0; color: #64748b; width: 45%;'>{label}</td>
                    <td style='padding: 8px 0; color: #0f172a; font-weight: 600;'>{value}</td>
                </tr>";

        public static Content Build(
            Offer offer,
            ARI.Domain.Entities.Application application,
            JobPosting? job,
            string? candidateBaseUrl)
        {
            var position = offer.Position ?? job?.Title ?? "vị trí ứng tuyển";
            var baseUrl = string.IsNullOrWhiteSpace(candidateBaseUrl) ? string.Empty : candidateBaseUrl.TrimEnd('/');

            var subject = $"[ARISP] Thư mời nhận việc — {position}";

            var optionalRows = string.Empty;
            if (!string.IsNullOrWhiteSpace(offer.Bonus)) optionalRows += Row("Thưởng", offer.Bonus!);
            if (!string.IsNullOrWhiteSpace(offer.Benefits)) optionalRows += Row("Phúc lợi", offer.Benefits!);
            if (!string.IsNullOrWhiteSpace(offer.EmploymentType)) optionalRows += Row("Loại hợp đồng", offer.EmploymentType!);
            if (!string.IsNullOrWhiteSpace(offer.WorkLocation)) optionalRows += Row("Nơi làm việc", offer.WorkLocation!);

            // Hạn trả lời chỉ hiện khi có — không in "không thời hạn" thành một dòng trống.
            var deadlineBlock = offer.ExpiresAt is { } exp
                ? $@"<p style='margin: 20px 0 0; color: #b45309;'><strong>Vui lòng phản hồi trước {Date(exp)}.</strong></p>"
                : string.Empty;

            var actionBlock = string.IsNullOrEmpty(baseUrl)
                ? string.Empty
                : $@"
            <div style='text-align: center; margin: 28px 0;'>
                <a href='{baseUrl}/candidate/applications/{application.Id}/offer'
                   style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>
                   Xem và phản hồi thư mời
                </a>
            </div>";

            var html = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
            <h2 style='color: #0f172a; margin: 0 0 4px;'>Chúc mừng {application.CandidateName}!</h2>
            <p style='color: #475569; margin: 0 0 20px;'>Chúng tôi rất vui được mời bạn gia nhập đội ngũ ở vị trí <strong>{position}</strong>.</p>

            <table style='width: 100%; border-collapse: collapse; background: #f8fafc; border-radius: 8px; padding: 12px;'>
                {Row("Vị trí", position)}
                {Row("Mức lương", Money(offer.SalaryAmount, offer.SalaryCurrency) + SalaryPeriodLabel(offer.SalaryPeriod))}
                {Row("Ngày dự kiến đi làm", Date(offer.StartDate))}
                {optionalRows}
            </table>
            {deadlineBlock}
            {actionBlock}
            <p style='color: #475569;'>Nếu có bất kỳ điều gì bạn muốn trao đổi thêm, hãy trả lời trực tiếp thư này — chúng tôi luôn sẵn sàng.</p>
            <p style='color: #475569; margin-bottom: 0;'>Trân trọng,</p>
            <p style='color: #0f172a; font-weight: 600; margin-top: 4px;'>Đội ngũ nhân sự ARISP</p>
        </div>";

            return new Content(subject, html);
        }

        /// <summary>Nhắc ứng viên khi sắp hết hạn — tác vụ nền gửi, KHÔNG qua trình soạn thảo.</summary>
        public static Content BuildReminder(
            Offer offer, ARI.Domain.Entities.Application application, JobPosting? job, string? candidateBaseUrl)
        {
            var position = offer.Position ?? job?.Title ?? "vị trí ứng tuyển";
            var baseUrl = string.IsNullOrWhiteSpace(candidateBaseUrl) ? string.Empty : candidateBaseUrl.TrimEnd('/');

            var link = string.IsNullOrEmpty(baseUrl)
                ? string.Empty
                : $@"<p><a href='{baseUrl}/candidate/applications/{application.Id}/offer'>Xem và phản hồi thư mời</a></p>";

            return new Content(
                $"[ARISP] Nhắc phản hồi thư mời nhận việc — {position}",
                $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
            <p>Chào {application.CandidateName},</p>
            <p>Thư mời nhận việc vị trí <strong>{position}</strong> sẽ hết hạn vào <strong>{Date(offer.ExpiresAt)}</strong>.</p>
            <p>Nếu bạn cần thêm thời gian hoặc muốn trao đổi về điều kiện, chỉ cần trả lời thư này.</p>
            {link}
            <p>Trân trọng,<br/>Đội ngũ nhân sự ARISP</p>
        </div>");
        }
    }
}
