using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Storage
{
    /// <summary>
    /// Lưu file vào thư mục "uploads" trên đĩa local — dùng cho môi trường dev.
    /// storageKey có dạng "/uploads/&lt;folder&gt;/&lt;guid&gt;.ext" và được phục vụ tĩnh qua middleware
    /// UseStaticFiles(RequestPath="/uploads") trong Program.cs (middleware tự phục vụ cả thư mục con).
    /// </summary>
    public class LocalFileStorageService : IFileStorageService
    {
        private const string UrlPrefix = "/uploads/";
        private readonly string _uploadsFolder;

        public LocalFileStorageService()
        {
            _uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        }

        public async Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, StorageFolder folder, CancellationToken ct = default)
        {
            var segment = folder.ToSegment();
            var targetFolder = Path.Combine(_uploadsFolder, segment);
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            var ext = Path.GetExtension(originalFileName);
            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(targetFolder, uniqueFileName);
            await File.WriteAllBytesAsync(filePath, content, ct);

            return $"{UrlPrefix}{segment}/{uniqueFileName}";
        }

        /// <summary>
        /// storageKey → đường dẫn tuyệt đối trên đĩa. Giữ nguyên thư mục con của key mới
        /// ("/uploads/cv/x.pdf") và vẫn đọc được key cũ phẳng ("/uploads/x.pdf").
        /// Trả về <c>null</c> nếu key thoát ra ngoài thư mục uploads (chặn path traversal).
        /// </summary>
        private string? ResolvePath(string storageKey)
        {
            if (string.IsNullOrEmpty(storageKey)) return null;

            var relative = storageKey.Replace('\\', '/').TrimStart('/');
            if (relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring("uploads/".Length);
            if (string.IsNullOrEmpty(relative)) return null;

            var fullPath = Path.GetFullPath(Path.Combine(_uploadsFolder, relative));
            var rootPath = Path.GetFullPath(_uploadsFolder) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default)
        {
            // Đã là đường dẫn tương đối phục vụ tĩnh — frontend tự ghép origin backend.
            return Task.FromResult(storageKey ?? string.Empty);
        }

        public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default)
        {
            // Static middleware không set được Content-Disposition theo request; frontend dùng
            // thuộc tính download để gợi ý tải về. Trả về cùng đường dẫn xem.
            return Task.FromResult(storageKey ?? string.Empty);
        }

        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            try
            {
                var filePath = ResolvePath(storageKey);
                if (filePath != null && File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { /* best-effort */ }
            return Task.CompletedTask;
        }

        public async Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default)
        {
            var filePath = ResolvePath(storageKey);
            if (filePath == null || !File.Exists(filePath)) return null;
            return await File.ReadAllBytesAsync(filePath, ct);
        }
    }
}
