using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>IGeminiProvider giả — chỉ cấu hình ReviewCvAsync (đường dùng của UploadProfileCv), các API khác không dùng.</summary>
internal sealed class FakeCvReviewProvider : IGeminiProvider
{
    public Result<CvReviewResultDto> ReviewResult { get; set; } =
        Result.Success(new CvReviewResultDto { IsValidCv = true, OverallScore = 80, Verdict = "good", Provider = "Gemini" });

    public Task<Result<CvReviewResultDto>> ReviewCvAsync(byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText, CancellationToken ct = default)
        => Task.FromResult(ReviewResult);

    public Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
        string jdText, byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText,
        string? rubricInstruction = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
        byte[]? jdFileBytes, string? jdMimeType, string? fallbackJdText, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<CvContactVerificationResultDto>> VerifyCvContactInfoAsync(
        byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText,
        string formName, string formPhone, string formEmail, CancellationToken ct = default) => throw new NotImplementedException();
}

internal static class PortalProfileData
{
    public static CandidateAccount Account(out Guid id, string? cvUrl = null)
    {
        var acc = new CandidateAccount
        {
            Id = Guid.NewGuid(), Email = "cand@corp.io", FullName = "Cũ", EmailVerified = true, IsActive = true,
            ProfileCvUrl = cvUrl,
        };
        id = acc.Id;
        return acc;
    }
}

/// <summary>
/// Sửa hồ sơ ứng viên (<see cref="UpdateMyProfileCommandHandler"/>): thiếu tài khoản → NotFound; cập nhật trim
/// họ tên/điện thoại, suy ra chuỗi Location từ phường+tỉnh, serialize kỹ năng; không có địa giới → Location null.
/// </summary>
public class UpdateMyProfileCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var res = await new UpdateMyProfileCommandHandler(new InMemoryUnitOfWork(), new RecordingFileStorage())
            .Handle(new UpdateMyProfileCommand(Guid.NewGuid(), new CandidateProfileUpdateRequest()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Derives_location_and_serializes_skills()
    {
        var acc = PortalProfileData.Account(out var id);
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var req = new CandidateProfileUpdateRequest
        {
            FullName = "  Nguyễn Văn A  ", Phone = " 0900000000 ",
            ProvinceName = "Hà Nội", WardName = "Phường Láng",
            Skills = new List<string> { "C#", " ", "SQL" },
        };

        var res = await new UpdateMyProfileCommandHandler(uow, new RecordingFileStorage())
            .Handle(new UpdateMyProfileCommand(id, req), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Nguyễn Văn A", acc.FullName);
        Assert.Equal("0900000000", acc.Phone);
        Assert.Equal("Phường Láng, Hà Nội", acc.Location);
        Assert.Equal(new[] { "C#", "SQL" }, JsonSerializer.Deserialize<List<string>>(acc.SkillsJson));
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Empty_location_becomes_null()
    {
        var acc = PortalProfileData.Account(out var id);
        var uow = new InMemoryUnitOfWork().Seed(acc);

        var res = await new UpdateMyProfileCommandHandler(uow, new RecordingFileStorage())
            .Handle(new UpdateMyProfileCommand(id, new CandidateProfileUpdateRequest { Headline = "Dev" }), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Null(acc.Location);
    }
}

/// <summary>
/// Tải CV hồ sơ + đánh giá Gemini (<see cref="UploadProfileCvCommandHandler"/>): thiếu tài khoản → NotFound;
/// CV không hợp lệ bị chặn (không lưu); hợp lệ thì lưu file + đính đánh giá; AI hỏng thì vẫn lưu CV, không đánh giá.
/// </summary>
public class UploadProfileCvCommandHandlerTests
{
    private static UploadProfileCvCommand Cmd(Guid id) => new(id, new byte[] { 1, 2, 3 }, "cv.pdf", ".pdf");

    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var handler = new UploadProfileCvCommandHandler(
            new InMemoryUnitOfWork(), new RecordingFileStorage(), new StubDocumentParser(), new FakeCvReviewProvider());

        var res = await handler.Handle(Cmd(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Invalid_cv_rejected()
    {
        var acc = PortalProfileData.Account(out var id);
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var gemini = new FakeCvReviewProvider { ReviewResult = Result.Success(new CvReviewResultDto { IsValidCv = false }) };

        var res = await new UploadProfileCvCommandHandler(uow, new RecordingFileStorage(), new StubDocumentParser(), gemini)
            .Handle(Cmd(id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("invalid_cv", res.ErrorCode);
        Assert.Null(acc.ProfileCvUrl);   // không lưu CV không hợp lệ
    }

    [Fact]
    public async Task UTCID03_Valid_cv_saves_and_reviews()
    {
        var acc = PortalProfileData.Account(out var id);
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage();

        var res = await new UploadProfileCvCommandHandler(uow, storage, new StubDocumentParser(), new FakeCvReviewProvider())
            .Handle(Cmd(id), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.True(res.Value.AiAvailable);
        Assert.NotNull(res.Value.Review);
        Assert.Single(storage.Saved);
        Assert.False(string.IsNullOrEmpty(acc.ProfileCvUrl));
        Assert.Equal("cv.pdf", acc.ProfileCvFileName);
    }

    [Fact]
    public async Task UTCID04_Ai_unavailable_still_saves_cv()
    {
        var acc = PortalProfileData.Account(out var id);
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var gemini = new FakeCvReviewProvider { ReviewResult = Result.Failure<CvReviewResultDto>("Gemini API key missing") };

        var res = await new UploadProfileCvCommandHandler(uow, new RecordingFileStorage(), new StubDocumentParser(), gemini)
            .Handle(Cmd(id), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.False(res.Value.AiAvailable);
        Assert.Null(res.Value.Review);
        Assert.False(string.IsNullOrEmpty(acc.ProfileCvUrl)); // vẫn lưu CV
    }
}
