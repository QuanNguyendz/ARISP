using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// So khớp thông tin liên hệ với CV (<see cref="VerifyCvInfoCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "VerifyCvInfo" (UTCID01–12). Lưu ý: handler dùng SO KHỚP BẰNG CODE (0 token AI, ADR) — các dòng
/// confirm ghi "Gemini"/"MIME" trong báo cáo là dấu vết copy-paste từ ApplyToJob; VerifyCvInfo không tính MIME,
/// luôn trả <see cref="Result"/> Success với IsMatch true/false. Ta khẳng định HÀNH VI THỰC của handler.
/// </summary>
public class VerifyCvInfoCommandHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private const string MatchingCv = "Candidate User — phone 0901234567 — backend engineer .NET";

    private static VerifyCvInfoCommandHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage storage, StubDocumentParser parser)
        => new(uow, storage, parser);

    private static VerifyCvInfoCommand Cmd(byte[]? bytes, string? fileName)
        => new(CandidateId, "Candidate User", "0901234567", bytes, fileName);

    private static CandidateAccount Account(string? profileCvUrl = null)
        => new() { Id = CandidateId, Email = "candidate@example.com", ProfileCvUrl = profileCvUrl };

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingFileStorage(), new StubDocumentParser())
            .Handle(Cmd(new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tài khoản ứng viên.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Theory]
    [InlineData("cv.pdf")]    // UTCID02 — PDF đính kèm
    [InlineData("cv.docx")]   // UTCID03 — DOCX đính kèm
    [InlineData("cv.txt")]    // UTCID04 — đuôi lạ (parser vẫn parse được)
    public async Task UTCID02_to_04_attached_cv_verified(string fileName)
    {
        var uow = new InMemoryUnitOfWork().Seed(Account());
        var parser = new StubDocumentParser { Text = MatchingCv };
        var res = await Handler(uow, new RecordingFileStorage(), parser)
            .Handle(Cmd(new byte[] { 1, 2, 3 }, fileName), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);
        Assert.Null(res.Value.MismatchDetails);
    }

    [Fact]
    public async Task UTCID05_No_attachment_no_profile_cv()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: null));
        var res = await Handler(uow, new RecordingFileStorage(), new StubDocumentParser())
            .Handle(Cmd(null, null), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn cần tải CV lên hồ sơ hoặc đính kèm CV.", res.Error);
        Assert.Equal("no_cv", res.ErrorCode);
    }

    [Fact]
    public async Task UTCID06_Profile_cv_reads_empty()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { FileBytes = null };   // ReadAllBytes → null
        var res = await Handler(uow, storage, new StubDocumentParser())
            .Handle(Cmd(null, null), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không đọc được file CV trong hồ sơ. Vui lòng tải lại CV.", res.Error);
        Assert.Equal("cv_unreadable", res.ErrorCode);
    }

    [Fact]
    public async Task UTCID07_Valid_profile_cv_parsed_and_verified()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { FileBytes = new byte[] { 9, 9, 9 } };
        var parser = new StubDocumentParser { Text = MatchingCv };
        var res = await Handler(uow, storage, parser).Handle(Cmd(null, null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);
    }

    [Fact]
    public async Task UTCID08_Parser_returns_null_treated_as_matching()
    {
        // cvText rỗng → không so được text → guard coi là hợp lệ (IsMatch true).
        var uow = new InMemoryUnitOfWork().Seed(Account());
        var res = await Handler(uow, new RecordingFileStorage(), new StubDocumentParser { Text = null! })
            .Handle(Cmd(new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);
    }

    [Fact]
    public async Task UTCID09_Parser_throws_treated_as_matching()
    {
        // Handler nuốt lỗi parse (best-effort) → cvText="" → guard hợp lệ. (Stub ném thông điệp cố định.)
        var uow = new InMemoryUnitOfWork().Seed(Account());
        var res = await Handler(uow, new RecordingFileStorage(), new StubDocumentParser { ThrowOnParse = true })
            .Handle(Cmd(new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsMatch);
    }

    [Fact]
    public async Task UTCID10_Mismatch_returns_not_match()
    {
        // Báo cáo ghi "Gemini failure"; thực tế handler chỉ trả IsMatch=false + chi tiết lệch (không có Result.Failure).
        var uow = new InMemoryUnitOfWork().Seed(Account());
        var parser = new StubDocumentParser { Text = "Hoàn toàn khác — 0000, nội dung không liên quan." };
        var res = await Handler(uow, new RecordingFileStorage(), parser)
            .Handle(Cmd(new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsMatch);
        Assert.NotNull(res.Value.MismatchDetails);
    }

    [Fact]
    public async Task UTCID11_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new RecordingFileStorage(), new StubDocumentParser())
            .Handle(Cmd(new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID12_Profile_cv_read_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { ReadThrows = new Exception("Read Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, storage, new StubDocumentParser())
            .Handle(Cmd(null, "cv.pdf"), CancellationToken.None));
        Assert.Equal("Read Error", ex.Message);
    }
}
