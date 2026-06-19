using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using ARISP.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ARISP.Infrastructure.Storage
{
    /// <summary>
    /// Lưu file lên object storage S3-compatible (Cloudflare R2 / AWS S3 / MinIO).
    /// File để private; hiển thị qua presigned URL có thời hạn.
    /// </summary>
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3;
        private readonly S3StorageOptions _options;
        private readonly ILogger<S3FileStorageService> _logger;

        public S3FileStorageService(IAmazonS3 s3, S3StorageOptions options, ILogger<S3FileStorageService> logger)
        {
            _s3 = s3;
            _options = options;
            _logger = logger;
        }

        public async Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(originalFileName);
            var prefix = string.IsNullOrWhiteSpace(_options.KeyPrefix) ? string.Empty : _options.KeyPrefix.Trim('/') + "/";
            var key = $"{prefix}{Guid.NewGuid()}{ext}";

            using var ms = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = ms,
                ContentType = contentType,
                DisablePayloadSigning = true // R2 yêu cầu để tránh lỗi chữ ký streaming
            };
            await _s3.PutObjectAsync(request, ct);

            return key;
        }

        public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(storageKey))
                return Task.FromResult(string.Empty);

            // URL tuyệt đối (đã từng resolve) — trả về nguyên trạng.
            if (storageKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                storageKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(storageKey);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = storageKey.TrimStart('/'),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(_options.UrlExpiryMinutes)
            };
            return Task.FromResult(_s3.GetPreSignedURL(request));
        }

        public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(storageKey))
                return Task.FromResult(string.Empty);

            // Ép tải về bằng Content-Disposition=attachment trên presigned URL.
            var safeName = string.IsNullOrWhiteSpace(downloadFileName) ? "cv" : downloadFileName.Replace("\"", string.Empty);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = storageKey.TrimStart('/'),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(_options.UrlExpiryMinutes),
                ResponseHeaderOverrides = { ContentDisposition = $"attachment; filename=\"{safeName}\"" }
            };
            return Task.FromResult(_s3.GetPreSignedURL(request));
        }

        public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(storageKey)) return;
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.Bucket,
                    Key = storageKey.TrimStart('/')
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không xoá được object {Key} trên S3 storage.", storageKey);
            }
        }
    }
}
