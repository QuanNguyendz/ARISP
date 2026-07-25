using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MediatR;

namespace ARI.Application.OnlineTest
{
    /// <summary>File xuất ra (bytes + tên + content-type) để controller trả về.</summary>
    public record OnlineTestExportFileDto(byte[] Content, string FileName, string ContentType);

    // ============================================================
    // GET /api/online-test/jobs/{jobId}/results/export — tải bảng điểm .xlsx (staff)
    // ============================================================

    public record ExportOnlineTestResultsQuery(Guid JobPostingId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestExportFileDto>>;

    public class ExportOnlineTestResultsQueryHandler : IRequestHandler<ExportOnlineTestResultsQuery, Result<OnlineTestExportFileDto>>
    {
        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IUnitOfWork _unitOfWork;

        public ExportOnlineTestResultsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestExportFileDto>> Handle(ExportOnlineTestResultsQuery request, CancellationToken ct)
        {
            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<OnlineTestExportFileDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestExportFileDto>("Bạn không có quyền xuất điểm của tin này.", CommonErrorCodes.Forbidden);

            var dto = await OnlineTestResultsBuilder.BuildAsync(_unitOfWork, job, ct);
            var bytes = BuildXlsx(dto);
            var fileName = $"bang-diem-trac-nghiem-{Slug(job.Title)}-{DateTime.UtcNow:yyyyMMdd}.xlsx";

            return Result.Success(new OnlineTestExportFileDto(bytes, fileName, XlsxContentType));
        }

        private static byte[] BuildXlsx(OnlineTestJobResultsDto dto)
        {
            using var mem = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(mem, SpreadsheetDocumentType.Workbook))
            {
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                var wsPart = wbPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                wsPart.Worksheet = new Worksheet(sheetData);

                var sheets = wbPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1U, Name = "Ket qua" });

                // Dòng tiêu đề tổng hợp.
                sheetData.Append(RowOf(Text($"Bảng điểm trắc nghiệm — {dto.JobTitle}")));
                sheetData.Append(RowOf(Text(
                    $"Điểm sàn: {dto.PassScore}/100 · Đã thi: {dto.SubmissionCount} · Đạt: {dto.PassedCount} · Chưa đạt: {dto.NotPassedCount} · Điểm TB: {dto.AverageScore}")));
                sheetData.Append(new Row());

                // Header cột.
                sheetData.Append(RowOf(
                    Text("STT"), Text("Họ tên"), Text("Email"), Text("Vòng"),
                    Text("Số câu đúng"), Text("Tổng câu"), Text("Điểm"), Text("Kết quả"), Text("Nộp lúc")));

                int i = 1;
                foreach (var r in dto.Rows)
                {
                    var local = r.SubmittedAt.ToOffset(TimeSpan.FromHours(7));
                    sheetData.Append(RowOf(
                        Number(i),
                        Text(r.CandidateName),
                        Text(r.CandidateEmail),
                        Number(r.RoundNumber),
                        Number(r.CorrectCount),
                        Number(r.TotalQuestions),
                        Number((double)r.Score),
                        Text(r.IsPassed ? "Đạt" : "Chưa đạt"),
                        Text(local.ToString("dd/MM/yyyy HH:mm"))));
                    i++;
                }

                wbPart.Workbook.Save();
            }
            return mem.ToArray();
        }

        private static Row RowOf(params Cell[] cells)
        {
            var row = new Row();
            row.Append(cells);
            return row;
        }

        private static Cell Text(string? value) => new()
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(value ?? string.Empty)),
        };

        private static Cell Number(double value) => new()
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
        };

        private static string Slug(string s)
        {
            var cleaned = new string((s ?? "job").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').ToArray());
            return string.Join("-", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant() is { Length: > 0 } r
                ? (r.Length > 40 ? r[..40] : r)
                : "job";
        }
    }
}
