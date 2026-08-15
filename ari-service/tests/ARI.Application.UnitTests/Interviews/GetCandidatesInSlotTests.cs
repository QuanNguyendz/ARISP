using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Danh sách ứng viên trong một ca cho nhân sự (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetCandidatesInSlotAsync</c>, test-plan B18).
///
/// Trọng tâm:
///  - <c>CandidateState</c> phân biệt được BA kết cục khác nhau (ứng viên báo bận / hệ thống huỷ vì
///    quá hạn / nhân sự loại hồ sơ) mà KHÔNG đọc nội dung <c>DeclineReason</c>.
///  - Phiên/đánh giá/mã phải lọc theo VÒNG, nếu không một phiên đang chạy ở vòng khác sẽ hiện thành
///    huy hiệu "Đang thực hiện" trên dòng của vòng này.
/// </summary>
public class GetCandidatesInSlotTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    private static Guid Owner => Guid.NewGuid();

    [Fact]
    public async Task Khong_phai_chu_tin_thi_bi_tu_choi()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    /// <summary>
    /// Ba booking có DeclineReason GIỐNG HỆT NHAU, chỉ khác `declined_by`. Nếu nhãn còn phụ thuộc
    /// văn xuôi thì test này sập — đó chính là lỗi cũ (người bị hệ thống huỷ vì quá hạn bị gắn nhãn
    /// "Từ chối (báo bận)").
    /// </summary>
    [Fact]
    public async Task Trang_thai_phan_biet_bao_ban_qua_han_va_bi_loai_du_ly_do_giong_nhau()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 5);

        var a1 = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var a2 = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var a3 = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "not_pass");

        var b1 = SchedulingData.Booking(a1.Id, slot.Id, status: BookingStatus.Declined, confirmation: "declined", declinedBy: BookingDeclinedBy.Candidate);
        var b2 = SchedulingData.Booking(a2.Id, slot.Id, status: BookingStatus.Declined, confirmation: "declined", declinedBy: BookingDeclinedBy.System);
        var b3 = SchedulingData.Booking(a3.Id, slot.Id, status: BookingStatus.Cancelled, confirmation: "declined", declinedBy: BookingDeclinedBy.Staff);
        b1.DeclineReason = b2.DeclineReason = b3.DeclineReason = "cùng một câu chữ";

        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(a1, a2, a3).Seed(b1, b2, b3);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var byBooking = System.Linq.Enumerable.ToDictionary(res.Value, d => d.BookingId);
        Assert.Equal(SlotCandidateState.DeclinedByCandidate, byBooking[b1.Id].CandidateState);
        Assert.Equal(SlotCandidateState.ExpiredNoResponse, byBooking[b2.Id].CandidateState);
        Assert.Equal(SlotCandidateState.RejectedByStaff, byBooking[b3.Id].CandidateState);

        // Không dòng nào còn chiếm chỗ.
        Assert.All(res.Value, d => Assert.False(d.OccupiesSeat));
    }

    [Fact]
    public async Task OccupiesSeat_dung_theo_trang_thai_booking()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 5);
        var appHeld = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var appFree = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var held = SchedulingData.Booking(appHeld.Id, slot.Id, status: BookingStatus.Scheduled, confirmation: "pending");
        var freed = SchedulingData.Booking(appFree.Id, slot.Id, status: BookingStatus.Declined, confirmation: "declined");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(appHeld, appFree).Seed(held, freed);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var byBooking = System.Linq.Enumerable.ToDictionary(res.Value, d => d.BookingId);
        Assert.True(byBooking[held.Id].OccupiesSeat);
        Assert.Equal(SlotCandidateState.Pending, byBooking[held.Id].CandidateState);
        Assert.False(byBooking[freed.Id].OccupiesSeat);
    }

    /// <summary>
    /// Hồ sơ bị loại KHÔNG còn ghi đè ConfirmationStatus/DeclineReason nữa: lý do thật của ứng viên
    /// phải giữ nguyên trên màn hình, còn "hồ sơ đã bị loại" đi riêng qua ApplicationStatus.
    /// </summary>
    [Fact]
    public async Task Ho_so_bi_loai_khong_ghi_de_ly_do_that_cua_ung_vien()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "not_pass");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1,
            status: BookingStatus.Cancelled, confirmation: "declined", declinedBy: BookingDeclinedBy.Staff);
        booking.DeclineReason = "Ứng viên đã bị loại khỏi quy trình tuyển dụng.";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(SlotCandidateState.RejectedByStaff, dto.CandidateState);
        Assert.Equal("not_pass", dto.ApplicationStatus);
        Assert.Equal(BookingStatus.Cancelled, dto.BookingStatus);
        Assert.Equal(1, dto.RoundNumber);
    }

    [Fact]
    public async Task Phien_that_danh_gia_va_ma_con_han_duoc_map_bo_qua_phien_thu()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var real = new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "completed", DurationSeconds = 1800 };
        var practice = new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "practice", Status = "completed", DurationSeconds = 900 };
        var eval = new Evaluation { ApplicationId = app.Id, SessionId = real.Id, SessionType = "real", AiVerdict = "pass", OverallScore = 72.6m, RoundNumber = 1 };
        var code = new InterviewCode { ApplicationId = app.Id, Code = "ABC123", RoundNumber = 1, ExpiresAt = DateTimeOffset.UtcNow.AddHours(2), UsedAt = null };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking)
            .Seed(real, practice).Seed(eval).Seed(code);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(real.Id, dto.SessionId);       // phiên THẬT, không phải practice
        Assert.Equal("completed", dto.SessionStatus);
        Assert.Equal(1800, dto.DurationSeconds);
        Assert.Equal("pass", dto.Verdict);
        Assert.Equal(73, dto.OverallScore);          // 72.6 → làm tròn
        Assert.Equal("ABC123", dto.InterviewCode);
        Assert.Equal(code.ExpiresAt, dto.CodeExpiresAt);
    }

    /// <summary>Bug "Tran Dung · Đang thực hiện": phiên vòng 2 rò sang dòng của vòng 1.</summary>
    [Fact]
    public async Task Phien_vong_khac_khong_ro_ri_sang_hang_nay()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slotR1 = SchedulingData.Slot(job.Id, round: 1);
        var bookingR1 = SchedulingData.Booking(app.Id, slotR1.Id, round: 1);

        var doneR1 = new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "completed", DurationSeconds = 1200 };
        var activeR2 = new InterviewSession { ApplicationId = app.Id, RoundNumber = 2, SessionType = "real", Status = "active" };
        var evalR1 = new Evaluation { ApplicationId = app.Id, SessionId = doneR1.Id, SessionType = "real", AiVerdict = "pass", OverallScore = 80m, RoundNumber = 1 };
        var evalR2 = new Evaluation { ApplicationId = app.Id, SessionId = activeR2.Id, SessionType = "real", AiVerdict = "not_pass", OverallScore = 40m, RoundNumber = 2 };
        var codeR2 = new InterviewCode { ApplicationId = app.Id, Code = "R2CODE", RoundNumber = 2, ExpiresAt = DateTimeOffset.UtcNow.AddHours(2) };

        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slotR1).Seed(bookingR1)
            .Seed(doneR1, activeR2).Seed(evalR1, evalR2).Seed(codeR2);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slotR1.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(doneR1.Id, dto.SessionId);
        Assert.Equal("completed", dto.SessionStatus);   // KHÔNG phải "active" của vòng 2
        Assert.Equal("pass", dto.Verdict);              // đánh giá của đúng vòng
        Assert.Null(dto.InterviewCode);                 // mã của vòng 2 không rò sang
    }

    /// <summary>
    /// Chọn phiên mới nhất theo THỜI GIAN. Bản cũ sắp theo Guid nên kết quả ổn định-nhưng-ngẫu-nhiên:
    /// hồ sơ này luôn đúng, hồ sơ kia luôn sai, và lỗi trông như dữ liệu thật.
    /// </summary>
    [Fact]
    public async Task Chon_phien_moi_nhat_theo_thoi_gian_khong_theo_Guid()
    {
        var owner = Owner;
        var job = SchedulingData.Job(owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);

        var older = new InterviewSession
        {
            Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), // Guid LỚN nhất nhưng CŨ hơn
            ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "aborted",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-5), CreatedAt = DateTimeOffset.UtcNow.AddHours(-5),
        };
        var newer = new InterviewSession
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), // Guid NHỎ nhất nhưng MỚI hơn
            ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "completed",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1), CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking).Seed(older, newer);

        var res = await Svc(uow).GetCandidatesInSlotAsync(slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.Equal(newer.Id, Assert.Single(res.Value).SessionId);
    }
}
