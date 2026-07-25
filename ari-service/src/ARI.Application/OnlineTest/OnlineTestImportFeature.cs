using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MediatR;

namespace ARI.Application.OnlineTest
{
    // ============================================================
    // Import ngân hàng câu hỏi từ file Excel (.xlsx) + tải file mẫu (staff)
    //   POST /api/online-test/jobs/{jobId}/questions/import
    //   GET  /api/online-test/jobs/{jobId}/questions/template
    // ------------------------------------------------------------
    // Layout cột (dòng đầu là tiêu đề, tự bỏ qua):
    //   A: Câu hỏi | B: Loại (single/multiple) | C..H: Phương án A..F | I: Đáp án đúng (VD "A" hoặc "A,C")
    // ============================================================

    /// <summary>Một dòng lỗi khi import (số dòng trong file + lý do).</summary>
    public record OnlineTestImportRowError(int Row, string Message);

    /// <summary>Kết quả import: số câu thêm được, số dòng lỗi, chi tiết lỗi.</summary>
    public record OnlineTestImportResultDto(int Imported, int Failed, List<OnlineTestImportRowError> Errors);

    public record ImportOnlineTestQuestionsCommand(
        Guid JobPostingId, byte[] FileBytes, string FileName, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestImportResultDto>>;

    public class ImportOnlineTestQuestionsCommandHandler
        : IRequestHandler<ImportOnlineTestQuestionsCommand, Result<OnlineTestImportResultDto>>
    {
        // Vị trí cột (0-based) theo layout file mẫu.
        private const int ColQuestion = 0;
        private const int ColType = 1;
        private const int ColFirstOption = 2; // C..H
        private const int OptionColumns = 6;  // tối đa 6 phương án
        private const int ColCorrect = 8;     // I

        private readonly IUnitOfWork _unitOfWork;

        public ImportOnlineTestQuestionsCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestImportResultDto>> Handle(ImportOnlineTestQuestionsCommand command, CancellationToken ct)
        {
            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, command.JobPostingId, command.UserId, command.Role, ct);
            if (job == null) return Result.Failure<OnlineTestImportResultDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestImportResultDto>("Bạn không có quyền nhập câu hỏi cho tin này.", CommonErrorCodes.Forbidden);

            List<string[]> rows;
            try
            {
                rows = ReadRows(command.FileBytes);
            }
            catch (Exception)
            {
                return Result.Failure<OnlineTestImportResultDto>("Không đọc được file Excel. Hãy dùng đúng định dạng .xlsx theo file mẫu.");
            }

            var errors = new List<OnlineTestImportRowError>();
            var toAdd = new List<OnlineTestQuestion>();
            int rowNumber = 0; // khớp số dòng Excel: dòng 1 là tiêu đề

            foreach (var cells in rows)
            {
                rowNumber++;

                // Bỏ dòng tiêu đề (nhận diện mềm) và dòng trống hoàn toàn.
                if (rowNumber == 1 && LooksLikeHeader(cells)) continue;
                if (cells.All(string.IsNullOrWhiteSpace)) continue;

                var parsed = BuildRequest(cells, out var rowError);
                if (rowError != null)
                {
                    errors.Add(new OnlineTestImportRowError(rowNumber, rowError));
                    continue;
                }

                var validation = CreateOnlineTestQuestionCommandHandler.ValidateQuestion(parsed!);
                if (validation != null)
                {
                    errors.Add(new OnlineTestImportRowError(rowNumber, validation));
                    continue;
                }

                var correct = CreateOnlineTestQuestionCommandHandler.NormalizeCorrect(parsed!);
                toAdd.Add(new OnlineTestQuestion
                {
                    JobPostingId = command.JobPostingId,
                    QuestionText = parsed!.QuestionText.Trim(),
                    Options = OnlineTestSupport.SerializeOptions(parsed.Options),
                    QuestionType = CreateOnlineTestQuestionCommandHandler.NormalizeType(parsed.QuestionType),
                    CorrectOptions = OnlineTestSupport.SerializeInts(correct),
                    CorrectOption = correct.First(),
                });
            }

            if (toAdd.Count > 0)
            {
                foreach (var q in toAdd)
                    await _unitOfWork.Repository<OnlineTestQuestion>().AddAsync(q, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success(new OnlineTestImportResultDto(toAdd.Count, errors.Count, errors));
        }

        /// <summary>Dựng request từ 1 dòng file. Trả về null + lý do nếu dòng không dùng được.</summary>
        private static UpsertOnlineTestQuestionRequest? BuildRequest(string[] cells, out string? error)
        {
            error = null;
            var question = Cell(cells, ColQuestion).Trim();
            if (string.IsNullOrWhiteSpace(question))
            {
                error = "Thiếu nội dung câu hỏi.";
                return null;
            }

            // Đọc 6 cột phương án theo đúng vị trí (A..F), nén rỗng + remap chỉ số đáp án đúng.
            var rawOptions = new string[OptionColumns];
            for (int i = 0; i < OptionColumns; i++)
                rawOptions[i] = Cell(cells, ColFirstOption + i).Trim();

            var keptOptions = new List<string>();
            var remap = new int[OptionColumns];
            for (int i = 0; i < OptionColumns; i++)
            {
                if (rawOptions[i].Length > 0)
                {
                    remap[i] = keptOptions.Count;
                    keptOptions.Add(rawOptions[i]);
                }
                else remap[i] = -1;
            }

            var correctCols = ParseCorrect(Cell(cells, ColCorrect));
            if (correctCols.Count == 0)
            {
                error = "Thiếu đáp án đúng (ghi ví dụ \"A\" hoặc \"A,C\").";
                return null;
            }

            var correctNew = new List<int>();
            foreach (var c in correctCols)
            {
                if (c < 0 || c >= OptionColumns || remap[c] < 0)
                {
                    error = "Đáp án đúng trỏ tới phương án đang để trống.";
                    return null;
                }
                correctNew.Add(remap[c]);
            }

            var type = ParseType(Cell(cells, ColType), correctNew.Count);
            return new UpsertOnlineTestQuestionRequest
            {
                QuestionText = question,
                Options = keptOptions,
                QuestionType = type,
                CorrectOptions = correctNew.Distinct().ToList(),
            };
        }

        private static bool LooksLikeHeader(string[] cells)
        {
            var first = Cell(cells, ColQuestion).Trim().ToLowerInvariant();
            return first is "câu hỏi" or "question" or "nội dung câu hỏi" or "noi dung cau hoi";
        }

        /// <summary>Chuyển "A", "A,C", "A; C", "1|3"… thành danh sách chỉ số 0-based.</summary>
        private static List<int> ParseCorrect(string raw)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            foreach (var tok in raw.Split(new[] { ',', ';', '/', '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = tok.Trim();
                if (t.Length == 0) continue;
                char c = char.ToUpperInvariant(t[0]);
                if (c >= 'A' && c <= 'Z') result.Add(c - 'A');
                else if (int.TryParse(t, out var n) && n >= 1) result.Add(n - 1); // ký hiệu số 1-based
            }
            return result.Distinct().ToList();
        }

        private static string ParseType(string raw, int correctCount)
        {
            var t = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (t is "multiple" or "multi" or "m" or "nhiều" or "nhiều đáp án" or "nhieu" or "nhieu dap an")
                return "multiple";
            if (t is "single" or "s" or "1" or "một" or "một đáp án" or "mot" or "mot dap an")
                return "single";
            // Không ghi rõ → suy ra từ số đáp án đúng.
            return correctCount > 1 ? "multiple" : "single";
        }

        private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] ?? string.Empty : string.Empty;

        // ---------- Đọc file .xlsx (worksheet đầu tiên) ----------

        private static List<string[]> ReadRows(byte[] bytes)
        {
            var rows = new List<string[]>();
            using var stream = new MemoryStream(bytes);
            using var doc = SpreadsheetDocument.Open(stream, false);

            var wbPart = doc.WorkbookPart;
            var sheet = wbPart?.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault();
            if (wbPart == null || sheet?.Id?.Value == null) return rows;

            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!.Value!);
            var sharedStrings = wbPart.SharedStringTablePart?.SharedStringTable
                ?.Elements<SharedStringItem>().ToList();

            var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>();
            if (sheetData == null) return rows;

            foreach (var row in sheetData.Elements<Row>())
            {
                var map = new Dictionary<int, string>();
                int maxCol = -1;
                foreach (var cell in row.Elements<Cell>())
                {
                    int col = ColumnIndex(cell.CellReference?.Value);
                    if (col < 0) continue;
                    map[col] = CellText(cell, sharedStrings);
                    if (col > maxCol) maxCol = col;
                }

                var arr = new string[maxCol + 1];
                for (int i = 0; i <= maxCol; i++)
                    arr[i] = map.TryGetValue(i, out var v) ? v : string.Empty;
                rows.Add(arr);
            }

            return rows;
        }

        private static string CellText(Cell cell, List<SharedStringItem>? sharedStrings)
        {
            if (cell.DataType?.Value == CellValues.SharedString)
            {
                if (int.TryParse(cell.CellValue?.InnerText, out var idx) &&
                    sharedStrings != null && idx >= 0 && idx < sharedStrings.Count)
                    return sharedStrings[idx].InnerText;
                return string.Empty;
            }
            if (cell.DataType?.Value == CellValues.InlineString)
                return cell.InlineString?.Text?.Text ?? cell.InnerText;

            return cell.CellValue?.InnerText ?? string.Empty;
        }

        /// <summary>"C5" → 2 (0-based). Trả -1 nếu không có phần chữ.</summary>
        private static int ColumnIndex(string? cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int col = 0, letters = 0;
            foreach (var ch in cellRef)
            {
                if (char.IsLetter(ch))
                {
                    col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                    letters++;
                }
                else break;
            }
            return letters == 0 ? -1 : col - 1;
        }
    }

    // ============================================================
    // GET /api/online-test/jobs/{jobId}/questions/template — tải file mẫu .xlsx
    // ============================================================

    public record GetOnlineTestImportTemplateQuery(Guid JobPostingId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestExportFileDto>>;

    public class GetOnlineTestImportTemplateQueryHandler
        : IRequestHandler<GetOnlineTestImportTemplateQuery, Result<OnlineTestExportFileDto>>
    {
        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IUnitOfWork _unitOfWork;

        public GetOnlineTestImportTemplateQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestExportFileDto>> Handle(GetOnlineTestImportTemplateQuery request, CancellationToken ct)
        {
            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<OnlineTestExportFileDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestExportFileDto>("Bạn không có quyền tải file mẫu của tin này.", CommonErrorCodes.Forbidden);

            var bytes = BuildTemplate();
            return Result.Success(new OnlineTestExportFileDto(bytes, "mau-ngan-hang-cau-hoi-trac-nghiem.xlsx", XlsxContentType));
        }

        private static byte[] BuildTemplate()
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
                sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1U, Name = "Cau hoi" });

                // Dòng tiêu đề (BẮT BUỘC giữ nguyên khi điền).
                sheetData.Append(RowOf(
                    Text("Câu hỏi"), Text("Loại (single/multiple)"),
                    Text("Phương án A"), Text("Phương án B"), Text("Phương án C"),
                    Text("Phương án D"), Text("Phương án E"), Text("Phương án F"),
                    Text("Đáp án đúng (VD: A hoặc A,C)")));

                // Ví dụ 1 đáp án.
                sheetData.Append(RowOf(
                    Text("HTTP status 404 nghĩa là gì?"), Text("single"),
                    Text("Thành công"), Text("Không tìm thấy"), Text("Lỗi máy chủ"),
                    Text("Chuyển hướng"), Text(""), Text(""), Text("B")));

                // Ví dụ nhiều đáp án.
                sheetData.Append(RowOf(
                    Text("Đâu là ngôn ngữ backend?"), Text("multiple"),
                    Text("C#"), Text("HTML"), Text("Python"),
                    Text("CSS"), Text(""), Text(""), Text("A, C")));

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
    }
}
