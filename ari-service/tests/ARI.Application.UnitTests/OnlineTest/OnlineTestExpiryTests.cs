using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Bài trắc nghiệm HẾT HẠN = hệ thống nộp thay một bài trống (<see cref="OnlineTestExpiry"/>).
///
/// Lỗi mà bộ test này khoá lại: ứng viên được hẹn giờ thi nhưng không vào làm từng bị tác vụ
/// "không tham dự" (ADR-059) xử lý như vòng phỏng vấn — lịch bị huỷ, chỗ trong ca bị TRẢ lại
/// ("Đã đặt 0/1"), hồ sơ tự sang <c>not_pass</c>, và Portal báo "Chưa có giờ làm bài" với một
/// người đã được hẹn. Đúng ra: chỉ ứng viên TỪ CHỐI mới trả chỗ; không vào làm thì vòng vẫn có
/// kết quả (0 điểm) và Recruiter quyết định.
/// </summary>
public class OnlineTestExpiryTests
{
    // Thời lượng bài 30' → bài đóng lúc giờ hẹn + 30', hạn chót = + 1' trễ mạng = 31' (ADR-072).
    private static readonly TimeSpan PastDeadline = TimeSpan.FromMinutes(40);

    private sealed record Seeded(
        InMemoryUnitOfWork Uow, JobPosting Job, ARI.Domain.Entities.Application App,
        AvailabilitySlot Slot, InterviewBooking Booking);

    private static Seeded Setup(
        TimeSpan startedAgo,
        string roundType = "online_test",
        string bookingStatus = BookingStatus.Scheduled,
        string appStatus = ApplicationStatuses.Interview,
        int durationMinutes = 30)
    {
        var job = OnlineTestData.Job(passScore: 70, perTest: 3);
        var app = OnlineTestData.Application(job.Id, Guid.NewGuid(), status: appStatus);
        var now = DateTimeOffset.UtcNow;
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1,
            StartTime = now - startedAgo, EndTime = now - startedAgo + TimeSpan.FromMinutes(15),
            Capacity = 1, BookedCount = 1,
        };
        var booking = new InterviewBooking
        {
            ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
            Status = bookingStatus,
        };

        var uow = new InMemoryUnitOfWork()
            .Seed(job).Seed(app).Seed(slot).Seed(booking)
            .Seed(new InterviewRoundConfig
            {
                JobPostingId = job.Id, RoundNumber = 1, RoundType = roundType, MaxDurationMinutes = durationMinutes,
            })
            .Seed(OnlineTestData.Single(job.Id, 0), OnlineTestData.Single(job.Id, 1), OnlineTestData.Single(job.Id, 2));

        return new Seeded(uow, job, app, slot, booking);
    }

    private static Task<System.Collections.Generic.List<OnlineTestExpiry.ExpiredSubmission>> Run(InMemoryUnitOfWork uow)
        => OnlineTestExpiry.AutoSubmitExpiredAsync(uow, DateTimeOffset.UtcNow, CancellationToken.None);

    // ---------- Nộp thay khi hết hạn ----------

    [Fact]
    public async Task Qua_han_chot_ma_chua_co_bai_thi_he_thong_nop_thay_bai_trong()
    {
        var s = Setup(PastDeadline);

        var created = await Run(s.Uow);

        var sub = Assert.Single(s.Uow.Repo<OnlineTestSubmission>().Items);
        Assert.Equal(OnlineTestSubmittedBy.System, sub.SubmittedBy);
        Assert.Equal(s.App.Id, sub.ApplicationId);
        Assert.Equal(1, sub.RoundNumber);
        // Chấm như một bài trống: cùng bộ đề đã bốc, cùng hàm chấm với bài ứng viên nộp.
        Assert.Equal(0m, sub.Score);
        Assert.False(sub.IsPassed);
        Assert.Equal(0, sub.CorrectCount);
        Assert.Equal(3, sub.TotalQuestions);

        var e = Assert.Single(created);
        Assert.Equal(sub.Id, e.SubmissionId);
        Assert.Equal(s.App.CandidateAccountId, e.CandidateAccountId);
    }

    [Fact]
    public async Task Nop_thay_KHONG_huy_lich_KHONG_tra_cho_KHONG_doi_trang_thai_ho_so()
    {
        // Đây chính là ba thứ đường "không tham dự" đã làm sai: huỷ lịch, trả chỗ, tự đánh trượt.
        var s = Setup(PastDeadline);

        await Run(s.Uow);

        var booking = Assert.Single(s.Uow.Repo<InterviewBooking>().Items);
        Assert.Equal(BookingStatus.Scheduled, booking.Status);
        Assert.Null(booking.DeclinedBy);
        Assert.Equal(1, Assert.Single(s.Uow.Repo<AvailabilitySlot>().Items).BookedCount);
        Assert.Equal(ApplicationStatuses.Interview,
            Assert.Single(s.Uow.Repo<ARI.Domain.Entities.Application>().Items).Status);
    }

    // ---------- Chưa tới hạn chót ----------

    [Fact]
    public async Task Bai_vua_dong_nhung_chua_het_do_tre_mang_thi_chua_nop_thay()
    {
        // Người đang làm tự nộp ĐÚNG giờ đóng, và bài đó còn đang trên đường truyền — nộp thay lúc này
        // là giành mất bài thật của họ.
        var s = Setup(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(20));

        var created = await Run(s.Uow);

        Assert.Empty(created);
        Assert.Empty(s.Uow.Repo<OnlineTestSubmission>().Items);
    }

    [Theory]
    [InlineData(60, false)] // bài 60' vừa đóng, còn 1' trễ mạng → chưa tới hạn chót
    [InlineData(62, true)]
    public async Task Han_chot_tinh_theo_thoi_luong_cua_vong_thi(int minutesAgo, bool expected)
    {
        var s = Setup(TimeSpan.FromMinutes(minutesAgo), durationMinutes: 60);

        await Run(s.Uow);

        Assert.Equal(expected, s.Uow.Repo<OnlineTestSubmission>().Items.Any());
    }

    // ---------- Không đụng tới ----------

    [Fact]
    public async Task Da_co_bai_cua_ung_vien_thi_giu_nguyen()
    {
        var s = Setup(PastDeadline);
        s.Uow.Seed(OnlineTestData.Submission(s.App.Id, 80, passed: true, correct: 2, total: 3));

        var created = await Run(s.Uow);

        Assert.Empty(created);
        var sub = Assert.Single(s.Uow.Repo<OnlineTestSubmission>().Items);
        Assert.Equal(OnlineTestSubmittedBy.Candidate, sub.SubmittedBy);
        Assert.Equal(80m, sub.Score);
    }

    [Fact]
    public async Task Chay_lai_khong_sinh_bai_thu_hai()
    {
        var s = Setup(PastDeadline);

        await Run(s.Uow);
        var second = await Run(s.Uow);

        Assert.Empty(second);
        Assert.Single(s.Uow.Repo<OnlineTestSubmission>().Items);
    }

    [Theory]
    [InlineData(BookingStatus.Declined)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task Lich_da_dong_thi_khong_co_bai_nao_de_het_han(string bookingStatus)
    {
        // Người TỪ CHỐI tham dự đã trả chỗ và không còn bài thi nào — nộp thay cho họ là bịa ra
        // một lượt thi họ đã nói trước là sẽ không dự.
        var s = Setup(PastDeadline, bookingStatus: bookingStatus);

        Assert.Empty(await Run(s.Uow));
        Assert.Empty(s.Uow.Repo<OnlineTestSubmission>().Items);
    }

    [Theory]
    [InlineData("screening")]
    [InlineData("technical")]
    public async Task Vong_phong_van_khong_bi_nop_thay(string roundType)
    {
        var s = Setup(PastDeadline, roundType: roundType);

        Assert.Empty(await Run(s.Uow));
        Assert.Empty(s.Uow.Repo<OnlineTestSubmission>().Items);
    }

    [Theory]
    [InlineData(ApplicationStatuses.NotPass)]
    [InlineData(ApplicationStatuses.Withdrawn)]
    public async Task Ho_so_da_dong_thi_khong_ghi_them_ket_qua(string appStatus)
    {
        var s = Setup(PastDeadline, appStatus: appStatus);

        Assert.Empty(await Run(s.Uow));
        Assert.Empty(s.Uow.Repo<OnlineTestSubmission>().Items);
    }
}
