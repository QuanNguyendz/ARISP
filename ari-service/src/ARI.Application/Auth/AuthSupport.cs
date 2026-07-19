using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth
{
    /// <summary>
    /// Helpers dùng chung của feature Auth — logic chuyển verbatim từ private helpers
    /// của AuthController cũ (refresh token issuance, chuẩn hóa email, độ mạnh mật khẩu,
    /// email xác minh ứng viên).
    /// </summary>
    internal static class AuthSupport
    {
        public const int RefreshTokenExpiryDays = 30;

        /// <summary>
        /// Chuẩn hóa email: trim + chữ thường. Gmail/đa số provider không phân biệt hoa thường,
        /// nên chuẩn hóa để tránh tạo trùng tài khoản giữa đăng ký thủ công và đăng nhập Google.
        /// </summary>
        public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsStrongPassword(string password, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                errorMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
                return false;
            }

            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ hoa.";
                return false;
            }

            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ số.";
                return false;
            }

            const string specialCharacters = "!@#$%^&*";
            if (!password.Any(c => specialCharacters.Contains(c)))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sinh refresh token mới cho HR User, hash SHA256 rồi lưu vào bảng RefreshTokens.
        /// Trả về token gốc (chưa hash) để gửi cho client.
        /// </summary>
        public static async Task<string> IssueRefreshTokenForUserAsync(IUnitOfWork unitOfWork, Guid userId, CancellationToken ct = default)
        {
            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var tokenHash = TokenHashing.Sha256Base64(rawToken);

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await unitOfWork.Repository<RefreshToken>().AddAsync(refreshTokenEntity, ct);
            await unitOfWork.SaveChangesAsync();

            return rawToken;
        }

        /// <summary>
        /// Sinh refresh token mới cho Candidate, hash SHA256 rồi lưu vào bảng CandidateRefreshTokens.
        /// Trả về token gốc (chưa hash) để gửi cho client.
        /// </summary>
        public static async Task<string> IssueRefreshTokenForCandidateAsync(IUnitOfWork unitOfWork, Guid candidateAccountId, CancellationToken ct = default)
        {
            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var tokenHash = TokenHashing.Sha256Base64(rawToken);

            var refreshTokenEntity = new CandidateRefreshToken
            {
                Id = Guid.NewGuid(),
                CandidateAccountId = candidateAccountId,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await unitOfWork.Repository<CandidateRefreshToken>().AddAsync(refreshTokenEntity, ct);
            await unitOfWork.SaveChangesAsync();

            return rawToken;
        }

        /// <summary>
        /// Sinh token xác minh (Audience=candidate_verify, TTL 24h), lưu vào MagicLinks và gửi email kích hoạt.
        /// </summary>
        public static async Task SendCandidateVerificationEmailAsync(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IEmailQueue emailQueue,
            CandidateAccount candidate,
            CancellationToken ct = default)
        {
            var verifyToken = Guid.NewGuid().ToString("N");

            var magicLinkRecord = new MagicLink
            {
                Id = Guid.NewGuid(),
                Email = candidate.Email,
                TokenHash = verifyToken,
                Audience = MagicLinkAudience.CandidateEmailVerify,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await unitOfWork.Repository<MagicLink>().AddAsync(magicLinkRecord, ct);
            await unitOfWork.SaveChangesAsync();

            var frontendUrl = configuration["Authentication:AdminFrontendUrl"] ?? configuration["Auth:AdminFrontendUrl"] ?? "https://localhost:3000";
            var verifyLink = $"{frontendUrl}/auth/verify-email?token={verifyToken}&email={Uri.EscapeDataString(candidate.Email)}";

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                    <h2 style='color: #0056b3; text-align: center;'>Xác minh tài khoản ARISP</h2>
                    <p>Chào {candidate.FullName ?? "bạn"},</p>
                    <p>Cảm ơn bạn đã đăng ký. Vui lòng bấm nút bên dưới để xác minh email và kích hoạt tài khoản. Liên kết có hiệu lực trong 24 giờ:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{verifyLink}' style='background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; font-weight: bold; border-radius: 4px; display: inline-block;'>Xác minh email</a>
                    </div>
                    <p>Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:</p>
                    <p style='word-break: break-all; color: #666;'>{verifyLink}</p>
                    <hr style='border: none; border-top: 1px solid #eee;'/>
                    <p style='font-size: 12px; color: #999;'>Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email.</p>
                </div>";

            emailQueue.Enqueue(new EmailQueueItem(candidate.Email, "Xác minh tài khoản ARISP của bạn", emailBody));
        }
    }
}
