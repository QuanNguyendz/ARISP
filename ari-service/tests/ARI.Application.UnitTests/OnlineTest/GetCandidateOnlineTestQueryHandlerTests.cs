using System;
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

    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app) Setup(
        string status = "screening", int perTest = 50, int bank = 3)
    {
        var job = OnlineTestData.Job(perTest: perTest);
        var app = OnlineTestData.Application(job.Id, _accountId, status: status, email: Email);
        var questions = Enumerable.Range(0, bank).Select(_ => OnlineTestData.Single(job.Id, 0)).ToArray();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(questions);
        return (uow, job, app);
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
