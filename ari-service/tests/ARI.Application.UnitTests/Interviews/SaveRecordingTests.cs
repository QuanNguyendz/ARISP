using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Lưu video phỏng vấn thật (<see cref="ARI.Application.Services.InterviewService"/> <c>SaveRecordingAsync</c>,
/// test-plan B2, ADR-052). Chốt các guard (phiên lạ / buổi THỬ không quay / dữ liệu rỗng / vượt dung lượng),
/// chuẩn hoá content-type, đóng dấu hạn lưu theo <c>RecordingRetentionDays</c>, và ghi đè xoá file cũ.
/// </summary>
public class SaveRecordingTests
{
    private static ARI.Application.Services.InterviewService Svc(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, InterviewOptions? opts = null)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService(), storage, opts);

    private static InterviewSession RealSession() => PracticeData.Session(Guid.NewGuid(), type: "real");

    // ---------- Guards ----------

    [Fact]
    public async Task Missing_session_fails()
    {
        var storage = new RecordingFileStorage();
        var res = await Svc(new InMemoryUnitOfWork(), storage)
            .SaveRecordingAsync(Guid.NewGuid(), new byte[10], "a.webm", "video/webm", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy phiên phỏng vấn", res.Error);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Practice_session_is_rejected_and_nothing_is_stored()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "practice");
        var uow = new InMemoryUnitOfWork().Seed(session);
        var storage = new RecordingFileStorage();

        var res = await Svc(uow, storage)
            .SaveRecordingAsync(session.Id, new byte[10], "a.webm", "video/webm", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Phỏng vấn thử không quay video", res.Error); // ADR-038.6
        Assert.Empty(storage.Saved);
        Assert.Null(session.RecordingUrl);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Empty_content_is_rejected()
    {
        var session = RealSession();
        var uow = new InMemoryUnitOfWork().Seed(session);
        var storage = new RecordingFileStorage();

        var res = await Svc(uow, storage)
            .SaveRecordingAsync(session.Id, Array.Empty<byte>(), "a.webm", "video/webm", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Dữ liệu ghi hình rỗng", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Null(session.RecordingUrl);
    }

    [Fact]
    public async Task Oversized_content_is_rejected_by_the_configured_limit()
    {
        var session = RealSession();
        var uow = new InMemoryUnitOfWork().Seed(session);
        var storage = new RecordingFileStorage();
        var twoMb = new byte[2 * 1024 * 1024];

        var res = await Svc(uow, storage, new InterviewOptions { MaxRecordingSizeMb = 1 })
            .SaveRecordingAsync(session.Id, twoMb, "big.webm", "video/webm", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("vượt quá 1MB", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Null(session.RecordingUrl); // không set khi vượt hạn
    }

    // ---------- Happy path ----------

    [Fact]
    public async Task Real_recording_is_stored_with_clean_content_type_and_retention_stamp()
    {
        var session = RealSession();
        var uow = new InMemoryUnitOfWork().Seed(session);
        var storage = new RecordingFileStorage();
        var content = new byte[10];
        var expectedExpiry = DateTimeOffset.UtcNow.AddDays(7); // options mặc định: retention 7 ngày

        var res = await Svc(uow, storage) // default options (300MB, 7d)
            .SaveRecordingAsync(session.Id, content, "kiosk.webm", "video/webm;codecs=vp9,opus", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.Saved);
        Assert.Equal(10L, res.Value.SizeBytes);

        // Storage nhận content-type gốc đã bỏ tham số codecs, video vào thư mục riêng (không lẫn với CV).
        var saved = Assert.Single(storage.Saved);
        Assert.Equal("kiosk.webm", saved.FileName);
        Assert.Equal("video/webm", saved.ContentType);
        Assert.Equal(StorageFolder.Recording, saved.Folder);

        // Phiên được đóng dấu key + kích thước + hạn lưu ≈ now+7d, chưa xoá.
        Assert.Equal("recordings/kiosk.webm", session.RecordingUrl);
        Assert.Equal(10L, session.RecordingSizeBytes!.Value);
        Assert.NotNull(session.RecordingExpiresAt);
        Assert.InRange(session.RecordingExpiresAt!.Value, expectedExpiry.AddMinutes(-1), expectedExpiry.AddMinutes(1));
        Assert.Null(session.RecordingDeletedAt);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Reupload_deletes_the_previous_recording_first()
    {
        var session = RealSession();
        session.RecordingUrl = "stored/old.webm";       // đã có bản ghi cũ
        session.RecordingDeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var uow = new InMemoryUnitOfWork().Seed(session);
        var storage = new RecordingFileStorage();

        var res = await Svc(uow, storage)
            .SaveRecordingAsync(session.Id, new byte[5], "new.webm", "video/webm", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Contains("stored/old.webm", storage.Deleted);   // xoá key cũ trước
        Assert.Equal("recordings/new.webm", session.RecordingUrl); // trỏ sang bản mới
        Assert.Null(session.RecordingDeletedAt);               // reset cờ đã xoá
    }
}
