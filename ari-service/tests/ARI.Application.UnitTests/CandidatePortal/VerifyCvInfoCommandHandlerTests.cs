using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// So khớp thông tin liên hệ với CV bằng CODE (<see cref="VerifyCvInfoCommandHandler"/>, test-plan B11,
/// Flow 3) — 0 token AI. Chốt: khớp tên bỏ dấu/thường theo từng token (bỏ token 1 ký tự), khớp 9 số
/// cuối điện thoại (điện thoại &lt;8 số bị bỏ qua), CV rỗng/không đọc được → coi là hợp lệ (guard),
/// và các nhánh lỗi tài khoản/CV.
/// </summary>
public class VerifyCvInfoCommandHandlerTests
{
    private static VerifyCvInfoCommandHandler Handler(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, StubDocumentParser parser) =>
        new(uow, storage, parser);

    /// <summary>Dựng handler + tài khoản đã seed; CV đọc từ file đính kèm (parser trả <paramref name="cvText"/>).</summary>
    private static (VerifyCvInfoCommandHandler handler, Guid accId) Setup(string cvText)
    {
        var acc = new CandidateAccount { Email = "cand@example.io" };
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var parser = new StubDocumentParser { Text = cvText };
        return (Handler(uow, new RecordingFileStorage(), parser), acc.Id);
    }

    private static VerifyCvInfoCommand Cmd(Guid accId, string name, string phone) =>
        new(accId, name, phone, new byte[] { 1 }, "cv.pdf"); // đính kèm → đọc qua parser

    [Fact]
    public async Task Name_and_phone_present_in_cv_match()
    {
        var (handler, accId) = Setup("Nguyễn Văn A — SĐT 0900000000, 5 năm kinh nghiệm C#.");

        var res = await handler.Handle(Cmd(accId, "Nguyen Van A", "0900000000"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);          // bỏ dấu + 9 số cuối đều khớp
        Assert.Null(res.Value.MismatchDetails);
    }

    [Fact]
    public async Task Missing_name_reports_only_name_mismatch()
    {
        var (handler, accId) = Setup("Ứng viên có số điện thoại 0900000000, thành thạo .NET.");

        var res = await handler.Handle(Cmd(accId, "Tran Thi B", "0900000000"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsMatch);
        Assert.Contains("Họ và tên", res.Value.MismatchDetails);
        Assert.DoesNotContain("Số điện thoại", res.Value.MismatchDetails); // điện thoại vẫn khớp
    }

    [Fact]
    public async Task Different_phone_reports_only_phone_mismatch()
    {
        var (handler, accId) = Setup("Nguyễn Văn A — SĐT 0900000000.");

        var res = await handler.Handle(Cmd(accId, "Nguyen Van A", "0912345678"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsMatch);
        Assert.Contains("Số điện thoại", res.Value.MismatchDetails);
        Assert.DoesNotContain("Họ và tên", res.Value.MismatchDetails);     // tên vẫn khớp
    }

    [Fact]
    public async Task Short_phone_is_ignored_and_only_name_is_checked()
    {
        var (handler, accId) = Setup("Nguyễn Văn A, kỹ sư backend.");

        // Điện thoại 5 số (< 8) → bỏ qua hẳn; tên khớp → hợp lệ.
        var res = await handler.Handle(Cmd(accId, "Nguyen Van A", "12345"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);
        Assert.Null(res.Value.MismatchDetails);
    }

    [Fact]
    public async Task Unreadable_cv_text_is_treated_as_matching()
    {
        var (handler, accId) = Setup(cvText: ""); // CV scan/ảnh → parser trả rỗng

        var res = await handler.Handle(Cmd(accId, "Bất Kỳ Ai", "0999999999"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);            // guard: không so được thì coi là hợp lệ
        Assert.Null(res.Value.MismatchDetails);
    }

    // ---------- Nhánh lỗi tài khoản / CV ----------

    [Fact]
    public async Task Missing_account_is_unauthorized()
    {
        var uow = new InMemoryUnitOfWork(); // không seed tài khoản
        var handler = Handler(uow, new RecordingFileStorage(), new StubDocumentParser());

        var res = await handler.Handle(Cmd(Guid.NewGuid(), "A", "0900000000"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task No_attachment_and_no_profile_cv_fails_with_no_cv()
    {
        var acc = new CandidateAccount { Email = "cand@example.io", ProfileCvUrl = null };
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var handler = Handler(uow, new RecordingFileStorage(), new StubDocumentParser());

        var res = await handler.Handle(
            new VerifyCvInfoCommand(acc.Id, "A", "0900000000", null, null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("no_cv", res.ErrorCode);
    }

    [Fact]
    public async Task Profile_cv_that_reads_empty_fails_with_cv_unreadable()
    {
        var acc = new CandidateAccount { Email = "cand@example.io", ProfileCvUrl = "stored/cv.pdf" };
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage { FileBytes = null }; // ReadAllBytes trả null
        var handler = Handler(uow, storage, new StubDocumentParser());

        var res = await handler.Handle(
            new VerifyCvInfoCommand(acc.Id, "A", "0900000000", null, null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("cv_unreadable", res.ErrorCode);
    }
}
