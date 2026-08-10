using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Nhập ngân hàng câu hỏi từ file Excel (<see cref="ImportOnlineTestQuestionsCommandHandler"/>, test-plan B9):
/// phân quyền chủ tin TRƯỚC khi đọc file, file hỏng → lỗi thân thiện, và đọc layout A..I với remap chỉ số
/// đáp án đúng khi có cột phương án để trống + import từng phần (dòng lỗi không chặn dòng hợp lệ).
/// </summary>
public class ImportOnlineTestQuestionsCommandHandlerTests
{
    // Cột theo file mẫu: A=Câu hỏi | B=Loại | C..H=Phương án A..F | I=Đáp án đúng.
    private static readonly string[] Header =
    {
        "Câu hỏi", "Loại (single/multiple)",
        "Phương án A", "Phương án B", "Phương án C", "Phương án D", "Phương án E", "Phương án F",
        "Đáp án đúng (VD: A hoặc A,C)"
    };

    private static Task<Result<OnlineTestImportResultDto>> Run(InMemoryUnitOfWork uow, ImportOnlineTestQuestionsCommand cmd)
        => new ImportOnlineTestQuestionsCommandHandler(uow).Handle(cmd, CancellationToken.None);

    [Fact]
    public async Task Non_owner_is_forbidden_without_reading_the_file()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);
        var garbage = new byte[] { 1, 2, 3 }; // không phải xlsx — nhưng không được đọc tới

        var res = await Run(uow, new ImportOnlineTestQuestionsCommand(job.Id, garbage, "q.xlsx", Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode); // chặn ở quyền, chưa chạm parser
        Assert.Empty(uow.Repo<OnlineTestQuestion>().Items);
    }

    [Fact]
    public async Task Unknown_job_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Run(uow, new ImportOnlineTestQuestionsCommand(Guid.NewGuid(), new byte[] { 1 }, "q.xlsx", Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Corrupt_file_returns_friendly_error_and_saves_nothing()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var garbage = new byte[] { 0xFF, 0x00, 0x11, 0x22 }; // không mở được bằng OpenXML

        var res = await Run(uow, new ImportOnlineTestQuestionsCommand(job.Id, garbage, "bad.bin", owner, AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Contains("Không đọc được file Excel", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(uow.Repo<OnlineTestQuestion>().Items);
    }

    [Fact]
    public async Task Header_is_skipped_and_correct_indices_are_remapped_over_blank_options()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var file = OnlineTestData.BuildXlsx(
            Header,
            // 1 đáp án — đáp án đúng "B" = phương án thứ 2.
            new[] { "HTTP 404?", "single", "Thành công", "Không tìm thấy", "", "", "", "", "B" },
            // Nhiều đáp án — cột D (phương án B) để trống → "A,C" remap còn [0,1]; loại suy ra từ 2 đáp án.
            new[] { "Ngôn ngữ backend?", "", "C#", "", "Python", "", "", "", "A,C" });

        var res = await Run(uow, new ImportOnlineTestQuestionsCommand(job.Id, file, "q.xlsx", owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Imported);
        Assert.Equal(0, res.Value.Failed);
        Assert.Empty(res.Value.Errors);

        var saved = uow.Repo<OnlineTestQuestion>().Items;
        Assert.Equal(2, saved.Count);

        var single = saved.Single(q => q.QuestionType == "single");
        Assert.Equal("[1]", single.CorrectOptions);        // "B" → chỉ số 1

        var multiple = saved.Single(q => q.QuestionType == "multiple");
        Assert.Equal("[0,1]", multiple.CorrectOptions);    // "A,C" remap qua cột D trống
    }

    [Fact]
    public async Task Partial_import_keeps_valid_rows_and_reports_failed_rows()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var file = OnlineTestData.BuildXlsx(
            Header,                                                                       // dòng 1: tiêu đề (bỏ)
            new[] { "Câu hợp lệ?", "single", "A", "B", "", "", "", "", "A" },              // dòng 2: hợp lệ
            new[] { "Chỉ 1 phương án?", "single", "duy nhất", "", "", "", "", "", "A" },   // dòng 3: <2 phương án
            new[] { "Trỏ ô trống?", "single", "chỉ A", "", "", "", "", "", "B" },          // dòng 4: đáp án trỏ ô trống
            new[] { "Thiếu đáp án?", "single", "A", "B", "", "", "", "", "" },             // dòng 5: thiếu đáp án đúng
            Array.Empty<string>());                                                       // dòng 6: trống (bỏ qua)

        var res = await Run(uow, new ImportOnlineTestQuestionsCommand(job.Id, file, "q.xlsx", owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Imported);
        Assert.Equal(3, res.Value.Failed);
        Assert.Equal(new[] { 3, 4, 5 }, res.Value.Errors.Select(e => e.Row).ToArray()); // đúng số dòng Excel
        Assert.Contains("ít nhất 2", res.Value.Errors[0].Message);
        Assert.Contains("để trống", res.Value.Errors[1].Message);
        Assert.Contains("Thiếu đáp án đúng", res.Value.Errors[2].Message);
        Assert.Single(uow.Repo<OnlineTestQuestion>().Items);   // chỉ dòng hợp lệ được lưu
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
