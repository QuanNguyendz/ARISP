using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.Options;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Vòng TRẮC NGHIỆM trên Candidate Portal — danh sách hồ sơ và chi tiết hồ sơ.
///
/// Lỗi mà bộ test này khoá lại: Portal suy "lỡ buổi / quá hạn" bằng cách tìm một phiên phỏng vấn
/// thật, mà vòng trắc nghiệm KHÔNG BAO GIỜ có phiên nào. Kết quả: ứng viên đã nộp bài vòng 1 và đang
/// có lịch vòng 2 vẫn đọc "Bạn đã lỡ buổi phỏng vấn vòng 1… hồ sơ dừng lại ở vòng này", còn thẻ vòng 1
/// gắn nhãn "Quá hạn". Bằng chứng của vòng trắc nghiệm là BÀI ĐÃ NỘP.
/// </summary>
public class PortalOnlineTestRoundTests
{
    private sealed record Seeded(InMemoryUnitOfWork Uow, ARI.Domain.Entities.Application App, JobPosting Job);

    /// <summary>Tin: vòng 1 trắc nghiệm, vòng 2 sơ loại. Hồ sơ đang ở chuỗi vòng, có lịch vòng 1.</summary>
    private static Seeded Setup(TimeSpan testStartedAgo)
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id, status: ApplicationStatuses.Interview);
        var testSlot = Slot(job.Id, 1, DateTimeOffset.UtcNow - testStartedAgo, TimeSpan.FromMinutes(15));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(testSlot)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" })
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2, RoundType = "screening" })
            .Seed(new InterviewInvite
            {
                Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 1, TokenHash = "x",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            })
            .Seed(Booking(app.Id, testSlot.Id, 1));
        return new Seeded(uow, app, job);
    }

    private static AvailabilitySlot Slot(Guid jobId, int round, DateTimeOffset start, TimeSpan length) => new()
    {
        Id = Guid.NewGuid(), JobPostingId = jobId, RoundNumber = round,
        StartTime = start, EndTime = start + length, Timezone = "Asia/Ho_Chi_Minh", Capacity = 1,
    };

    private static InterviewBooking Booking(Guid appId, Guid slotId, int round) => new()
    {
        Id = Guid.NewGuid(), ApplicationId = appId, AvailabilitySlotId = slotId, RoundNumber = round,
        Status = BookingStatus.Scheduled, ConfirmationStatus = BookingConfirmationStatus.Pending,
    };

    private static OnlineTestSubmission Submission(Guid appId, string by = OnlineTestSubmittedBy.Candidate) => new()
    {
        ApplicationId = appId, RoundNumber = 1, Score = 80, IsPassed = true, SubmittedBy = by,
    };

    // ---------- Danh sách hồ sơ ----------

    private static async Task<JsonElement> List(InMemoryUnitOfWork uow)
    {
        var res = await new GetMyApplicationsQueryHandler(uow, new RecordingFileStorage(), new InterviewOptions())
            .Handle(new GetMyApplicationsQuery(PortalAppsData.CandidateId, null), CancellationToken.None);
        return PortalAppsData.Json(res)[0];
    }

    [Fact]
    public async Task Da_nop_bai_trac_nghiem_thi_khong_bi_bao_lo_buoi()
    {
        var s = Setup(TimeSpan.FromDays(1));
        s.Uow.Seed(Submission(s.App.Id));

        var card = await List(s.Uow);

        Assert.Equal(JsonValueKind.Null, card.GetProperty("MissedInterview").ValueKind);
    }

    [Fact]
    public async Task Co_lich_vong_2_thi_vong_dang_hoat_dong_la_vong_2_va_goi_dung_loai_vong()
    {
        // Qua vòng trắc nghiệm = Recruiter xếp thẳng lịch vòng 2, không có lời mời vòng 2 nào.
        var s = Setup(TimeSpan.FromDays(1));
        s.Uow.Seed(Submission(s.App.Id));
        var round2 = Slot(s.Job.Id, 2, DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromMinutes(30));
        s.Uow.Seed(round2).Seed(Booking(s.App.Id, round2.Id, 2));

        var card = await List(s.Uow);

        Assert.Equal(2, card.GetProperty("ActiveRound").GetInt32());
        Assert.Equal("screening", card.GetProperty("UpcomingInterview").GetProperty("RoundType").GetString());
    }

    [Fact]
    public async Task Lich_sap_toi_cua_vong_trac_nghiem_tra_kem_loai_vong()
    {
        var s = Setup(TimeSpan.FromHours(-3)); // bài thi mở sau 3 tiếng nữa

        var card = await List(s.Uow);

        Assert.Equal("online_test", card.GetProperty("UpcomingInterview").GetProperty("RoundType").GetString());
    }

    [Fact]
    public async Task Vong_phong_van_qua_gio_khong_co_phien_van_la_lo_buoi()
    {
        // Luật "lỡ buổi" của vòng hội thoại giữ nguyên — chỉ vòng trắc nghiệm được tách ra.
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id, status: ApplicationStatuses.Interview);
        var slot = Slot(job.Id, 1, DateTimeOffset.UtcNow.AddDays(-1), TimeSpan.FromMinutes(30));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "technical" })
            .Seed(Booking(app.Id, slot.Id, 1));

        var card = await List(uow);

        Assert.NotEqual(JsonValueKind.Null, card.GetProperty("MissedInterview").ValueKind);
    }

    // ---------- Chi tiết hồ sơ ----------

    private static async Task<string> Round1Status(InMemoryUnitOfWork uow, Guid appId)
    {
        var res = await new GetMyApplicationDetailQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetMyApplicationDetailQuery(appId, PortalAppsData.CandidateId, PortalAppsData.Email), CancellationToken.None);
        var sessions = PortalAppsData.Json(res).GetProperty("Sessions");
        return sessions.EnumerateArray()
            .First(x => x.GetProperty("RoundNumber").GetInt32() == 1)
            .GetProperty("Status").GetString()!;
    }

    [Fact]
    public async Task Vong_trac_nghiem_da_nop_bai_la_completed_khong_phai_qua_han()
    {
        var s = Setup(TimeSpan.FromDays(1));
        s.Uow.Seed(Submission(s.App.Id));

        Assert.Equal("completed", await Round1Status(s.Uow, s.App.Id));
    }

    [Fact]
    public async Task Vong_trac_nghiem_he_thong_nop_thay_la_het_han()
    {
        var s = Setup(TimeSpan.FromDays(1));
        s.Uow.Seed(Submission(s.App.Id, OnlineTestSubmittedBy.System));

        Assert.Equal("expired", await Round1Status(s.Uow, s.App.Id));
    }

    [Theory]
    [InlineData(30, "scheduled")]   // ca 15' đã hết nhưng cửa vào (1 tiếng) vẫn mở
    [InlineData(90, "expired")]     // cửa vào đã đóng, chưa có bài
    public async Task Vong_trac_nghiem_chua_co_bai_tinh_theo_cua_vao_khong_theo_gio_ket_thuc_ca(
        int minutesAgo, string expected)
    {
        var s = Setup(TimeSpan.FromMinutes(minutesAgo));

        Assert.Equal(expected, await Round1Status(s.Uow, s.App.Id));
    }
}
