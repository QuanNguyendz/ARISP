using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interviews;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Danh sách phiên phỏng vấn cho HR (<see cref="GetHrInterviewSessionsQueryHandler"/>) — Report5 tab
/// "GetHrInterviewSessions" (UTCID01–03).
///
/// <b>ADR-061:</b> endpoint này từng trả MỌI phiên phỏng vấn của công ty kèm <c>RecordingUrl</c> cho
/// bất kỳ nhân sự nào. Nay handler lọc theo phạm vi do SERVER tính; quản trị viên vẫn thấy tất cả,
/// nên ba ca dưới đây chạy dưới danh nghĩa quản trị viên và giữ nguyên kỳ vọng cũ.
/// </summary>
public class GetHrInterviewSessionsQueryHandlerTests
{
    private static readonly Guid AdminA = Guid.Parse("42000000-0000-0000-0000-000000000001");

    private static Task<Result<List<HrInterviewSessionItem>>> Run(FakeInterviewService svc)
        => new GetHrInterviewSessionsQueryHandler(svc, new InMemoryUnitOfWork())
            .Handle(new GetHrInterviewSessionsQuery(null, AdminA, AppRoles.HrAdmin), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Returns_sessions()
    {
        var svc = new FakeInterviewService { HrSessions = new() { new(), new() } };
        var res = await Run(svc);
        Assert.True(res.IsSuccess);
        Assert.Same(svc.HrSessions, res.Value);
    }

    [Fact]
    public async Task UTCID02_Empty()
    {
        var svc = new FakeInterviewService();
        var res = await Run(svc);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID03_Exception_propagates()
    {
        var svc = new FakeInterviewService { HrSessionsThrows = new Exception("Session DB Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(svc));
        Assert.Equal("Session DB Error", ex.Message);
    }
}

/// <summary>Bắt đầu phiên (<see cref="StartInterviewSessionCommandHandler"/>) — Report5 tab "StartInterviewSession" (UTCID01–04): forward Request nguyên vẹn.</summary>
public class StartInterviewSessionCommandHandlerTests
{
    private static readonly Guid AppA = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static StartSessionRequest Req(string type, int round) => new() { ApplicationId = AppA, RoundNumber = round, SessionType = type };

    [Fact]
    public async Task UTCID01_Real_forwards()
    {
        var svc = new FakeInterviewService();
        var req = Req("real", 2);
        var res = await new StartInterviewSessionCommandHandler(svc).Handle(new StartInterviewSessionCommand(req), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Same(req, svc.LastStartRequest);
    }

    [Fact]
    public async Task UTCID02_Practice_forwards()
    {
        var svc = new FakeInterviewService();
        var req = Req("practice", 1);
        var res = await new StartInterviewSessionCommandHandler(svc).Handle(new StartInterviewSessionCommand(req), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Same(req, svc.LastStartRequest);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewService { StartResult = Result.Failure<StartSessionResponse>("Unable to start session.") };
        var res = await new StartInterviewSessionCommandHandler(svc).Handle(new StartInterviewSessionCommand(Req("real", 2)), CancellationToken.None);
        Assert.Equal("Unable to start session.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewService { StartThrows = new Exception("Start Session Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new StartInterviewSessionCommandHandler(svc).Handle(new StartInterviewSessionCommand(Req("real", 2)), CancellationToken.None));
        Assert.Equal("Start Session Error", ex.Message);
    }
}

/// <summary>Cấu hình media (<see cref="GetMediaConfigQueryHandler"/>) — Report5 tab "GetMediaConfig" (UTCID01–04): forward SessionId/AccountId/Email.</summary>
public class GetMediaConfigQueryHandlerTests
{
    private static readonly Guid SessionA = Guid.Parse("43000000-0000-0000-0000-000000000001");
    private static readonly Guid CandidateA = Guid.Parse("44000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_By_account()
    {
        var svc = new FakeInterviewService();
        var res = await new GetMediaConfigQueryHandler(svc).Handle(new GetMediaConfigQuery(SessionA, CandidateA, null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, (Guid?)CandidateA, (string?)null, false), svc.LastMedia);
    }

    [Fact]
    public async Task UTCID02_By_email()
    {
        var svc = new FakeInterviewService();
        var res = await new GetMediaConfigQueryHandler(svc).Handle(new GetMediaConfigQuery(SessionA, null, "candidate@example.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, (Guid?)null, (string?)"candidate@example.com", false), svc.LastMedia);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewService { MediaResult = Result.Failure<PracticeMediaConfigResponse>("Media config unavailable.") };
        var res = await new GetMediaConfigQueryHandler(svc).Handle(new GetMediaConfigQuery(SessionA, CandidateA, null), CancellationToken.None);
        Assert.Equal("Media config unavailable.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewService { MediaThrows = new Exception("Media Config Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new GetMediaConfigQueryHandler(svc).Handle(new GetMediaConfigQuery(SessionA, CandidateA, null), CancellationToken.None));
        Assert.Equal("Media Config Error", ex.Message);
    }
}

/// <summary>Tổng hợp giọng nói (<see cref="SynthesizeSpeechCommandHandler"/>) — Report5 tab "SynthesizeSpeech" (UTCID01–04).</summary>
public class SynthesizeSpeechCommandHandlerTests
{
    private static readonly Guid SessionA = Guid.Parse("43000000-0000-0000-0000-000000000001");
    private static readonly Guid CandidateA = Guid.Parse("44000000-0000-0000-0000-000000000001");
    private const string Text = "Hello candidate";

    [Fact]
    public async Task UTCID01_By_account()
    {
        var svc = new FakeInterviewService();
        var res = await new SynthesizeSpeechCommandHandler(svc).Handle(new SynthesizeSpeechCommand(SessionA, Text, CandidateA, null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, Text, (Guid?)CandidateA, (string?)null, false), svc.LastSpeech);
    }

    [Fact]
    public async Task UTCID02_By_email()
    {
        var svc = new FakeInterviewService();
        var res = await new SynthesizeSpeechCommandHandler(svc).Handle(new SynthesizeSpeechCommand(SessionA, Text, null, "candidate@example.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, Text, (Guid?)null, (string?)"candidate@example.com", false), svc.LastSpeech);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewService { SpeechResult = Result.Failure<string>("TTS unavailable.") };
        var res = await new SynthesizeSpeechCommandHandler(svc).Handle(new SynthesizeSpeechCommand(SessionA, Text, CandidateA, null), CancellationToken.None);
        Assert.Equal("TTS unavailable.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewService { SpeechThrows = new Exception("TTS Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new SynthesizeSpeechCommandHandler(svc).Handle(new SynthesizeSpeechCommand(SessionA, Text, CandidateA, null), CancellationToken.None));
        Assert.Equal("TTS Error", ex.Message);
    }
}

/// <summary>Nộp câu trả lời (<see cref="SubmitAnswerCommandHandler"/>) — Report5 tab "SubmitAnswer" (UTCID01–04).</summary>
public class SubmitAnswerCommandHandlerTests
{
    private static readonly Guid SessionA = Guid.Parse("43000000-0000-0000-0000-000000000001");
    private static readonly Guid QuestionA = Guid.Parse("45000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_With_response_time()
    {
        var svc = new FakeInterviewService();
        var res = await new SubmitAnswerCommandHandler(svc).Handle(new SubmitAnswerCommand(SessionA, QuestionA, "My answer", 5000), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, QuestionA, "My answer", (int?)5000), svc.LastAnswer);
    }

    [Fact]
    public async Task UTCID02_Null_response_time()
    {
        var svc = new FakeInterviewService();
        var res = await new SubmitAnswerCommandHandler(svc).Handle(new SubmitAnswerCommand(SessionA, QuestionA, "My answer", null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, QuestionA, "My answer", (int?)null), svc.LastAnswer);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewService { AnswerResult = Result.Failure<Answer>("Submit answer failed.") };
        var res = await new SubmitAnswerCommandHandler(svc).Handle(new SubmitAnswerCommand(SessionA, QuestionA, "My answer", 5000), CancellationToken.None);
        Assert.Equal("Submit answer failed.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewService { AnswerThrows = new Exception("Submit Answer Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new SubmitAnswerCommandHandler(svc).Handle(new SubmitAnswerCommand(SessionA, QuestionA, "My answer", 5000), CancellationToken.None));
        Assert.Equal("Submit Answer Error", ex.Message);
    }
}

/// <summary>Kết thúc phiên (<see cref="EndInterviewSessionCommandHandler"/>) — Report5 tab "EndInterviewSession" (UTCID01–04).</summary>
public class EndInterviewSessionCommandHandlerTests
{
    private static readonly Guid SessionA = Guid.Parse("43000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_Completed()
    {
        var svc = new FakeInterviewService();
        var res = await new EndInterviewSessionCommandHandler(svc).Handle(new EndInterviewSessionCommand(SessionA, "completed"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, "completed"), svc.LastEnd);
    }

    [Fact]
    public async Task UTCID02_Aborted()
    {
        var svc = new FakeInterviewService();
        var res = await new EndInterviewSessionCommandHandler(svc).Handle(new EndInterviewSessionCommand(SessionA, "aborted"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((SessionA, "aborted"), svc.LastEnd);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewService { EndResult = Result.Failure<bool>("End session failed.") };
        var res = await new EndInterviewSessionCommandHandler(svc).Handle(new EndInterviewSessionCommand(SessionA, "completed"), CancellationToken.None);
        Assert.Equal("End session failed.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewService { EndThrows = new Exception("End Session Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new EndInterviewSessionCommandHandler(svc).Handle(new EndInterviewSessionCommand(SessionA, "completed"), CancellationToken.None));
        Assert.Equal("End Session Error", ex.Message);
    }
}

/// <summary>
/// HR xác nhận/override đánh giá (<see cref="ConfirmHrReviewCommandHandler"/>) — Report5 tab "ConfirmHrReview" (UTCID01–05):
/// chọn base URL theo config (Frontend:CandidateBaseUrl → Authentication:AdminFrontendUrl → http://localhost:3000), propagate.
/// </summary>
public class ConfirmHrReviewCommandHandlerTests
{
    private static readonly Guid HrA = Guid.Parse("41000000-0000-0000-0000-000000000001");
    private static readonly Guid EvalA = Guid.Parse("46000000-0000-0000-0000-000000000001");

    private static IConfiguration Config(params (string Key, string Value)[] kv)
        => new ConfigurationBuilder().AddInMemoryCollection(kv.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value))).Build();

    private static ConfirmReviewRequest Req(string verdict = "pass", string? reason = null)
        => new() { EvaluationId = EvalA, FinalVerdict = verdict, OverrideReason = reason };

    [Fact]
    public async Task UTCID01_Uses_candidate_base_url()
    {
        var svc = new FakeInterviewService();
        var cfg = Config(("Frontend:CandidateBaseUrl", "https://candidate.example.com"), ("Authentication:AdminFrontendUrl", "https://admin.example.com"));
        var res = await new ConfirmHrReviewCommandHandler(svc, cfg).Handle(new ConfirmHrReviewCommand(HrA, Req()), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("https://candidate.example.com", svc.LastHrReview!.Value.BaseUrl);
    }

    [Fact]
    public async Task UTCID02_Falls_back_to_admin_url()
    {
        var svc = new FakeInterviewService();
        var cfg = Config(("Authentication:AdminFrontendUrl", "https://admin.example.com"));
        var res = await new ConfirmHrReviewCommandHandler(svc, cfg).Handle(new ConfirmHrReviewCommand(HrA, Req()), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("https://admin.example.com", svc.LastHrReview!.Value.BaseUrl);
    }

    /// <summary>
    /// Chưa cấu hình gốc URL nào → chuỗi RỖNG, cố ý không còn mặc định <c>http://localhost:3000</c>.
    /// Mặc định đó khiến thư gửi từ server thật mang nút bấm dẫn về máy lập trình viên: không lỗi,
    /// không log, chỉ phát hiện khi ứng viên báo link hỏng. Nay thiếu cấu hình bị chặn ngay lúc boot
    /// ở môi trường non-Development (<see cref="FrontendUrls"/>), nên trạng thái này không tồn tại
    /// trên môi trường thật — và ở đây không ném lỗi để một lá thư thiếu link không kéo đổ cả thao
    /// tác nghiệp vụ đang chạy.
    /// </summary>
    [Fact]
    public async Task UTCID03_No_configured_url_yields_empty()
    {
        var svc = new FakeInterviewService();
        var res = await new ConfirmHrReviewCommandHandler(svc, Config()).Handle(new ConfirmHrReviewCommand(HrA, Req()), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", svc.LastHrReview!.Value.BaseUrl);
    }

    [Fact]
    public async Task UTCID04_Failure_forwarded()
    {
        var svc = new FakeInterviewService { HrReviewResult = Result.Failure<bool>("HR review failed.") };
        var res = await new ConfirmHrReviewCommandHandler(svc, Config()).Handle(new ConfirmHrReviewCommand(HrA, Req("not_pass", "Insufficient skills")), CancellationToken.None);
        Assert.Equal("HR review failed.", res.Error);
    }

    [Fact]
    public async Task UTCID05_Exception_propagates()
    {
        var svc = new FakeInterviewService { HrReviewThrows = new Exception("HR Review Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new ConfirmHrReviewCommandHandler(svc, Config()).Handle(new ConfirmHrReviewCommand(HrA, Req("not_pass", "Insufficient skills")), CancellationToken.None));
        Assert.Equal("HR Review Error", ex.Message);
    }
}
