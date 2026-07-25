using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.Admin
{
    /// <summary>Helpers dùng chung của feature Admin — chuyển verbatim từ private helpers của AdminController cũ.</summary>
    internal static class AdminSupport
    {
        /// <summary>Sinh mật khẩu tạm thời 12 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt.</summary>
        public static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@#$%&!";
            const string all = upper + lower + digits + special;

            var password = new char[12];
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[12];
            rng.GetBytes(bytes);

            // Đảm bảo ít nhất 1 ký tự mỗi loại
            password[0] = upper[bytes[0] % upper.Length];
            password[1] = lower[bytes[1] % lower.Length];
            password[2] = digits[bytes[2] % digits.Length];
            password[3] = special[bytes[3] % special.Length];

            // Phần còn lại lấy ngẫu nhiên từ tất cả
            for (int i = 4; i < 12; i++)
                password[i] = all[bytes[i] % all.Length];

            // Xáo trộn vị trí
            var result = password.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray();
            return new string(result);
        }

        /// <summary>Ghi audit log (KHÔNG SaveChanges — caller gộp chung transaction).</summary>
        public static async Task WriteAuditAsync(
            IUnitOfWork unitOfWork, Guid? actorId, string action, string entityType, Guid? entityId, string metadata,
            CancellationToken ct = default)
        {
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Metadata = metadata,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await unitOfWork.Repository<AuditLog>().AddAsync(audit, ct);
        }

        /// <summary>Gửi email chào mừng kèm thông tin đăng nhập cho staff mới (best-effort, không fail request).</summary>
        public static async Task SendStaffWelcomeEmailAsync(IEmailService emailService, User newUser, string pw)
        {
            var roleName = (newUser.Role ?? string.Empty).ToLower() == "hr_admin" ? "HR Admin" : "Recruiter";
            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #f8fafc;'>
                    <div style='text-align: center; margin-bottom: 24px;'>
                        <h2 style='color: #1e293b; margin: 0;'>Chào mừng bạn đến với ARISP</h2>
                        <p style='color: #64748b; margin: 4px 0 0;'>AI-Powered Recruitment & Interview Support Platform</p>
                    </div>
                    <p>Xin chào <strong>{newUser.FullName}</strong>,</p>
                    <p>Tài khoản <strong>{roleName}</strong> của bạn đã được tạo thành công trên hệ thống ARISP. Dưới đây là thông tin đăng nhập:</p>
                    <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin: 20px 0;'>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b; width: 120px;'>Email:</td>
                                <td style='padding: 8px 0; font-weight: 600;'>{newUser.Email}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b;'>Mật khẩu:</td>
                                <td style='padding: 8px 0; font-weight: 600; font-family: monospace; font-size: 15px; letter-spacing: 1px;'>{pw}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b;'>Vai trò:</td>
                                <td style='padding: 8px 0; font-weight: 600;'>{roleName}</td>
                            </tr>
                        </table>
                    </div>
                    <div style='background-color: #fef3c7; border: 1px solid #f59e0b; border-radius: 6px; padding: 12px; margin: 16px 0;'>
                        <p style='margin: 0; color: #92400e; font-size: 14px;'>⚠️ Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên để đảm bảo an toàn tài khoản.</p>
                    </div>
                    <div style='text-align: center; margin: 28px 0;'>
                        <a href='http://localhost:3001/login' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Đăng nhập cổng nhân sự</a>
                    </div>
                    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'/>
                    <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Email này được gửi tự động từ hệ thống ARISP. Vui lòng không trả lời.</p>
                </div>";

            try
            {
                await emailService.SendEmailAsync(newUser.Email, $"[ARISP] Tài khoản {roleName} đã được tạo", emailBody);
            }
            catch
            {
                // Best-effort: tài khoản đã tạo thành công, lỗi gửi email không làm fail request.
            }
        }
    }
}
