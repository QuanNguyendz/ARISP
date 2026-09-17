using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Ghi một PHIÊN BẢN bộ tiêu chí chấm CV cho tin (ADR-070). Là đường ghi duy nhất: màn tin của HM,
    /// bước dựng tin từ phiếu, và dữ liệu mẫu dev đều gọi vào đây, nên luật "mỗi tin một bộ sống" và
    /// "lưu bộ mới thì chấm lại mọi hồ sơ" không thể bị một cửa nào bỏ qua.
    /// </summary>
    public class CvRubricService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IRagIngestionService _ragIngestion;
        private readonly ICvScoringQueue _queue;
        private readonly ILogger<CvRubricService> _logger;

        public CvRubricService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IRagIngestionService ragIngestion,
            ICvScoringQueue queue,
            ILogger<CvRubricService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _ragIngestion = ragIngestion;
            _queue = queue;
            _logger = logger;
        }

        /// <param name="criteria">Đã qua <see cref="CvRubricEditing.Normalize"/>.</param>
        /// <returns>Tài liệu sống sau khi lưu, và cờ có thật sự đổi gì không.</returns>
        public async Task<Result<(PlaybookDocument Document, bool Changed)>> SaveForJobAsync(
            Guid jobPostingId, IReadOnlyList<RubricCriterion> criteria, Guid actorUserId, CancellationToken ct)
        {
            var errors = ScoringRubric.Validate(criteria);
            if (errors.Count > 0)
                return Result.Failure<(PlaybookDocument, bool)>(string.Join(" | ", errors));

            var json = ScoringRubric.Serialize(criteria);
            var previous = await CvRubricStore.LiveAsync(_unitOfWork, jobPostingId, ct);

            // Lưu lại đúng bộ đang dùng thì không tạo phiên bản mới — nếu không, bấm Lưu hai lần là chấm lại
            // toàn bộ hồ sơ một lần vô ích.
            if (previous != null && string.Equals(previous.RubricJson, json, StringComparison.Ordinal))
                return Result.Success((previous, false));

            var bytes = RubricSheet.Build(criteria);
            var fileName = $"bo-tieu-chi-cham-cv-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(bytes, fileName, RubricSheet.XlsxContentType, StorageFolder.Playbook, ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<(PlaybookDocument, bool)>($"Không lưu được file bộ tiêu chí: {ex.Message}", CommonErrorCodes.ServerError);
            }

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

            var parsedText = ScoringRubric.ToPromptText(criteria);
            var document = new PlaybookDocument
            {
                Scope = PlaybookScope.ScopeJobPosting,
                ScopeRefId = jobPostingId,
                DocumentType = ScoringRubric.TypeCvRubric,
                FileName = fileName,
                FileUrl = storageKey,
                FileFormat = "xlsx",
                ParsedText = parsedText,
                RubricJson = json,
                Status = "ready",
                UploadedByUserId = actorUserId,
            };

            // Hai bước lưu (xoá mềm bản cũ rồi mới chèn) vì index "mỗi tin một bộ sống" lọc theo deleted_at —
            // EF không biết sắp thứ tự câu lệnh theo điều kiện lọc. Chèn hỏng (người khác vừa lưu) thì trả
            // lại bản cũ nếu chưa có bản sống nào khác.
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
                await TryRestoreAsync(previous, jobPostingId, ct);
                _logger.LogWarning(ex, "Lưu bộ tiêu chí CV cho tin {JobId} thất bại", jobPostingId);
                return Result.Failure<(PlaybookDocument, bool)>(
                    "Bộ tiêu chí vừa được người khác cập nhật. Hãy tải lại trang rồi lưu lại.", CommonErrorCodes.Conflict);
            }

            // Kho tri thức chỉ là phần phụ (AI diễn giải khi phỏng vấn); điểm CV đọc từ RubricJson. Lỗi
            // ingest không được xoá mất một phiên bản đã lưu.
            try
            {
                await _ragIngestion.IngestAsync("playbook", document.Id, parsedText,
                    scope: PlaybookScope.ScopeJobPosting, documentType: ScoringRubric.TypeCvRubric, ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ingest bộ tiêu chí CV {DocId} vào RAG thất bại — điểm CV không bị ảnh hưởng.", document.Id);
            }

            _queue.EnqueueJob(jobPostingId);
            return Result.Success((document, true));
        }

        private async Task TryRestoreAsync(PlaybookDocument? previous, Guid jobPostingId, CancellationToken ct)
        {
            if (previous == null) return;
            try
            {
                if (await CvRubricStore.HasLiveAsync(_unitOfWork, jobPostingId, ct)) return;
                previous.DeletedAt = null;
                _unitOfWork.Repository<PlaybookDocument>().Update(previous);
                await _unitOfWork.SaveChangesAsync(ct);
                await _ragIngestion.IngestAsync("playbook", previous.Id, previous.ParsedText ?? string.Empty,
                    scope: PlaybookScope.ScopeJobPosting, documentType: ScoringRubric.TypeCvRubric, ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không khôi phục được bộ tiêu chí cũ {DocId}", previous.Id);
            }
        }

        /// <summary>
        /// Chép bộ tiêu chí trên phiếu sang tin vừa dựng (ADR-070). Phiếu lập trước ADR-070 không có bộ
        /// tiêu chí → bỏ qua; cổng gửi duyệt sẽ chặn cho tới khi HM khai ở màn tin.
        /// </summary>
        public async Task<Result> CopyFromRequestAsync(JobPosting job, RecruitmentRequest request, CancellationToken ct)
        {
            var criteria = ScoringRubric.Deserialize(request.CvRubricJson);
            if (criteria.Count == 0) return Result.Success();

            var saved = await SaveForJobAsync(job.Id, criteria, request.RequestedByUserId, ct);
            if (saved.IsSuccess) return Result.Success();
            return saved.ErrorCode == null ? Result.Failure(saved.Error!) : Result.Failure(saved.Error!, saved.ErrorCode);
        }

        /// <summary>Danh sách tiêu chí của một phiếu (để hiện lại trên form và màn duyệt).</summary>
        public static List<RubricCriterion> FromRequest(RecruitmentRequest request)
            => ScoringRubric.Deserialize(request.CvRubricJson);
    }
}
