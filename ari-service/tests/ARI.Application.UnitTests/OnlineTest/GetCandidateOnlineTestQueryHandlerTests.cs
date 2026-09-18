using System;
using ARI.Domain.Constants;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Lấy đề cho ứng viên (<see cref="GetCandidateOnlineTestQueryHandler"/>): KHÔNG lộ đáp án đúng,
/// chỉ trả câu hỏi khi CV đã duyệt, phản ánh trạng thái đã nộp, và bốc đề DETERMINISTIC (refresh không đổi đề).
/// </summary>
public class GetCandidateOnlineTestQueryHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();
    private const string Email = "cand@example.io";

    private static Task<Result<CandidateOnlineTestDto>> Run(InMemoryUnitOfWork uow, GetCandidateOnlineTestQuery q)
        => new GetCandidateOnlineTestQueryHandler(uow).Handle(q, CancellationToken.None);

    /// <summary>
    /// Dựng tin + hồ sơ + ngân hàng đề + vòng trắc nghiệm 30', kèm một ca đang MỞ (giờ hẹn vừa qua).
    ///
    /// Bài mở từ giờ hẹn và đóng sau đúng thời lượng bài, nên không gieo lịch thì MỌI ca test đều
    /// không nhận được đề — và test sẽ đo nhầm: nó tưởng đang kiểm luật duyệt CV trong khi thực tế
    /// bị chặn ở luật giờ.
    /// </summary>
    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app) Setup(
        string status = "screening", int perTest = 50, int bank = 3, bool scheduled = true, int durationMinutes = 30)
    {
        var job = OnlineTestData.Job(perTest: perTest);
        var app = OnlineTestData.Application(job.Id, _accountId, status: status, email: Email);
        var questions = Enumerable.Range(0, bank).Select(_ => OnlineTestData.Single(job.Id, 0)).ToArray();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(questions)
            .Seed(OnlineTestData.TestRound(job.Id, durationMinutes));

        if (scheduled)
        {
            var slot = new AvailabilitySlot
            {
                JobPostingId = job.Id, RoundNumber = 1,
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                EndTime = DateTimeOffset.UtcNow.AddMinutes(25),
                Capacity = null,
            };
            uow.Seed(slot).Seed(new InterviewBooking
            {
                ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
                Status = BookingStatus.Scheduled,
            });
        }

        return (uow, job, app);
    }

    // ---------- Khung giờ thi: [giờ hẹn, giờ hẹn + thời lượng) — ADR-072 ----------

    [Fact]
    public async Task Chua_xep_lich_thi_khong_co_de()
    {
        // Chưa có giờ hẹn thì không có cửa nào để mở — và ứng viên cũng chưa được mời làm bài.
        var (uow, _, app) = Setup(scheduled: false);

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.CanStart);
        Assert.Empty(res.Value.Questions);
        Assert.Null(res.Value.OpensAt);
    }

    [Fact]
    public async Task Chua_toi_gio_hen_thi_khong_co_de()
    {
        var (uow, job, app) = Setup(scheduled: false);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1,
            StartTime = DateTimeOffset.UtcNow.AddHours(2),
            EndTime = DateTimeOffset.UtcNow.AddHours(3),
            Capacity = null,
        };
        uow.Seed(slot).Seed(new InterviewBooking
        {
            ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
            Status = BookingStatus.Scheduled,
        });

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.False(res.Value.CanStart);
        Assert.Empty(res.Value.Questions);
        // Vẫn trả giờ mở để giao diện nói được "quay lại lúc mấy giờ", thay vì một ô trống.
        Assert.NotNull(res.Value.OpensAt);
    }

    [Fact]
    public async Task Het_thoi_luong_bai_thi_dong_bai_du_con_trong_mot_tieng()
    {
        // Trước ADR-072 cửa vào mở một tiếng bất kể bài dài bao nhiêu: ca 09:00, bài 30' vẫn nhận người
        // vào lúc 09:45 rồi cho họ nguyên 30 phút. Nay bài đóng đúng lúc giờ hẹn + thời lượng.
        var (uow, job, app) = Setup(scheduled: false);
        var start = DateTimeOffset.UtcNow.AddMinutes(-45);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1, StartTime = start, EndTime = start.AddMinutes(30), Capacity = null,
        };
        uow.Seed(slot).Seed(new InterviewBooking
        {
            ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
            Status = BookingStatus.Scheduled,
        });

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.False(res.Value.CanStart);
        Assert.Empty(res.Value.Questions);
        Assert.True(res.Value.Expired);
    }

    [Fact]
    public async Task Dung_gio_thi_mo_bai_va_gio_dong_la_gio_hen_cong_thoi_luong()
    {
        var (uow, _, app) = Setup();

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.Value.CanStart);
        Assert.NotEmpty(res.Value.Questions);
        Assert.Equal(res.Value.OpensAt!.Value.AddMinutes(30), res.Value.ClosesAt);
        Assert.Equal(30, res.Value.DurationMinutes);
        Assert.False(res.Value.Expired);
    }

    [Fact]
    public async Task Vao_muon_van_vao_duoc_nhung_gio_dong_khong_doi()
    {
        // Ca 09:00, bài 30', vào lúc 09:15 → vẫn làm được, nhưng đồng hồ đếm tới 09:30 (còn 15'),
        // không phải 09:45. Giờ đóng là của cả ca, không phải của từng người.
        var (uow, job, app) = Setup(scheduled: false);
        var start = DateTimeOffset.UtcNow.AddMinutes(-15);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1, StartTime = start, EndTime = start.AddMinutes(30), Capacity = null,
        };
        uow.Seed(slot).Seed(new InterviewBooking
        {
            ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
            Status = BookingStatus.Scheduled,
        });

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.Value.CanStart);
        Assert.Equal(start.AddMinutes(30), res.Value.ClosesAt);
        // Giờ server đi kèm để đồng hồ phía trình duyệt đếm theo server, không theo giờ máy ứng viên.
        Assert.NotNull(res.Value.ServerNow);
        var remaining = res.Value.ClosesAt!.Value - res.Value.ServerNow!.Value;
        Assert.InRange(remaining.TotalMinutes, 14.9, 15.1);
    }

    [Fact]
    public async Task Thoi_luong_doc_tu_vong_trac_nghiem()
    {
        // Một nguồn duy nhất (ADR-072): đổi số phút của vòng là đổi giờ đóng bài.
        var (uow, _, app) = Setup(durationMinutes: 45);

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.Equal(45, res.Value.DurationMinutes);
        Assert.Equal(res.Value.OpensAt!.Value.AddMinutes(45), res.Value.ClosesAt);
    }

    // ---------- Hết hạn ----------

    /// <summary>Hồ sơ có lịch thi đã bắt đầu <paramref name="hoursAgo"/> tiếng trước.</summary>
    private (InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app) ScheduledAgo(double hoursAgo)
    {
        var (uow, job, app) = Setup(scheduled: false);
        var slot = new AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1,
            StartTime = DateTimeOffset.UtcNow.AddHours(-hoursAgo),
            EndTime = DateTimeOffset.UtcNow.AddHours(-hoursAgo + 1),
            Capacity = null,
        };
        uow.Seed(slot).Seed(new InterviewBooking
        {
            ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1,
            Status = BookingStatus.Scheduled,
        });
        return (uow, app);
    }

    [Fact]
    public async Task Cua_da_dong_ma_chua_co_bai_thi_la_het_han_va_van_con_gio_hen()
    {
        // Lỗi đã gặp: ứng viên được hẹn giờ nhưng không vào làm, Portal lại báo "Chưa có giờ làm bài".
        // Giờ hẹn phải còn nguyên (lịch không bị huỷ) để giao diện nói được "đã hết hạn lúc mấy giờ".
        var (uow, app) = ScheduledAgo(3);

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.Value.Expired);
        Assert.False(res.Value.CanStart);
        Assert.False(res.Value.AlreadySubmitted);
        Assert.Empty(res.Value.Questions);
        Assert.NotNull(res.Value.OpensAt);
    }

    [Fact]
    public async Task Bai_he_thong_nop_thay_thi_la_het_han_khong_phai_da_nop()
    {
        var (uow, app) = ScheduledAgo(3);
        uow.Seed(new OnlineTestSubmission
        {
            ApplicationId = app.Id, RoundNumber = 1, SubmittedBy = OnlineTestSubmittedBy.System,
        });

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.Value.Expired);
        Assert.True(res.Value.AlreadySubmitted);
    }

    [Fact]
    public async Task Bai_ung_vien_tu_nop_thi_khong_phai_het_han()
    {
        // Người tự nộp (kể cả bài tự nộp lúc hết giờ) vẫn là người ĐÃ LÀM BÀI.
        var (uow, app) = ScheduledAgo(3);
        uow.Seed(OnlineTestData.Submission(app.Id, 40, passed: false));

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.False(res.Value.Expired);
        Assert.True(res.Value.AlreadySubmitted);
    }

    [Fact]
    public async Task Cv_passed_returns_questions_without_correct_answers()
    {
        var (uow, _, app) = Setup(status: "screening", bank: 3);

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.CvPassed);
        Assert.Equal(3, res.Value.TotalQuestions);
        Assert.Equal(3, res.Value.Questions.Count);
        Assert.False(res.Value.AlreadySubmitted);
        // DTO ứng viên (CandidateTestQuestionDto) không có trường đáp án đúng — đảm bảo ở compile-time.
        Assert.All(res.Value.Questions, q => Assert.NotEmpty(q.Options));
    }

    [Fact]
    public async Task Cv_not_passed_returns_metadata_but_hides_questions()
    {
        var (uow, _, app) = Setup(status: "cv_submitted", bank: 3);

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.CvPassed);
        Assert.Empty(res.Value.Questions);        // chưa duyệt CV → không lộ câu hỏi
        Assert.Equal(3, res.Value.TotalQuestions); // vẫn cho biết bài có bao nhiêu câu
    }

    [Fact]
    public async Task Already_submitted_state_is_reflected()
    {
        var (uow, _, app) = Setup(status: "screening", bank: 3);
        uow.Seed(new OnlineTestSubmission
        {
            ApplicationId = app.Id,
            RoundNumber = 1,
            Score = 80m,
            IsPassed = true,
        });

        var res = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        Assert.True(res.Value.AlreadySubmitted);
    }

    [Fact]
    public async Task Khong_lo_diem_diem_san_hay_ket_qua_cho_ung_vien()
    {
        // Điểm sàn là thông tin nội bộ, và kết quả chỉ công bố khi cả vòng đã chốt — ứng viên chỉ
        // biết "đã nộp bài". Khoá bằng phản chiếu để không ai vô tình thêm lại các trường đó:
        // giấu trên giao diện mà vẫn gửi số xuống trình duyệt thì mở tab mạng ra là đọc được.
        var names = typeof(ARI.Application.DTOs.CandidateOnlineTestDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("Score", names);
        Assert.DoesNotContain("IsPassed", names);
        Assert.DoesNotContain("PassScore", names);
    }

    [Fact]
    public async Task Draw_is_deterministic_across_repeated_reads()
    {
        // Ngân hàng lớn hơn số câu/bài → phải bốc; 2 lần đọc phải ra CÙNG bộ đề, cùng thứ tự.
        var (uow, _, app) = Setup(status: "screening", perTest: 5, bank: 20);

        var first = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));
        var second = await Run(uow, new GetCandidateOnlineTestQuery(app.Id, _accountId, Email));

        var idsA = first.Value.Questions.Select(q => q.Id).ToList();
        var idsB = second.Value.Questions.Select(q => q.Id).ToList();

        Assert.Equal(5, idsA.Count);
        Assert.Equal(idsA, idsB);
    }

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, new GetCandidateOnlineTestQuery(Guid.NewGuid(), _accountId, Email));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }
}
