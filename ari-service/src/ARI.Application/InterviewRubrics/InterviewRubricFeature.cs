using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.InterviewRubrics
{
    // ===================================================================================
    //  Bộ tiêu chí chấm PHỎNG VẤN theo tin (ADR-073)
    //
    //  Hiring Manager khai cho từng tin: một bộ CHUNG áp mọi vòng hội thoại, và (tuỳ chọn) bộ RIÊNG cho vòng
    //  cần chấm khác. Không có bộ công ty dự phòng — bộ cấp công ty chỉ là mẫu để chép. Cùng trình soạn,
    //  cùng bộ chuẩn hoá với bộ tiêu chí chấm CV (ADR-070), nhưng không có ý kiểm.
    // ===================================================================================

    /// <summary>Một bộ tiêu chí đang sống (chung của tin hoặc riêng của một vòng).</summary>
    public record InterviewRubricSetDto(
        int? RoundNumber,
        IReadOnlyList<CvRubricCriterionInput> Criteria,
        Guid? DocumentId,
        DateTimeOffset? SavedAt,
        string? SavedBy);

    /// <summary>Một vòng hội thoại của tin và bộ tiêu chí nào đang áp vào nó.</summary>
    public record InterviewRubricRoundDto(int RoundNumber, string RoundType, bool HasOwnRubric, bool Covered);

    public record JobInterviewRubricDto(
        InterviewRubricSetDto JobLevel,
        IReadOnlyList<InterviewRubricSetDto> RoundSets,
        IReadOnlyList<InterviewRubricRoundDto> Rounds,
        /// <summary>Vòng hội thoại chưa có bộ nào áp vào — rỗng mới đăng tin được.</summary>
        IReadOnlyList<int> MissingRounds,
        bool CanEdit,
        /// <summary>Số buổi phỏng vấn đã xong nhưng đang chờ bộ tiêu chí để chấm.</summary>
        int WaitingSessionCount);

    public record GetJobInterviewRubricQuery(Guid JobPostingId, Guid? UserId, string? Role) : IRequest<Result<JobInterviewRubricDto>>;

    public class GetJobInterviewRubricQueryHandler : IRequestHandler<GetJobInterviewRubricQuery, Result<JobInterviewRubricDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetJobInterviewRubricQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<JobInterviewRubricDto>> Handle(GetJobInterviewRubricQuery request, CancellationToken ct)
        {
            var (ok, job) = await JobAccess.CanViewAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<JobInterviewRubricDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok)
                return Result.Failure<JobInterviewRubricDto>("Bạn không thuộc đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            var docs = await InterviewRubricStore.LiveForJobAsync(_unitOfWork, job.Id, ct);
            var authorIds = docs.Select(d => d.UploadedByUserId).Distinct().ToList();
            var authors = authorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<User>().FindAsync(u => authorIds.Contains(u.Id), ct))
                    .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            InterviewRubricSetDto ToDto(PlaybookDocument? d, int? round) => new(
                round,
                d == null ? new List<CvRubricCriterionInput>() : CvRubricEditing.ToInput(ScoringRubric.Deserialize(d.RubricJson)),
                d?.Id,
                d?.CreatedAt,
                d != null && authors.TryGetValue(d.UploadedByUserId, out var name) ? name : null);

            var jobLevel = docs.Where(d => d.Scope == PlaybookScope.ScopeJobPosting)
                .OrderByDescending(d => d.CreatedAt).FirstOrDefault();
            var roundSets = docs.Where(d => d.Scope == PlaybookScope.ScopeRound && d.RoundNumber != null)
                .GroupBy(d => d.RoundNumber!.Value)
                .Select(g => ToDto(g.OrderByDescending(d => d.CreatedAt).First(), g.Key))
                .OrderBy(s => s.RoundNumber)
                .ToList();

            var jobId = job.Id;
            var configs = (await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == jobId, ct)).ToList();
            var rounds = configs
                .Where(r => InterviewRoundTypes.NeedsHiringManager(r.RoundType))
                .GroupBy(r => r.RoundNumber)
                .Select(g => g.First())
                .OrderBy(r => r.RoundNumber)
                .Select(r => new InterviewRubricRoundDto(
                    r.RoundNumber, r.RoundType,
                    roundSets.Any(s => s.RoundNumber == r.RoundNumber),
                    InterviewRubricStore.Pick(docs, r.RoundNumber) != null))
                .ToList();
            if (configs.Count == 0)
                rounds.Add(new InterviewRubricRoundDto(1, InterviewRoundTypes.Screening, false, InterviewRubricStore.Pick(docs, 1) != null));

            var canEdit = !string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase)
                          && await PlaybookAccess.CanManageJobAsync(_unitOfWork, job.Id, request.UserId, request.Role, ct);

            var appIds = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().QueryAsync(
                q => q.Where(a => a.JobPostingId == jobId).Select(a => a.Id), ct);
            var waiting = appIds.Count == 0 ? 0 : await _unitOfWork.Repository<InterviewSession>().CountAsync(
                s => appIds.Contains(s.ApplicationId) && s.EvaluationStatus == EvaluationStatuses.BlockedNoRubric, ct);

            return Result.Success(new JobInterviewRubricDto(
                ToDto(jobLevel, null),
                roundSets,
                rounds,
                rounds.Where(r => !r.Covered).Select(r => r.RoundNumber).ToList(),
                canEdit,
                waiting));
        }
    }

    public record SaveJobInterviewRubricCommand(
        Guid JobPostingId, int? RoundNumber, IReadOnlyList<CvRubricCriterionInput> Criteria, Guid? UserId, string? Role)
        : IRequest<Result<JobInterviewRubricDto>>;

    public class SaveJobInterviewRubricCommandHandler : IRequestHandler<SaveJobInterviewRubricCommand, Result<JobInterviewRubricDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly InterviewRubricService _rubrics;
        private readonly INotificationService _notifications;
        private readonly ISender _sender;

        public SaveJobInterviewRubricCommandHandler(
            IUnitOfWork unitOfWork, InterviewRubricService rubrics, INotificationService notifications, ISender sender)
        {
            _unitOfWork = unitOfWork;
            _rubrics = rubrics;
            _notifications = notifications;
            _sender = sender;
        }

        public async Task<Result<JobInterviewRubricDto>> Handle(SaveJobInterviewRubricCommand request, CancellationToken ct)
        {
            if (request.UserId is not { } actorId)
                return Result.Failure<JobInterviewRubricDto>("Không xác định được người lưu.", CommonErrorCodes.Forbidden);

            var accessError = await InterviewRubricAccess.CheckAsync(_unitOfWork, request.JobPostingId, request.RoundNumber, actorId, request.Role, ct);
            if (accessError != null) return accessError.As<JobInterviewRubricDto>();

            var normalized = CvRubricEditing.Normalize(request.Criteria, RubricPurpose.Interview);
            if (!normalized.IsValid)
                return Result.Failure<JobInterviewRubricDto>(string.Join(" · ", normalized.Errors.Take(10)));

            var saved = await _rubrics.SaveAsync(request.JobPostingId, request.RoundNumber, normalized.Criteria, actorId, ct);
            if (saved.IsFailure)
                return saved.ErrorCode == null
                    ? Result.Failure<JobInterviewRubricDto>(saved.Error!)
                    : Result.Failure<JobInterviewRubricDto>(saved.Error!, saved.ErrorCode);

            if (saved.Value.Changed)
                await InterviewRubricAccess.AfterChangeAsync(_unitOfWork, _notifications, request.JobPostingId, actorId,
                    "job_interview_rubric_saved", request.RoundNumber, normalized.Criteria.Count, saved.Value.Document.Id, ct);

            return await _sender.Send(new GetJobInterviewRubricQuery(request.JobPostingId, request.UserId, request.Role), ct);
        }
    }

    /// <summary>Bỏ bộ riêng của một vòng — vòng quay về dùng bộ chung của tin.</summary>
    public record RemoveJobInterviewRubricRoundCommand(Guid JobPostingId, int RoundNumber, Guid? UserId, string? Role)
        : IRequest<Result<JobInterviewRubricDto>>;

    public class RemoveJobInterviewRubricRoundCommandHandler
        : IRequestHandler<RemoveJobInterviewRubricRoundCommand, Result<JobInterviewRubricDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly InterviewRubricService _rubrics;
        private readonly INotificationService _notifications;
        private readonly ISender _sender;

        public RemoveJobInterviewRubricRoundCommandHandler(
            IUnitOfWork unitOfWork, InterviewRubricService rubrics, INotificationService notifications, ISender sender)
        {
            _unitOfWork = unitOfWork;
            _rubrics = rubrics;
            _notifications = notifications;
            _sender = sender;
        }

        public async Task<Result<JobInterviewRubricDto>> Handle(RemoveJobInterviewRubricRoundCommand request, CancellationToken ct)
        {
            if (request.UserId is not { } actorId)
                return Result.Failure<JobInterviewRubricDto>("Không xác định được người thao tác.", CommonErrorCodes.Forbidden);

            var accessError = await InterviewRubricAccess.CheckAsync(_unitOfWork, request.JobPostingId, request.RoundNumber, actorId, request.Role, ct);
            if (accessError != null) return accessError.As<JobInterviewRubricDto>();

            // Tin đang tuyển mà bỏ bộ riêng khi chưa có bộ chung là để vòng đó không còn gì để chấm.
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (job != null && string.Equals(job.Status, "active", StringComparison.OrdinalIgnoreCase)
                && await InterviewRubricStore.JobLevelAsync(_unitOfWork, job.Id, ct) == null)
                return Result.Failure<JobInterviewRubricDto>(
                    "Tin đang tuyển chưa có bộ tiêu chí chung — bỏ bộ riêng của vòng này là vòng không còn gì để chấm. Hãy khai bộ chung trước.",
                    InterviewRubricErrors.Required);

            var removed = await _rubrics.RemoveRoundAsync(request.JobPostingId, request.RoundNumber, ct);
            if (removed.IsFailure)
                return removed.ErrorCode == null
                    ? Result.Failure<JobInterviewRubricDto>(removed.Error!)
                    : Result.Failure<JobInterviewRubricDto>(removed.Error!, removed.ErrorCode);

            if (removed.Value)
                await InterviewRubricAccess.AfterChangeAsync(_unitOfWork, _notifications, request.JobPostingId, actorId,
                    "job_interview_rubric_round_removed", request.RoundNumber, 0, null, ct);

            return await _sender.Send(new GetJobInterviewRubricQuery(request.JobPostingId, request.UserId, request.Role), ct);
        }
    }

    public static class InterviewRubricErrors
    {
        /// <summary>Tin có vòng phỏng vấn chưa có bộ tiêu chí — FE dịch mã này (bảng <c>CODE_KEYS</c>).</summary>
        public const string Required = "interview_rubric_required";
    }

    internal sealed class AccessFailure
    {
        public string Message { get; }
        public string? Code { get; }
        public AccessFailure(string message, string? code) { Message = message; Code = code; }
        public Result<T> As<T>() => Code == null ? Result.Failure<T>(Message) : Result.Failure<T>(Message, Code);
    }

    internal static class InterviewRubricAccess
    {
        /// <summary>
        /// Cùng luật với playbook theo tin (ADR-069): Hiring Manager chính của tin, hoặc quản trị viên. Bộ riêng
        /// phải gắn đúng một vòng HỘI THOẠI có thật (vòng trắc nghiệm tự chấm, không có gì để tiêu chí điều khiển).
        /// </summary>
        public static async Task<AccessFailure?> CheckAsync(
            IUnitOfWork uow, Guid jobPostingId, int? roundNumber, Guid actorId, string? role, CancellationToken ct)
        {
            var (error, code) = await PlaybookAccess.CheckWriteAsync(
                uow, PlaybookScope.ScopeJobPosting, jobPostingId, actorId, role, ct);
            if (error != null) return new AccessFailure(error, code);

            if (roundNumber.HasValue)
            {
                var roundError = await PlaybookAccess.CheckRoundAsync(uow, jobPostingId, roundNumber, ct);
                if (roundError != null) return new AccessFailure(roundError, null);
            }
            return null;
        }

        /// <summary>Ghi audit + báo Recruiter chủ tin + làm mới các màn đang mở tin.</summary>
        public static async Task AfterChangeAsync(
            IUnitOfWork uow, INotificationService notifications, Guid jobPostingId, Guid actorId,
            string auditAction, int? roundNumber, int criteriaCount, Guid? documentId, CancellationToken ct)
        {
            var job = await uow.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return;

            await Admin.AdminSupport.WriteAuditAsync(uow, actorId, auditAction, nameof(JobPosting), job.Id,
                AuditMetadata.Serialize(new { jobTitle = job.Title, roundNumber, criteriaCount }), ct);

            if (job.CreatedByUserId != actorId && documentId is { } docId)
            {
                await uow.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = job.CreatedByUserId,
                    Type = "system",
                    Title = "Bộ tiêu chí chấm phỏng vấn đã được cập nhật",
                    Body = roundNumber is { } r
                        ? $"Tin \"{job.Title}\" có bộ tiêu chí chấm phỏng vấn riêng cho vòng {r}."
                        : $"Tin \"{job.Title}\" có bộ tiêu chí chấm phỏng vấn mới.",
                    Link = await StaffLinks.JobAsync(uow, job.CreatedByUserId, job.Id, ct),
                    DedupKey = $"interview_rubric_saved:{job.Id}:{docId}",
                    IsRead = false,
                }, ct);
            }
            await uow.SaveChangesAsync(ct);

            // Cổng đăng tin và khối "Kết quả phỏng vấn" đọc bộ tiêu chí — mọi màn đang mở tin phải tải lại.
            var teamUserIds = await uow.Repository<JobHiringTeamMember>().QueryAsync(
                q => q.Where(m => m.JobPostingId == job.Id && m.DeletedAt == null).Select(m => m.UserId), ct);
            var payload = new { JobId = job.Id, Status = job.Status, Title = job.Title };
            foreach (var userId in teamUserIds.Append(job.CreatedByUserId).Distinct())
                await notifications.PublishUserEventAsync(userId, "ReceiveJobPostingUpdate", payload, ct);
        }
    }

    // ---------------------------------------------------------------------------------
    //  Công cụ điền nhanh — không lưu gì
    // ---------------------------------------------------------------------------------

    /// <summary>Mẫu bộ tiêu chí phỏng vấn của công ty (playbook <c>interview_rubric</c> cấp công ty) để HM chép.</summary>
    public record GetInterviewRubricTemplatesQuery : IRequest<Result<List<CvRubricTemplateDto>>>;

    public class GetInterviewRubricTemplatesQueryHandler
        : IRequestHandler<GetInterviewRubricTemplatesQuery, Result<List<CvRubricTemplateDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetInterviewRubricTemplatesQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<CvRubricTemplateDto>>> Handle(GetInterviewRubricTemplatesQuery request, CancellationToken ct)
        {
            var docs = await _unitOfWork.Repository<PlaybookDocument>().FindAsync(
                p => p.DeletedAt == null
                     && p.Scope == PlaybookScope.ScopeOrg
                     && p.DocumentType == ScoringRubric.TypeInterviewRubric
                     && p.RubricJson != null, ct);

            var items = docs
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new CvRubricTemplateDto(
                    d.Id,
                    System.IO.Path.GetFileNameWithoutExtension(d.FileName),
                    d.CreatedAt,
                    CvRubricEditing.ToInput(ScoringRubric.Deserialize(d.RubricJson)
                        .Select(c => { c.Checks = null; return c; }))))
                .Where(t => t.Criteria.Count > 0)
                .ToList();

            return Result.Success(items);
        }
    }

    /// <summary>AI gợi ý bản nháp bộ tiêu chí chấm phỏng vấn từ nội dung tin (ADR-073).</summary>
    public record SuggestInterviewRubricCommand(CvRubricSuggestionInput Input) : IRequest<Result<CvRubricDraftDto>>;

    public class SuggestInterviewRubricCommandHandler : IRequestHandler<SuggestInterviewRubricCommand, Result<CvRubricDraftDto>>
    {
        private readonly IGeminiProvider _gemini;

        public SuggestInterviewRubricCommandHandler(IGeminiProvider gemini) => _gemini = gemini;

        public async Task<Result<CvRubricDraftDto>> Handle(SuggestInterviewRubricCommand request, CancellationToken ct)
        {
            var input = request.Input;
            if (string.IsNullOrWhiteSpace(input.Title))
                return Result.Failure<CvRubricDraftDto>("Hãy nhập vị trí cần tuyển trước khi nhờ AI gợi ý.");
            if (string.IsNullOrWhiteSpace(input.Description) && string.IsNullOrWhiteSpace(input.Requirements))
                return Result.Failure<CvRubricDraftDto>("Tin cần có mô tả công việc hoặc yêu cầu ứng viên để AI có căn cứ gợi ý.");

            var suggested = await _gemini.SuggestInterviewRubricAsync(input, ct);
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
                })
                .ToList();
            CvRubricEditing.RebalanceWeights(rows);

            var normalized = CvRubricEditing.Normalize(rows, RubricPurpose.Interview);
            return Result.Success(new CvRubricDraftDto(CvRubricEditing.ToInput(normalized.Criteria), normalized.Errors));
        }
    }
}
