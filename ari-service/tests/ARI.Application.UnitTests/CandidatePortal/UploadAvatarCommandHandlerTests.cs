using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Ảnh đại diện ứng viên tự tải lên: chặn định dạng/kích thước, lưu đúng thư mục avatars/, và dọn
/// ảnh cũ — nhưng chỉ dọn file của chính hệ thống, không đụng URL ảnh Google.
/// </summary>
public class UploadAvatarCommandHandlerTests
{
    private static UploadAvatarCommandHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage storage)
        => new(uow, storage);

    private static CandidateAccount Candidate(string? avatar = null) => new()
    {
        Email = "cand@gmail.com",
        FullName = "Nguyen Van A",
        EmailVerified = true,
        AvatarUrl = avatar,
    };

    private static byte[] Bytes(int size = 1024) => new byte[size];

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".gif")]
    [InlineData(".svg")]
    [InlineData(".exe")]
    [InlineData("")]
    public async Task Dinh_dang_khong_hop_le_bi_tu_choi(string ext)
    {
        var uow = new InMemoryUnitOfWork().Seed(Candidate());
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(uow.Repo<CandidateAccount>().Items[0].Id, Bytes(), $"anh{ext}", ext),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Anh_vuot_2mb_bi_tu_choi()
    {
        var uow = new InMemoryUnitOfWork().Seed(Candidate());
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(
                uow.Repo<CandidateAccount>().Items[0].Id,
                Bytes(UploadAvatarCommandHandler.MaxBytes + 1),
                "anh.png",
                ".png"),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("2MB", res.Error);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task File_rong_bi_tu_choi()
    {
        var uow = new InMemoryUnitOfWork().Seed(Candidate());
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(uow.Repo<CandidateAccount>().Items[0].Id, Array.Empty<byte>(), "anh.png", ".png"),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Khong_tim_thay_tai_khoan_tra_NotFound()
    {
        var uow = new InMemoryUnitOfWork();
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(Guid.NewGuid(), Bytes(), "anh.png", ".png"),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Luu_anh_vao_thu_muc_avatars_va_ghi_vao_ho_so()
    {
        var acc = Candidate();
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(acc.Id, Bytes(), "toi.png", ".png"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var saved = Assert.Single(storage.Saved);
        Assert.Equal(StorageFolder.Avatar, saved.Folder);
        Assert.Equal("image/png", saved.ContentType);
        Assert.Equal("avatars/toi.png", acc.AvatarUrl);
        Assert.Equal("/files/avatars/toi.png", res.Value.AvatarUrl);
    }

    [Fact]
    public async Task Doi_anh_thi_xoa_anh_cu_da_tai_len()
    {
        var acc = Candidate("avatars/anh-cu.png");
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(acc.Id, Bytes(), "anh-moi.webp", ".webp"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("avatars/anh-cu.png", Assert.Single(storage.Deleted));
    }

    [Fact]
    public async Task Khong_goi_xoa_khi_anh_cu_la_url_google()
    {
        // Ảnh Google nằm trên máy chủ Google, không phải file của ta — gọi DeleteAsync với URL đó là
        // vô nghĩa (và với provider S3 sẽ là một khoá rác gửi lên bucket).
        var acc = Candidate("https://lh3.googleusercontent.com/a/abc123");
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(acc.Id, Bytes(), "anh.jpg", ".jpg"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(storage.Deleted);
        Assert.Equal("avatars/anh.jpg", acc.AvatarUrl);
    }

    [Fact]
    public async Task Ext_viet_hoa_van_duoc_chap_nhan()
    {
        var acc = Candidate();
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var storage = new RecordingFileStorage();

        var res = await Handler(uow, storage).Handle(
            new UploadAvatarCommand(acc.Id, Bytes(), "ANH.JPG", ".JPG"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("image/jpeg", Assert.Single(storage.Saved).ContentType);
    }

    // Hồ sơ trả về phải dựng đúng URL cho CẢ HAI dạng lưu trong cột avatar_url: ảnh Google là URL
    // tuyệt đối (giữ nguyên), ảnh tự tải lên là storageKey (phải đổi sang URL — R2 còn cần presign).
    [Theory]
    [InlineData("https://lh3.googleusercontent.com/a/abc123", "https://lh3.googleusercontent.com/a/abc123")]
    [InlineData("avatars/x.png", "/files/avatars/x.png")]
    [InlineData(null, null)]
    public async Task Ho_so_dung_url_anh_cho_ca_hai_dang_luu(string? stored, string? expected)
    {
        var acc = Candidate(stored);
        var uow = new InMemoryUnitOfWork().Seed(acc);

        var res = await new GetMyProfileQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetMyProfileQuery(acc.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(expected, res.Value.AvatarUrl);
    }
}
