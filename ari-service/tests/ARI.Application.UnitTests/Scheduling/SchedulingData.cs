using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>Factory dựng entity cho test luồng Scheduling (ADR-048).</summary>
internal static class SchedulingData
{
    public static readonly DateTimeOffset Future = DateTimeOffset.UtcNow.AddDays(3);
    public static readonly DateTimeOffset Past = DateTimeOffset.UtcNow.AddDays(-3);

    public static JobPosting Job(Guid? owner = null) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = "Backend Developer",
    };

    /// <summary>Tạo tin và trả luôn id chủ tin — hầu hết test giờ phải truyền chủ tin để qua cửa kiểm quyền.</summary>
    public static JobPosting Job(out Guid owner)
    {
        owner = Guid.NewGuid();
        return Job(owner);
    }

    public static ARI.Domain.Entities.Application Application(
        Guid jobId, Guid? accountId, string status = "screening", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = "Nguyen Van A",
        Status = status,
    };

    public static AvailabilitySlot Slot(
        Guid jobId, int round = 1, int capacity = 1, int booked = 0, DateTimeOffset? start = null) => new()
    {
        JobPostingId = jobId,
        RoundNumber = round,
        StartTime = start ?? Future,
        EndTime = (start ?? Future).AddHours(1),
        Capacity = capacity,
        BookedCount = booked,
    };

    public static CreateSlotRequest SlotRequest(
        Guid jobId, int round = 1, int capacity = 1,
        DateTimeOffset? start = null, DateTimeOffset? end = null, string tz = "Asia/Ho_Chi_Minh") => new()
    {
        JobPostingId = jobId,
        RoundNumber = round,
        Capacity = capacity,
        StartTime = start ?? Future,
        EndTime = end ?? (start ?? Future).AddHours(1),
        Timezone = tz,
    };

    public static InterviewBooking Booking(
        Guid appId, Guid slotId, int round = 1, string status = "scheduled",
        string confirmation = "pending", DateTimeOffset? respondedAt = null, string? declinedBy = null) => new()
    {
        ApplicationId = appId,
        AvailabilitySlotId = slotId,
        RoundNumber = round,
        Status = status,
        ConfirmationStatus = confirmation,
        RespondedAt = respondedAt,
        DeclinedBy = declinedBy,
    };

    /// <summary>
    /// Booking mà ứng viên ĐÃ BÁO BẬN (từ chối kèm lý do) — trạng thái DUY NHẤT dời lịch được
    /// (ADR-059). Chỗ ở ca cũ đã được trả lúc từ chối nên ca đó để booked = 0.
    /// </summary>
    public static InterviewBooking DeclinedBooking(Guid appId, Guid slotId, int round = 1) =>
        Booking(appId, slotId, round,
            status: "declined", confirmation: "declined",
            respondedAt: DateTimeOffset.UtcNow.AddHours(-6), declinedBy: "candidate");
}

/// <summary>
/// Giả lập 2 câu lệnh SQL nguyên tử chốt/nhả chỗ mà handler dùng qua <c>ExecuteSqlRawAsync</c>.
/// Giữ booked_count ở "DB ảo" tách khỏi entity đang theo dõi — ĐÚNG như production (raw SQL cập nhật
/// row chứ không mutate entity EF), nhờ vậy guard chống overbooking (booked_count &lt; capacity) và
/// DTO trả về (BookedCount += 1) đều khớp hành vi thật.
/// </summary>
internal sealed class SlotSqlEmulator
{
    private readonly Dictionary<Guid, int> _booked = new();
    private readonly Dictionary<Guid, int> _capacity = new();

    public SlotSqlEmulator(InMemoryUnitOfWork uow)
    {
        foreach (var s in uow.Repo<AvailabilitySlot>().Items)
        {
            _booked[s.Id] = s.BookedCount;
            _capacity[s.Id] = s.Capacity;
        }
        uow.OnExecuteSqlRaw = Handle;
    }

    /// <summary>booked_count "trong DB" sau các lệnh SQL đã chạy.</summary>
    public int BookedCountOf(Guid slotId) => _booked.TryGetValue(slotId, out var b) ? b : 0;

    private Task<int> Handle(string sql, object[] prms, CancellationToken ct)
    {
        // Hai dạng tham số cùng tồn tại:
        //   gán 1 người : {0}=now, {1}=slotId            (AssignSlot, huỷ lịch khi loại hồ sơ)
        //   dời N người : {0}=now, {1}=số chỗ, {2}=slotId (RescheduleBookings — chiếm/trả cả nhóm)
        var isBatch = prms.Length >= 3;
        var slotId = (Guid)prms[isBatch ? 2 : 1];
        var amount = isBatch ? Convert.ToInt32(prms[1]) : 1;
        if (!_booked.ContainsKey(slotId)) return Task.FromResult(0);

        if (sql.Contains("booked_count = booked_count +")) // chiếm chỗ: chỉ khi còn đủ cho CẢ nhóm
        {
            if (_booked[slotId] + amount <= _capacity[slotId]) { _booked[slotId] += amount; return Task.FromResult(1); }
            return Task.FromResult(0);
        }
        if (sql.Contains("GREATEST(booked_count -")) // trả chỗ (không âm)
        {
            _booked[slotId] = Math.Max(_booked[slotId] - amount, 0);
            return Task.FromResult(1);
        }
        return Task.FromResult(0);
    }
}
