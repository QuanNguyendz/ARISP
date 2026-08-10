using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Ghi nhận tín hiệu nghi vấn của một phiên (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>RecordCheatSignalAsync</c>, test-plan B3). Nguồn gọi: <c>SessionHub.ReportCheatSignal</c> +
/// <c>InterviewSessionFeature</c>. Khoá: guard loại tín hiệu chạy TRƯỚC khi tra phiên, chuẩn hoá
/// loại (trim + lower), làm sạch payload do client gửi (chặn phình to / ép JSON hợp lệ cho cột jsonb),
/// và trả về số tín hiệu CÙNG LOẠI của phiên (ADR-054).
/// </summary>
public class RecordCheatSignalTests
{
    private static ARI.Application.Services.InterviewService Svc(
        InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => InterviewServiceFactory.Create(uow, notif);

    private static CheatDetectionSignal Signal(Guid sessionId, string type, string payload = "{}")
        => new() { SessionId = sessionId, SignalType = type, Payload = payload };

    // ---------- Guards ----------

    [Fact]
    public async Task Blank_signal_type_fails_before_looking_up_the_session()
    {
        // uow rỗng: nếu guard KHÔNG chạy trước khi tra phiên thì lỗi sẽ là "không tìm thấy phiên".
        var uow = new InMemoryUnitOfWork();

        var res = await Svc(uow, new()).RecordCheatSignalAsync(Guid.NewGuid(), "   ", null, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Thiếu loại tín hiệu", res.Error);
        Assert.Empty(uow.Repo<CheatDetectionSignal>().Items);   // không lưu gì
        Assert.Equal(0, uow.SaveChangesCount);                  // không chạm DB
    }

    [Fact]
    public async Task Missing_session_fails()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(Guid.NewGuid(), "tab_hidden", null, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy phiên phỏng vấn", res.Error);
        Assert.Empty(uow.Repo<CheatDetectionSignal>().Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ---------- Happy path ----------

    [Fact]
    public async Task Signal_type_is_trimmed_lowercased_and_count_is_returned_per_type()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork()
            .Seed(session)
            .Seed(Signal(session.Id, "fullscreen_exit"));   // 1 tín hiệu cùng loại đã có sẵn

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "  FULLSCREEN_EXIT ", null, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value);                          // đếm theo loại đã chuẩn hoá → 2

        var added = uow.Repo<CheatDetectionSignal>().Items.Last();   // AddAsync nối vào cuối
        Assert.Equal("fullscreen_exit", added.SignalType);   // trim + lower
        Assert.Equal("{}", added.Payload);                   // payload null → "{}"
        Assert.Equal(session.Id, added.SessionId);
    }

    [Fact]
    public async Task Signals_of_a_different_type_are_not_counted()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork()
            .Seed(session)
            .Seed(Signal(session.Id, "tab_hidden"));         // loại khác → không tính vào count

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "window_blur", null, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value);                          // chỉ 1 window_blur
        Assert.Equal(2, uow.Repo<CheatDetectionSignal>().Items.Count);
    }

    // ---------- Làm sạch payload ----------

    [Fact]
    public async Task Payload_with_invalid_prefix_is_normalized_to_empty_object()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "tab_hidden", "DROP TABLE users", CancellationToken.None);

        Assert.True(res.IsSuccess);
        var added = Assert.Single(uow.Repo<CheatDetectionSignal>().Items);
        Assert.Equal("{}", added.Payload);                   // không mở đầu bằng { hoặc [ → bỏ
    }

    [Fact]
    public async Task Oversized_payload_is_normalized_to_empty_object()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork().Seed(session);
        var huge = "{" + new string('a', 2500);              // >2000 ký tự nhưng vẫn mở bằng '{'

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "tab_hidden", huge, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var added = Assert.Single(uow.Repo<CheatDetectionSignal>().Items);
        Assert.Equal("{}", added.Payload);                   // quá dài → bỏ (bảo vệ cột jsonb)
    }

    [Fact]
    public async Task Valid_json_object_payload_is_trimmed_and_preserved()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "shortcut_blocked", "  {\"x\":1}  ", CancellationToken.None);

        Assert.True(res.IsSuccess);
        var added = Assert.Single(uow.Repo<CheatDetectionSignal>().Items);
        Assert.Equal("{\"x\":1}", added.Payload);            // hợp lệ → chỉ trim, giữ nguyên nội dung
    }

    [Fact]
    public async Task Valid_json_array_payload_is_preserved()
    {
        var session = PracticeData.Session(Guid.NewGuid(), type: "real");
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new())
            .RecordCheatSignalAsync(session.Id, "page_unload", "[1,2]", CancellationToken.None);

        Assert.True(res.IsSuccess);
        var added = Assert.Single(uow.Repo<CheatDetectionSignal>().Items);
        Assert.Equal("[1,2]", added.Payload);                // mở bằng '[' cũng hợp lệ
    }
}
