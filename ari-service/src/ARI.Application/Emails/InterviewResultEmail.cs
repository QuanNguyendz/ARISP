using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Thư báo kết quả một vòng phỏng vấn sau khi Hiring Manager chốt (ADR-074 — hoàn thiện offer).
    ///
    /// Ba đoạn HTML này trước đây nằm giữa thân <c>InterviewService.SubmitHrReviewAsync</c> và
    /// <c>TriggerAutoProgressionAsync</c>, gửi thẳng qua SMTP: HM không xem được mình sắp gửi gì, không sửa
    /// được một chữ, và thư không để lại dấu vết nào ở tab "Lịch sử email" — trong khi đây chính là thư
    /// quan trọng nhất ứng viên nhận được. Tách ra đây để trình soạn thảo dựng bản xem trước bằng CHÍNH
    /// nội dung sẽ gửi (quy tắc 21).
    /// </summary>
    public static class InterviewResultEmail
    {
        public record Content(string Subject, string Html);

        /// <summary>Biến thể thư — suy từ verdict + vòng, KHÔNG do client chọn.</summary>
        public static class Variants
        {
            /// <summary>Đạt vòng N, còn vòng sau — báo sẽ được xếp lịch vòng N+1.</summary>
            public const string NextRound = "next_round";

            /// <summary>Đạt vòng cuối — báo thư mời nhận việc sẽ tới.</summary>
            public const string FinalPass = "final_pass";

            /// <summary>Không đạt — thư cảm ơn.</summary>
            public const string NotPass = "not_pass";
        }

        /// <summary>
        /// Biến thể theo đúng luật đổi trạng thái hồ sơ của ADR-053: đạt &amp; còn vòng → <c>interview</c>,
        /// đạt &amp; vòng cuối → <c>pass</c>, không đạt → <c>not_pass</c>. Dùng CHUNG một hàm cho lệnh chốt và
        /// bản xem trước, nên thư không thể nói "chúc mừng qua vòng cuối" trong khi hồ sơ vẫn ở giữa phễu.
        /// </summary>
        public static string ResolveVariant(string? finalVerdict, int roundNumber, int totalRounds)
        {
            if (!string.Equals(finalVerdict, "pass", StringComparison.OrdinalIgnoreCase))
                return Variants.NotPass;
            return roundNumber >= totalRounds ? Variants.FinalPass : Variants.NextRound;
        }

        /// <summary>
        /// Tổng số vòng của tin = <c>max(InterviewRoundConfig.RoundNumber)</c>; tin chưa khai vòng nào coi như
        /// 1 vòng. "Vòng cuối" là điều kiện duy nhất để hồ sơ thành <c>pass</c> (ADR-053).
        /// </summary>
        public static async Task<int> TotalRoundsAsync(IUnitOfWork unitOfWork, Guid jobPostingId, CancellationToken ct)
        {
            var rounds = await unitOfWork.Repository<InterviewRoundConfig>()
                .QueryAsync(q => q.Where(r => r.JobPostingId == jobPostingId).Select(r => r.RoundNumber), ct);
            return rounds.Count == 0 ? 1 : Math.Max(1, rounds.Max());
        }

        public static Content Build(
            ARI.Domain.Entities.Application application, JobPosting? job, string variant, int roundNumber,
            string? portalBaseUrl)
        {
            var jobTitle = job?.Title ?? "vị trí ứng tuyển";
            var baseUrl = string.IsNullOrWhiteSpace(portalBaseUrl) ? string.Empty : portalBaseUrl.TrimEnd('/');
            var portalLink = $"{baseUrl}/candidate/applications/{application.Id}";

            return variant switch
            {
                Variants.NextRound => NextRound(application, jobTitle, roundNumber, portalLink),
                Variants.FinalPass => FinalPass(application, jobTitle, portalLink),
                _ => NotPass(application, jobTitle, portalLink),
            };
        }

        private static Content NextRound(
            ARI.Domain.Entities.Application application, string jobTitle, int roundNumber, string portalLink)
        {
            var nextRound = roundNumber + 1;
            var html = Frame("#1e3a8a, #2563eb", "THÔNG BÁO KẾT QUẢ VÒNG PHỎNG VẤN", $$"""
                <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                <p>Chúc mừng Anh/Chị đã vượt qua vòng phỏng vấn số <strong>{{roundNumber}}</strong> của vị trí <strong>{{jobTitle}}</strong>.</p>
                <p>Đội ngũ tuyển dụng ARISP trân trọng kính mời Anh/Chị tiếp tục tham gia <strong>vòng phỏng vấn số {{nextRound}}</strong>.</p>
                <p><strong>Bộ phận nhân sự sẽ xếp lịch vòng {{nextRound}}</strong> và gửi Anh/Chị một thư mời riêng kèm <strong>giờ hẹn cụ thể và địa điểm</strong>. Trong thư đó, Anh/Chị bấm xác nhận tham dự hoặc báo bận để được xếp khung giờ khác.</p>
                {{Button(portalLink, "#2563eb", "Xem tiến trình hồ sơ")}}
                <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                """);
            return new Content($"ARISP - Chúc mừng bạn đã qua vòng {roundNumber} – vị trí {jobTitle}", html);
        }

        private static Content FinalPass(
            ARI.Domain.Entities.Application application, string jobTitle, string portalLink)
        {
            var html = Frame("#059669, #10b981", "THƯ CHÚC MỪNG VƯỢT QUA VÒNG PHỎNG VẤN", $$"""
                <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                <p>Chúng tôi vô cùng vui mừng thông báo rằng Anh/Chị đã chính thức vượt qua các vòng đánh giá năng lực của vị trí tuyển dụng <strong>{{jobTitle}}</strong> tại ARISP.</p>
                <p>Đội ngũ tuyển dụng đánh giá rất cao năng lực chuyên môn, phong cách làm việc cũng như sự phù hợp của Anh/Chị với định hướng phát triển của chúng tôi.</p>
                <p>Thư mời nhận việc chính thức (Offer Letter) sẽ được gửi tới Anh/Chị qua email và hiển thị ngay trong hồ sơ ứng tuyển trên hệ thống, kèm nút xác nhận. Bộ phận Nhân sự cũng sẽ liên hệ trực tiếp để trao đổi thêm nếu Anh/Chị cần.</p>
                <p>Cảm ơn Anh/Chị đã luôn dành sự quan tâm và nỗ lực trong suốt hành trình tuyển dụng cùng ARISP.</p>
                {{Button(portalLink, "#059669", "Xem kết quả chi tiết")}}
                <p style="margin-bottom: 0;">Trân trọng,<br><strong>Trưởng Ban Tuyển Dụng ARISP</strong></p>
                """);
            return new Content($"ARISP - Chúc mừng bạn đã vượt qua vòng phỏng vấn – vị trí {jobTitle}", html);
        }

        private static Content NotPass(
            ARI.Domain.Entities.Application application, string jobTitle, string portalLink)
        {
            var html = Frame("#4b5563, #6b7280", "THƯ CẢM ƠN THAM GIA PHỎNG VẤN", $$"""
                <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                <p>Đội ngũ tuyển dụng ARISP chân thành cảm ơn Anh/Chị đã dành thời gian và tâm huyết tham gia quy trình ứng tuyển vào vị trí <strong>{{jobTitle}}</strong>.</p>
                <p>Sau khi cân nhắc kỹ lưỡng dựa trên kết quả phỏng vấn và so sánh với định hướng hiện tại của vị trí, chúng tôi rất tiếc phải thông báo rằng chưa thể đồng hành cùng Anh/Chị trong đợt tuyển dụng lần này.</p>
                <p>Hồ sơ năng lực của Anh/Chị sẽ được lưu trữ bảo mật trong cơ sở dữ liệu ứng viên tiềm năng của ARISP. Chúng tôi sẽ chủ động liên hệ ngay khi có những cơ hội nghề nghiệp mới phù hợp hơn với thế mạnh của Anh/Chị.</p>
                {{Button(portalLink, "#4b5563", "Xem thông tin hồ sơ")}}
                <p>Chúc Anh/Chị luôn dồi dào sức khỏe, may mắn và gặt hái nhiều thành công trên con đường sự nghiệp sắp tới.</p>
                <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                """);
            return new Content($"ARISP - Thư cảm ơn tham gia phỏng vấn – vị trí {jobTitle}", html);
        }

        private static string Button(string href, string color, string label) => $$"""
            <div style="text-align: center; margin: 30px 0;">
                <a href="{{href}}" style="background-color: {{color}}; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block;">{{label}}</a>
            </div>
            """;

        private static string Frame(string gradient, string heading, string body) => $$"""
            <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
                <div style="background: linear-gradient(135deg, {{gradient}}); padding: 24px; text-align: center; color: white;">
                    <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">{{heading}}</h2>
                </div>
                <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                    {{body}}
                </div>
                <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                    <p style="margin: 0;">Đây là thư điện tử từ hệ thống tuyển dụng ARISP.</p>
                </div>
            </div>
            """;
    }
}
