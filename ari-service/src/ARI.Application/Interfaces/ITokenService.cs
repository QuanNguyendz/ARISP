using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>Mint JWT access token cho staff (User) và ứng viên (CandidateAccount).</summary>
    public interface ITokenService
    {
        string CreateStaffToken(User user);
        string CreateCandidateToken(CandidateAccount candidate);
    }
}
