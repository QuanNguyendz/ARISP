using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
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
    }
}
