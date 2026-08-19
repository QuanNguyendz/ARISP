using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.Options;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

internal static class PortalAppsData
{
    public static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public const string Email = "candidate@example.com";

    public static ARI.Domain.Entities.Application App(Guid jobId, string status = "cv_submitted", Guid? owner = null,
        string email = Email, Guid? analysisId = null, string? cvFileUrl = null, DateTimeOffset? updatedAt = null)
        => new()
        {
            Id = Guid.NewGuid(), JobPostingId = jobId, CandidateAccountId = owner ?? CandidateId,
            CandidateEmail = email, CandidateName = "A", Status = status, CvJdAnalysisId = analysisId,
            CvFileUrl = cvFileUrl, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
        };

    /// <summary>Gán CandidateAccountId ĐÚNG giá trị truyền vào (kể cả null) — cho nhánh auto-link/IDOR.</summary>
    public static ARI.Domain.Entities.Application AppRaw(Guid jobId, Guid? owner, string email = Email)
        => new()
        {
            Id = Guid.NewGuid(), JobPostingId = jobId, CandidateAccountId = owner, CandidateEmail = email,
            CandidateName = "A", Status = "cv_submitted", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        };

    public static InterviewSession Session(Guid appId, string sessionType = "real", int round = 1, string status = "completed",
        string? recordingUrl = null) => new()
        {
            Id = Guid.NewGuid(), ApplicationId = appId, RoundNumber = round, RoundType = "technical",
            InterviewLanguage = "vi", SessionType = sessionType, Status = status, RecordingUrl = recordingUrl,
        };

    public static Evaluation Eval(Guid sessionId, Guid appId, string verdict = "pass", decimal score = 80m) => new()
    {
        Id = Guid.NewGuid(), SessionId = sessionId, ApplicationId = appId, RoundNumber = 1, SessionType = "real",
        AiVerdict = verdict, OverallScore = score,
    };

    public static ARI.Domain.Entities.HrReview Review(Guid evalId, bool shareEval = false, bool shareTranscript = false,
        bool shareRecording = false, bool shareFeedback = false, string? feedback = null, string finalVerdict = "pass") => new()
        {
            Id = Guid.NewGuid(), EvaluationId = evalId, ReviewedByUserId = Guid.NewGuid(),
            ShareEvaluation = shareEval, ShareTranscript = shareTranscript, ShareRecording = shareRecording,
            ShareFeedback = shareFeedback, CandidateFeedback = feedback, FinalVerdict = finalVerdict,
        };

    public static JsonElement Json(Result<object> res)
        => JsonDocument.Parse(JsonSerializer.Serialize(res.Value)).RootElement.Clone();
}

/// <summary>
/// Danh sách hồ sơ của chính ứng viên (<see cref="GetMyApplicationsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetMyApplications" (UTCID01–16). Trả <c>Result&lt;object&gt;</c> (mảng ẩn danh) → assert qua JSON.
/// Lưu ý nhánh lỗi: handler có try/catch bọc toàn thân + fail-safe riêng cho auto-link SQL, nên
/// UTCID14 KHÔNG ném (SQL lỗi bị nuốt), còn UTCID15/16 trả <c>Result.Failure("Lỗi tải hồ sơ: …")</c> chứ không ném.
/// </summary>
public class GetMyApplicationsQueryHandlerTests
{
    private static GetMyApplicationsQueryHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage? storage = null, InterviewOptions? opts = null)
        => new(uow, storage ?? new RecordingFileStorage(), opts ?? new InterviewOptions());

    private static Task<Result<object>> Run(GetMyApplicationsQueryHandler h, string? email)
        => h.Handle(new GetMyApplicationsQuery(PortalAppsData.CandidateId, email), CancellationToken.None);

    [Fact]
    public async Task UTCID01_No_apps_email_null_skips_autolink()
    {
        var uow = new InMemoryUnitOfWork();
        var sqlCalled = false;
        uow.OnExecuteSqlRaw = (_, _, _) => { sqlCalled = true; return Task.FromResult(0); };

        var res = await Run(Handler(uow), email: null);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, PortalAppsData.Json(res).GetArrayLength());
        Assert.False(sqlCalled);
    }

    [Fact]
    public async Task UTCID02_No_apps_email_runs_autolink()
    {
        var uow = new InMemoryUnitOfWork();
        var sqlCalled = false;
        uow.OnExecuteSqlRaw = (_, _, _) => { sqlCalled = true; return Task.FromResult(0); };

        var res = await Run(Handler(uow), email: PortalAppsData.Email);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, PortalAppsData.Json(res).GetArrayLength());
        Assert.True(sqlCalled);
    }

    [Fact]
    public async Task UTCID03_One_basic_application()
    {
        var job = JobBoardData.PublicJob(title: "Backend Developer");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PortalAppsData.App(job.Id));
        var res = await Run(Handler(uow), null);
        var arr = PortalAppsData.Json(res);
        Assert.Equal(1, arr.GetArrayLength());
        Assert.Equal(1, arr[0].GetProperty("ActiveRound").GetInt32());
        Assert.Equal(0, arr[0].GetProperty("Rounds").GetArrayLength());
    }

    [Fact]
    public async Task UTCID04_Ordered_by_updated_at_desc()
    {
        var job = JobBoardData.PublicJob();
        var newer = PortalAppsData.App(job.Id, updatedAt: DateTimeOffset.UtcNow);
        var older = PortalAppsData.App(job.Id, updatedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(older, newer);
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.Equal(newer.Id.ToString(), arr[0].GetProperty("Id").GetString());
    }

    [Fact]
    public async Task UTCID05_Missing_job_null_fields()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalAppsData.App(Guid.NewGuid()));   // không seed job
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.Equal(JsonValueKind.Null, arr[0].GetProperty("JobTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, arr[0].GetProperty("Location").ValueKind);
    }

    [Fact]
    public async Task UTCID06_Missing_analysis_null_match_score()
    {
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PortalAppsData.App(job.Id, analysisId: Guid.NewGuid()));
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.Equal(JsonValueKind.Null, arr[0].GetProperty("MatchScore").ValueKind);
    }

    [Fact]
    public async Task UTCID07_Resolved_cv_file_url()
    {
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PortalAppsData.App(job.Id, cvFileUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetUrlResult = "/files/resolved" };
        var arr = PortalAppsData.Json(await Run(Handler(uow, storage), null));
        Assert.Equal("/files/resolved", arr[0].GetProperty("CvFileUrl").GetString());
    }

    [Fact]
    public async Task UTCID08_Practice_limit_reached()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PortalAppsData.Session(app.Id, sessionType: "practice", round: 1));
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));   // PracticeAttemptsPerRound=1
        Assert.False(arr[0].GetProperty("PracticeAvailable").GetBoolean());
    }

    [Fact]
    public async Task UTCID09_Real_done_disables_practice()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PortalAppsData.Session(app.Id, sessionType: "real", round: 1));
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.False(arr[0].GetProperty("PracticeAvailable").GetBoolean());
    }

    [Fact]
    public async Task UTCID10_Latest_invite_sets_active_round()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewInvite { Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 2, TokenHash = "x", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.Equal(2, arr[0].GetProperty("ActiveRound").GetInt32());
    }

    [Fact]
    public async Task UTCID11_Completed_unshared_eval_pending_hidden_verdict()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var session = PortalAppsData.Session(app.Id, status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(PortalAppsData.Eval(session.Id, app.Id));
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.True(arr[0].GetProperty("PendingHrReview").GetBoolean());
        Assert.Equal(JsonValueKind.Null, arr[0].GetProperty("Rounds")[0].GetProperty("Verdict").ValueKind);
    }

    [Fact]
    public async Task UTCID12_Shared_eval_and_feedback()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var session = PortalAppsData.Session(app.Id, status: "completed");
        var eval = PortalAppsData.Eval(session.Id, app.Id, verdict: "pass", score: 80m);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(eval)
            .Seed(PortalAppsData.Review(eval.Id, shareEval: true, shareFeedback: true, feedback: "Great", finalVerdict: "pass"));
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.Equal("pass", arr[0].GetProperty("Rounds")[0].GetProperty("Verdict").GetString());
        Assert.Equal(80, arr[0].GetProperty("Rounds")[0].GetProperty("OverallScore").GetInt32());
        Assert.Equal("Great", arr[0].GetProperty("HrFeedback").GetString());
    }

    [Fact]
    public async Task UTCID13_Active_code_and_future_booking()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var slot = new AvailabilitySlot { Id = Guid.NewGuid(), JobPostingId = job.Id, RoundNumber = 1, StartTime = DateTimeOffset.UtcNow.AddDays(1), EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(1), Timezone = "UTC", Capacity = 5 };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 1, Code = "ABC123", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() })
            .Seed(new InterviewBooking { Id = Guid.NewGuid(), ApplicationId = app.Id, AvailabilitySlotId = slot.Id, RoundNumber = 1, Status = "scheduled" })
            .Seed(slot);
        var arr = PortalAppsData.Json(await Run(Handler(uow), null));
        Assert.NotEqual(JsonValueKind.Null, arr[0].GetProperty("InterviewCode").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, arr[0].GetProperty("UpcomingInterview").ValueKind);
    }

    [Fact]
    public async Task UTCID14_Autolink_sql_error_is_swallowed()
    {
        // Báo cáo kỳ vọng "Throws"; handler có fail-safe try/catch quanh auto-link → nuốt lỗi, vẫn trả Success.
        var uow = new InMemoryUnitOfWork();
        uow.OnExecuteSqlRaw = (_, _, _) => throw new Exception("SQL Error");
        var res = await Run(Handler(uow), email: PortalAppsData.Email);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, PortalAppsData.Json(res).GetArrayLength());
    }

    [Fact]
    public async Task UTCID15_Application_query_error_becomes_failure()
    {
        // Toàn thân bọc try/catch → lỗi query hồ sơ trả Result.Failure("Lỗi tải hồ sơ: …"), KHÔNG ném.
        var uow = new InMemoryUnitOfWork().FailRepo<ARI.Domain.Entities.Application>("Application DB Error");
        var res = await Run(Handler(uow), null);
        Assert.True(res.IsFailure);
        Assert.Contains("Application DB Error", res.Error);
    }

    [Fact]
    public async Task UTCID16_Cv_url_error_becomes_failure()
    {
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PortalAppsData.App(job.Id, cvFileUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("CV URL Error") };
        var res = await Run(Handler(uow, storage), null);
        Assert.True(res.IsFailure);
        Assert.Contains("CV URL Error", res.Error);
    }
}

/// <summary>
/// Chi tiết 1 hồ sơ (<see cref="GetMyApplicationDetailQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetMyApplicationDetail" (UTCID01–12): IDOR (NotFound/Forbidden) + auto-link email không phân biệt hoa/thường,
/// job thiếu → field null, cổng chia sẻ (eval/transcript/recording/feedback), chọn ca/mã mới nhất, và lỗi lookup/storage.
/// </summary>
public class GetMyApplicationDetailQueryHandlerTests
{
    private static GetMyApplicationDetailQueryHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage? storage = null)
        => new(uow, storage ?? new RecordingFileStorage());

    private static Task<Result<object>> Run(GetMyApplicationDetailQueryHandler h, Guid id, Guid? candidate = null, string? email = PortalAppsData.Email)
        => h.Handle(new GetMyApplicationDetailQuery(id, candidate ?? PortalAppsData.CandidateId, email), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Unknown_application()
    {
        var res = await Run(Handler(new InMemoryUnitOfWork()), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
        Assert.Equal(ARI.Application.Common.CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Owner_gets_detail_with_sessions()
    {
        var job = JobBoardData.PublicJob(title: "Backend Developer");
        var app = PortalAppsData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PortalAppsData.Session(app.Id));
        var res = await Run(Handler(uow), app.Id);
        Assert.True(res.IsSuccess);
        var obj = PortalAppsData.Json(res);
        Assert.Equal("Backend Developer", obj.GetProperty("JobTitle").GetString());
        Assert.True(obj.GetProperty("Sessions").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task UTCID03_Owner_null_email_case_insensitive_links()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.AppRaw(job.Id, owner: null, email: PortalAppsData.Email);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);
        var res = await Run(Handler(uow), app.Id, email: "CANDIDATE@EXAMPLE.COM");
        Assert.True(res.IsSuccess);
        Assert.Equal(PortalAppsData.CandidateId, app.CandidateAccountId);   // auto-link
        Assert.True(uow.SaveChangesCount >= 1);
    }

    [Fact]
    public async Task UTCID04_Foreign_application_forbidden()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.AppRaw(job.Id, owner: Guid.NewGuid(), email: "owner@example.io");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);
        var res = await Run(Handler(uow), app.Id, email: "attacker@example.io");
        Assert.True(res.IsFailure);
        Assert.Equal(ARI.Application.Common.CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID05_Missing_job_null_fields()
    {
        var app = PortalAppsData.App(Guid.NewGuid());   // job không seed
        var uow = new InMemoryUnitOfWork().Seed(app);
        var obj = PortalAppsData.Json(await Run(Handler(uow), app.Id));
        Assert.Equal(JsonValueKind.Null, obj.GetProperty("JobTitle").ValueKind);
    }

    [Fact]
    public async Task UTCID06_Completed_unshared_eval_pending_null_eval()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var session = PortalAppsData.Session(app.Id, status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(PortalAppsData.Eval(session.Id, app.Id));
        var obj = PortalAppsData.Json(await Run(Handler(uow), app.Id));
        var round1 = obj.GetProperty("Sessions")[0];
        Assert.True(round1.GetProperty("PendingHrReview").GetBoolean());
        Assert.Equal(JsonValueKind.Null, round1.GetProperty("Evaluation").ValueKind);
    }

    [Fact]
    public async Task UTCID07_Shared_evaluation_fields()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var session = PortalAppsData.Session(app.Id, status: "completed");
        var eval = PortalAppsData.Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(eval)
            .Seed(PortalAppsData.Review(eval.Id, shareEval: true));
        var obj = PortalAppsData.Json(await Run(Handler(uow), app.Id));
        var evalNode = obj.GetProperty("Sessions")[0].GetProperty("Evaluation");
        Assert.NotEqual(JsonValueKind.Null, evalNode.ValueKind);
        Assert.Equal("pass", evalNode.GetProperty("AiVerdict").GetString());
    }

    [Fact]
    public async Task UTCID08_Shared_transcript_recording_feedback()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var session = PortalAppsData.Session(app.Id, status: "completed", recordingUrl: "rec/x");
        var eval = PortalAppsData.Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(eval)
            .Seed(PortalAppsData.Review(eval.Id, shareEval: true, shareTranscript: true, shareRecording: true, shareFeedback: true, feedback: "fb"));
        var storage = new RecordingFileStorage { GetUrlResult = "/files/rec" };
        var obj = PortalAppsData.Json(await Run(Handler(uow, storage), app.Id));
        var round1 = obj.GetProperty("Sessions")[0];
        Assert.True(round1.GetProperty("TranscriptShared").GetBoolean());
        Assert.Equal("/files/rec", round1.GetProperty("RecordingUrl").GetString());
        Assert.Equal("fb", round1.GetProperty("HrFeedback").GetString());
    }

    [Fact]
    public async Task UTCID09_Earliest_future_slot()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var early = new AvailabilitySlot { Id = Guid.NewGuid(), JobPostingId = job.Id, RoundNumber = 1, StartTime = DateTimeOffset.UtcNow.AddDays(1), EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(1), Timezone = "UTC", Capacity = 5 };
        var late = new AvailabilitySlot { Id = Guid.NewGuid(), JobPostingId = job.Id, RoundNumber = 1, StartTime = DateTimeOffset.UtcNow.AddDays(3), EndTime = DateTimeOffset.UtcNow.AddDays(3).AddHours(1), Timezone = "UTC", Capacity = 5 };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewBooking { Id = Guid.NewGuid(), ApplicationId = app.Id, AvailabilitySlotId = early.Id, RoundNumber = 1, Status = "scheduled" })
            .Seed(new InterviewBooking { Id = Guid.NewGuid(), ApplicationId = app.Id, AvailabilitySlotId = late.Id, RoundNumber = 1, Status = "scheduled" })
            .Seed(early, late);
        var obj = PortalAppsData.Json(await Run(Handler(uow), app.Id));
        var upcoming = obj.GetProperty("UpcomingInterview");
        Assert.Equal(early.StartTime, upcoming.GetProperty("StartTime").GetDateTimeOffset());
    }

    [Fact]
    public async Task UTCID10_Highest_active_code()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 1, Code = "AAA111", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() })
            .Seed(new InterviewCode { Id = Guid.NewGuid(), ApplicationId = app.Id, RoundNumber = 2, Code = "BBB222", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), CreatedByUserId = Guid.NewGuid() });
        var obj = PortalAppsData.Json(await Run(Handler(uow), app.Id));
        Assert.Equal(2, obj.GetProperty("InterviewCode").GetProperty("RoundNumber").GetInt32());
    }

    [Fact]
    public async Task UTCID11_Application_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<ARI.Domain.Entities.Application>("Application DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(Handler(uow), Guid.NewGuid()));
        Assert.Equal("Application DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID12_File_storage_error()
    {
        var job = JobBoardData.PublicJob();
        var app = PortalAppsData.App(job.Id, cvFileUrl: "stored/cv.pdf");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("Storage Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(Handler(uow, storage), app.Id));
        Assert.Equal("Storage Error", ex.Message);
    }
}
