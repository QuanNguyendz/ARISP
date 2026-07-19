using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application.CandidatePortal
{
    /// <summary>
    /// GET /api/portal/jobs/{jobPostingId}/cv-match — phân tích độ phù hợp CV–JD dùng CHÍNH CV
    /// trong hồ sơ của ứng viên hiện tại. Kết quả cache theo (job + CV hash); phân tích chạy nền,
    /// FE poll trạng thái (processing/completed/failed).
    /// </summary>
    public record GetCvMatchQuery(Guid JobPostingId, Guid CandidateId) : IRequest<Result<CvMatchResponse>>;

    public class GetCvMatchQueryHandler : IRequestHandler<GetCvMatchQuery, Result<CvMatchResponse>>
    {
        // Trạng thái phân tích CV-JD đang chạy nền (key = "{jobId}:{cvHash}"). Dùng cho lỗi AI
        // (không ghi row vào DB) để poll biết được kết quả thất bại. Kết quả thành công nằm ở DB cache.
        private sealed class MatchJobState
        {
            public string Status = "processing";
            public string? Message;
        }
        private static readonly ConcurrentDictionary<string, MatchJobState> _matchJobs = new();

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IServiceScopeFactory _scopeFactory;

        public GetCvMatchQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage, IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _scopeFactory = scopeFactory;
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

            var cvHash = PortalSupport.ComputeHash(bytes);
            var key = $"{jobPostingId}:{cvHash}";

            // 1. Đã có kết quả cache trong DB (không chạy lại Gemini).
            var cached = (await _unitOfWork.Repository<CvJdAnalysis>()
                .FindAsync(x => x.JobPostingId == jobPostingId && x.CvHash == cvHash, ct)).FirstOrDefault();
            if (cached != null && cached.Status == "completed")
            {
                _matchJobs.TryRemove(key, out _);
                resp.AiAvailable = true;
                resp.Status = "completed";
                resp.Analysis = new CvMatchAnalysisDto
                {
                    MatchScore = cached.MatchScore,
                    Summary = cached.Summary,
                    SkillsMatched = PortalSupport.DeserializeStringList(cached.SkillsMatched),
                    SkillsGaps = PortalSupport.DeserializeStringList(cached.SkillsGaps),
                    ExperienceRelevance = cached.ExperienceRelevance,
                    OverallRecommendation = cached.OverallRecommendation,
                    ReviewedBy = cached.AiModel
                };
                return Result.Success(resp);
            }
            if (cached != null && cached.Status == "failed")
            {
                _matchJobs.TryRemove(key, out _);
                resp.AiAvailable = false;
                resp.Status = "failed";
                resp.Message = cached.ErrorMessage ?? "CV không hợp lệ.";
                return Result.Success(resp);
            }

            // 2. Có job nền đang/đã chạy. Lỗi AI (không ghi DB) được giữ ở bộ nhớ để poll đọc.
            if (_matchJobs.TryGetValue(key, out var state))
            {
                if (state.Status == "failed")
                {
                    _matchJobs.TryRemove(key, out _);
                    resp.AiAvailable = false;
                    resp.Status = "failed";
                    resp.Message = state.Message ?? "Phân tích CV thất bại.";
                    return Result.Success(resp);
                }
                resp.Status = "processing";
                return Result.Success(resp);
            }

            // 3. Chưa có gì → khởi chạy phân tích ở nền và trả "processing" để FE poll.
            if (_matchJobs.TryAdd(key, new MatchJobState { Status = "processing" }))
            {
                var bytesCopy = bytes;
                var fileNameCopy = fileName;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<ICvJdAnalysisService>();
                        var notifSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        using var bgStream = new System.IO.MemoryStream(bytesCopy);
                        var r = await svc.AnalyzeAndCacheAsync(jobPostingId, bgStream, fileNameCopy, CancellationToken.None);
                        if (!r.IsFailure)
                        {
                            // Thành công → đã nằm trong DB cache, bỏ trạng thái nền.
                            _matchJobs.TryRemove(key, out _);

                            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var notifRepo = uow.Repository<Notification>();
                            var dedupKey = $"ai_analysis:{jobPostingId}";

                            var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == candidateId && n.DedupKey == dedupKey, CancellationToken.None);
                            var existingNotif = existingNotifs.FirstOrDefault();
                            if (existingNotif == null)
                            {
                                var newNotif = new Notification
                                {
                                    CandidateAccountId = candidateId,
                                    DedupKey = dedupKey,
                                    Type = "system",
                                    Title = "Phân tích CV hoàn tất",
                                    Body = $"AI đã hoàn tất phân tích CV của bạn. Vui lòng bấm vào để xem kết quả.",
                                    Link = $"/jobs/{jobPostingId}",
                                    IsRead = false
                                };
                                await notifRepo.AddAsync(newNotif, CancellationToken.None);
                                await uow.SaveChangesAsync(CancellationToken.None);
                            }

                            // Notify candidate
                            await notifSvc.PublishUserEventAsync(candidateId, "ReceiveUserNotification", new { Type = "AiAnalysisComplete" }, CancellationToken.None);
                        }
                        else if (_matchJobs.TryGetValue(key, out var s))
                        {
                            // Lỗi AI không ghi DB → giữ trạng thái failed cho poll kế tiếp.
                            s.Status = "failed";
                            s.Message = r.Error;
                        }
                    }
                    catch (Exception)
                    {
                        if (_matchJobs.TryGetValue(key, out var s))
                        {
                            s.Status = "failed";
                            s.Message = "Lỗi hệ thống khi phân tích CV.";
                        }
                    }
                });
            }

            resp.Status = "processing";
            return Result.Success(resp);
        }
    }
}
