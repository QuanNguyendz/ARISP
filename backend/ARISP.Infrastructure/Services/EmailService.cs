using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace ARISP.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            // 1. Đọc thông tin cấu hình cổng SMTP Mail từ appsettings.json
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderName = _configuration["EmailSettings:SenderName"] ?? "ARISP Recruitment System";
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var appPassword = _configuration["EmailSettings:AppPassword"] ?? ""; // Mật khẩu ứng dụng của Gmail

            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(appPassword))
            {
                throw new InvalidOperationException("Email settings are not fully configured in appsettings.json");
            }

            // 2. Thiết lập cấu trúc bức thư bằng MimeKit
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            // Thiết lập nội dung định dạng HTML để vẽ giao diện nút bấm (Button) cho đẹp
            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            // 3. Kết nối cổng SMTP MailKit để ship thư đi thực tế
            using var client = new SmtpClient();
            try
            {
                // Kết nối với giao thức STARTTLS (Cổng 587 bảo mật cao)
                await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);

                // Đăng nhập bằng tài khoản và mật khẩu ứng dụng Google
                await client.AuthenticateAsync(senderEmail, appPassword);

                // Bắn thư đi
                await client.SendAsync(emailMessage);
            }
            catch (Exception ex)
            {
                // Thêm log lỗi ở đây nếu hệ thống của bạn có cài Logger (Ví dụ: Serilog / ILogger)
                throw new Exception($"Failed to send email: {ex.Message}", ex);
            }
            finally
            {
                // Ngắt kết nối an toàn để giải phóng tài nguyên mạng mạng
                await client.DisconnectAsync(true);
            }
        }
    }
}
