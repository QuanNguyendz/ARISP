using System;

namespace ARI.Application.Playbooks
{
    /// <summary>Dòng danh sách playbook — property khớp shape anonymous cũ của controller.</summary>
    public record PlaybookListItemDto(
        Guid Id, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
        string FileName, string FileFormat, string Status, DateTimeOffset CreatedAt, string? UploadedBy);

    /// <summary>Payload trả về sau khi upload playbook thành công.</summary>
    public record UploadedPlaybookDto(
        Guid Id, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
        string FileName, string FileFormat, string Status, DateTimeOffset CreatedAt);
}
