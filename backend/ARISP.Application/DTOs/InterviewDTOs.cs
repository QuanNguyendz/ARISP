using System;
using System.Collections.Generic;

namespace ARISP.Application.DTOs
{
    public class StartSessionRequest
    {
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string SessionType { get; set; } = "real"; // practice | real
    }

    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = "active";
        public string Language { get; set; } = "vi";
        public string? HeyGenSdpOffer { get; set; }
        public string? HeyGenSessionId { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public Guid QuestionId { get; set; }
        public string Transcript { get; set; } = string.Empty;
        public int? ResponseTimeMs { get; set; }
    }

    public class GenerateCodeRequest
    {
        public Guid ApplicationId { get; set; }
        public int? RoundNumber { get; set; }
    }

    public class GenerateBatchRequest
    {
        public List<Guid> ApplicationIds { get; set; } = new();
        public int? RoundNumber { get; set; }
    }

    public class ValidateCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class InterviewCodeSummaryDto
    {
        public string Code { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Một phiên phỏng vấn hiển thị trong danh sách HR.
    /// </summary>
    public class HrInterviewSessionItem
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty; // practice | real
        public string Status { get; set; } = string.Empty;      // pending | active | completed | aborted | error
        public string InterviewLanguage { get; set; } = "vi";
        public int? DurationSeconds { get; set; }
        public bool HasRecording { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? EvaluationId { get; set; }
        public string? Verdict { get; set; }
    }
}
