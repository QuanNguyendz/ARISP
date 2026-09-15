using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Bài làm chi tiết của ứng viên (<see cref="GetOnlineTestAnswerSheetQueryHandler"/>).
///
/// Điểm tổng chỉ nói "30/100" — nó không trả lời được câu mà người sàng lọc thật sự hỏi: <i>sai ở
/// đâu</i>. Ba điều bộ test này khoá:
/// 1. Bộ đề được DỰNG LẠI từ phép bốc deterministic, nên câu ứng viên BỎ TRẮNG vẫn có mặt — nếu lấy
///    khoá của bài làm đã lưu làm bộ đề thì "làm sai" và "không làm" trông giống hệt nhau.
/// 2. Vị từ đúng/sai GIỐNG HỆT lúc chấm điểm (tập chọn khớp hoàn toàn tập đáp án đúng), nếu không
///    màn xem bài sẽ mâu thuẫn với chính con điểm bên cạnh nó.
/// 3. Đáp án đúng chỉ đi qua cổng nhân sự.
/// </summary>
public class OnlineTestAnswerSheetTests
{
    private static readonly Guid JobId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("90000000-0000-0000-0000-0000000000aa");
    private static readonly Guid AppId = Guid.Parse("90000000-0000-0000-0000-0000000000bb");

    private static OnlineTestQuestion Question(string id, string text, int correct) => new()
    {
        Id = Guid.Parse(id),
        JobPostingId = JobId,
        QuestionText = text,
        Options = "[\"A\",\"B\",\"C\",\"D\"]",
        QuestionType = "single",
        CorrectOptions = $"[{correct}]",
        CorrectOption = correct,
    };

    /// <summary>Tin + hồ sơ + hai câu hỏi; điểm sàn 70, bài lấy hết ngân hàng.</summary>
    private static InMemoryUnitOfWork Seeded()
        => new InMemoryUnitOfWork()
            .Seed(new JobPosting
            {
                Id = JobId, CreatedByUserId = OwnerId, Title = "Backend Developer",
                OnlineTestPassScore = 70, OnlineTestQuestionsPerTest = 0,
            })
            .Seed(new ARI.Domain.Entities.Application
            {
                Id = AppId, JobPostingId = JobId, Status = "interview", CandidateName = "Ứng viên A",
            })
            .Seed(Question("90000000-0000-0000-0000-000000000101", "Câu 1", correct: 0))
            .Seed(Question("90000000-0000-0000-0000-000000000102", "Câu 2", correct: 2));

    private static OnlineTestSubmission Submission(string answersJson, decimal score = 50m) => new()
    {
        ApplicationId = AppId, RoundNumber = 1, SelectedAnswers = answersJson,
        Score = score, IsPassed = score >= 70, CorrectCount = 1, TotalQuestions = 2,
    };

    private static Task<Result<ARI.Application.DTOs.OnlineTestAnswerSheetDto?>> Run(
        InMemoryUnitOfWork uow, Guid? userId = null, string? role = null) =>
        new GetOnlineTestAnswerSheetQueryHandler(uow)
            .Handle(new GetOnlineTestAnswerSheetQuery(AppId, userId ?? OwnerId, role ?? AppRoles.Recruiter),
                CancellationToken.None);

    [Fact]
    public async Task Chua_nop_bai_thi_tra_null_chu_khong_phai_loi()
    {
        // Màn gọi tới đây để HỎI "có bài không" — trả lỗi thì nó phải phân biệt lỗi thật với
        // "chưa thi", và đó là chỗ dễ hiển thị sai.
        var res = await Run(Seeded());

        Assert.True(res.IsSuccess);
        Assert.Null(res.Value);
    }

    [Fact]
    public async Task Cau_bo_trang_van_co_mat_trong_bai_lam()
    {
        // Bài làm chỉ lưu câu ĐÃ trả lời. Lấy khoá của nó làm bộ đề thì câu bỏ trắng biến mất, và
        // "làm sai" với "không làm" trông giống hệt nhau.
        var uow = Seeded().Seed(Submission("{\"90000000-0000-0000-0000-000000000101\":[0]}"));

        var res = await Run(uow);

        Assert.True(res.IsSuccess);
        var items = res.Value!.Items;
        Assert.Equal(2, items.Count);

        var blank = items.Single(i => i.QuestionText == "Câu 2");
        Assert.Empty(blank.SelectedOptions);
        Assert.False(blank.IsCorrect);
    }

    [Fact]
    public async Task Cham_dung_sai_giong_het_luc_cham_diem()
    {
        var uow = Seeded().Seed(Submission(
            "{\"90000000-0000-0000-0000-000000000101\":[0],\"90000000-0000-0000-0000-000000000102\":[1]}"));

        var res = await Run(uow);

        var items = res.Value!.Items;
        Assert.True(items.Single(i => i.QuestionText == "Câu 1").IsCorrect);   // chọn 0, đáp án 0
        Assert.False(items.Single(i => i.QuestionText == "Câu 2").IsCorrect);  // chọn 1, đáp án 2
    }

    [Fact]
    public async Task Tra_kem_dap_an_dung_va_dap_an_da_khoanh()
    {
        var uow = Seeded().Seed(Submission("{\"90000000-0000-0000-0000-000000000102\":[1]}"));

        var res = await Run(uow);

        var item = res.Value!.Items.Single(i => i.QuestionText == "Câu 2");
        Assert.Equal(new[] { 1 }, item.SelectedOptions);
        Assert.Equal(new[] { 2 }, item.CorrectOptions);
        Assert.Equal(4, item.Options.Count);
    }

    [Fact]
    public async Task Bai_lam_hong_thi_coi_nhu_bo_trang_het_chu_khong_chet_man()
    {
        var uow = Seeded().Seed(Submission("khong-phai-json"));

        var res = await Run(uow);

        Assert.True(res.IsSuccess);
        Assert.All(res.Value!.Items, i => Assert.Empty(i.SelectedOptions));
    }

    [Fact]
    public async Task Nhan_su_ngoai_tin_khong_xem_duoc()
    {
        // Response mang ĐÁP ÁN ĐÚNG — để hở là lộ đề.
        var res = await Run(Seeded(), userId: Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Hiring_Manager_cua_tin_xem_duoc()
    {
        // HM là người RA ĐỀ (quyết định chuyên môn) nên đương nhiên xem được bài làm.
        var hmId = Guid.NewGuid();
        var uow = Seeded().Seed(new JobHiringTeamMember
        {
            JobPostingId = JobId, UserId = hmId,
            RoleOnJob = JobTeamRoles.HiringManager, IsPrimary = true,
        }).Seed(Submission("{}"));

        var res = await Run(uow, userId: hmId, role: AppRoles.HiringManager);

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
    }
}
