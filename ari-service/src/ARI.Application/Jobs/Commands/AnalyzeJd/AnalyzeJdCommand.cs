using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Jobs.Commands.AnalyzeJd
{
    /// <summary>
    /// Upload file JD (PDF/DOCX), phân tích bằng Gemini để trích xuất các trường auto-fill
    /// cho form tạo tin. File JD được lưu trữ; storageKey + metadata trả về (ADR-042).
    /// </summary>
    public record AnalyzeJdCommand(byte[] Bytes, string FileName, string Ext) : IRequest<Result<AnalyzeJdResponse>>;

    public class AnalyzeJdCommandHandler : IRequestHandler<AnalyzeJdCommand, Result<AnalyzeJdResponse>>
    {
        private readonly IDocumentParserService _documentParser;
        private readonly IFileStorageService _fileStorage;
        private readonly IGeminiProvider _geminiProvider;

        public AnalyzeJdCommandHandler(
            IDocumentParserService documentParser,
            IFileStorageService fileStorage,
            IGeminiProvider geminiProvider)
        {
            _documentParser = documentParser;
            _fileStorage = fileStorage;
            _geminiProvider = geminiProvider;
        }

        public async Task<Result<AnalyzeJdResponse>> Handle(AnalyzeJdCommand command, CancellationToken ct)
        {
            var (bytes, fileName, ext) = (command.Bytes, command.FileName, command.Ext);

            // Parse text (dùng làm fallback cho Gemini, nhất là với DOCX không gửi inline được)
            string jdText;
            try
            {
                using var stream = new MemoryStream(bytes);
                jdText = (await _documentParser.ParseDocumentAsync(stream, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch (Exception ex)
            {
                return Result.Failure<AnalyzeJdResponse>($"Không thể đọc nội dung file JD: {ex.Message}");
            }

            var contentType = ext == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            // Lưu file JD qua abstraction (Local dev / R2 prod). DB lưu storageKey.
            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(bytes, fileName, contentType, StorageFolder.Jd, ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<AnalyzeJdResponse>($"Không thể lưu file JD: {ex.Message}", CommonErrorCodes.ServerError);
            }

            // URL xem được ngay: FE mở file JD vừa tải lên trước cả khi tin được tạo. Trước đây FE nhận
            // đúng storageKey ("jd/xxx.pdf") rồi ghép với base URL → 404, nên khung xem PDF trắng trơn
            // còn DOCX rơi vào nhánh lỗi chung ("có thể do CORS").
            var viewUrl = await _fileStorage.GetUrlAsync(storageKey, ct);

            // Gọi Gemini trích xuất (PDF gửi inline, DOCX dùng fallback text)
            var pdfBytes = ext == ".pdf" ? bytes : null;
            var extraction = await _geminiProvider.ExtractJobFromJdAsync(pdfBytes, ext == ".pdf" ? "application/pdf" : null, jdText, ct);

            if (extraction.IsFailure)
            {
                // Vẫn trả file đã lưu để người dùng tạo tin thủ công, kèm cảnh báo phân tích thất bại.
                return Result.Success(new AnalyzeJdResponse
                {
                    IsValidJd = false,
                    JdFileUrl = storageKey,
                    JdFileViewUrl = viewUrl,
                    JdFileName = fileName,
                    JdFileFormat = ext.TrimStart('.'),
                    JobDescription = string.IsNullOrWhiteSpace(jdText) ? null : jdText
                });
            }

            var data = extraction.Value;
            var response = new AnalyzeJdResponse
            {
                IsValidJd = data.IsValidJd,
                JdFileUrl = storageKey,
                JdFileViewUrl = viewUrl,
                JdFileName = fileName,
                JdFileFormat = ext.TrimStart('.'),
                Title = data.Title,
                Department = data.Department,
                JobDescription = !string.IsNullOrWhiteSpace(data.JobDescription) ? data.JobDescription : (string.IsNullOrWhiteSpace(jdText) ? null : jdText),
                JobCategory = data.JobCategory,
                ExperienceLevel = data.ExperienceLevel,
                EmploymentType = data.EmploymentType,
                WorkMode = data.WorkMode,
                Location = data.Location,
                Skills = data.Skills ?? new List<string>(),
                LanguageRequirement = data.LanguageRequirement,
                SalaryMin = data.SalaryMin,
                SalaryMax = data.SalaryMax
            };

            return Result.Success(response);
        }
    }
}
