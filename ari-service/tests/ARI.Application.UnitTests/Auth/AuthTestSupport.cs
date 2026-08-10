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
    public string StaffToken { get; set; } = "staff-jwt";
    public string CandidateToken { get; set; } = "cand-jwt";
    public int StaffCount { get; private set; }
    public int CandidateCount { get; private set; }

    public string CreateStaffToken(User user) { StaffCount++; return StaffToken; }
    public string CreateCandidateToken(CandidateAccount candidate) { CandidateCount++; return CandidateToken; }
    public string CreateKioskSessionToken(Guid sessionId, Guid applicationId, int ttlHours) => throw new NotImplementedException();
}

/// <summary>IPasswordHasher giả — Verify trả kết quả cấu hình + đếm lần gọi (để assert "không Verify").</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool VerifyResult { get; set; } = true;
    public int VerifyCallCount { get; private set; }

    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string password, string hash) { VerifyCallCount++; return VerifyResult; }
}

/// <summary>IEmailQueue giả — ghi lại email đã enqueue.</summary>
internal sealed class RecordingEmailQueue : IEmailQueue
{
    public List<EmailQueueItem> Items { get; } = new();
    public void Enqueue(EmailQueueItem item) => Items.Add(item);
    public ValueTask<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
