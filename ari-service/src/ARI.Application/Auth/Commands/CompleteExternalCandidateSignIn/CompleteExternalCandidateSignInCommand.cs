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
    public record CompleteExternalCandidateSignInCommand(string Email, string? Name) : IRequest<Result<ExternalSignInTokens>>;

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
                if (!candidate.EmailVerified) candidate.EmailVerified = true;
                _unitOfWork.Repository<CandidateAccount>().Update(candidate);
                await _unitOfWork.SaveChangesAsync();
            }

            var token = _tokenService.CreateCandidateToken(candidate);
            var refreshToken = await AuthSupport.IssueRefreshTokenForCandidateAsync(_unitOfWork, candidate.Id, ct);

            return Result.Success(new ExternalSignInTokens(token, refreshToken, AppRoles.Candidate));
        }
    }
}
