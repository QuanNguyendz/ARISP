using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    /// <summary>
    /// Đẩy tài liệu (JD/CV/Playbook) sang RAG service (Python) để chunk + embed + lưu pgvector.
    /// Toàn bộ vòng đời ingestion thuộc service Python (ADR-039 mở rộng) — .NET chỉ là client.
    /// Idempotent theo (sourceType, sourceId) khi <paramref name="replaceExisting"/> = true.
    /// </summary>
    public interface IRagIngestionService
    {
        /// <returns>Số chunk đã ghi.</returns>
        Task<int> IngestAsync(
            string sourceType,           // jd | cv | playbook
            Guid sourceId,               // jobPostingId | applicationId | playbookDocumentId
            string text,
            string? scope = null,        // company | job_posting | round (playbook)
            string? documentType = null, // question_bank | must_ask | ...
            bool replaceExisting = true,
            CancellationToken ct = default);
    }
}
