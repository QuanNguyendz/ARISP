using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Gửi thư điện tử cơ bản hỗ trợ giao diện HTML
        /// </summary>
        /// <param name="toEmail">Địa chỉ email người nhận</param>
        /// <param name="subject">Tiêu đề thư</param>
        /// <param name="htmlMessage">Nội dung thư định dạng HTML</param>
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);

        /// <summary>
        /// Gửi thư và trả về <c>Message-Id</c> của thư vừa gửi (null nếu gửi lỗi/không xác định).
        /// Truyền <paramref name="inReplyToMessageId"/> để thư này TRẢ LỜI vào một thư trước đó —
        /// Gmail gộp chung luồng thay vì đẻ ra thư rời. Dùng cho thư nhắc lịch: ứng viên thấy nó
        /// nằm ngay dưới thư mời phỏng vấn của đúng vòng đó.
        /// </summary>
        Task<string?> SendThreadedEmailAsync(
            string toEmail, string subject, string htmlMessage, string? inReplyToMessageId = null);
    }
}
