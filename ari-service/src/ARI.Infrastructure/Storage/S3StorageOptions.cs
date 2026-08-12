namespace ARI.Infrastructure.Storage
{
    /// <summary>
    /// Cấu hình object storage S3-compatible (Cloudflare R2 / AWS S3 / MinIO).
    /// Map từ section "Storage:S3" trong appsettings + user-secrets/env.
    /// </summary>
    public class S3StorageOptions
    {
        /// <summary>Endpoint S3, vd https://&lt;account_id&gt;.r2.cloudflarestorage.com</summary>
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        /// <summary>R2 dùng "auto".</summary>
        public string Region { get; set; } = "auto";
        /// <summary>
        /// Prefix gốc tuỳ chọn cho TOÀN bucket (vd "prod" khi dùng chung bucket giữa các môi trường).
        /// Để rỗng là mặc định. KHÔNG dùng để phân loại file — loại file do <c>StorageFolder</c>
        /// quyết định (cv/, jd/, recordings/, playbooks/).
        /// </summary>
        public string KeyPrefix { get; set; } = string.Empty;
        /// <summary>Thời hạn presigned URL (phút).</summary>
        public int UrlExpiryMinutes { get; set; } = 60;
    }
}
