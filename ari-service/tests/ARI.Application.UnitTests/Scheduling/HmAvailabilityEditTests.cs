using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Hiring Manager SỬA LẠI lịch đã gửi (ADR-067 bổ sung).
///
/// Trước đó HM chỉ khai được lịch như tác dụng phụ của việc duyệt shortlist, và đường đó chỉ biết
/// THÊM — khai nhầm giờ hay có việc đột xuất thì không có chỗ nào sửa, mà Recruiter vẫn xếp ca vào
/// khung sai vì hệ thống nói rằng HM rảnh.
///
/// Hai điều bộ test này khoá:
/// 1. Khai lại danh sách KHÔNG được âm thầm xoá khung đang diễn ra (khung đó không gửi lại được vì
///    luật đòi giờ bắt đầu ở tương lai — xoá nó nghĩa là mỗi lần sửa lịch lại tự huỷ đúng khung mình
///    đang ở trong đó, kèm mọi ca đã xếp vào).
/// 2. Sửa lịch phải ĐẾM ĐƯỢC những buổi đã hẹn nay rơi ra ngoài. Luật khớp giờ chỉ chạy lúc gán ca
///    nên không có chốt chặn nào bắt được việc này; không đếm thì nó hỏng lặng lẽ tới hôm phỏng vấn.
/// </summary>
public class HmAvailabilityEditTests
{
    private static readonly Guid JobId = Guid.Parse("87000000-0000-0000-0000-000000000001");
    private static readonly Guid HmId = Guid.Parse("87000000-0000-0000-0000-0000000000aa");

    private static HiringManagerAvailability Window(DateTimeOffset start, DateTimeOffset end) => new()
    {
        JobPostingId = JobId, RoundNumber = 1, HiringManagerUserId = HmId,
        StartTime = start, EndTime = end,
    };

    private static HmAvailabilityWindowInput Input(DateTimeOffset start, DateTimeOffset end)
        => new(start, end, null);

    /// <summary>
    /// Tin có Hiring Manager chính <see cref="HmId"/> đang hoạt động. ADR-068: khung giờ chỉ có hiệu lực khi
    /// là của HM chính HIỆN TẠI — khung không gắn với ai là khung của không ai cả.
    /// </summary>
    private static InMemoryUnitOfWork NewUow()
    {
        var uow = new InMemoryUnitOfWork();
        HiringManagerSeed.Primary(uow, JobId, HmId);
        return uow;
    }

    // ---------- Khai lại danh sách ----------

    [Fact]
    public async Task Khai_lai_khong_dung_toi_khung_dang_dien_ra()
    {
        var now = DateTimeOffset.UtcNow;
        var running = Window(now.AddHours(-1), now.AddHours(3));   // đã bắt đầu, còn hiệu lực
        var future = Window(now.AddDays(1), now.AddDays(1).AddHours(2));
        var uow = NewUow().Seed(running).Seed(future);

        var replacement = Input(now.AddDays(2), now.AddDays(2).AddHours(2));
        var count = await HmAvailabilityWriteGate.ReplaceAsync(
            uow, JobId, 1, HmId, new[] { replacement }, CancellationToken.None);
        await uow.SaveChangesAsync();

        Assert.Equal(1, count);

        var left = uow.Repo<HiringManagerAvailability>().Items;
        Assert.Equal(2, left.Count);                                   // khung đang chạy + khung mới
        Assert.Contains(left, w => w.Id == running.Id);                // KHÔNG bị xoá
        Assert.DoesNotContain(left, w => w.Id == future.Id);           // khung tương lai bị thay
        Assert.Contains(left, w => w.StartTime == replacement.StartTime);
    }

    [Fact]
    public async Task Khai_lai_danh_sach_rong_thi_go_het_khung_tuong_lai()
    {
        // "Tuần tới tôi bận hết" là một câu trả lời hợp lệ, không phải lỗi.
        var now = DateTimeOffset.UtcNow;
        var uow = NewUow()
            .Seed(Window(now.AddDays(1), now.AddDays(1).AddHours(2)))
            .Seed(Window(now.AddDays(2), now.AddDays(2).AddHours(2)));

        var count = await HmAvailabilityWriteGate.ReplaceAsync(
            uow, JobId, 1, HmId, Array.Empty<HmAvailabilityWindowInput>(), CancellationToken.None);
        await uow.SaveChangesAsync();

        Assert.Equal(0, count);
        Assert.Empty(uow.Repo<HiringManagerAvailability>().Items);
    }

    // ---------- Đếm hậu quả ----------

    /// <summary>Một buổi đã hẹn ở ca <paramref name="slotStart"/>–+1h của vòng 1.</summary>
    private static InMemoryUnitOfWork WithBooking(InMemoryUnitOfWork uow, DateTimeOffset slotStart)
    {
        var slot = new AvailabilitySlot
        {
            JobPostingId = JobId, RoundNumber = 1,
            StartTime = slotStart, EndTime = slotStart.AddHours(1), Capacity = 1, BookedCount = 1,
        };
        return uow.Seed(slot).Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id));
    }

    [Fact]
    public async Task Buoi_da_hen_nam_ngoai_lich_moi_thi_bi_dem()
    {
        var now = DateTimeOffset.UtcNow;
        var uow = WithBooking(NewUow(), now.AddDays(1).AddHours(9));
        // Lịch còn lại chỉ có khung ngày kia — buổi đã hẹn ngày mai rơi ra ngoài.
        uow.Seed(Window(now.AddDays(2), now.AddDays(2).AddHours(8)));

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Single(affected);
    }

    [Fact]
    public async Task Buoi_da_hen_van_nam_trong_lich_thi_khong_bi_dem()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(1).AddHours(9);
        var uow = WithBooking(NewUow(), start);
        uow.Seed(Window(start.AddHours(-1), start.AddHours(3)));   // bao trọn ca

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Empty(affected);
    }

    [Fact]
    public async Task Buoi_da_dien_ra_khong_bi_dem()
    {
        // Lịch rảnh khai lại hôm nay không nói được điều gì về một buổi đã xong.
        var now = DateTimeOffset.UtcNow;
        var uow = WithBooking(NewUow(), now.AddDays(-1));

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Empty(affected);
    }

    [Fact]
    public async Task Go_het_lich_thi_moi_buoi_sap_toi_deu_bi_dem()
    {
        // Đúng tình huống "có việc đột xuất": rút hết khung thì mọi buổi phía trước đều thành buổi
        // mà người bắt buộc phải dự đã báo là không dự được.
        var now = DateTimeOffset.UtcNow;
        var uow = WithBooking(NewUow(), now.AddDays(1));
        WithBooking(uow, now.AddDays(2));

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Equal(2, affected.Count);
    }

    [Fact]
    public async Task Ca_chua_ai_dat_thi_khong_tinh_la_hau_qua()
    {
        // Ca trống nằm ngoài lịch mới thì chỉ là ca không dùng được — không ai bị lỡ hẹn, nên nó
        // không thuộc con số cảnh báo (màn xếp lịch đã có chip "Ngoài lịch HM" cho việc đó).
        var now = DateTimeOffset.UtcNow;
        var uow = NewUow().Seed(new AvailabilitySlot
        {
            JobPostingId = JobId, RoundNumber = 1,
            StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(1), Capacity = 1,
        });

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Empty(affected);
    }

    // ---------- Vòng không cần HM có mặt ----------

    /// <summary>Tin có HM chính + một vòng loại <paramref name="roundType"/>.</summary>
    private static InMemoryUnitOfWork JobWithRound(int round, string roundType) => NewUow()
        .Seed(new JobPosting { Id = JobId, Title = "Backend Developer", CreatedByUserId = Guid.NewGuid() })
        .Seed(new InterviewRoundConfig { JobPostingId = JobId, RoundNumber = round, RoundType = roundType });

    [Fact]
    public async Task Khung_gio_cua_HM_CU_khong_con_hieu_luc()
    {
        // ADR-068: lịch rảnh là lịch của MỘT NGƯỜI. Sau khi HR Leader chuyển tin, giờ rảnh của người cũ
        // không nói gì về việc người mới có mặt được hay không.
        var now = DateTimeOffset.UtcNow;
        var uow = NewUow().Seed(new HiringManagerAvailability
        {
            JobPostingId = JobId, RoundNumber = 1, HiringManagerUserId = Guid.NewGuid(), // người không còn là HM
            StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(4),
        });

        Assert.Empty(await HmAvailabilitySupport.ActiveWindowsAsync(uow, JobId, 1, CancellationToken.None));
    }

    private static Task<Result<HmAvailabilitySaveResult>> Save(InMemoryUnitOfWork uow, int round)
    {
        var start = DateTimeOffset.UtcNow.AddDays(2);
        return new SetHmAvailabilityCommandHandler(uow, new RecordingNotificationService()).Handle(
            new SetHmAvailabilityCommand(JobId, round, new[] { Input(start, start.AddHours(3)) },
                HmId, AppRoles.HiringManager),
            CancellationToken.None);
    }

    [Fact]
    public async Task Vong_trac_nghiem_khong_nhan_lich_ranh()
    {
        // Bài thi trực tuyến không ai ngồi cùng và luật khớp giờ bỏ qua vòng đó. Nhận vào thì khung giờ
        // hiện trên màn xếp lịch của Recruiter như thể nó ràng buộc được gì.
        var uow = JobWithRound(1, "online_test");

        var res = await Save(uow, 1);

        Assert.True(res.IsFailure);
        Assert.Contains("trắc nghiệm", res.Error);
        Assert.Empty(uow.Repo<HiringManagerAvailability>().Items);
    }

    [Fact]
    public async Task Vong_phong_van_thi_HM_khai_lich_duoc()
    {
        var uow = JobWithRound(2, "technical");

        var res = await Save(uow, 2);

        Assert.True(res.IsSuccess);
        var saved = Assert.Single(uow.Repo<HiringManagerAvailability>().Items);
        Assert.Equal(2, saved.RoundNumber);
        Assert.Equal(HmId, saved.HiringManagerUserId);
    }

    [Fact]
    public async Task Chi_dem_dung_vong_dang_sua()
    {
        // HM sửa lịch vòng 1 thì buổi của vòng 2 không liên quan.
        var now = DateTimeOffset.UtcNow;
        var slot = new AvailabilitySlot
        {
            JobPostingId = JobId, RoundNumber = 2,
            StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(1), Capacity = 1, BookedCount = 1,
        };
        var uow = NewUow().Seed(slot)
            .Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id, round: 2));

        var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
            uow, JobId, 1, CancellationToken.None);

        Assert.Empty(affected);
    }
}
