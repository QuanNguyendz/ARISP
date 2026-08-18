using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Phạm vi Playbook (ADR-025): tài liệu nào được nói vào buổi phỏng vấn nào. Trước đây câu hỏi này
/// không được đặt ra — mọi chunk playbook của hệ thống đều được truy hồi, nên ngân hàng câu hỏi của
/// vị trí khác lọt vào, và tài liệu đã xoá vẫn tiếp tục có tiếng nói.
/// </summary>
public class PlaybookEligibilityTests
{
    private static readonly Guid JobA = Guid.NewGuid();
    private static readonly Guid JobB = Guid.NewGuid();

    private static PlaybookDocument Doc(
        string scope, Guid? refId = null, int? round = null, string type = "style_guide",
        DateTimeOffset? deletedAt = null) => new()
    {
        Scope = scope,
        ScopeRefId = refId,
        RoundNumber = round,
        DocumentType = type,
        FileName = $"{scope}.md",
        UploadedByUserId = Guid.NewGuid(),
        DeletedAt = deletedAt,
    };

    private static Task<List<Guid>> Run(InMemoryUnitOfWork uow, Guid job, int round)
        => PlaybookScope.EligibleDocumentIdsAsync(uow, job, round, CancellationToken.None);

    [Fact]
    public async Task Org_playbook_applies_to_every_job()
    {
        var org = Doc(PlaybookScope.ScopeOrg);
        var uow = new InMemoryUnitOfWork().Seed(org);

        Assert.Contains(org.Id, await Run(uow, JobA, 1));
        Assert.Contains(org.Id, await Run(uow, JobB, 3));
    }

    [Fact]
    public async Task Job_playbook_only_applies_to_its_own_job()
    {
        var ofA = Doc(PlaybookScope.ScopeJobPosting, JobA);
        var uow = new InMemoryUnitOfWork().Seed(ofA);

        Assert.Contains(ofA.Id, await Run(uow, JobA, 1));
        Assert.DoesNotContain(ofA.Id, await Run(uow, JobB, 1)); // đây là chỗ rò rỉ cũ
    }

    [Fact]
    public async Task Round_playbook_needs_both_job_and_round_to_match()
    {
        var round2OfA = Doc(PlaybookScope.ScopeRound, JobA, round: 2);
        var uow = new InMemoryUnitOfWork().Seed(round2OfA);

        Assert.Contains(round2OfA.Id, await Run(uow, JobA, 2));
        Assert.DoesNotContain(round2OfA.Id, await Run(uow, JobA, 1)); // sai vòng
        Assert.DoesNotContain(round2OfA.Id, await Run(uow, JobB, 2)); // sai tin
    }

    [Fact]
    public async Task Deleted_playbook_never_applies()
    {
        var deleted = Doc(PlaybookScope.ScopeOrg, deletedAt: DateTimeOffset.UtcNow);
        var uow = new InMemoryUnitOfWork().Seed(deleted);

        Assert.Empty(await Run(uow, JobA, 1));
    }

    /// <summary>Must-ask lấy cả scope round — bản cũ chỉ đọc job_posting nên khai theo vòng là mất trắng.</summary>
    [Fact]
    public async Task Must_ask_documents_cover_job_and_round_scope_only()
    {
        var jobLevel = Doc(PlaybookScope.ScopeJobPosting, JobA, type: PlaybookScope.TypeMustAsk);
        var roundLevel = Doc(PlaybookScope.ScopeRound, JobA, round: 2, type: PlaybookScope.TypeMustAsk);
        var otherRound = Doc(PlaybookScope.ScopeRound, JobA, round: 3, type: PlaybookScope.TypeMustAsk);
        var otherJob = Doc(PlaybookScope.ScopeJobPosting, JobB, type: PlaybookScope.TypeMustAsk);
        var orgLevel = Doc(PlaybookScope.ScopeOrg, type: PlaybookScope.TypeMustAsk);
        var notMustAsk = Doc(PlaybookScope.ScopeJobPosting, JobA, type: "question_bank");
        var uow = new InMemoryUnitOfWork().Seed(jobLevel, roundLevel, otherRound, otherJob, orgLevel, notMustAsk);

        var ids = (await PlaybookScope.MustAskDocumentsAsync(uow, JobA, 2, CancellationToken.None))
            .Select(d => d.Id).ToList();

        Assert.Equal(2, ids.Count);
        Assert.Contains(jobLevel.Id, ids);
        Assert.Contains(roundLevel.Id, ids);
    }
}

/// <summary>
/// Tách câu must-ask. Đây là điều kiện CHẶN kết thúc phiên nên mỗi dòng rác lọt vào là một câu AI
/// buộc phải hỏi ứng viên — bản cũ tách theo "\n" và ";" rồi lấy tất.
/// </summary>
public class ParseMustAskLinesTests
{
    [Fact]
    public void Keeps_real_questions_and_strips_bullets()
    {
        var lines = PlaybookScope.ParseMustAskLines(
            "- Bạn đã làm việc với hệ thống phân tán chưa?\n"
            + "* Kể về một sự cố production bạn từng xử lý.\n"
            + "1. Vì sao bạn muốn rời công ty hiện tại?");

        Assert.Equal(3, lines.Count);
        Assert.Equal("Bạn đã làm việc với hệ thống phân tán chưa?", lines[0]);
        Assert.Equal("Kể về một sự cố production bạn từng xử lý.", lines[1]);
        Assert.Equal("Vì sao bạn muốn rời công ty hiện tại?", lines[2]);
    }

    [Fact]
    public void Drops_headings_rules_blanks_and_too_short_lines()
    {
        var lines = PlaybookScope.ParseMustAskLines(
            "# Câu hỏi bắt buộc\n"
            + "---\n"
            + "\n"
            + "   \n"
            + "- ?\n"
            + "Bạn tự đánh giá thế nào về kỹ năng SQL?");

        var only = Assert.Single(lines);
        Assert.Equal("Bạn tự đánh giá thế nào về kỹ năng SQL?", only);
    }

    /// <summary>Dấu chấm phẩy giữa câu là chuyện bình thường — bản cũ cắt đôi câu ở đó.</summary>
    [Fact]
    public void Does_not_split_on_semicolon()
    {
        var only = Assert.Single(PlaybookScope.ParseMustAskLines(
            "Bạn dùng Redis để cache; vậy khi cache miss thì xử lý ra sao?"));

        Assert.Equal("Bạn dùng Redis để cache; vậy khi cache miss thì xử lý ra sao?", only);
    }

    [Fact]
    public void Removes_duplicates_ignoring_case()
    {
        var lines = PlaybookScope.ParseMustAskLines(
            "Bạn mong đợi mức lương bao nhiêu?\n"
            + "bạn mong đợi mức lương bao nhiêu?\n"
            + "- Bạn mong đợi mức lương bao nhiêu?");

        Assert.Single(lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t\n")]
    public void Empty_input_yields_nothing(string? text)
        => Assert.Empty(PlaybookScope.ParseMustAskLines(text));
}
