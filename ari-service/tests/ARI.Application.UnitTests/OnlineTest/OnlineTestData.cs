using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ARI.Domain.Entities;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>Factory dựng entity Online Test cho test — serialize JSON đúng format mà handler đọc/chấm.</summary>
internal static class OnlineTestData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static JobPosting Job(int passScore = 70, int perTest = 50, Guid? owner = null) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = "Backend Developer",
        OnlineTestPassScore = passScore,
        OnlineTestQuestionsPerTest = perTest,
        OnlineTestDurationMinutes = 30,
    };

    public static ARI.Domain.Entities.Application Application(
        Guid jobId, Guid? accountId, string status = "screening", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = "Nguyen Van A",
        Status = status,
    };

    /// <summary>Một câu hỏi với tập đáp án đúng cho trước (0-based). optionCount phương án giả.</summary>
    public static OnlineTestQuestion Question(Guid jobId, string type, int[] correct, int optionCount = 4)
    {
        var options = Enumerable.Range(0, optionCount).Select(i => $"Option {(char)('A' + i)}").ToList();
        return new OnlineTestQuestion
        {
            JobPostingId = jobId,
            QuestionText = "Câu hỏi mẫu?",
            Options = JsonSerializer.Serialize(options, Json),
            QuestionType = type,
            CorrectOptions = JsonSerializer.Serialize(correct, Json),
            CorrectOption = correct.Length > 0 ? correct[0] : 0,
        };
    }

    /// <summary>Câu hỏi 1 đáp án đúng.</summary>
    public static OnlineTestQuestion Single(Guid jobId, int correct, int optionCount = 4)
        => Question(jobId, "single", new[] { correct }, optionCount);

    /// <summary>Một lượt nộp bài đã chấm (điểm/đậu-rớt do test tự set để phản ánh đúng dữ liệu lưu).</summary>
    public static OnlineTestSubmission Submission(
        Guid appId, decimal score, bool passed, int round = 1,
        int correct = 0, int total = 0, int tabSwitch = 0, DateTimeOffset? at = null) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        Score = score,
        IsPassed = passed,
        CorrectCount = correct,
        TotalQuestions = total,
        TabSwitchCount = tabSwitch,
        CreatedAt = at ?? DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Dựng file .xlsx thật trong bộ nhớ để test import (mỗi <c>string[]</c> là 1 dòng, phần tử = 1 ô A,B,C…).
    /// Ô có <c>CellReference</c> đầy đủ ("A1","B1"…) — bắt buộc để <c>ReadRows</c> nhận diện cột;
    /// ô rỗng được bỏ qua (thưa) đúng như Excel thật, dòng không phần tử → dòng trống.
    /// </summary>
    public static byte[] BuildXlsx(params string[][] rows)
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

            uint rowIndex = 1;
            foreach (var cells in rows)
            {
                var row = new Row { RowIndex = rowIndex };
                for (int c = 0; c < cells.Length; c++)
                {
                    var value = cells[c];
                    if (string.IsNullOrEmpty(value)) continue; // ô rỗng: thưa như Excel
                    row.Append(new Cell
                    {
                        CellReference = $"{ColumnLetter(c)}{rowIndex}",
                        DataType = CellValues.InlineString,
                        InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(value)),
                    });
                }
                sheetData.Append(row);
                rowIndex++;
            }

            wbPart.Workbook.Save();
        }
        return mem.ToArray();
    }

    private static string ColumnLetter(int zeroBased)
    {
        var s = string.Empty;
        for (int n = zeroBased; n >= 0; n = n / 26 - 1)
            s = (char)('A' + n % 26) + s;
        return s;
    }
}
