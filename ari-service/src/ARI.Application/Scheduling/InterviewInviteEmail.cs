using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Scheduling
{
    /// <summary>
    /// Thư mời phỏng vấn kèm lịch hẹn — nội dung DUY NHẤT gửi cho ứng viên khi nhân sự duyệt CV
    /// (vòng 1) hoặc xếp lịch vòng tiếp theo. Gom về một chỗ vì trước đây HTML nằm rải trong
    /// <see cref="StaffScheduling"/>: sửa câu chữ ở một luồng là luồng kia lệch ngay.
    ///
    /// Ba điểm nội dung phụ thuộc ngữ cảnh (không được viết cứng):
    /// 1. <b>Theo vòng</b> — vòng 1 là "qua vòng duyệt CV", vòng N là "đã hoàn thành vòng N-1".
    /// 2. <b>Theo loại vòng</b> — vòng trắc nghiệm làm trực tuyến nên KHÔNG có địa điểm và KHÔNG
    ///    nhắc phỏng vấn thử (vòng trắc nghiệm không hỗ trợ thử).
    /// 3. <b>Địa điểm</b> — lấy từ cấu hình hệ thống (single-tenant: một văn phòng), không hardcode.
    /// </summary>
    public static class InterviewInviteEmail
    {
        /// <summary>Khoá cấu hình hệ thống — Super Admin nhập một lần trong Cấu hình hệ thống.</summary>
        public const string LocationAddressKey = "interview_location_address";
        public const string LocationDirectionsKey = "interview_location_directions";
        public const string LocationMapUrlKey = "interview_location_map_url";

        public record Content(string Subject, string Html);

        public static bool IsOnlineTest(string? roundType) =>
            string.Equals((roundType ?? string.Empty).Trim(), "online_test", StringComparison.OrdinalIgnoreCase);

        public static string RoundLabel(string? roundType) => (roundType ?? string.Empty).ToLowerInvariant() switch
        {
            "screening" => "Sơ loại (Screening)",
            "technical" => "Chuyên môn (Technical)",
            "online_test" => "Trắc nghiệm (Online Test)",
            "hr" => "Phỏng vấn với HR",
            "culture_fit" => "Đánh giá mức độ phù hợp văn hoá",
            _ => string.IsNullOrWhiteSpace(roundType) ? "Phỏng vấn" : roundType!,
        };

        /// <summary>
        /// Dựng tiêu đề + thân thư. Không tự gửi: nơi gọi quyết định gửi hay không (best-effort).
        /// </summary>
        public static async Task<Content> BuildAsync(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ARI.Domain.Entities.Application app,
            JobPosting? job,
            int round,
            Guid bookingId,
            DateTimeOffset startTimeUtc,
            CancellationToken ct = default)
        {
            var jobTitle = job?.Title ?? "vị trí ứng tuyển";

            var roundConfigs = (await unitOfWork.Repository<InterviewRoundConfig>()
                    .FindAsync(r => r.JobPostingId == app.JobPostingId, ct))
                .OrderBy(r => r.RoundNumber)
                .ToList();

            var currentType = roundConfigs.FirstOrDefault(r => r.RoundNumber == round)?.RoundType;
            var previousType = roundConfigs.FirstOrDefault(r => r.RoundNumber == round - 1)?.RoundType;
            var onlineTest = IsOnlineTest(currentType);

            // Giờ hẹn hiển thị theo múi giờ VN (+7) — ứng viên và nhân sự đều ở VN.
            var local = startTimeUtc.ToOffset(TimeSpan.FromHours(7));
            var whenText = $"{local:HH:mm} - {VietnameseWeekday(local)}, ngày {local:dd/MM/yyyy} (giờ VN)";

            var baseUrl = (configuration["Frontend:CandidateBaseUrl"]
                           ?? configuration["Authentication:AdminFrontendUrl"]
                           ?? "http://localhost:3000").TrimEnd('/');
            var confirmLink = $"{baseUrl}/portal/schedule/{app.Id}?booking={bookingId}&action=confirm";
            var declineLink = $"{baseUrl}/portal/schedule/{app.Id}?booking={bookingId}&action=decline";
            var deadlineHours = int.TryParse(configuration["Scheduling:ConfirmDeadlineHours"], out var dh) && dh > 0 ? dh : 48;

            // Mở đầu thay đổi theo vòng: vòng 1 là kết quả duyệt CV, vòng sau là kết quả vòng trước đó.
            var openingHtml = round <= 1
                ? "<p style='color:#334155; font-size:15px;'>Cảm ơn bạn đã quan tâm tới cơ hội nghề nghiệp tại ARISP. "
                  + "<strong>Chúc mừng — hồ sơ của bạn đã qua vòng duyệt CV.</strong> "
                  + "Bộ phận nhân sự trân trọng mời bạn tham dự vòng tuyển chọn đầu tiên theo lịch dưới đây.</p>"
                : $"<p style='color:#334155; font-size:15px;'><strong>Chúc mừng — bạn đã hoàn thành vòng {round - 1}"
                  + (string.IsNullOrWhiteSpace(previousType) ? string.Empty : $" ({RoundLabel(previousType)})")
                  + $"</strong> và được mời tiếp vào <strong>vòng {round}</strong> của vị trí này.</p>";

            var roundHeading = onlineTest
                ? $"Bài trắc nghiệm vòng {round}"
                : $"Buổi phỏng vấn vòng {round}";

            var locationHtml = onlineTest
                ? "<tr><td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Hình thức</td>"
                  + "<td style='padding:6px 0; color:#0f172a; font-size:15px;'><strong>Làm bài trực tuyến</strong> trên Candidate Portal — không cần tới văn phòng.</td></tr>"
                : await BuildLocationRowsAsync(unitOfWork, ct);

            var processHtml = roundConfigs.Count == 0
                ? string.Empty
                : "<ol style='padding-left:20px; color:#334155; font-size:14px; line-height:1.7; margin:6px 0 0;'>"
                  + string.Join(string.Empty, roundConfigs.Select(r =>
                      $"<li style='margin:4px 0;'>Vòng {r.RoundNumber}: <strong>{RoundLabel(r.RoundType)}</strong>"
                      + (r.RoundNumber == round
                          ? " — <span style='color:#4f46e5; font-weight:bold;'>bạn đang ở vòng này</span>"
                          : string.Empty)
                      + "</li>"))
                  + "</ol>";

            // Phỏng vấn thử chỉ có ở vòng hội thoại với AI; vòng trắc nghiệm không hỗ trợ thử.
            var practiceHtml = onlineTest
                ? "<p style='color:#475569; font-size:13px;'>Bài trắc nghiệm mở trực tiếp trên Candidate Portal đúng khung giờ trên. "
                  + "Bạn chỉ có <strong>một lượt làm bài</strong>, hãy chuẩn bị đường truyền ổn định trước khi bắt đầu.</p>"
                : "<p style='color:#475569; font-size:13px;'>Buổi phỏng vấn thật diễn ra <strong>tại văn phòng</strong> — nhân sự sẽ cấp "
                  + "<strong>Mã phỏng vấn (Interview Code)</strong> cho bạn tại chỗ. Trước ngày hẹn, bạn có thể luyện tập miễn phí "
                  + "với chế độ <em>phỏng vấn thử</em> trên Candidate Portal.</p>";

            var subject = onlineTest
                ? $"[ARISP] Mời làm bài trắc nghiệm vòng {round} - vị trí {jobTitle}"
                : $"[ARISP] Thư mời phỏng vấn vòng {round} - vị trí {jobTitle}";

            var html = $@"
        <div style='font-family: Arial, sans-serif; max-width: 640px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <h2 style='color:#4f46e5; margin:0 0 16px;'>{roundHeading} - {jobTitle}</h2>
            <p style='color:#334155; font-size:15px;'>Chào <strong>{app.CandidateName}</strong>,</p>
            {openingHtml}

            <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin:18px 0; border:1px solid #e2e8f0; border-radius:10px; padding:14px 16px; background-color:#f8fafc;'>
                <tr>
                    <td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Thời gian</td>
                    <td style='padding:6px 0; color:#0f172a; font-size:15px;'><strong>{whenText}</strong></td>
                </tr>
                {locationHtml}
                <tr>
                    <td style='padding:6px 0; color:#64748b; font-size:14px;'>Vị trí</td>
                    <td style='padding:6px 0; color:#0f172a; font-size:15px;'>{jobTitle}</td>
                </tr>
            </table>

            <p style='margin:18px 0 4px; color:#0f172a; font-size:15px;'><strong>Quy trình tuyển chọn của vị trí này:</strong></p>
            {processHtml}

            <p style='margin-top:22px; color:#334155; font-size:15px;'>Vui lòng phản hồi lịch hẹn bằng một trong hai lựa chọn dưới đây:</p>
            <table role='presentation' cellpadding='0' cellspacing='0' style='margin:16px auto;'>
                <tr>
                    <td style='padding:0 8px;'>
                        <a href='{confirmLink}' style='display:inline-block; padding:12px 26px; background-color:#16a34a; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold; font-size:15px;'>&#10003; Xác nhận tham dự</a>
                    </td>
                    <td style='padding:0 8px;'>
                        <a href='{declineLink}' style='display:inline-block; padding:12px 26px; background-color:#dc2626; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold; font-size:15px;'>&#10007; Tôi bận, xin đổi lịch</a>
                    </td>
                </tr>
            </table>

            <div style='background-color:#fff7ed; border:1px solid #fed7aa; border-radius:8px; padding:12px 14px; margin:16px 0;'>
                <p style='margin:0; color:#9a3412; font-size:13px;'>
                    <strong>Lưu ý:</strong> Mỗi lịch chỉ phản hồi <strong>một lần</strong> — sau khi bấm Xác nhận hoặc Đổi lịch, bạn sẽ <strong>không thể thay đổi</strong> lựa chọn.
                    Nếu bạn <strong>không phản hồi trong vòng {deadlineHours} giờ</strong>, lịch sẽ tự động bị huỷ và nhân sự sẽ sắp xếp lại.
                    Khi báo bận, vui lòng ghi rõ lý do để nhân sự xếp khung giờ phù hợp hơn.
                </p>
            </div>

            {practiceHtml}
            <hr style='border:none; border-top:1px solid #e2e8f0; margin:22px 0;' />
            <p style='color:#334155; font-size:14px; margin:0;'>Trân trọng,</p>
            <p style='color:#334155; font-size:14px; margin:4px 0 0;'><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            return new Content(subject, html);
        }

        /// <summary>
        /// Địa điểm + chỉ dẫn lấy từ cấu hình hệ thống. Chưa cấu hình thì bỏ hẳn dòng địa điểm
        /// thay vì in địa chỉ rỗng — thư mời thiếu địa chỉ còn hơn thư mời có địa chỉ sai.
        /// </summary>
        private static async Task<string> BuildLocationRowsAsync(IUnitOfWork unitOfWork, CancellationToken ct)
        {
            var keys = new[] { LocationAddressKey, LocationDirectionsKey, LocationMapUrlKey };
            var settings = (await unitOfWork.Repository<SystemSetting>()
                    .FindAsync(s => keys.Contains(s.Key), ct))
                .ToDictionary(s => s.Key, s => s.Value ?? string.Empty);

            string Value(string key) => settings.TryGetValue(key, out var v) ? v.Trim() : string.Empty;

            var address = Value(LocationAddressKey);
            var directions = Value(LocationDirectionsKey);
            var mapUrl = Value(LocationMapUrlKey);

            if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(directions))
                return string.Empty;

            var rows = new List<string>();
            if (!string.IsNullOrWhiteSpace(address))
            {
                var mapLink = string.IsNullOrWhiteSpace(mapUrl)
                    ? string.Empty
                    : $" <a href='{mapUrl}' style='color:#4f46e5;'>(xem bản đồ)</a>";
                rows.Add("<tr><td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Địa điểm</td>"
                         + $"<td style='padding:6px 0; color:#0f172a; font-size:15px;'>{address}{mapLink}</td></tr>");
            }
            if (!string.IsNullOrWhiteSpace(directions))
            {
                rows.Add("<tr><td style='padding:6px 0; color:#64748b; font-size:14px;'>Chỉ dẫn</td>"
                         + $"<td style='padding:6px 0; color:#475569; font-size:14px;'>{directions}</td></tr>");
            }
            return string.Join(string.Empty, rows);
        }

        /// <summary>"Thứ 2".."Thứ 7"/"Chủ nhật" — culture vi-VN của .NET trả "Thứ Hai", không phải dạng số quen dùng.</summary>
        private static string VietnameseWeekday(DateTimeOffset value) => value.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            _ => "Chủ nhật",
        };
    }
}
