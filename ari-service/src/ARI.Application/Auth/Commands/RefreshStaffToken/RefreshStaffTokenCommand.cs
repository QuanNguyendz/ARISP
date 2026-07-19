using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.RefreshStaffToken
{
    /// <summary>Rotation refresh token cho staff: revoke token cũ, phát cặp token mới.</summary>
    public record RefreshStaffTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

    public class RefreshStaffTokenCommandHandler : IRequestHandler<RefreshStaffTokenCommand, Result<AuthResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public RefreshStaffTokenCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<Result<AuthResponse>> Handle(RefreshStaffTokenCommand request, CancellationToken ct)
        {
            var tokenHash = TokenHashing.Sha256Base64(request.RefreshToken);

            var storedTokens = await _unitOfWork.Repository<RefreshToken>().FindAsync(rt =>
                rt.TokenHash == tokenHash
                && rt.RevokedAt == null
                && rt.ExpiresAt > DateTimeOffset.UtcNow, ct);
            var storedToken = storedTokens.FirstOrDefault();

            if (storedToken == null)
                return Result.Failure<AuthResponse>("Invalid or expired refresh token.", AuthErrorCodes.InvalidCredentials);

            // Revoke token cũ
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RefreshToken>().Update(storedToken);
            await _unitOfWork.SaveChangesAsync();

            // Tìm user để sinh JWT mới
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(storedToken.UserId, ct);
            if (user == null)
                return Result.Failure<AuthResponse>("User not found.", AuthErrorCodes.InvalidCredentials);

            var newAccessToken = _tokenService.CreateStaffToken(user);
            var newRefreshToken = await AuthSupport.IssueRefreshTokenForUserAsync(_unitOfWork, user.Id, ct);

            return Result.Success(new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                FullName = user.FullName ?? "",
                Role = user.Role
            });
        }
    }
}
