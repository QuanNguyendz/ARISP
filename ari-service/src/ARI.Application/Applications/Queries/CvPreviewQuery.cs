using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Applications.Queries
{
    /// <summary>Bản PDF của CV, kèm tên file gợi ý cho trình duyệt.</summary>
    public sealed record CvPreviewPdf(byte[] Content, string FileName);

    /// <summary>
    /// Bản PDF dùng để XEM TRƯỚC một CV .doc/.docx (xem <see cref="IDocumentPdfConverter"/> về lý do).
    ///
    /// Trả <see cref="CommonErrorCodes.NotFound"/> khi không dựng được — đó là câu trả lời ĐÚNG chứ
    /// không phải lỗi: giao diện lùi về bộ dựng phía trình duyệt như trước. CV vốn đã là PDF cũng đi
    /// lối này, vì hỏi "cho tôi bản PDF của CV" thì file gốc chính là câu trả lời.
    /// </summary>
    public sealed record GetCvPreviewQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<CvPreviewPdf>>;

    public sealed class GetCvPreviewQueryHandler : IRequestHandler<GetCvPreviewQuery, Result<CvPreviewPdf>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IDocumentPdfConverter _converter;
        private readonly ILogger<GetCvPreviewQueryHandler> _logger;

        public GetCvPreviewQueryHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IDocumentPdfConverter converter,
            ILogger<GetCvPreviewQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _converter = converter;
            _logger = logger;
        }

        public async Task<Result<CvPreviewPdf>> Handle(GetCvPreviewQuery request, CancellationToken ct)
        {
            // Phạm vi do SERVER quyết (quy tắc 19): CV là dữ liệu nhạy cảm nhất của ứng viên, nên đi
            // qua đúng cổng mà mọi màn hồ sơ khác đang dùng.
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.ApplicationId, request.UserId, request.Role, ct);

            if (application == null)
                return Result.Failure<CvPreviewPdf>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<CvPreviewPdf>(JobAccessErrors.ApplicationForbidden, CommonErrorCodes.Forbidden);

            var key = application.CvFileUrl;
            if (string.IsNullOrWhiteSpace(key))
                return Result.Failure<CvPreviewPdf>("Hồ sơ này không có CV.", CommonErrorCodes.NotFound);

            var displayName = $"{application.CandidateName ?? "CV"}.pdf";

            // CV vốn là PDF: trả thẳng, không dựng lại thứ đã đúng sẵn.
            if (key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var original = await _fileStorage.ReadAllBytesAsync(key, ct);
                return original == null
                    ? Result.Failure<CvPreviewPdf>("Không đọc được file CV.", CommonErrorCodes.NotFound)
                    : Result.Success(new CvPreviewPdf(original, displayName));
            }

            var cacheKey = PreviewKeyOf(key);

            // Đã dựng lần trước thì dùng lại. Chuyển đổi tốn ~250MB RAM và vài giây, mà sàng lọc là
            // mở đi mở lại cùng một hồ sơ — dựng mỗi lượt xem là tự đánh sập chính mình.
            var cached = await _fileStorage.ReadAllBytesAsync(cacheKey, ct);
            if (cached is { Length: > 0 })
                return Result.Success(new CvPreviewPdf(cached, displayName));

            if (!_converter.IsAvailable)
                return Result.Failure<CvPreviewPdf>("Môi trường này chưa cài bộ chuyển đổi tài liệu.", CommonErrorCodes.NotFound);

            var source = await _fileStorage.ReadAllBytesAsync(key, ct);
            if (source == null)
                return Result.Failure<CvPreviewPdf>("Không đọc được file CV.", CommonErrorCodes.NotFound);

            var pdf = await _converter.ToPdfAsync(source, Path.GetFileName(key), ct);
            if (pdf == null)
                return Result.Failure<CvPreviewPdf>("Không dựng được bản xem trước.", CommonErrorCodes.NotFound);

            // Ghi cache là việc phụ: hỏng thì lượt này vẫn xem được, chỉ là lượt sau dựng lại.
            try { await _fileStorage.SaveAtAsync(cacheKey, pdf, "application/pdf", ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Không lưu được bản xem trước của CV {Key}.", key); }

            return Result.Success(new CvPreviewPdf(pdf, displayName));
        }

        /// <summary>
        /// Khoá của bản dựng, suy từ khoá gốc: <c>cv/abc.docx</c> → <c>cv/abc.docx.preview.pdf</c>.
        ///
        /// Giữ nguyên cả đuôi gốc chứ không thay thế: hai CV <c>abc.doc</c> và <c>abc.docx</c> mà cùng
        /// rút về <c>abc.pdf</c> thì người này xem nhầm hồ sơ của người kia.
        /// </summary>
        private static string PreviewKeyOf(string storageKey) => $"{storageKey}.preview.pdf";
    }
}
