using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.OnlineTest;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using ARI.Application.Common;

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

        /// <summary>
        /// Vòng ứng viên tham gia TỪ NHÀ, không phải tới văn phòng: bài trắc nghiệm và vòng sơ loại.
        ///
        /// Chỉ nói về ĐỊA ĐIỂM. Vòng sơ loại vẫn là buổi hội thoại với AI, vẫn có Hiring Manager cho
        /// vào phòng và vẫn cần Mã phỏng vấn — chỉ khác là ứng viên ngồi ở nhà. Vòng chuyên môn giữ
        /// nguyên: tới văn phòng.
        /// </summary>
        public static bool IsRemoteRound(string? roundType) =>
            IsOnlineTest(roundType)
            || string.Equals((roundType ?? string.Empty).Trim(), "screening", StringComparison.OrdinalIgnoreCase);

        // ---- Câu chữ theo LOẠI vòng --------------------------------------------------------------
        // Vòng trắc nghiệm là một BÀI THI, không phải buổi phỏng vấn: gọi nó là "phỏng vấn" trong
        // chuông, thư nhắc hay Portal là nói sai thứ ứng viên sắp làm. Gom về đây để mọi nơi gửi cho
        // ứng viên gọi cùng một tên.

        /// <summary>"lịch làm bài trắc nghiệm" | "lịch phỏng vấn" — dùng GIỮA câu.</summary>
        public static string AppointmentNoun(string? roundType) =>
            IsOnlineTest(roundType) ? "lịch làm bài trắc nghiệm" : "lịch phỏng vấn";

        /// <summary>"Bài trắc nghiệm" | "Buổi phỏng vấn" — dùng ĐẦU câu.</summary>
        public static string SessionNoun(string? roundType) =>
            IsOnlineTest(roundType) ? "Bài trắc nghiệm" : "Buổi phỏng vấn";

        /// <summary>
        /// Hậu quả của việc không phản hồi và không tham dự. Hai loại vòng khác hẳn nhau: vòng phỏng
        /// vấn thì hồ sơ dừng lại (ADR-059); vòng trắc nghiệm thì bài HẾT HẠN và hệ thống nộp thay —
        /// hồ sơ vẫn ở vòng đó, Recruiter quyết định (xem OnlineTestExpiry).
        /// </summary>
        public static string NoShowConsequenceHtml(string? roundType) => IsOnlineTest(roundType)
            ? "Bài thi <strong>đóng đúng giờ kết thúc</strong> ở trên — vào muộn thì thời gian làm bài ngắn lại tương ứng. "
              + "Nếu bạn <strong>không vào làm bài</strong> trước giờ đóng, bài thi sẽ <strong>hết hạn và được hệ thống tự động nộp</strong>."
            : "Nếu bạn <strong>không phản hồi và không tham dự</strong> buổi phỏng vấn trên, hồ sơ của bạn sẽ <strong>dừng lại ở vòng này</strong>.";

        /// <summary>
        /// Dòng "Thời gian" của thư. Vòng trắc nghiệm in CẢ khung giờ (mở – đóng, ADR-072): bài thi đóng
        /// cứng lúc giờ hẹn + thời lượng, nên chỉ in giờ mở là giấu mất mốc quan trọng nhất với người
        /// định vào muộn. Vòng phỏng vấn chỉ cần giờ hẹn.
        /// </summary>
        public static string WhenText(DateTimeOffset startTimeUtc, InterviewRoundConfig? round)
        {
            // Giờ hẹn hiển thị theo múi giờ VN (+7) — ứng viên và nhân sự đều ở VN.
            var local = startTimeUtc.ToOffset(TimeSpan.FromHours(7));
            var day = $"{VietnameseWeekday(local)}, ngày {local:dd/MM/yyyy} (giờ VN)";
            if (!OnlineTestWindow.IsTestRound(round)) return $"{local:HH:mm} - {day}";

            var closes = OnlineTestWindow.ClosesAt(startTimeUtc, OnlineTestWindow.DurationOf(round))
                .ToOffset(TimeSpan.FromHours(7));
            return $"{local:HH:mm} – {closes:HH:mm} - {day}";
        }

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

            var currentRound = roundConfigs.FirstOrDefault(r => r.RoundNumber == round);
            var currentType = currentRound?.RoundType;
            var previousType = roundConfigs.FirstOrDefault(r => r.RoundNumber == round - 1)?.RoundType;
            var onlineTest = IsOnlineTest(currentType);
            var remote = IsRemoteRound(currentType);

            var whenText = WhenText(startTimeUtc, currentRound);

            var baseUrl = FrontendUrls.Candidate(configuration);
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
                ? "<tr><td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Thời lượng</td>"
                  + $"<td style='padding:6px 0; color:#0f172a; font-size:15px;'><strong>{OnlineTestWindow.DurationOf(currentRound)} phút</strong> — "
                  + "vào lúc nào trong khung giờ cũng được, nhưng đồng hồ đếm tới giờ đóng bài: vào muộn thì còn ít thời gian hơn.</td></tr>"
                  + "<tr><td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Hình thức</td>"
                  + "<td style='padding:6px 0; color:#0f172a; font-size:15px;'><strong>Làm bài trực tuyến</strong> trên Candidate Portal — không cần tới văn phòng.</td></tr>"
                : remote
                    ? "<tr><td style='padding:6px 0; color:#64748b; font-size:14px; width:110px;'>Hình thức</td>"
                      + "<td style='padding:6px 0; color:#0f172a; font-size:15px;'><strong>Phỏng vấn trực tuyến</strong> — bạn tham gia từ nhà, không cần tới văn phòng.</td></tr>"
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
                : remote
                    ? "<p style='color:#475569; font-size:13px;'>Buổi phỏng vấn diễn ra <strong>trực tuyến</strong> — bạn tham gia tại nhà, "
                      + "nhân sự sẽ gửi <strong>Mã phỏng vấn (Interview Code)</strong> trước giờ hẹn. Hãy chuẩn bị "
                      + "<strong>micro, camera và đường truyền ổn định</strong>. Trước ngày hẹn, bạn có thể luyện tập miễn phí "
                      + "với chế độ <em>phỏng vấn thử</em> trên Candidate Portal.</p>"
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
                        <a href='{declineLink}' style='display:inline-block; padding:12px 26px; background-color:#dc2626; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold; font-size:15px;'>&#10007; Từ chối tham dự</a>
                    </td>
                </tr>
            </table>

            <div style='background-color:#fff7ed; border:1px solid #fed7aa; border-radius:8px; padding:12px 14px; margin:16px 0;'>
                <p style='margin:0; color:#9a3412; font-size:13px;'>
                    <strong>Lưu ý:</strong> Mỗi lịch chỉ phản hồi <strong>một lần</strong> — sau khi bấm Xác nhận hoặc Từ chối, bạn sẽ <strong>không thể thay đổi</strong> lựa chọn.
                    {NoShowConsequenceHtml(currentType)}
                    Bạn <strong>vẫn muốn tham gia</strong> nhưng bận đúng giờ này? Hãy <strong>liên hệ trực tiếp bộ phận nhân sự</strong> để thống nhất một khung giờ khác — hệ thống không tự xếp lại lịch.
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
        /// Thư NHẮC ứng viên chưa phản hồi lịch. Gửi dưới dạng trả lời thư mời (cùng luồng) nên chỉ
        /// cần nhắc gọn: giờ hẹn, hạn chót là chính buổi hẹn, và hậu quả nếu im lặng.
        /// </summary>
        public static async Task<Content> BuildReminderAsync(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ARI.Domain.Entities.Application app,
            JobPosting? job,
            int round,
            Guid bookingId,
            DateTimeOffset startTimeUtc,
            CancellationToken ct = default)
        {
            var invite = await BuildAsync(unitOfWork, configuration, app, job, round, bookingId, startTimeUtc, ct);
            var roundConfig = (await unitOfWork.Repository<InterviewRoundConfig>()
                    .FindAsync(r => r.JobPostingId == app.JobPostingId && r.RoundNumber == round, ct))
                .FirstOrDefault();
            var roundType = roundConfig?.RoundType;

            var whenText = WhenText(startTimeUtc, roundConfig);
            var hoursLeft = Math.Max(1, (int)Math.Round((startTimeUtc - DateTimeOffset.UtcNow).TotalHours));

            var baseUrl = FrontendUrls.Candidate(configuration);
            var confirmLink = $"{baseUrl}/portal/schedule/{app.Id}?booking={bookingId}&action=confirm";
            var declineLink = $"{baseUrl}/portal/schedule/{app.Id}?booking={bookingId}&action=decline";

            var html = $@"
        <div style='font-family: Arial, sans-serif; max-width: 640px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <p style='color:#334155; font-size:15px;'>Chào <strong>{app.CandidateName}</strong>,</p>
            <p style='color:#334155; font-size:15px;'>Chúng tôi <strong>chưa nhận được phản hồi</strong> của bạn cho {AppointmentNoun(roundType)} <strong>vòng {round}</strong> dưới đây.</p>
            <p style='text-align:center; font-size:18px; font-weight:bold; color:#4f46e5; margin:18px 0;'>{whenText}</p>
            <p style='color:#334155; font-size:15px;'>{(IsOnlineTest(roundType) ? "Bài trắc nghiệm mở" : "Buổi phỏng vấn diễn ra")} sau khoảng <strong>{hoursLeft} giờ</strong> nữa. Vui lòng chọn một trong hai:</p>
            <table role='presentation' cellpadding='0' cellspacing='0' style='margin:16px auto;'>
                <tr>
                    <td style='padding:0 8px;'>
                        <a href='{confirmLink}' style='display:inline-block; padding:12px 26px; background-color:#16a34a; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold; font-size:15px;'>&#10003; Xác nhận tham dự</a>
                    </td>
                    <td style='padding:0 8px;'>
                        <a href='{declineLink}' style='display:inline-block; padding:12px 26px; background-color:#dc2626; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold; font-size:15px;'>&#10007; Từ chối tham dự</a>
                    </td>
                </tr>
            </table>
            <div style='background-color:#fff7ed; border:1px solid #fed7aa; border-radius:8px; padding:12px 14px; margin:16px 0;'>
                <p style='margin:0; color:#9a3412; font-size:13px;'>
                    {NoShowConsequenceHtml(roundType)}
                    Vẫn muốn tham gia nhưng bận đúng giờ này? Hãy liên hệ trực tiếp bộ phận nhân sự để thống nhất lịch khác.
                </p>
            </div>
            <hr style='border:none; border-top:1px solid #e2e8f0; margin:22px 0;' />
            <p style='color:#334155; font-size:14px; margin:0;'>Trân trọng,</p>
            <p style='color:#334155; font-size:14px; margin:4px 0 0;'><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            // Giữ nguyên tiêu đề gốc + tiền tố Re: — client thư gộp luồng theo References, nhưng
            // tiêu đề trùng giúp cả những client chỉ gom theo tiêu đề.
            return new Content($"Re: {invite.Subject}", html);
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
