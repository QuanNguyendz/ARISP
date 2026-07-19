namespace ARI.Application.Common
{
    /// <summary>Mã lỗi chung cho <see cref="Result.ErrorCode"/> — controller map về HTTP status gốc.</summary>
    public static class CommonErrorCodes
    {
        public const string NotFound = "not_found";   // → 404
        public const string Conflict = "conflict";    // → 409
    }
}
