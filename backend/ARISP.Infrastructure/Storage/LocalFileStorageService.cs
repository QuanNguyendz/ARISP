using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Storage
{
    /// <summary>
    /// Lưu file vào thư mục "uploads" trên đĩa local — dùng cho môi trường dev.
    /// storageKey có dạng "/uploads/&lt;guid&gt;.ext" và được phục vụ tĩnh qua middleware
    /// UseStaticFiles(RequestPath="/uploads") trong Program.cs.
    /// </summary>
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _uploadsFolder;

        public LocalFileStorageService()
        {
            _uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        }

        public async Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken ct = default)
        {
            if (!Directory.Exists(_uploadsFolder))
                Directory.CreateDirectory(_uploadsFolder);

            var ext = Path.GetExtension(originalFileName);
            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(_uploadsFolder, uniqueFileName);
            await File.WriteAllBytesAsync(filePath, content, ct);

            return $"/uploads/{uniqueFileName}";
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
                if (string.IsNullOrEmpty(storageKey)) return Task.CompletedTask;
                var fileName = Path.GetFileName(storageKey);
                var filePath = Path.Combine(_uploadsFolder, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { /* best-effort */ }
            return Task.CompletedTask;
        }
    }
}
