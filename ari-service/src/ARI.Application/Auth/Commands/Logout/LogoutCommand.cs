using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.Logout
{
    /// <summary>Revoke refresh token hiện tại (tìm cả 2 bảng staff + candidate). Luôn thành công.</summary>
    public record LogoutCommand(string? RefreshToken) : IRequest<Result>;

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LogoutCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var tokenHash = TokenHashing.Sha256Base64(request.RefreshToken);

                // Thử tìm trong bảng RefreshTokens (HR User)
                var hrTokens = await _unitOfWork.Repository<RefreshToken>().FindAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null, ct);
                var hrToken = hrTokens.FirstOrDefault();
                if (hrToken != null)
                {
                    hrToken.RevokedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<RefreshToken>().Update(hrToken);
                }

                // Thử tìm trong bảng CandidateRefreshTokens
                var candidateTokens = await _unitOfWork.Repository<CandidateRefreshToken>().FindAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null, ct);
                var candidateToken = candidateTokens.FirstOrDefault();
                if (candidateToken != null)
                {
                    candidateToken.RevokedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<CandidateRefreshToken>().Update(candidateToken);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            return Result.Success();
        }
    }
}
