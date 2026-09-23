using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARI.Application.CandidatePortal
{
    /// <summary>
    /// GET /api/portal/jobs/{jobPostingId}/cv-match — độ phù hợp của CHÍNH CV trong hồ sơ ứng viên với
    /// tin này, chấm theo bộ tiêu chí của tin (ADR-070). Kết quả dùng lại theo (tin, file CV, bộ tiêu
    /// chí); chấm chạy nền, FE poll trạng thái (processing/completed/failed/rubric_pending).
    ///
    /// Trạng thái "đang chấm" và "vừa hỏng" đọc từ <see cref="CvScoringInFlight"/> — chung với hàng đợi
    /// chấm hồ sơ, nên xem độ phù hợp rồi nộp ngay không gọi AI hai lần.
    /// </summary>
    public record GetCvMatchQuery(Guid JobPostingId, Guid CandidateId) : IRequest<Result<CvMatchResponse>>;

    public class GetCvMatchQueryHandler : IRequestHandler<GetCvMatchQuery, Result<CvMatchResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICvScoringService _scoring;
        private readonly CvScoringInFlight _inFlight;

        public GetCvMatchQueryHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IServiceScopeFactory scopeFactory,
            ICvScoringService scoring,
            CvScoringInFlight inFlight)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _scopeFactory = scopeFactory;
            _scoring = scoring;
            _inFlight = inFlight;
        }

        public async Task<Result<CvMatchResponse>> Handle(GetCvMatchQuery request, CancellationToken ct)
        {
            var (jobPostingId, candidateId) = (request.JobPostingId, request.CandidateId);

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Result.Failure<CvMatchResponse>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            // Chưa có CV trong hồ sơ → FE hiện nút "Tải CV lên".
            if (string.IsNullOrEmpty(acc.ProfileCvUrl))
                return Result.Success(new CvMatchResponse { HasCv = false, Status = "none" });

            var fileName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName)
                ? "cv" + System.IO.Path.GetExtension(acc.ProfileCvUrl)
                : acc.ProfileCvFileName;

            var resp = new CvMatchResponse
            {
                HasCv = true,
                CvFileName = fileName,
                CvUrl = await _fileStorage.GetUrlAsync(acc.ProfileCvUrl, ct),
                CvDownloadUrl = await _fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, fileName, ct)
            };

            var bytes = await _fileStorage.ReadAllBytesAsync(acc.ProfileCvUrl, ct);
            if (bytes == null || bytes.Length == 0)
            {
                resp.AiAvailable = false;
                resp.Status = "failed";
                resp.Message = "Không đọc được file CV đã lưu.";
                return Result.Success(resp);
            }

            var lookup = await _scoring.LookupAsync(jobPostingId, bytes, ct);

            // Tin chưa có bộ tiêu chí → KHÔNG chấm (ADR-070).
            if (lookup.Rubric == null || lookup.Key == null)
            {
                resp.AiAvailable = false;
                resp.Status = "rubric_pending";
                resp.Message = "Tin này chưa sẵn sàng chấm độ phù hợp CV. Bạn vẫn có thể ứng tuyển bình thường.";
                return Result.Success(resp);
            }

            // 1. Đã có kết quả cho đúng (tin, file, bộ tiêu chí).
            if (lookup.Existing is { } cached)
            {
                if (string.Equals(cached.Status, CvAnalysisStatuses.InvalidCv, StringComparison.OrdinalIgnoreCase))
                {
                    resp.AiAvailable = false;
                    resp.Status = "failed";
                    resp.Message = cached.ErrorMessage ?? "File CV không hợp lệ.";
                    return Result.Success(resp);
                }

                resp.AiAvailable = true;
                resp.Status = "completed";
                resp.Analysis = new CvMatchAnalysisDto
                {
                    MatchScore = cached.MatchScore,
                    Summary = cached.Summary,
                    SkillsMatched = PortalSupport.DeserializeStringList(cached.SkillsMatched),
                    SkillsGaps = PortalSupport.DeserializeStringList(cached.SkillsGaps),
                    ExperienceRelevance = cached.ExperienceRelevance,
                    ReviewedBy = cached.AiModel
                };
                return Result.Success(resp);
            }

            var key = lookup.Key;

            // 2. Đang chấm (từ lượt poll trước hoặc từ hàng đợi hồ sơ).
            if (_inFlight.IsRunning(key))
            {
                resp.Status = "processing";
                return Result.Success(resp);
            }

            // 3. Vừa hỏng và chưa tới giờ thử lại → báo lỗi, không gọi AI liên tục mỗi lượt poll.
            if (_inFlight.ShouldBackOff(key) && _inFlight.LastFailure(key) is { } failure)
            {
                resp.AiAvailable = false;
                resp.Status = "failed";
                resp.Message = failure.Message;
                return Result.Success(resp);
            }

            // 4. Chưa có gì → chấm ở nền và trả "processing" để FE poll. Khoá được giữ ngay trong lượt
            //    chấm, nên poll kế tiếp thấy IsRunning; hai request đồng thời thì người sau chờ rồi dùng
            //    lại kết quả người trước.
            var bytesCopy = bytes;
            var fileNameCopy = fileName;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ICvScoringService>();
                    var r = await svc.ScoreAsync(jobPostingId, bytesCopy, fileNameCopy, CancellationToken.None);
                    if (r.IsFailure) return;

                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var notifSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var dedupKey = $"ai_analysis:{jobPostingId}";
                    var exists = await uow.Repository<Notification>().CountAsync(
                        n => n.CandidateAccountId == candidateId && n.DedupKey == dedupKey, CancellationToken.None) > 0;
                    if (!exists)
                    {
                        await uow.Repository<Notification>().AddAsync(new Notification
                        {
                            CandidateAccountId = candidateId,
                            DedupKey = dedupKey,
                            Type = "system",
                            Title = "Phân tích CV hoàn tất",
                            Body = "AI đã hoàn tất phân tích CV của bạn. Vui lòng bấm vào để xem kết quả.",
                            Link = $"/jobs/{jobPostingId}",
                            IsRead = false
                        }, CancellationToken.None);
                        await uow.SaveChangesAsync(CancellationToken.None);
                    }

                    await notifSvc.PublishUserEventAsync(candidateId, "ReceiveUserNotification",
                        new { Type = "AiAnalysisComplete", JobPostingId = jobPostingId }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        scope.ServiceProvider.GetService<ILogger<GetCvMatchQueryHandler>>()
                            ?.LogError(ex, "Chấm độ phù hợp CV nền thất bại cho tin {JobId}", jobPostingId);
                    }
                    catch { /* không để tác vụ nền làm sập tiến trình */ }
                }
            });

            resp.Status = "processing";
            return Result.Success(resp);
        }
    }
}
