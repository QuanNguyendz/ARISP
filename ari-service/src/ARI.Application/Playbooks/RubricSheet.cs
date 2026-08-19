using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Đọc/ghi file Excel bộ tiêu chí chấm điểm (ADR-060). Cùng khuôn với import ngân hàng đề trắc
    /// nghiệm (ADR-049) để HR chỉ phải học một cách làm: tải mẫu → điền → upload → báo lỗi theo dòng.
    ///
    /// Layout: A = Mã tiêu chí, B = Tên hiển thị, C = Trọng số (%), D = Chuẩn chấm (tuỳ chọn).
    /// Dòng 1 là tiêu đề, luôn bỏ qua.
    /// </summary>
    public static class RubricSheet
    {
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public record RowError(int Row, string Message);

        /// <summary>Kết quả đọc file: tiêu chí đọc được + lỗi từng dòng.</summary>
        public record ParseResult(List<RubricCriterion> Criteria, List<RowError> Errors);

        public static ParseResult Parse(byte[] bytes)
        {
            var criteria = new List<RubricCriterion>();
            var errors = new List<RowError>();

            List<string[]> rows;
            try
            {
                rows = ReadRows(bytes);
            }
            catch (Exception ex)
            {
                errors.Add(new RowError(0, $"Không đọc được file Excel: {ex.Message}"));
                return new ParseResult(criteria, errors);
            }

            // Dòng 1 = tiêu đề.
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNo = i + 1;

                string Cell(int idx) => idx < row.Length ? (row[idx] ?? string.Empty).Trim() : string.Empty;

                var key = Cell(0);
                var name = Cell(1);
                var weightText = Cell(2);
                var description = Cell(3);

                // Dòng trống hoàn toàn → bỏ qua, không coi là lỗi (người dùng hay để dòng thừa cuối file).
                if (key.Length == 0 && name.Length == 0 && weightText.Length == 0) continue;

                if (!TryParseWeight(weightText, out var weight))
                {
                    errors.Add(new RowError(rowNo, $"Trọng số '{weightText}' không phải số hợp lệ."));
                    continue;
                }

                criteria.Add(new RubricCriterion
                {
                    Key = key.ToLowerInvariant().Replace(' ', '_'),
                    Name = name,
                    Weight = weight,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                });
            }

            return new ParseResult(criteria, errors);
        }

        /// <summary>Chấp nhận "40", "40%", "40,5" (dấu phẩy thập phân kiểu VN) và "40.5".</summary>
        private static bool TryParseWeight(string text, out decimal weight)
        {
            weight = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var cleaned = text.Replace("%", string.Empty).Replace(",", ".").Trim();
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out weight);
        }

        /// <summary>File mẫu kèm ví dụ có tổng đúng 100 — người dùng sửa đè lên là dùng được ngay.</summary>
        public static byte[] BuildTemplate(bool forCv)
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
                sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1U, Name = "Tieu chi" });

                var rows = new List<string?[]>
                {
                    new[] { "Mã tiêu chí (a-z, _)", "Tên hiển thị",
                            "Trọng số (%) — tổng phải = 100", "Chuẩn chấm (tuỳ chọn)" },
                };

                if (forCv)
                {
                    rows.Add(new[] { "experience", "Kinh nghiệm liên quan", "40",
                        "Số năm và độ liên quan của kinh nghiệm so với JD; dự án tương đương tính điểm cao." });
                    rows.Add(new[] { "hard_skills", "Kỹ năng chuyên môn", "35",
                        "Khớp với danh sách kỹ năng bắt buộc trong JD; có bằng chứng sử dụng thực tế." });
                    rows.Add(new[] { "education", "Học vấn & chứng chỉ", "15",
                        "Ngành học phù hợp, chứng chỉ liên quan." });
                    rows.Add(new[] { "cv_quality", "Chất lượng trình bày CV", "10",
                        "Rõ ràng, có số liệu kết quả, không lỗi trình bày." });
                }
                else
                {
                    rows.Add(new[] { "technical", "Chuyên môn", "40",
                        "Trả lời đúng và sâu về kỹ thuật cốt lõi của vị trí; nêu được đánh đổi." });
                    rows.Add(new[] { "problem_solving", "Giải quyết vấn đề", "25",
                        "Phân tích có cấu trúc, đặt câu hỏi làm rõ trước khi trả lời." });
                    rows.Add(new[] { "communication", "Giao tiếp", "20",
                        "Diễn đạt mạch lạc, đúng trọng tâm, không lan man." });
                    rows.Add(new[] { "culture_fit", "Phù hợp văn hoá", "15",
                        "Thái độ hợp tác, tinh thần học hỏi, khớp giá trị công ty." });
                }

                for (int i = 0; i < rows.Count; i++)
                    sheetData.Append(RowOf((uint)(i + 1), rows[i]));

                wbPart.Workbook.Save();
            }
            return mem.ToArray();
        }

        // ---------- Đọc worksheet đầu tiên (cùng cách với OnlineTestImportFeature) ----------

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

        /// <summary>
        /// Dựng một dòng có đủ <c>RowIndex</c> + <c>CellReference</c> ("A1", "B1"…). Excel tự suy được
        /// khi thiếu, nhưng bộ đọc ở trên định vị cột BẰNG CellReference — thiếu là đọc ra bảng rỗng.
        /// </summary>
        private static Row RowOf(uint rowIndex, params string?[] values)
        {
            var row = new Row { RowIndex = rowIndex };
            for (int i = 0; i < values.Length; i++)
                row.Append(TextCell($"{ColumnName(i)}{rowIndex}", values[i]));
            return row;
        }

        /// <summary>0 → "A", 25 → "Z", 26 → "AA".</summary>
        private static string ColumnName(int index)
        {
            var name = string.Empty;
            for (var i = index; i >= 0; i = i / 26 - 1)
                name = (char)('A' + i % 26) + name;
            return name;
        }

        private static Cell TextCell(string reference, string? value) => new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(value ?? string.Empty)),
        };
    }
}
