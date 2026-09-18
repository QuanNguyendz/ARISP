using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Cấu hình bài thi trắc nghiệm theo job (<see cref="UpdateOnlineTestSettingsCommandHandler"/>, test-plan B6):
/// biên các giá trị (điểm sàn 0–100, số câu 1–200, thời lượng 1–300') được kiểm TRƯỚC khi tra tin,
/// phân quyền chủ tin, và ghi 3 field + <c>UpdatedAt</c> khi hợp lệ.
/// </summary>
public class UpdateOnlineTestSettingsCommandHandlerTests
{
    private static Task<Result<OnlineTestBankDto>> Run(InMemoryUnitOfWork uow, UpdateOnlineTestSettingsCommand cmd)
        => new UpdateOnlineTestSettingsCommandHandler(uow).Handle(cmd, CancellationToken.None);

    public static IEnumerable<object[]> OutOfRange()
    {
        yield return new object[] { 101, 50, 30, "Điểm sàn" };
        yield return new object[] { -1, 50, 30, "Điểm sàn" };
        yield return new object[] { 70, 0, 30, "Số câu mỗi bài" };
        yield return new object[] { 70, 201, 30, "Số câu mỗi bài" };
        yield return new object[] { 70, 50, 0, "Thời lượng" };
        yield return new object[] { 70, 50, 301, "Thời lượng" };
    }

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public async Task Out_of_range_settings_are_rejected_before_lookup(int pass, int perTest, int duration, string fragment)
    {
        var uow = new InMemoryUnitOfWork(); // rỗng: validate chặn trước khi tra tin

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(
            Guid.NewGuid(), pass, perTest, duration, Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Contains(fragment, res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData(100, 200, 300)]
    [InlineData(0, 1, 1)]
    public async Task Owner_can_save_boundary_values(int pass, int perTest, int duration)
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(passScore: 70, perTest: 50, owner: owner);
        var round = OnlineTestData.TestRound(job.Id, durationMinutes: 30);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(round);
        var before = DateTimeOffset.UtcNow;

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, pass, perTest, duration, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(pass, res.Value.PassScore);
        Assert.Equal(perTest, res.Value.QuestionsPerTest);
        Assert.Equal(duration, res.Value.DurationMinutes);
        Assert.Equal(pass, job.OnlineTestPassScore);
        Assert.Equal(perTest, job.OnlineTestQuestionsPerTest);
        // Thời lượng ghi vào VÒNG trắc nghiệm — cùng cột với ô "Số phút" ở màn tạo tin (ADR-072).
        Assert.Equal(duration, round.MaxDurationMinutes);
        Assert.True(job.UpdatedAt >= before);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ---------- Thời lượng = số phút của vòng trắc nghiệm (ADR-072) ----------

    [Fact]
    public async Task Doc_thoi_luong_tu_vong_trac_nghiem()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(OnlineTestData.TestRound(job.Id, durationMinutes: 45));

        var res = await new GetOnlineTestBankQueryHandler(uow).Handle(
            new GetOnlineTestBankQuery(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.Equal(45, res.Value.DurationMinutes);
    }

    [Fact]
    public async Task Tin_chua_co_vong_trac_nghiem_thi_khong_co_cho_luu_thoi_luong()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var withDuration = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, 80, 30, 45, owner, AppRoles.Recruiter));
        Assert.True(withDuration.IsFailure);
        Assert.Contains("chưa có vòng trắc nghiệm", withDuration.Error);
        Assert.Equal(0, uow.SaveChangesCount);

        // Không gửi thời lượng thì phần còn lại vẫn lưu được, và màn hình biết là chưa có thời lượng.
        var withoutDuration = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, 80, 30, null, owner, AppRoles.Recruiter));
        Assert.True(withoutDuration.IsSuccess);
        Assert.Null(withoutDuration.Value.DurationMinutes);
        Assert.Equal(80, job.OnlineTestPassScore);
    }

    private static (InMemoryUnitOfWork Uow, ARI.Domain.Entities.JobPosting Job, ARI.Domain.Entities.InterviewRoundConfig Round, Guid Owner)
        JobWithBooking(TimeSpan startsIn, bool submitted = false)
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var round = OnlineTestData.TestRound(job.Id, durationMinutes: 30);
        var app = OnlineTestData.Application(job.Id, Guid.NewGuid());
        var start = DateTimeOffset.UtcNow + startsIn;
        var slot = new ARI.Domain.Entities.AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1, StartTime = start, EndTime = start.AddMinutes(30), Capacity = null,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(round).Seed(app).Seed(slot)
            .Seed(new ARI.Domain.Entities.InterviewBooking
            {
                ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1, Status = BookingStatus.Scheduled,
            });
        if (submitted) uow.Seed(OnlineTestData.Submission(app.Id, 50, passed: false));
        return (uow, job, round, owner);
    }

    [Theory]
    [InlineData(120)]  // ca ngày mai: ứng viên đã nhận thư ghi giờ đóng bài
    [InlineData(-10)]  // ca đang mở: ứng viên có thể đang làm bài
    public async Task Khong_doi_duoc_thoi_luong_khi_con_ung_vien_cho_thi(int startsInMinutes)
    {
        var s = JobWithBooking(TimeSpan.FromMinutes(startsInMinutes));

        var res = await Run(s.Uow, new UpdateOnlineTestSettingsCommand(s.Job.Id, 70, 50, 45, s.Owner, AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(OnlineTestWindow.DurationLockedCode, res.ErrorCode);
        Assert.Equal(30, s.Round.MaxDurationMinutes);
        Assert.Equal(0, s.Uow.SaveChangesCount);
    }

    [Fact]
    public async Task Giu_nguyen_thoi_luong_thi_van_sua_duoc_diem_san_khi_dang_co_ca_thi()
    {
        // Luật chỉ chặn việc ĐỔI thời lượng — không được khoá luôn cả điểm sàn / số câu.
        var s = JobWithBooking(TimeSpan.FromMinutes(-10));

        var res = await Run(s.Uow, new UpdateOnlineTestSettingsCommand(s.Job.Id, 80, 50, 30, s.Owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(80, s.Job.OnlineTestPassScore);
    }

    [Theory]
    [InlineData(-60, false)] // ca đã đóng (bài 30')
    [InlineData(-10, true)]  // ca đang mở nhưng ứng viên đã nộp bài
    public async Task Ca_thi_da_xong_thi_khong_chan_doi_thoi_luong(int startsInMinutes, bool submitted)
    {
        var s = JobWithBooking(TimeSpan.FromMinutes(startsInMinutes), submitted);

        var res = await Run(s.Uow, new UpdateOnlineTestSettingsCommand(s.Job.Id, 70, 50, 45, s.Owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        Assert.Equal(45, s.Round.MaxDurationMinutes);
    }

    [Fact]
    public async Task Doi_thoi_luong_keo_gio_ket_thuc_cua_ca_thi_chua_dong()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var future = DateTimeOffset.UtcNow.AddDays(1);
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        var upcoming = new ARI.Domain.Entities.AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1, StartTime = future, EndTime = future.AddMinutes(30), Capacity = null,
        };
        var closed = new ARI.Domain.Entities.AvailabilitySlot
        {
            JobPostingId = job.Id, RoundNumber = 1, StartTime = past, EndTime = past.AddMinutes(30), Capacity = null,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(OnlineTestData.TestRound(job.Id, 30)).Seed(upcoming, closed);

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, 70, 50, 45, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        // Giờ kết thúc của ca thi là giờ đóng bài — màn xếp lịch và thư mời in đúng khung mới.
        Assert.Equal(future.AddMinutes(45), upcoming.EndTime);
        // Ca đã diễn ra là lịch sử với thời lượng cũ.
        Assert.Equal(past.AddMinutes(30), closed.EndTime);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new UpdateOnlineTestSettingsCommand(job.Id, 80, 30, 45, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền cấu hình bài thi", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
