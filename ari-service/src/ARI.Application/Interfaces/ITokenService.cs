using System;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>Mint JWT access token cho staff (User) và ứng viên (CandidateAccount).</summary>
    public interface ITokenService
    {
        string CreateStaffToken(User user);
        string CreateCandidateToken(CandidateAccount candidate);

        /// <summary>
        /// Token phạm vi MỘT phiên phỏng vấn thật tại Kiosk (ADR-052): role <c>Kiosk_session</c> +
        /// claim <c>session_id</c>. Dùng cho máy Kiosk dùng chung — không đăng nhập tài khoản ứng viên.
        /// </summary>
        string CreateKioskSessionToken(Guid sessionId, Guid applicationId, int ttlHours);
    }
}
