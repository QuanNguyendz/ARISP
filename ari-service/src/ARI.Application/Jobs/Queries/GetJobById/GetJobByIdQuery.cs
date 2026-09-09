using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Queries.GetJobById
{
    /// <summary>Chi tiết job cho trang mô tả công việc (staff xem được cả draft/paused + resolve URL file JD).</summary>
    public record GetJobByIdQuery(Guid Id, bool IsStaff, Guid? CurrentUserId = null, string? Role = null) : IRequest<Result<JobPostingResponse>>;

    public class GetJobByIdQueryHandler : IRequestHandler<GetJobByIdQuery, Result<JobPostingResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetJobByIdQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<JobPostingResponse>> Handle(GetJobByIdQuery request, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.Id, ct);
            if (job == null)
                return Result.Failure<JobPostingResponse>("Job posting not found.", CommonErrorCodes.NotFound);

            var isStaff = request.IsStaff;

            // Nhân sự KHÔNG phải quản trị viên chỉ được tính là staff với tin thuộc phạm vi của mình:
            // chủ tin, hoặc thành viên đội tuyển dụng (ADR-061). Trước đây chỗ này chỉ kiểm riêng
            // Recruiter bằng một câu so chuỗi, nên thêm vai trò mới là phải nhớ sửa tay ở đây —
            // Hiring Manager đã lọt đúng vào khe đó.
            if (isStaff && !RoleNames.IsAdmin(request.Role))
            {
                var (_, _, level) = await JobAccess.EvaluateAsync(
                    _unitOfWork, request.Id, request.CurrentUserId, request.Role, ct);
                if (level < JobAccessLevel.TeamMember) isStaff = false;
            }

            if (!isStaff && (job.Status != "active" || !job.IsPublicListing))
                return Result.Failure<JobPostingResponse>("Job posting not found or access denied.", CommonErrorCodes.NotFound);

            var rounds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(
                r => r.JobPostingId == request.Id,
                ct);

            var roundDtos = rounds.OrderBy(r => r.RoundNumber).Select(RoundConfigDto.FromEntity).ToList();
            var jobResponse = JobPostingResponse.FromEntity(job, roundDtos);

            var creator = await _unitOfWork.Repository<User>().GetByIdAsync(job.CreatedByUserId, ct);
            if (creator != null)
            {
                jobResponse.CreatedByName = string.IsNullOrWhiteSpace(creator.FullName) ? creator.Email : creator.FullName;
            }

            // Staff: resolve storageKey của file JD -> URL dùng được (để xem/tải file JD gốc + bản đã đóng dấu)
            if (isStaff)
            {
                if (!string.IsNullOrEmpty(jobResponse.JdFileUrl))
                    jobResponse.JdFileUrl = await _fileStorage.GetUrlAsync(jobResponse.JdFileUrl, ct);
                if (!string.IsNullOrEmpty(jobResponse.SignedJdFileUrl))
                    jobResponse.SignedJdFileUrl = await _fileStorage.GetUrlAsync(jobResponse.SignedJdFileUrl, ct);

                // Cổng Hiring Manager (ADR-061). Nằm TRONG nhánh staff vì HmSignOffReason là góp ý
                // nội bộ về tin; ứng viên xem tin trên Job Board đi qua đúng handler này với
                // isStaff = false.
                var primaryHm = await Common.Security.JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
                jobResponse.HmSignOffStatus = job.HmSignOffStatus;
                jobResponse.HmSignOffReason = job.HmSignOffReason;
                jobResponse.RequiresHmApproval = primaryHm != null;
                if (primaryHm != null)
                {
                    jobResponse.HiringManagerUserId = primaryHm.UserId;
                    var hmUser = await _unitOfWork.Repository<User>().GetByIdAsync(primaryHm.UserId, ct);
                    if (hmUser != null)
                        jobResponse.HiringManagerName =
                            string.IsNullOrWhiteSpace(hmUser.FullName) ? hmUser.Email : hmUser.FullName;
                }
            }

            return Result.Success(jobResponse);
        }
    }
}
