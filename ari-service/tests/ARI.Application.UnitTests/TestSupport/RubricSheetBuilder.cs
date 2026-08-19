using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Dựng file <c>.xlsx</c> bộ tiêu chí cho test upload (ADR-060). Ghi bằng <c>SharedString</c> — khác
/// đường của <c>RubricSheet.BuildTemplate</c> (dùng <c>InlineString</c>) — để bộ đọc phải xử lý được
/// cả hai kiểu Excel thật ngoài đời chứ không chỉ đọc lại đúng file do chính nó sinh ra.
/// </summary>
internal static class RubricSheetBuilder
{
    public static byte[] Build(params (string Key, string Name, string Weight)[] rows)
    {
        var full = new (string, string, string, string?)[rows.Length];
        for (int i = 0; i < rows.Length; i++)
            full[i] = (rows[i].Key, rows[i].Name, rows[i].Weight, null);
        return Build(full);
    }

    public static byte[] Build(params (string Key, string Name, string Weight, string? Description)[] rows)
    {
        using var mem = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(mem, SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var sharedPart = wbPart.AddNewPart<SharedStringTablePart>();
            sharedPart.SharedStringTable = new SharedStringTable();

            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            wsPart.Worksheet = new Worksheet(sheetData);

            wbPart.Workbook.AppendChild(new Sheets())
                .Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1U, Name = "Tieu chi" });

            sheetData.Append(Row(sharedPart, 1, "Mã tiêu chí", "Tên hiển thị", "Trọng số", "Chuẩn chấm"));
            for (int i = 0; i < rows.Length; i++)
            {
                var (key, name, weight, description) = rows[i];
                sheetData.Append(Row(sharedPart, (uint)(i + 2), key, name, weight, description));
            }

            wbPart.Workbook.Save();
        }
        return mem.ToArray();
    }

    private static Row Row(SharedStringTablePart shared, uint rowIndex, params string?[] values)
    {
        var row = new Row { RowIndex = rowIndex };
        for (int i = 0; i < values.Length; i++)
        {
            row.Append(new Cell
            {
                CellReference = $"{(char)('A' + i)}{rowIndex}",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(Intern(shared, values[i] ?? string.Empty).ToString()),
            });
        }
        return row;
    }

    private static int Intern(SharedStringTablePart part, string value)
    {
        var table = part.SharedStringTable;
        int index = 0;
        foreach (var item in table.Elements<SharedStringItem>())
        {
            if (item.InnerText == value) return index;
            index++;
        }
        table.AppendChild(new SharedStringItem(new Text(value)));
        table.Save();
        return index;
    }
}
