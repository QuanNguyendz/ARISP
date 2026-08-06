using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
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

            // Nếu là Recruiter thì chỉ được tính là Staff đối với tin do CHÍNH HỌ tạo ra.
            if (isStaff && string.Equals(request.Role, "recruiter", StringComparison.OrdinalIgnoreCase))
            {
                if (job.CreatedByUserId != request.CurrentUserId)
                {
                    isStaff = false;
                }
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
            }

            return Result.Success(jobResponse);
        }
    }
}
