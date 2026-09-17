using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth.Commands.CompleteExternalStaffSignIn
{
    /// <summary>Token trả cho FE sau khi hoàn tất OAuth callback (đưa vào URL fragment).</summary>
    public record ExternalSignInTokens(string AccessToken, string RefreshToken, string Role);

    /// <summary>
    /// Đuôi nghiệp vụ của Google OAuth callback cho STAFF: validate domain (system_settings ưu tiên,
    /// fallback appsettings), tra cứu tài khoản pre-provisioned (KHÔNG JIT tạo mới), mint token.
    /// Phần protocol (Challenge/AuthenticateAsync/SignOut/Redirect) ở lại controller.
    /// </summary>
    public record CompleteExternalStaffSignInCommand(string Email) : IRequest<Result<ExternalSignInTokens>>;

    public class CompleteExternalStaffSignInCommandHandler
        : IRequestHandler<CompleteExternalStaffSignInCommand, Result<ExternalSignInTokens>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public CompleteExternalStaffSignInCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        public async Task<Result<ExternalSignInTokens>> Handle(CompleteExternalStaffSignInCommand request, CancellationToken ct)
        {
            var email = request.Email;

            // Danh sách miền do Super Admin cấu hình qua UI (bảng system_settings).
            //
            // ĐỌC THEO SỰ TỒN TẠI CỦA HÀNG, không theo việc hàng đó có rỗng hay không. Màn Cài đặt hệ
            // thống ghi rõ "để trống = cho phép mọi miền", nhưng trước đây ô trống lại bị coi là "chưa
            // cấu hình" và rơi về `appsettings.json` — nơi có sẵn `"fpt.edu.vn, arisp.com"`. Hệ quả:
            // Super Admin xoá trắng ô rồi lưu, giao diện báo đã mở cho mọi miền, mà tài khoản gmail
            // vẫn bị chặn ở cửa Google; cùng tài khoản đó đăng nhập bằng mật khẩu thì lại vào được,
            // nên triệu chứng trông như lỗi của Google Sign-In chứ không phải của cấu hình.
            //
            // Hàng có mặt = Super Admin ĐÃ quyết định (màn Cài đặt luôn ghi khoá này khi bấm Lưu), kể
            // cả khi họ cố ý xoá trắng. Chỉ hệ thống CHƯA TỪNG cấu hình mới dùng giá trị mồi ở
            // appsettings — thứ để dựng máy lần đầu, không phải thứ ghi đè lựa chọn của người dùng.
            var dbDomainSetting = (await _unitOfWork.Repository<SystemSetting>()
                .FindAsync(s => s.Key == "allowed_email_domains", ct)).FirstOrDefault();
            var allowed = dbDomainSetting != null
                ? dbDomainSetting.Value ?? string.Empty
                : (_configuration["Authentication:AllowedDomains"] ?? _configuration["Auth:AllowedDomains"] ?? string.Empty);
            var allowedDomains = allowed
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().TrimStart('@').ToLower())
                .Where(s => s.Length > 0)
                .ToList();
            var emailDomain = email.Split('@').ElementAtOrDefault(1)?.ToLower() ?? string.Empty;

            var isDomainAllowed = !allowedDomains.Any() || allowedDomains.Contains(emailDomain);
            if (!isDomainAllowed)
                return Result.Failure<ExternalSignInTokens>("Email không thuộc tên miền được phép đăng nhập nội bộ.", AuthErrorCodes.DomainNotAllowed);

            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == email, ct);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                // Tài khoản chưa được Super Admin cấp phát → CHẶN đăng nhập, KHÔNG tự động tạo tài khoản
                return Result.Failure<ExternalSignInTokens>("Tài khoản này chưa được cấp quyền vào hệ thống nội bộ. Hãy liên hệ quản trị viên.", AuthErrorCodes.NotProvisioned);
            }

            if (!user.IsActive || user.Role == "Pending")
                return Result.Failure<ExternalSignInTokens>("Tài khoản đang chờ quản trị viên duyệt.", AuthErrorCodes.PendingApproval);

            user.LastLoginAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            var token = _tokenService.CreateStaffToken(user);
            var refreshToken = await AuthSupport.IssueRefreshTokenForUserAsync(_unitOfWork, user.Id, ct);

            return Result.Success(new ExternalSignInTokens(token, refreshToken, user.Role));
        }
    }
}
