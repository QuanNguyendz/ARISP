using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Xuất bảng điểm trắc nghiệm .xlsx cho staff (<see cref="ExportOnlineTestResultsQueryHandler"/>, test-plan B26):
/// phân quyền chủ tin, và file hợp lệ (content-type xlsx, tên file slug theo tiêu đề + ngày, mở lại được,
/// cột "Rời màn hình" = '-' khi TabSwitchCount 0).
/// </summary>
public class ExportOnlineTestResultsQueryHandlerTests
{
    private static Task<Result<OnlineTestExportFileDto>> Run(InMemoryUnitOfWork uow, ExportOnlineTestResultsQuery q)
        => new ExportOnlineTestResultsQueryHandler(uow).Handle(q, CancellationToken.None);

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    public async Task Missing_job_returns_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(),
            new ExportOnlineTestResultsQuery(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new ExportOnlineTestResultsQuery(job.Id, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Export_produces_openable_xlsx_with_slugged_filename_and_dash_for_zero_tabswitch()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner); // Title "Backend Developer"
        var app = OnlineTestData.Application(job.Id, accountId: null); // CandidateName "Nguyen Van A"
        var sub = OnlineTestData.Submission(app.Id, score: 90m, passed: true, correct: 18, total: 20, tabSwitch: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(sub);

        var res = await Run(uow, new ExportOnlineTestResultsQuery(job.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(XlsxContentType, res.Value.ContentType);
        Assert.Equal($"bang-diem-trac-nghiem-backend-developer-{DateTime.UtcNow:yyyyMMdd}.xlsx", res.Value.FileName);

        var texts = ReadInlineTexts(res.Value.Content); // mở lại file được → không ném lỗi
        Assert.Contains("Nguyen Van A", texts);          // dòng dữ liệu ứng viên
        Assert.Contains("-", texts);                      // TabSwitchCount 0 → '-'
    }

    /// <summary>Đọc lại toàn bộ text (inline string) trong worksheet đầu — chứng minh file mở được.</summary>
    private static string ReadInlineTexts(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var ws = doc.WorkbookPart!.WorksheetParts.First().Worksheet;
        return string.Join("\n", ws.Descendants<Text>().Select(t => t.Text));
    }
}
