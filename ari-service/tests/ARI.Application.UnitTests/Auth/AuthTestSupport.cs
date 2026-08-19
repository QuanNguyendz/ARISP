using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.UnitTests.Auth;

/// <summary>Fakes + factory dựng entity cho test các handler Auth (login / register / verify) — không JWT/DB thật.</summary>
internal static class AuthData
{
    /// <summary>Config rỗng: RegisterCandidate chỉ đọc URL frontend (có fallback), rỗng là đủ.</summary>
    public static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    public static CandidateAccount Candidate(
        string email = "cand@example.io", string? passwordHash = "hashed:pw",
        bool verified = true, string? fullName = "Candidate Name") =>
        new() { Email = email, PasswordHash = passwordHash ?? string.Empty, EmailVerified = verified, FullName = fullName! };

    public static User Staff(
        string email = "hr@example.io", string? passwordHash = "hashed:pw",
        bool active = true, string role = AppRoles.Recruiter, string? fullName = "Staff Name") =>
        new() { Email = email, PasswordHash = passwordHash, IsActive = active, Role = role, FullName = fullName };

    public static MagicLink VerifyLink(
        string email, string token = "tok", string? audience = null,
        DateTimeOffset? expiresAt = null, DateTimeOffset? usedAt = null) =>
        new()
        {
            Email = email,
            TokenHash = token,
            Audience = audience ?? MagicLinkAudience.CandidateEmailVerify,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            UsedAt = usedAt,
        };
}

/// <summary>ITokenService giả — trả token cấu hình được + đếm số lần mint (để assert "không cấp token").</summary>
internal sealed class FakeTokenService : ITokenService
{
    public string StaffToken { get; set; } = "staff-access-token";
    public string CandidateToken { get; set; } = "candidate-access-token";
    public int StaffCount { get; private set; }
    public int CandidateCount { get; private set; }

    /// <summary>Khi set: mint token tương ứng ném lỗi (case "token service throws" của test-plan).</summary>
    public Exception? StaffThrows { get; set; }
    public Exception? CandidateThrows { get; set; }

    public string CreateStaffToken(User user) { StaffCount++; if (StaffThrows != null) throw StaffThrows; return StaffToken; }
    public string CreateCandidateToken(CandidateAccount candidate) { CandidateCount++; if (CandidateThrows != null) throw CandidateThrows; return CandidateToken; }
    public string CreateKioskSessionToken(Guid sessionId, Guid applicationId, int ttlHours) => throw new NotImplementedException();
}

/// <summary>IPasswordHasher giả — Verify trả kết quả cấu hình + đếm lần gọi (để assert "không Verify").</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool VerifyResult { get; set; } = true;
    public int VerifyCallCount { get; private set; }

    /// <summary>Khi set: Hash/Verify ném lỗi (case "password hasher/verifier throws" của test-plan).</summary>
    public Exception? HashThrows { get; set; }
    public Exception? VerifyThrows { get; set; }

    public string Hash(string password) { if (HashThrows != null) throw HashThrows; return $"hashed:{password}"; }
    public bool Verify(string password, string hash) { VerifyCallCount++; if (VerifyThrows != null) throw VerifyThrows; return VerifyResult; }
}

/// <summary>IEmailQueue giả — ghi lại email đã enqueue.</summary>
internal sealed class RecordingEmailQueue : IEmailQueue
{
    public List<EmailQueueItem> Items { get; } = new();

    /// <summary>Khi set: <see cref="Enqueue"/> ném lỗi (case "email queue throws" của test-plan).</summary>
    public Exception? EnqueueThrows { get; set; }

    public void Enqueue(EmailQueueItem item) { if (EnqueueThrows != null) throw EnqueueThrows; Items.Add(item); }
    public ValueTask<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
