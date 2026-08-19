using System;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Dòng danh sách playbook — property khớp shape anonymous cũ của controller.
    /// <paramref name="CriteriaCount"/> chỉ có ở tài liệu bộ tiêu chí chấm điểm (ADR-060): danh sách
    /// phải cho thấy bộ nào đang có bao nhiêu tiêu chí, không thì HR không phân biệt được file rubric
    /// đã đọc được với file chỉ mới nằm đó.
    /// </summary>
    public record PlaybookListItemDto(
        Guid Id, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
        string FileName, string FileFormat, string Status, DateTimeOffset CreatedAt, string? UploadedBy,
        int? CriteriaCount = null);

    /// <summary>Payload trả về sau khi upload playbook thành công.</summary>
    public record UploadedPlaybookDto(
        Guid Id, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
        string FileName, string FileFormat, string Status, DateTimeOffset CreatedAt,
        int? CriteriaCount = null);
}
