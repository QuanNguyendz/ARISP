using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.RefreshCandidateToken
{
    /// <summary>Rotation refresh token cho ứng viên: revoke token cũ, phát cặp token mới.</summary>
    public record RefreshCandidateTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

    public class RefreshCandidateTokenCommandHandler : IRequestHandler<RefreshCandidateTokenCommand, Result<AuthResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public RefreshCandidateTokenCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<Result<AuthResponse>> Handle(RefreshCandidateTokenCommand request, CancellationToken ct)
        {
            var tokenHash = TokenHashing.Sha256Base64(request.RefreshToken);

            var storedTokens = await _unitOfWork.Repository<CandidateRefreshToken>().FindAsync(rt =>
                rt.TokenHash == tokenHash
                && rt.RevokedAt == null
                && rt.ExpiresAt > DateTimeOffset.UtcNow, ct);
            var storedToken = storedTokens.FirstOrDefault();

            if (storedToken == null)
                return Result.Failure<AuthResponse>("Invalid or expired refresh token.", AuthErrorCodes.InvalidCredentials);

            // Revoke token cũ
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateRefreshToken>().Update(storedToken);
            await _unitOfWork.SaveChangesAsync();

            // Tìm candidate để sinh JWT mới
            var candidate = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(storedToken.CandidateAccountId, ct);
            if (candidate == null)
                return Result.Failure<AuthResponse>("Candidate not found.", AuthErrorCodes.InvalidCredentials);

            var newAccessToken = _tokenService.CreateCandidateToken(candidate);
            var newRefreshToken = await AuthSupport.IssueRefreshTokenForCandidateAsync(_unitOfWork, candidate.Id, ct);

            return Result.Success(new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                FullName = candidate.FullName ?? "Candidate",
                Role = AppRoles.Candidate
            });
        }
    }
}
