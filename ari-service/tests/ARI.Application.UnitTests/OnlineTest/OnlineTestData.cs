using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>Factory dựng entity Online Test cho test — serialize JSON đúng format mà handler đọc/chấm.</summary>
internal static class OnlineTestData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static JobPosting Job(int passScore = 70, int perTest = 50, Guid? owner = null) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = "Backend Developer",
        OnlineTestPassScore = passScore,
        OnlineTestQuestionsPerTest = perTest,
        OnlineTestDurationMinutes = 30,
    };

    public static ARI.Domain.Entities.Application Application(
        Guid jobId, Guid? accountId, string status = "screening", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = "Nguyen Van A",
        Status = status,
    };

    /// <summary>Một câu hỏi với tập đáp án đúng cho trước (0-based). optionCount phương án giả.</summary>
    public static OnlineTestQuestion Question(Guid jobId, string type, int[] correct, int optionCount = 4)
    {
        var options = Enumerable.Range(0, optionCount).Select(i => $"Option {(char)('A' + i)}").ToList();
        return new OnlineTestQuestion
        {
            JobPostingId = jobId,
            QuestionText = "Câu hỏi mẫu?",
            Options = JsonSerializer.Serialize(options, Json),
            QuestionType = type,
            CorrectOptions = JsonSerializer.Serialize(correct, Json),
            CorrectOption = correct.Length > 0 ? correct[0] : 0,
        };
    }

    /// <summary>Câu hỏi 1 đáp án đúng.</summary>
    public static OnlineTestQuestion Single(Guid jobId, int correct, int optionCount = 4)
        => Question(jobId, "single", new[] { correct }, optionCount);
}
