using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CvScoring
{
    // ===================================================================================
    //  Trình soạn bộ tiêu chí chấm CV (ADR-070)
    //
    //  Bộ tiêu chí là DỮ LIỆU có cấu trúc (tên · trọng số · chuẩn chấm · 4 mức neo), nhập thẳng trên web.
    //  Excel và AI gợi ý chỉ là cách ĐIỀN NHANH vào trình soạn — không lối nào lưu thẳng, nên mọi bộ tiêu
    //  chí đều qua đúng một bộ chuẩn hoá (CvRubricEditing) trước khi thành bộ của tin.
    // ===================================================================================

    /// <summary>Bộ tiêu chí sống của một tin, kèm quyền sửa và tác động của việc sửa.</summary>
    public record JobCvRubricDto(
        IReadOnlyList<CvRubricCriterionInput> Criteria,
        Guid? DocumentId,
        DateTimeOffset? SavedAt,
        string? SavedBy,
        bool CanEdit,
        /// <summary>Số hồ sơ có CV của tin — lưu bộ tiêu chí mới là chấm lại chừng ấy hồ sơ.</summary>
        int ApplicationCount,
        /// <summary>Số hồ sơ đang chờ chấm / chấm lại theo bộ hiện hành.</summary>
        int PendingCount,
        /// <summary>Công thức cấp tin đang dùng (ADR-075) — luôn có, không khai thì là mặc định.</summary>
        CvScoringPolicy Policy,
        /// <summary>Chỉ có ở phản hồi của lệnh LƯU: lần lưu vừa rồi dẫn tới việc gì.</summary>
        CvRubricSaveOutcomeDto? SaveOutcome = null);

    /// <summary>Tác động của một lần lưu bộ tiêu chí / công thức (ADR-075).</summary>
    public record CvRubricSaveOutcomeDto(
        /// <summary><c>unchanged</c> | <c>recompute</c> (chỉ đổi công thức — tính lại, không gọi AI) | <c>ai_rescore</c>.</summary>
        string Mode,
        /// <summary>Số hồ sơ có CV bị ảnh hưởng.</summary>
        int Affected);

    public static class CvRubricSaveModes
    {
        public const string Unchanged = "unchanged";
        public const string Recompute = "recompute";
        public const string AiRescore = "ai_rescore";
    }

    public record GetJobCvRubricQuery(Guid JobPostingId, Guid? UserId, string? Role) : IRequest<Result<JobCvRubricDto>>;

    public class GetJobCvRubricQueryHandler : IRequestHandler<GetJobCvRubricQuery, Result<JobCvRubricDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly CvApplicationScorer _scorer;

        public GetJobCvRubricQueryHandler(IUnitOfWork unitOfWork, CvApplicationScorer scorer)
        {
            _unitOfWork = unitOfWork;
            _scorer = scorer;
        }

        public async Task<Result<JobCvRubricDto>> Handle(GetJobCvRubricQuery request, CancellationToken ct)
        {
            var (ok, job) = await JobAccess.CanViewAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<JobCvRubricDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok)
                return Result.Failure<JobCvRubricDto>("Bạn không thuộc đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            var live = await CvRubricStore.LiveAsync(_unitOfWork, job.Id, ct);
            string? savedBy = null;
            if (live != null)
            {
                var author = await _unitOfWork.Repository<User>().GetByIdAsync(live.UploadedByUserId, ct);
                savedBy = author == null ? null : (string.IsNullOrWhiteSpace(author.FullName) ? author.Email : author.FullName);
            }

            var canEdit = !string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase)
                          && await PlaybookAccess.CanManageJobAsync(_unitOfWork, job.Id, request.UserId, request.Role, ct);

            var jobId = job.Id;
            var appCount = await _unitOfWork.Repository<Domain.Entities.Application>().CountAsync(
                a => a.JobPostingId == jobId && a.CvFileUrl != null && a.CvFileUrl != "", ct);
            var pending = live == null ? 0 : (await _scorer.FindStaleApplicationIdsAsync(jobId, 100_000, ct)).Count;

            return Result.Success(new JobCvRubricDto(
                CvRubricEditing.ToInput(CvRubricStore.Criteria(live)),
                live?.Id, live?.CreatedAt, savedBy, canEdit, appCount, pending,
                CvRubricStore.Policy(live)));
        }
    }

    public record SaveJobCvRubricCommand(
        Guid JobPostingId, IReadOnlyList<CvRubricCriterionInput> Criteria, Guid? UserId, string? Role,
        /// <summary>Công thức cấp tin (ADR-075). <c>null</c> = mặc định.</summary>
        CvScoringPolicy? Policy = null)
        : IRequest<Result<JobCvRubricDto>>;

    public class SaveJobCvRubricCommandHandler : IRequestHandler<SaveJobCvRubricCommand, Result<JobCvRubricDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly CvRubricService _rubrics;
        private readonly INotificationService _notifications;
        private readonly ISender _sender;

        public SaveJobCvRubricCommandHandler(
            IUnitOfWork unitOfWork, CvRubricService rubrics, INotificationService notifications, ISender sender)
        {
            _unitOfWork = unitOfWork;
            _rubrics = rubrics;
            _notifications = notifications;
            _sender = sender;
        }

        public async Task<Result<JobCvRubricDto>> Handle(SaveJobCvRubricCommand request, CancellationToken ct)
        {
            if (request.UserId is not { } actorId)
                return Result.Failure<JobCvRubricDto>("Không xác định được người lưu.", CommonErrorCodes.Forbidden);

            // Cùng luật với playbook theo tin (ADR-069): HM chính của tin, hoặc quản trị viên.
            var (accessError, accessCode) = await PlaybookAccess.CheckWriteAsync(
                _unitOfWork, PlaybookScope.ScopeJobPosting, request.JobPostingId, actorId, request.Role, ct);
            if (accessError != null)
                return accessCode == null
                    ? Result.Failure<JobCvRubricDto>(accessError)
                    : Result.Failure<JobCvRubricDto>(accessError, accessCode);

            var normalized = CvRubricEditing.Normalize(request.Criteria, RubricPurpose.Cv);
            var errors = normalized.Errors.Concat(CvScoringPolicy.Validate(request.Policy)).ToList();
            if (errors.Count > 0)
                return Result.Failure<JobCvRubricDto>(string.Join(" · ", errors.Take(10)));

            var saved = await _rubrics.SaveForJobAsync(request.JobPostingId, normalized.Criteria, request.Policy, actorId, ct);
            if (saved.IsFailure)
                return saved.ErrorCode == null
                    ? Result.Failure<JobCvRubricDto>(saved.Error!)
                    : Result.Failure<JobCvRubricDto>(saved.Error!, saved.ErrorCode);

            var mode = !saved.Value.Changed ? CvRubricSaveModes.Unchanged
                : saved.Value.FormulaOnly ? CvRubricSaveModes.Recompute
                : CvRubricSaveModes.AiRescore;

            if (saved.Value.Changed)
            {
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
                if (job != null)
                {
                    await AuditSupport.WriteAsync(_unitOfWork, actorId, job, normalized.Criteria,
                        CvRubricStore.Policy(saved.Value.Document), saved.Value.FormulaOnly, ct);

                    // Recruiter chủ tin cần biết: điểm hồ sơ sắp thay đổi, và tin (nếu đang bị chặn) đã gửi
                    // duyệt được.
                    if (job.CreatedByUserId != actorId)
                    {
                        await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                        {
                            RecipientUserId = job.CreatedByUserId,
                            Type = "system",
                            Title = saved.Value.FormulaOnly
                                ? "Công thức chấm CV đã được cập nhật"
                                : "Bộ tiêu chí chấm CV đã được cập nhật",
                            Body = saved.Value.FormulaOnly
                                ? $"Tin \"{job.Title}\" có công thức chấm CV mới — điểm và khuyến nghị của các hồ sơ đang được tính lại (không chấm lại bằng AI)."
                                : $"Tin \"{job.Title}\" có bộ tiêu chí chấm CV mới — các hồ sơ đang được chấm lại.",
                            Link = await StaffLinks.JobAsync(_unitOfWork, job.CreatedByUserId, job.Id, ct),
                            DedupKey = $"cv_rubric_saved:{job.Id}:{saved.Value.Document.Id}",
                            IsRead = false,
                        }, ct);
                    }
                    await _unitOfWork.SaveChangesAsync(ct);

                    await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveJobPostingUpdate",
                        new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

                    // Mọi màn đang mở hồ sơ của tin phải chuyển sang "Đang chấm lại" NGAY. Lưu bộ tiêu chí chưa
                    // đổi dòng `applications` nào nên trigger realtime (ADR-057) chưa bắn — phải đẩy tay, tới
                    // đúng những người đọc được hồ sơ của tin. Khi từng hồ sơ chấm xong, trigger lo phần còn lại.
                    var teamUserIds = await _unitOfWork.Repository<JobHiringTeamMember>().QueryAsync(
                        q => q.Where(m => m.JobPostingId == job.Id && m.DeletedAt == null).Select(m => m.UserId), ct);
                    var rescoring = new { id = (Guid?)null, jobPostingId = job.Id };
                    foreach (var userId in teamUserIds.Append(job.CreatedByUserId).Distinct())
                        await _notifications.PublishUserEventAsync(userId, "ReceiveApplicationStatusUpdate", rescoring, ct);
                    await _notifications.PublishGroupEventAsync("hr_admin", "ReceiveApplicationStatusUpdate", rescoring, ct);
                }
            }

            var current = await _sender.Send(new GetJobCvRubricQuery(request.JobPostingId, request.UserId, request.Role), ct);
            return current.IsFailure
                ? current
                : Result.Success(current.Value! with
                {
                    SaveOutcome = new CvRubricSaveOutcomeDto(mode, mode == CvRubricSaveModes.Unchanged ? 0 : current.Value!.ApplicationCount),
                });
        }

        private static class AuditSupport
        {
            public static Task WriteAsync(
                IUnitOfWork uow, Guid actorId, JobPosting job, IReadOnlyList<RubricCriterion> criteria,
                CvScoringPolicy policy, bool formulaOnly, CancellationToken ct)
                => Admin.AdminSupport.WriteAuditAsync(uow, actorId, "job_cv_rubric_saved",
                    nameof(JobPosting), job.Id,
                    AuditMetadata.Serialize(new
                    {
                        jobTitle = job.Title,
                        criteriaCount = criteria.Count,
                        // ADR-075: công thức là quyết định của HM — audit ghi đủ để tra lại ai đặt ngưỡng nào, khi nào.
                        formulaOnly,
                        knockoutCount = criteria.Count(c => c.IsKnockout),
                        minScoreCount = criteria.Count(c => c.MinScore != null),
                        bands = policy.Bands,
                        tiers = policy.Tiers,
                    }), ct);
        }
    }

    // ---------------------------------------------------------------------------------
    //  Công cụ điền nhanh — không lưu gì
    // ---------------------------------------------------------------------------------

    /// <param name="Policy">Công thức đọc được từ file (sheet "Cong thuc"); <c>null</c> = file không khai → trình soạn giữ công thức đang có.</param>
    public record CvRubricDraftDto(
        IReadOnlyList<CvRubricCriterionInput> Criteria, IReadOnlyList<string> Warnings, CvScoringPolicy? Policy = null);

    /// <summary>Đọc file Excel thành bản nháp cho trình soạn. Lỗi trả về dạng cảnh báo để người dùng sửa ngay trên web.</summary>
    /// <param name="Purpose">Bộ CV hay bộ phỏng vấn — trình soạn phỏng vấn dùng chung cửa này (ADR-073/075).</param>
    public record ParseCvRubricSheetCommand(byte[] Bytes, RubricPurpose Purpose = RubricPurpose.Cv) : IRequest<Result<CvRubricDraftDto>>;

    public class ParseCvRubricSheetCommandHandler : IRequestHandler<ParseCvRubricSheetCommand, Result<CvRubricDraftDto>>
    {
        public Task<Result<CvRubricDraftDto>> Handle(ParseCvRubricSheetCommand request, CancellationToken ct)
        {
            if (request.Bytes == null || request.Bytes.Length == 0)
                return Task.FromResult(Result.Failure<CvRubricDraftDto>("File Excel trống."));

            var parsed = RubricSheet.Parse(request.Bytes);
            if (parsed.Criteria.Count == 0)
            {
                var reason = parsed.Errors.FirstOrDefault()?.Message ?? "Không đọc được tiêu chí nào trong file.";
                return Task.FromResult(Result.Failure<CvRubricDraftDto>(reason));
            }

            var normalized = CvRubricEditing.Normalize(CvRubricEditing.ToInput(parsed.Criteria), request.Purpose);
            var forCv = request.Purpose == RubricPurpose.Cv;
            var warnings = parsed.Errors.Select(e => e.Row > 0 ? $"Dòng {e.Row}: {e.Message}" : e.Message)
                .Concat(normalized.Errors)
                .Concat(forCv ? CvScoringPolicy.Validate(parsed.Policy) : Enumerable.Empty<string>())
                .ToList();

            return Task.FromResult(Result.Success(new CvRubricDraftDto(
                CvRubricEditing.ToInput(normalized.Criteria), warnings, forCv ? parsed.Policy : null)));
        }
    }

    /// <summary>Xuất bản nháp đang soạn ra file Excel (không đòi hợp lệ — để gửi người khác góp ý).</summary>
    public record ExportCvRubricSheetQuery(
        IReadOnlyList<CvRubricCriterionInput> Criteria, CvScoringPolicy? Policy = null, RubricPurpose Purpose = RubricPurpose.Cv)
        : IRequest<Result<byte[]>>;

    public class ExportCvRubricSheetQueryHandler : IRequestHandler<ExportCvRubricSheetQuery, Result<byte[]>>
    {
        public Task<Result<byte[]>> Handle(ExportCvRubricSheetQuery request, CancellationToken ct)
        {
            var normalized = CvRubricEditing.Normalize(request.Criteria, request.Purpose);
            return Task.FromResult(normalized.Criteria.Count == 0
                ? Result.Failure<byte[]>("Chưa có tiêu chí nào để xuất.")
                : Result.Success(RubricSheet.Build(normalized.Criteria, request.Policy, request.Purpose)));
        }
    }

    /// <summary>Một mẫu bộ tiêu chí của công ty (playbook <c>cv_rubric</c> cấp công ty do HR Leader tải lên).</summary>
    public record CvRubricTemplateDto(
        Guid Id, string Name, DateTimeOffset CreatedAt, IReadOnlyList<CvRubricCriterionInput> Criteria,
        /// <summary>Công thức đi kèm mẫu (ADR-075) — mẫu không khai thì là mặc định.</summary>
        CvScoringPolicy? Policy = null);

    public record GetCvRubricTemplatesQuery : IRequest<Result<List<CvRubricTemplateDto>>>;

    public class GetCvRubricTemplatesQueryHandler : IRequestHandler<GetCvRubricTemplatesQuery, Result<List<CvRubricTemplateDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCvRubricTemplatesQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<CvRubricTemplateDto>>> Handle(GetCvRubricTemplatesQuery request, CancellationToken ct)
        {
            var docs = await _unitOfWork.Repository<PlaybookDocument>().FindAsync(
                p => p.DeletedAt == null
                     && p.Scope == PlaybookScope.ScopeOrg
                     && p.DocumentType == ScoringRubric.TypeCvRubric
                     && p.RubricJson != null, ct);

            var items = docs
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new CvRubricTemplateDto(
                    d.Id,
                    System.IO.Path.GetFileNameWithoutExtension(d.FileName),
                    d.CreatedAt,
                    CvRubricEditing.ToInput(ScoringRubric.Deserialize(d.RubricJson)),
                    CvScoringPolicy.FromStorage(d.ScoringPolicyJson)))
                .Where(t => t.Criteria.Count > 0)
                .ToList();

            return Result.Success(items);
        }
    }

    /// <summary>AI gợi ý bản nháp bộ tiêu chí từ nội dung phiếu / tin (ADR-070).</summary>
    public record SuggestCvRubricCommand(CvRubricSuggestionInput Input) : IRequest<Result<CvRubricDraftDto>>;

    public class SuggestCvRubricCommandHandler : IRequestHandler<SuggestCvRubricCommand, Result<CvRubricDraftDto>>
    {
        private readonly IGeminiProvider _gemini;

        public SuggestCvRubricCommandHandler(IGeminiProvider gemini) => _gemini = gemini;

        public async Task<Result<CvRubricDraftDto>> Handle(SuggestCvRubricCommand request, CancellationToken ct)
        {
            var input = request.Input;
            if (string.IsNullOrWhiteSpace(input.Title))
                return Result.Failure<CvRubricDraftDto>("Hãy nhập vị trí cần tuyển trước khi nhờ AI gợi ý.");
            if (string.IsNullOrWhiteSpace(input.Description) && string.IsNullOrWhiteSpace(input.Requirements))
                return Result.Failure<CvRubricDraftDto>("Hãy nhập mô tả công việc hoặc yêu cầu ứng viên để AI có căn cứ gợi ý.");

            var suggested = await _gemini.SuggestCvRubricAsync(input, ct);
            if (suggested.IsFailure)
                return Result.Failure<CvRubricDraftDto>(suggested.Error!);

            var rows = suggested.Value!
                .Take(ScoringRubric.MaxCriteria)
                .Select(s => new CvRubricCriterionInput
                {
                    Name = s.Name,
                    Weight = s.Weight,
                    Description = s.Description,
                    Levels = new RubricLevels { Excellent = s.Excellent, Good = s.Good, Fair = s.Fair, Poor = s.Poor },
                    Checks = s.Checks?.Take(ScoringRubric.MaxChecks).Select(text => new RubricCheck { Text = text }).ToList(),
                })
                .ToList();
            CvRubricEditing.RebalanceWeights(rows);

            // AI chỉ gợi ý TIÊU CHÍ; công thức (ngưỡng, điều kiện bắt buộc) là quyết định của HM — bản nháp không đụng tới.
            var normalized = CvRubricEditing.Normalize(rows, RubricPurpose.Cv);
            return Result.Success(new CvRubricDraftDto(CvRubricEditing.ToInput(normalized.Criteria), normalized.Errors));
        }
    }
}
