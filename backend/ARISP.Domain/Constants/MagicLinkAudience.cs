namespace ARISP.Domain.Constants
{
    /// <summary>
    /// Loại tài khoản sở hữu MagicLink/token khôi phục mật khẩu.
    /// Hai cổng đăng nhập tách biệt (Candidate vs Staff nội bộ) dùng giá trị riêng
    /// để token reset không lẫn lộn giữa hai bảng tài khoản.
    /// </summary>
    public static class MagicLinkAudience
    {
        public const string Candidate = "candidate";
        public const string Staff = "staff";

        /// <summary>Token xác minh email khi ứng viên đăng ký tài khoản mới.</summary>
        public const string CandidateEmailVerify = "candidate_verify";
    }
}
