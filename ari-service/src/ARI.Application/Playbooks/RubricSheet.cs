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
    /// Layout: A = Mã tiêu chí (để trống thì hệ thống tự sinh từ tên), B = Tên hiển thị, C = Trọng số (%),
    /// D = Chuẩn chấm, E–H = mức neo của bốn dải (ADR-070, tuỳ chọn),
    /// I = ý kiểm — mỗi dòng trong ô là một ý (Alt+Enter), tuỳ chọn; hậu tố <c>(x2)</c>/<c>(x3)</c> = trọng số ý (ADR-075).
    /// Riêng bộ CV (ADR-075): J = Loại (Chấm điểm / Điều kiện bắt buộc — dòng điều kiện để trống trọng số),
    /// K = Điểm tối thiểu của tiêu chí; sheet thứ hai "Cong thuc" = ngưỡng dải + ngưỡng khuyến nghị.
    /// Dòng 1 là tiêu đề, luôn bỏ qua. File cũ chỉ có A–D (hoặc A–I, một sheet) vẫn đọc được — công thức mặc định.
    /// </summary>
    public static class RubricSheet
    {
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public const string CriteriaSheetName = "Tieu chi";
        public const string PolicySheetName = "Cong thuc";

        public record RowError(int Row, string Message);

        /// <summary>
        /// Kết quả đọc file: tiêu chí đọc được + lỗi từng dòng + công thức (sheet "Cong thuc"; <c>null</c> khi file
        /// không có sheet đó — tức công thức mặc định).
        /// </summary>
        public record ParseResult(List<RubricCriterion> Criteria, List<RowError> Errors, CvScoringPolicy? Policy = null);

        /// <summary>"Kinh nghiệm .NET (x2)" → ("Kinh nghiệm .NET", 2). Số ngoài 1–3 vẫn đọc ra để bộ kiểm báo lỗi.</summary>
        private static readonly System.Text.RegularExpressions.Regex CheckWeightSuffix =
            new(@"\s*\(\s*[xX×]\s*(\d{1,2})\s*\)\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static ParseResult Parse(byte[] bytes)
        {
            var criteria = new List<RubricCriterion>();
            var errors = new List<RowError>();

            List<string[]> rows;
            List<string[]>? policyRows;
            try
            {
                rows = ReadRows(bytes, null)!;
                policyRows = ReadRows(bytes, PolicySheetName, mustExist: true);
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

                var kindText = Cell(9);
                var kind = ParseKind(kindText);
                if (kind == InvalidKind)
                {
                    errors.Add(new RowError(rowNo, $"Loại '{kindText}' không hợp lệ — dùng 'Chấm điểm' hoặc 'Điều kiện bắt buộc'."));
                    continue;
                }
                var knockout = kind == RubricCriterionKinds.Knockout;

                decimal weight = 0;
                // Điều kiện bắt buộc không mang trọng số — để trống cột C là đúng.
                if (!(knockout && string.IsNullOrWhiteSpace(weightText)) && !TryParseWeight(weightText, out weight))
                {
                    errors.Add(new RowError(rowNo, $"Trọng số '{weightText}' không phải số hợp lệ."));
                    continue;
                }

                int? minScore = null;
                var minText = Cell(10);
                if (minText.Length > 0)
                {
                    if (!int.TryParse(minText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m))
                    {
                        errors.Add(new RowError(rowNo, $"Điểm tối thiểu '{minText}' phải là số nguyên từ 1 đến 100."));
                        continue;
                    }
                    minScore = m;
                }

                var levels = new RubricLevels
                {
                    Excellent = NullIfEmpty(Cell(4)),
                    Good = NullIfEmpty(Cell(5)),
                    Fair = NullIfEmpty(Cell(6)),
                    Poor = NullIfEmpty(Cell(7)),
                };

                var checks = CvRubricEditing.NormalizeChecks(
                    Cell(8).Split('\n', '\r').Select(ParseCheckLine));

                criteria.Add(new RubricCriterion
                {
                    // Mã để trống → sinh từ tên. Người khai không phải tự nghĩ ra "hard_skills".
                    Key = key.Length == 0 && name.Length > 0
                        ? CvRubricEditing.Slugify(name)
                        : key.ToLowerInvariant().Replace(' ', '_'),
                    Name = name,
                    Weight = weight,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Levels = levels.IsEmpty ? null : levels,
                    Checks = checks,
                    Kind = kind,
                    MinScore = minScore,
                });
            }

            var policy = policyRows == null ? null : ParsePolicy(policyRows, errors);
            return new ParseResult(criteria, errors, policy);
        }

        private const string InvalidKind = "\0invalid";

        /// <summary>Trống / "Chấm điểm" → null · "Điều kiện bắt buộc" / "Bắt buộc" / "knockout" → knockout · khác → không hợp lệ.</summary>
        private static string? ParseKind(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var slug = CvRubricEditing.Slugify(text);
            return slug switch
            {
                "cham_diem" or "scored" or "tieu_chi_cham_diem" => null,
                "dieu_kien_bat_buoc" or "bat_buoc" or "knockout" or "dieu_kien" => RubricCriterionKinds.Knockout,
                _ => InvalidKind,
            };
        }

        private static RubricCheck ParseCheckLine(string line)
        {
            var m = CheckWeightSuffix.Match(line);
            if (!m.Success) return new RubricCheck { Text = line };
            return new RubricCheck
            {
                Text = line[..m.Index],
                Weight = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            };
        }

        // Sheet "Cong thuc": A = khoá máy đọc, B = giá trị, C = ý nghĩa (chỉ để người đọc).
        private const string KeyExcellentFrom = "excellent_from";
        private const string KeyGoodFrom = "good_from";
        private const string KeyFairFrom = "fair_from";
        private const string KeyStrongHireFrom = "strong_hire_from";
        private const string KeyHireFrom = "hire_from";
        private const string KeyCautionFrom = "caution_from";

        private static CvScoringPolicy ParsePolicy(List<string[]> rows, List<RowError> errors)
        {
            var policy = new CvScoringPolicy();
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var key = (row.Length > 0 ? row[0] : string.Empty)?.Trim().ToLowerInvariant() ?? string.Empty;
                var valueText = (row.Length > 1 ? row[1] : string.Empty)?.Trim() ?? string.Empty;
                if (key.Length == 0) continue;
                if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    errors.Add(new RowError(i + 1, $"Sheet '{PolicySheetName}': giá trị '{valueText}' của '{key}' phải là số nguyên."));
                    continue;
                }
                switch (key)
                {
                    case KeyExcellentFrom: policy.Bands.ExcellentFrom = value; break;
                    case KeyGoodFrom: policy.Bands.GoodFrom = value; break;
                    case KeyFairFrom: policy.Bands.FairFrom = value; break;
                    case KeyStrongHireFrom: policy.Tiers.StrongHireFrom = value; break;
                    case KeyHireFrom: policy.Tiers.HireFrom = value; break;
                    case KeyCautionFrom: policy.Tiers.CautionFrom = value; break;
                    default:
                        errors.Add(new RowError(i + 1, $"Sheet '{PolicySheetName}': không nhận ra khoá '{key}'."));
                        break;
                }
            }
            return policy;
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string?[] HeaderRow(CvScoringPolicy? policy, RubricPurpose purpose)
        {
            var b = (policy ?? CvScoringPolicy.Default).Normalized().Bands;
            string Range(string band) => b.Range(band) is { } r ? $"{r.Min:0}–{r.Max:0}" : string.Empty;
            var header = new List<string?>
            {
                "Mã tiêu chí (để trống để hệ thống tự sinh)", "Tên tiêu chí",
                purpose == RubricPurpose.Cv
                    ? "Trọng số (%) — tổng các tiêu chí chấm điểm phải = 100; điều kiện bắt buộc để trống"
                    : "Trọng số (%) — tổng phải = 100",
                "Chuẩn chấm",
                $"Mức {Range(ScoringRubric.BandExcellent)} (xuất sắc)", $"Mức {Range(ScoringRubric.BandGood)} (tốt)",
                $"Mức {Range(ScoringRubric.BandFair)} (đạt một phần)", $"Mức {Range(ScoringRubric.BandPoor)} (chưa đạt)",
                purpose == RubricPurpose.Cv
                    ? "Ý kiểm — mỗi dòng một ý (Alt+Enter); thêm (x2) hoặc (x3) cuối dòng để ý đó nặng gấp đôi / gấp ba"
                    : "Ý kiểm — mỗi dòng một ý (Alt+Enter); quyết định điểm trong dải",
            };
            if (purpose == RubricPurpose.Cv)
            {
                header.Add("Loại — Chấm điểm (để trống) / Điều kiện bắt buộc");
                header.Add("Điểm tối thiểu của tiêu chí (1–100, tuỳ chọn) — thấp hơn thì khuyến nghị 'Loại'");
            }
            return header.ToArray();
        }

        private static string?[] PolicyHeaderRow => new string?[] { "Khoá (không sửa)", "Giá trị", "Ý nghĩa" };

        private static List<string?[]> PolicyRows(CvScoringPolicy? policy)
        {
            var p = (policy ?? CvScoringPolicy.Default).Normalized();
            string N(int v) => v.ToString(CultureInfo.InvariantCulture);
            return new List<string?[]>
            {
                PolicyHeaderRow,
                new[] { KeyExcellentFrom, N(p.Bands.ExcellentFrom), "Dải Xuất sắc bắt đầu từ (tới 100)" },
                new[] { KeyGoodFrom, N(p.Bands.GoodFrom), "Dải Tốt bắt đầu từ" },
                new[] { KeyFairFrom, N(p.Bands.FairFrom), "Dải Đạt một phần bắt đầu từ (dưới mức này là Chưa đạt)" },
                new[] { KeyStrongHireFrom, N(p.Tiers.StrongHireFrom), "Khuyến nghị 'Rất phù hợp' từ điểm tổng" },
                new[] { KeyHireFrom, N(p.Tiers.HireFrom), "Khuyến nghị 'Phù hợp' từ điểm tổng" },
                new[] { KeyCautionFrom, N(p.Tiers.CautionFrom), "Khuyến nghị 'Cân nhắc' từ điểm tổng (dưới mức này là 'Chưa phù hợp')" },
            };
        }

        /// <summary>
        /// Xuất một bộ tiêu chí ra file Excel đúng bố cục đọc vào (ADR-070). Dùng cho nút "Xuất Excel" của
        /// trình soạn và làm file lưu kèm mỗi phiên bản bộ tiêu chí của tin — tải về là mở lại được.
        /// Bộ CV có thêm cột J–K và sheet công thức (ADR-075); bộ phỏng vấn giữ bố cục A–I.
        /// </summary>
        public static byte[] Build(
            IReadOnlyList<RubricCriterion> criteria, CvScoringPolicy? policy = null, RubricPurpose purpose = RubricPurpose.Cv)
        {
            var forCv = purpose == RubricPurpose.Cv;
            var rows = new List<string?[]> { HeaderRow(policy, purpose) };
            rows.AddRange(criteria.Select(c =>
            {
                var cells = new List<string?>
                {
                    c.Key, c.Name,
                    c.IsKnockout ? null : c.Weight.ToString("0.##", CultureInfo.InvariantCulture),
                    c.Description,
                    c.Levels?.Excellent, c.Levels?.Good, c.Levels?.Fair, c.Levels?.Poor,
                    c.Checks is { Count: > 0 } checks
                        ? string.Join("\n", checks.Select(x => forCv && x.EffectiveWeight != 1 ? $"{x.Text} (x{x.EffectiveWeight})" : x.Text))
                        : null,
                };
                if (forCv)
                {
                    cells.Add(c.IsKnockout ? "Điều kiện bắt buộc" : null);
                    cells.Add(c.MinScore?.ToString(CultureInfo.InvariantCulture));
                }
                return cells.ToArray();
            }));

            var sheets = new List<(string, IReadOnlyList<string?[]>)> { (CriteriaSheetName, rows) };
            if (forCv) sheets.Add((PolicySheetName, PolicyRows(policy)));
            return Write(sheets);
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
            var purpose = forCv ? RubricPurpose.Cv : RubricPurpose.Interview;
            var rows = new List<string?[]> { HeaderRow(null, purpose) };

            if (forCv)
            {
                rows.Add(new[] { "", "Kinh nghiệm liên quan", "40",
                    "Số năm và độ liên quan của kinh nghiệm so với JD; dự án tương đương tính điểm cao.",
                    "Từ 4 năm làm sản phẩm thật cùng lĩnh vực, có vai trò dẫn dắt.",
                    "2–4 năm, đúng lĩnh vực.",
                    "Dưới 2 năm hoặc lĩnh vực gần.",
                    "Chỉ có dự án học tập / thực tập ngắn.",
                    "Có ≥ 4 năm làm sản phẩm thật đúng lĩnh vực (x2)\nTừng giữ vai trò dẫn dắt kỹ thuật hoặc trưởng nhóm\nCó dự án quy mô lớn (nhiều người dùng / dữ liệu lớn)\nCó kết quả đo được (%, thời gian, số người dùng)",
                    "", "" });
                rows.Add(new[] { "", "Kỹ năng chuyên môn", "35",
                    "Khớp với danh sách kỹ năng bắt buộc trong JD; có bằng chứng sử dụng thực tế.",
                    "Đủ mọi kỹ năng bắt buộc, có kết quả đo được.",
                    "Đủ phần lớn kỹ năng bắt buộc.",
                    "Thiếu vài kỹ năng bắt buộc.",
                    "Thiếu phần lớn kỹ năng bắt buộc.",
                    "Dùng đủ mọi kỹ năng bắt buộc trong dự án thật\nCó chứng chỉ hoặc đóng góp mã nguồn mở liên quan\nMô tả được chiều sâu (tối ưu, thiết kế) chứ không chỉ liệt kê",
                    "", "50" });
                rows.Add(new[] { "", "Học vấn & chứng chỉ", "15",
                    "Ngành học phù hợp, chứng chỉ liên quan.", "", "", "", "", "", "", "" });
                rows.Add(new[] { "", "Chất lượng trình bày CV", "10",
                    "Rõ ràng, có số liệu kết quả, không lỗi trình bày.", "", "", "", "", "", "", "" });
                rows.Add(new[] { "", "Được phép làm việc hợp pháp tại Việt Nam", "",
                    "Ví dụ điều kiện bắt buộc — chỉ dùng cho yêu cầu thật sự thiết yếu; xoá dòng này nếu không cần.",
                    "", "", "", "", "", "Điều kiện bắt buộc", "" });
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

            var sheets = new List<(string, IReadOnlyList<string?[]>)> { (CriteriaSheetName, rows) };
            if (forCv) sheets.Add((PolicySheetName, PolicyRows(null)));
            return Write(sheets);
        }

        private static byte[] Write(IReadOnlyList<(string Name, IReadOnlyList<string?[]> Rows)> sheetsToWrite)
        {
            using var mem = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(mem, SpreadsheetDocumentType.Workbook))
            {
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                var sheets = wbPart.Workbook.AppendChild(new Sheets());

                uint sheetId = 1;
                foreach (var (name, rows) in sheetsToWrite)
                {
                    var wsPart = wbPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    wsPart.Worksheet = new Worksheet(sheetData);
                    sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = sheetId++, Name = name });

                    for (int i = 0; i < rows.Count; i++)
                        sheetData.Append(RowOf((uint)(i + 1), rows[i]));
                }

                wbPart.Workbook.Save();
            }
            return mem.ToArray();
        }

        // ---------- Đọc worksheet (cùng cách với OnlineTestImportFeature) ----------

        /// <param name="sheetName"><c>null</c> = sheet đầu tiên (bảng tiêu chí — file cũ chỉ có một sheet).</param>
        /// <param name="mustExist">Không có sheet tên đó thì trả <c>null</c> thay vì rơi về sheet đầu.</param>
        private static List<string[]>? ReadRows(byte[] bytes, string? sheetName, bool mustExist = false)
        {
            var rows = new List<string[]>();
            using var stream = new MemoryStream(bytes);
            using var doc = SpreadsheetDocument.Open(stream, false);

            var wbPart = doc.WorkbookPart;
            var all = wbPart?.Workbook.Sheets?.Elements<Sheet>().ToList() ?? new List<Sheet>();
            var sheet = sheetName == null
                ? all.FirstOrDefault()
                : all.FirstOrDefault(s => string.Equals(s.Name?.Value?.Trim(), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null && mustExist) return null;
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
