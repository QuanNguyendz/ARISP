using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Documents
{
    /// <summary>
    /// Chuyển DOCX/DOC sang PDF bằng LibreOffice headless (<c>soffice --convert-to pdf</c>).
    ///
    /// <b>Vì sao gọi tiến trình ngoài chứ không dùng thư viện .NET.</b> Không có thư viện .NET nào
    /// dựng được bố cục Word cho tài liệu bất kỳ — OpenXML đọc được cấu trúc nhưng không biết chữ rơi
    /// xuống đâu trên trang. Bố cục là phần khó nhất, và LibreOffice là bộ duy nhất chạy được ngoại
    /// tuyến làm đúng việc đó.
    ///
    /// <b>Chạy tuần tự (một lượt một).</b> Hai tiến trình <c>soffice</c> dùng CHUNG một thư mục hồ sơ
    /// sẽ tranh nhau và cái thứ hai thoát ngay lập tức. Mỗi lượt lại ngốn ~250MB RAM, mà VPS chỉ có
    /// 3.8GB cho sáu container — nên hàng đợi một làn vừa là đúng kỹ thuật vừa là đúng hạ tầng.
    ///
    /// <b>Không bao giờ ném lỗi.</b> Mọi kết cục xấu trả <c>null</c>; nơi gọi lùi về bản xem trước cũ.
    /// </summary>
    public sealed class LibreOfficePdfConverter : IDocumentPdfConverter
    {
        /// <summary>Đường dẫn quen thuộc theo hệ điều hành, dùng khi cấu hình không khai và PATH không có.</summary>
        private static readonly string[] WellKnownPaths =
        {
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        };

        private static readonly SemaphoreSlim Gate = new(1, 1);

        private readonly ILogger<LibreOfficePdfConverter> _logger;
        private readonly string? _sofficePath;
        private readonly int _timeoutSeconds;

        public LibreOfficePdfConverter(IConfiguration configuration, ILogger<LibreOfficePdfConverter> logger)
        {
            _logger = logger;
            _sofficePath = ResolveSoffice(configuration["Documents:SofficePath"]);
            _timeoutSeconds = int.TryParse(configuration["Documents:ConvertTimeoutSeconds"], out var t) && t > 0 ? t : 90;

            if (_sofficePath == null)
                _logger.LogInformation(
                    "Không tìm thấy LibreOffice — bản xem trước DOCX sẽ dùng bộ dựng phía trình duyệt. "
                    + "Khai đường dẫn ở `Documents:SofficePath` nếu đã cài ở chỗ khác.");
        }

        public bool IsAvailable => _sofficePath != null;

        private static string? ResolveSoffice(string? configured)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return File.Exists(configured) ? configured : null;

            return WellKnownPaths.FirstOrDefault(File.Exists);
        }

        public async Task<byte[]?> ToPdfAsync(byte[] content, string fileName, CancellationToken ct = default)
        {
            if (_sofficePath == null || content.Length == 0) return null;

            // Thư mục riêng cho MỖI lượt: `--convert-to` ghi file ra cùng tên gốc, nên hai lượt cùng
            // tên file mà chung thư mục sẽ đọc nhầm kết quả của nhau.
            var work = Path.Combine(Path.GetTempPath(), "arisp-pdf", Guid.NewGuid().ToString("N"));

            await Gate.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(work);

                var ext = Path.GetExtension(fileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".docx";
                var input = Path.Combine(work, $"in{ext}");
                await File.WriteAllBytesAsync(input, content, ct);

                // `-env:UserInstallation` trỏ hồ sơ LibreOffice vào thư mục ghi được của lượt này.
                // Không có nó, container chạy bằng user không đặc quyền sẽ không tạo nổi hồ sơ mặc
                // định trong HOME và tiến trình thoát ngay mà không báo gì ra stderr.
                var profile = new Uri(Path.Combine(work, "profile")).AbsoluteUri;
                var psi = new ProcessStartInfo(_sofficePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = work,
                };
                foreach (var arg in new[]
                         {
                             $"-env:UserInstallation={profile}",
                             "--headless", "--norestore", "--nolockcheck", "--nodefault", "--nofirststartwizard",
                             "--convert-to", "pdf", "--outdir", work, input,
                         })
                    psi.ArgumentList.Add(arg);

                using var process = Process.Start(psi);
                if (process == null) return null;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    _logger.LogWarning("LibreOffice quá {Seconds}s khi chuyển {File} — bỏ lượt này.", _timeoutSeconds, fileName);
                    return null;
                }

                var output = Path.Combine(work, "in.pdf");
                if (process.ExitCode != 0 || !File.Exists(output))
                {
                    // stderr của soffice gần như luôn rỗng kể cả khi hỏng, nên log cả mã thoát.
                    _logger.LogWarning(
                        "LibreOffice không chuyển được {File} (exit {Code}): {Error}",
                        fileName, process.ExitCode, (await process.StandardError.ReadToEndAsync()).Trim());
                    return null;
                }

                return await File.ReadAllBytesAsync(output, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi chuyển {File} sang PDF.", fileName);
                return null;
            }
            finally
            {
                Gate.Release();
                try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); } catch { /* best-effort */ }
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* đã thoát */ }
        }
    }
}
