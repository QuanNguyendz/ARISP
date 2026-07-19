namespace ARI.Application.Common
{
    /// <summary>Mã lỗi chung cho <see cref="Result.ErrorCode"/> — controller map về HTTP status gốc.</summary>
    public static class CommonErrorCodes
    {
        public const string NotFound = "not_found";       // → 404
        public const string Conflict = "conflict";        // → 409
        public const string Unauthorized = "unauthorized"; // → 401
        public const string Forbidden = "forbidden";       // → 403
        public const string ServerError = "server_error";  // → 500
    }
}
