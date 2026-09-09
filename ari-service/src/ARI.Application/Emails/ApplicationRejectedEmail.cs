using System;
using ARI.Domain.Entities;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Thư cảm ơn khi hồ sơ bị loại. Tách khỏi <c>ApplicationService.RejectApplicationAsync</c>
    /// (nơi nó vốn là chuỗi HTML nội suy giữa thân hàm) để trình soạn thảo dựng được bản xem trước
    /// bằng CHÍNH nội dung sẽ gửi — bản xem trước lấy từ nguồn khác là bản xem trước nói dối.
    /// </summary>
    public static class ApplicationRejectedEmail
    {
        public record Content(string Subject, string Html);

        public static Content Build(
            ARI.Domain.Entities.Application application, JobPosting? job, string? portalBaseUrl = null)
        {
            var jobTitle = job?.Title ?? "Vị trí tuyển dụng";
            var baseUrl = string.IsNullOrWhiteSpace(portalBaseUrl) ? string.Empty : portalBaseUrl.TrimEnd('/');

            var subject = $"[ARISP] - Thư cảm ơn ứng tuyển vị trí {jobTitle}";

            var viewApplicationButton = string.IsNullOrEmpty(baseUrl)
                ? string.Empty
                : $@"
            <div style='text-align: center; margin: 28px 0;'>
                <a href='{baseUrl}/candidate/applications/{application.Id}' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem hồ sơ của bạn</a>
            </div>";

            var html = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
            <h3 style='color: #333;'>Chào {application.CandidateName},</h3>
            <p>Cảm ơn bạn đã quan tâm đến cơ hội nghề nghiệp tại ARISP và dành thời gian nộp hồ sơ ứng tuyển cho vị trí <strong>{jobTitle}</strong>.</p>
            <p>Chúng tôi rất ấn tượng với hồ sơ và kinh nghiệm của bạn. Tuy nhiên, sau khi xem xét kỹ lưỡng các yêu cầu hiện tại của công việc, chúng tôi rất tiếc chưa thể tiến xa hơn với bạn trong đợt tuyển dụng này.</p>
            <p>Thông tin của bạn sẽ được lưu giữ trong hệ thống cơ sở dữ liệu tài năng của chúng tôi. Nếu có các cơ hội phù hợp hơn trong tương lai, chúng tôi sẽ chủ động liên hệ lại.</p>{viewApplicationButton}
            <p>Chúc bạn luôn nhiều sức khỏe và may mắn trên con đường sự nghiệp của mình.</p>
            <br/>
            <p>Trân trọng,</p>
            <p><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            return new Content(subject, html);
        }
    }
}
