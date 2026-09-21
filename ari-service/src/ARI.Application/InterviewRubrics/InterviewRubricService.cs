using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ARI.Application.InterviewRubrics
{
    /// <summary>
    /// Đường GHI duy nhất của bộ tiêu chí chấm PHỎNG VẤN theo tin (ADR-073): bộ chung của tin, hoặc bộ riêng của
    /// một vòng. Màn tin của Hiring Manager và dữ liệu mẫu dev đều gọi vào đây, nên luật "mỗi (tin, vòng) một bộ
    /// sống" và "khai xong thì các buổi đang chờ tự được chấm" không cửa nào bỏ qua được.
    ///
    /// Bộ tiêu chí phỏng vấn KHÔNG có ý kiểm: ý kiểm (ADR-071) là dấu hiệu tra được trên CV; câu trả lời phỏng vấn
    /// được chấm theo chuẩn chấm + mức neo. Ý kiểm lọt vào (Excel, mẫu CV) bị bỏ ở đây.
    /// </summary>
    public class InterviewRubricService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IRagIngestionService _ragIngestion;
        private readonly InterviewEvaluator _evaluator;
        private readonly IEvaluationQueue? _queue;
        private readonly ILogger<InterviewRubricService>? _logger;

        public InterviewRubricService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IRagIngestionService ragIngestion,
            InterviewEvaluator evaluator,
            IEvaluationQueue? queue = null,
            ILogger<InterviewRubricService>? logger = null)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _ragIngestion = ragIngestion;
            _evaluator = evaluator;
            _queue = queue;
            _logger = logger;
        }

        /// <param name="roundNumber">Null = bộ chung của tin; có giá trị = bộ riêng của vòng đó.</param>
        /// <param name="criteria">Đã qua <see cref="CvRubricEditing.Normalize"/>.</param>
        /// <returns>Tài liệu sống sau khi lưu, và cờ có thật sự đổi gì không.</returns>
        public async Task<Result<(PlaybookDocument Document, bool Changed)>> SaveAsync(
            Guid jobPostingId, int? roundNumber, IReadOnlyList<RubricCriterion> criteria, Guid actorUserId, CancellationToken ct)
        {
            var clean = criteria.Select(c => new RubricCriterion
            {
                Key = c.Key,
                Name = c.Name,
                Weight = c.Weight,
                Description = c.Description,
                Levels = c.Levels,
                Checks = null,
            }).ToList();

            var errors = ScoringRubric.Validate(clean, RubricPurpose.Interview);
            if (errors.Count > 0)
                return Result.Failure<(PlaybookDocument, bool)>(string.Join(" | ", errors));

            var json = ScoringRubric.Serialize(clean);
            var previous = roundNumber is { } r
                ? await InterviewRubricStore.RoundLevelAsync(_unitOfWork, jobPostingId, r, ct)
                : await InterviewRubricStore.JobLevelAsync(_unitOfWork, jobPostingId, ct);

            // Lưu lại đúng bộ đang dùng thì không tạo phiên bản mới.
            if (previous != null && string.Equals(previous.RubricJson, json, StringComparison.Ordinal))
                return Result.Success((previous, false));

            var bytes = RubricSheet.Build(clean, null, RubricPurpose.Interview);
            var suffix = roundNumber is { } rn ? $"-vong-{rn}" : string.Empty;
            var fileName = $"bo-tieu-chi-phong-van{suffix}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(bytes, fileName, RubricSheet.XlsxContentType, StorageFolder.Playbook, ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<(PlaybookDocument, bool)>($"Không lưu được file bộ tiêu chí: {ex.Message}", CommonErrorCodes.ServerError);
            }

            var scope = roundNumber.HasValue ? PlaybookScope.ScopeRound : PlaybookScope.ScopeJobPosting;

            // Gỡ chunk của bản cũ TRƯỚC khi xoá mềm (ADR-025) — lỗi thì dừng, bản cũ vẫn nguyên vẹn.
            if (previous != null)
            {
                try
                {
                    await _ragIngestion.IngestAsync("playbook", previous.Id, string.Empty, ct: ct);
                }
                catch (Exception ex)
                {
                    await _fileStorage.DeleteAsync(storageKey, ct);
                    return Result.Failure<(PlaybookDocument, bool)>(
                        $"Không gỡ được bộ tiêu chí cũ khỏi kho tri thức của AI: {ex.Message}. Vui lòng thử lại.",
                        CommonErrorCodes.ServerError);
                }

                previous.DeletedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<PlaybookDocument>().Update(previous);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var parsedText = ScoringRubric.ToPromptText(clean);
            var document = new PlaybookDocument
            {
                Scope = scope,
                ScopeRefId = jobPostingId,
                RoundNumber = roundNumber,
                DocumentType = ScoringRubric.TypeInterviewRubric,
                FileName = fileName,
                FileUrl = storageKey,
                FileFormat = "xlsx",
                ParsedText = parsedText,
                RubricJson = json,
                Status = "ready",
                UploadedByUserId = actorUserId,
            };

            // Hai bước lưu (xoá mềm bản cũ rồi mới chèn) vì index "mỗi (tin, vòng) một bộ sống" lọc theo
            // deleted_at. Chèn hỏng (người khác vừa lưu) thì trả lại bản cũ nếu chưa có bản sống nào khác.
            var repo = _unitOfWork.Repository<PlaybookDocument>();
            await repo.AddAsync(document, ct);
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                repo.Delete(document);
                await _fileStorage.DeleteAsync(storageKey, ct);
                await TryRestoreAsync(previous, jobPostingId, roundNumber, ct);
                _logger?.LogWarning(ex, "Lưu bộ tiêu chí phỏng vấn cho tin {JobId} (vòng {Round}) thất bại", jobPostingId, roundNumber);
                return Result.Failure<(PlaybookDocument, bool)>(
                    "Bộ tiêu chí vừa được người khác cập nhật. Hãy tải lại trang rồi lưu lại.", CommonErrorCodes.Conflict);
            }

            // Kho tri thức là phần phụ (AI diễn giải khi phỏng vấn); điểm đọc từ RubricJson. Lỗi ingest không
            // được xoá mất một phiên bản đã lưu.
            try
            {
                await _ragIngestion.IngestAsync("playbook", document.Id, parsedText,
                    scope: scope, documentType: ScoringRubric.TypeInterviewRubric, ct: ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Ingest bộ tiêu chí phỏng vấn {DocId} vào RAG thất bại — điểm không bị ảnh hưởng.", document.Id);
            }

            await ReleaseWaitingSessionsAsync(jobPostingId, ct);
            return Result.Success((document, true));
        }

        /// <summary>Bỏ bộ riêng của một vòng — vòng quay về dùng bộ chung của tin. Bộ chung thì không xoá được, chỉ thay.</summary>
        public async Task<Result<bool>> RemoveRoundAsync(Guid jobPostingId, int roundNumber, CancellationToken ct)
        {
            var doc = await InterviewRubricStore.RoundLevelAsync(_unitOfWork, jobPostingId, roundNumber, ct);
            if (doc == null) return Result.Success(false);

            try
            {
                await _ragIngestion.IngestAsync("playbook", doc.Id, string.Empty, ct: ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool>(
                    $"Không gỡ được bộ tiêu chí khỏi kho tri thức của AI: {ex.Message}. Vui lòng thử lại.", CommonErrorCodes.ServerError);
            }

            doc.DeletedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<PlaybookDocument>().Update(doc);
            await _unitOfWork.SaveChangesAsync(ct);

            await ReleaseWaitingSessionsAsync(jobPostingId, ct);
            return Result.Success(true);
        }

        /// <summary>Buổi phỏng vấn của tin đang chờ bộ tiêu chí → đưa về "chờ chấm" và vào hàng ngay.</summary>
        private async Task ReleaseWaitingSessionsAsync(Guid jobPostingId, CancellationToken ct)
        {
            try
            {
                foreach (var id in await _evaluator.ReleaseBlockedSessionsAsync(jobPostingId, ct))
                    _queue?.Enqueue(id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Lượt quét định kỳ cũng tìm ra các phiên này — lỗi ở đây chỉ làm báo cáo đến chậm vài phút.
                _logger?.LogWarning(ex, "Không đưa được các buổi đang chờ của tin {JobId} vào hàng chấm.", jobPostingId);
            }
        }

        private async Task TryRestoreAsync(PlaybookDocument? previous, Guid jobPostingId, int? roundNumber, CancellationToken ct)
        {
            if (previous == null) return;
            try
            {
                var live = roundNumber is { } r
                    ? await InterviewRubricStore.RoundLevelAsync(_unitOfWork, jobPostingId, r, ct)
                    : await InterviewRubricStore.JobLevelAsync(_unitOfWork, jobPostingId, ct);
                if (live != null) return;

                previous.DeletedAt = null;
                _unitOfWork.Repository<PlaybookDocument>().Update(previous);
                await _unitOfWork.SaveChangesAsync(ct);
                await _ragIngestion.IngestAsync("playbook", previous.Id, previous.ParsedText ?? string.Empty,
                    scope: previous.Scope, documentType: ScoringRubric.TypeInterviewRubric, ct: ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Không khôi phục được bộ tiêu chí phỏng vấn cũ {DocId}", previous.Id);
            }
        }
    }
}
