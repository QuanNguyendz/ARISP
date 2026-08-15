using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.CompleteExternalStaffSignIn;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.CompleteExternalCandidateSignIn
{
    /// <summary>
    /// Đuôi nghiệp vụ của Google OAuth callback cho CANDIDATE: KHÔNG validate domain,
    /// JIT tạo CandidateAccount nếu chưa có (ứng viên đăng ký tự do), mint token.
    /// </summary>
    /// <param name="Picture">URL ảnh đại diện Google (claim "picture") — dùng làm ảnh ban đầu, tuỳ chọn.</param>
    public record CompleteExternalCandidateSignInCommand(string Email, string? Name, string? Picture = null)
        : IRequest<Result<ExternalSignInTokens>>;

    public class CompleteExternalCandidateSignInCommandHandler
        : IRequestHandler<CompleteExternalCandidateSignInCommand, Result<ExternalSignInTokens>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public CompleteExternalCandidateSignInCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<Result<ExternalSignInTokens>> Handle(CompleteExternalCandidateSignInCommand request, CancellationToken ct)
        {
            var email = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == email, ct);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
            {
                // JIT provisioning — ứng viên đăng nhập Google lần đầu: tạo tài khoản tự do (không cần mật khẩu)
                candidate = new CandidateAccount
                {
                    Email = email,
                    PasswordHash = string.Empty,
                    FullName = string.IsNullOrWhiteSpace(request.Name) ? email.Split('@')[0] : request.Name,
                    EmailVerified = true,
                    AvatarUrl = NormalizePicture(request.Picture),
                    LastLoginAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<CandidateAccount>().AddAsync(candidate, ct);
                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                if (!candidate.IsActive)
                    return Result.Failure<ExternalSignInTokens>("Account disabled.", AuthErrorCodes.AccountDisabled);

                candidate.LastLoginAt = DateTimeOffset.UtcNow;

                // CHỐNG CHIẾM TÀI KHOẢN TRƯỚC (pre-hijacking):
                // Kẻ xấu đăng ký form web bằng email của người khác và đặt mật khẩu của HẮN. Tài khoản
                // đó nằm im với EmailVerified=false nên hắn chưa vào được. Nhưng khi chủ nhân thật của
                // email đăng nhập bằng Google, ta tìm thấy đúng tài khoản này và đánh dấu đã xác minh —
                // tức là VÔ TÌNH XÁC MINH HỘ cho mật khẩu của kẻ xấu, từ đó hắn đăng nhập được và đọc
                // được CV, hồ sơ ứng tuyển, kết quả phỏng vấn của nạn nhân.
                //
                // Vì vậy: mật khẩu đặt trên một tài khoản CHƯA từng xác minh email là mật khẩu không ai
                // chứng minh được quyền sở hữu → xoá đi trước khi đánh dấu xác minh. Google vừa xác thực
                // người đang đứng đây mới là chủ email.
                //
                // Người dùng ngay tình đăng ký bằng mật khẩu, chưa bấm link xác minh rồi quay sang đăng
                // nhập Google cũng rơi vào nhánh này và mất mật khẩu vừa đặt. Đó là đánh đổi có chủ ý:
                // họ đang đăng nhập được và đặt lại mật khẩu ngay trong Hồ sơ → Đặt mật khẩu, còn kịch
                // bản kia là mất tài khoản.
                if (!candidate.EmailVerified)
                {
                    candidate.PasswordHash = string.Empty;
                    candidate.EmailVerified = true;
                }

                // Ảnh Google chỉ ĐIỀN VÀO CHỖ TRỐNG, không bao giờ ghi đè: ứng viên đã tự tải ảnh lên
                // thì mỗi lần đăng nhập Google sẽ đạp mất lựa chọn của họ.
                if (string.IsNullOrWhiteSpace(candidate.AvatarUrl))
                    candidate.AvatarUrl = NormalizePicture(request.Picture);

                _unitOfWork.Repository<CandidateAccount>().Update(candidate);
                await _unitOfWork.SaveChangesAsync();
            }

            var token = _tokenService.CreateCandidateToken(candidate);
            var refreshToken = await AuthSupport.IssueRefreshTokenForCandidateAsync(_unitOfWork, candidate.Id, ct);

            return Result.Success(new ExternalSignInTokens(token, refreshToken, AppRoles.Candidate));
        }

        /// <summary>
        /// Chỉ nhận URL http(s) tuyệt đối. Cột <c>AvatarUrl</c> dùng chung cho cả storageKey của ảnh tự
        /// tải lên, nên một giá trị lạ lọt vào đây sẽ bị hiểu nhầm thành khoá file khi dựng URL.
        /// </summary>
        private static string? NormalizePicture(string? picture)
        {
            if (string.IsNullOrWhiteSpace(picture)) return null;
            var trimmed = picture.Trim();
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    ? trimmed
                    : null;
        }
    }
}
