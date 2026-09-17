using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks.Queries.GetPlaybooks;
using MediatR;

namespace ARI.Application.Playbooks.Queries.GetJobPlaybooks
{
    /// <summary>
    /// Playbook của MỘT tin (phạm vi <c>job_posting</c> + <c>round</c>) kèm cờ người gọi có được thêm/xoá.
    /// Cờ do SERVER quyết (quy tắc 19) — giao diện chỉ việc hiện hay ẩn nút.
    /// </summary>
    public record JobPlaybooksDto(List<PlaybookListItemDto> Items, bool CanManage);

    /// <summary>
    /// Playbook theo tin, đọc từ chính màn tin (ADR-069). Mọi thành viên đội tuyển dụng đọc được — Recruiter
    /// cần biết AI sẽ hỏi theo tài liệu nào — nhưng chỉ HM chính và quản trị viên được viết.
    /// </summary>
    public record GetJobPlaybooksQuery(Guid JobPostingId, Guid? UserId, string? Role) : IRequest<Result<JobPlaybooksDto>>;

    public class GetJobPlaybooksQueryHandler : IRequestHandler<GetJobPlaybooksQuery, Result<JobPlaybooksDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetJobPlaybooksQueryHandler(IUnitOfWork unitOfWork) { _unitOfWork = unitOfWork; }

        public async Task<Result<JobPlaybooksDto>> Handle(GetJobPlaybooksQuery request, CancellationToken ct)
        {
            var (ok, job) = await JobAccess.CanViewAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<JobPlaybooksDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok)
                return Result.Failure<JobPlaybooksDto>("Bạn không thuộc đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            var jobId = job.Id;
            // Bộ tiêu chí chấm CV có panel riêng (ADR-070) — không lặp lại ở đây kèm nút xoá không dùng được.
            var items = await PlaybookListing.LoadAsync(_unitOfWork,
                q => q.Where(d => d.ScopeRefId == jobId
                                  && d.DocumentType != ScoringRubric.TypeCvRubric
                                  && (d.Scope == PlaybookScope.ScopeJobPosting || d.Scope == PlaybookScope.ScopeRound)), ct);

            // Cả tin trước, rồi theo vòng — đúng thứ tự AI ghép ngữ cảnh cho một buổi phỏng vấn.
            items = items
                .OrderBy(i => i.Scope == PlaybookScope.ScopeRound ? 1 : 0)
                .ThenBy(i => i.RoundNumber ?? 0)
                .ThenByDescending(i => i.CreatedAt)
                .ToList();

            var canManage = !string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase)
                            && await PlaybookAccess.CanManageJobAsync(_unitOfWork, jobId, request.UserId, request.Role, ct);

            return Result.Success(new JobPlaybooksDto(items, canManage));
        }
    }
}
