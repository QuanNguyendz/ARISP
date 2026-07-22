namespace ARI.Application.Auth
{
    /// <summary>
    /// Mã lỗi machine-readable cho <see cref="Common.Result.ErrorCode"/> — controller dùng để map
    /// failure về đúng HTTP status như trước refactor (401/403/404/redirect). Message hiển thị
    /// vẫn nằm trong <c>Result.Error</c>.
    /// </summary>
    public static class AuthErrorCodes
    {
        public const string InvalidCredentials = "invalid_credentials";      // → 401
        public const string AccountDisabled = "account_disabled";            // → 401 (staff) / redirect error (candidate Google)
        public const string PasswordlessGoogle = "passwordless_google";      // → 400
        public const string SsoOnly = "sso_only";                            // → 400
        public const string EmailNotVerified = "email_not_verified";         // → 403 + code trong body
        public const string NotFound = "not_found";                          // → 404
        public const string PendingApproval = "pending_approval";            // external staff → redirect status=pending
        public const string NotProvisioned = "account_not_provisioned";      // external staff → redirect status=rejected
        public const string DomainNotAllowed = "domain_not_allowed";         // external staff → 403 Forbid
    }
}
