using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Queries.GetRecruiters
{
    /// <summary>
    /// Tải tuyển dụng của từng Recruiter — màn "Phân công & tải tuyển dụng" của HR Lead.
    ///
    /// Đây là nghiệp vụ quản lý tuyển dụng theo mô hình ATS, không phải CRUD tài khoản: người
    /// quản lý theo dõi *req load* (số tin đang gánh), *pipeline theo giai đoạn* và *tuổi chờ*
    /// của từng nút thắt để phát hiện quá tải trước khi vỡ SLA, rồi cân tải bằng cách chuyển
    /// giao tin (<see cref="Commands.ReassignJob.ReassignJobCommand"/>).
    ///
    /// Vòng đời tài khoản (tạo/đổi vai trò/khoá) KHÔNG nằm ở đây — thuộc Super Admin
    /// (ADR-023 pre-provisioning, ADR-041 AccountRequest).
    /// </summary>
    public record GetRecruitersQuery : IRequest<Result<List<RecruiterOverviewDto>>>;

    public class GetRecruitersQueryHandler
        : IRequestHandler<GetRecruitersQuery, Result<List<RecruiterOverviewDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Trạng thái hồ sơ coi như đã đóng — không còn tốn công của recruiter nữa.</summary>
        private static readonly HashSet<string> ClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "pass", "not_pass", "rejected", "withdrawn"
        };

        public GetRecruitersQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<RecruiterOverviewDto>>> Handle(GetRecruitersQuery request, CancellationToken ct)
        {
            var recruiters = (await _unitOfWork.Repository<User>()
                .FindAsync(u => u.Role == AppRoles.Recruiter && u.DeletedAt == null, ct)).ToList();

            if (recruiters.Count == 0)
                return Result.Success(new List<RecruiterOverviewDto>());

            var recruiterIds = recruiters.Select(r => r.Id).ToHashSet();
            var now = DateTimeOffset.UtcNow;

            // Nạp theo lô rồi gộp trong bộ nhớ. Số recruiter của một doanh nghiệp là hàng chục,
            // gọi mỗi người một query sẽ thành N+1 mà không nhanh hơn.
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => j.DeletedAt == null && recruiterIds.Contains(j.CreatedByUserId), ct)).ToList();

            var jobOwner = jobs.ToDictionary(j => j.Id, j => j.CreatedByUserId);
            var jobIds = jobOwner.Keys.ToHashSet();

            var applications = jobIds.Count == 0
                ? new List<Domain.Entities.Application>()
                : (await _unitOfWork.Repository<Domain.Entities.Application>()
                    .FindAsync(a => a.DeletedAt == null && jobIds.Contains(a.JobPostingId), ct)).ToList();

            var appOwner = applications
                .Where(a => jobOwner.ContainsKey(a.JobPostingId))
                .ToDictionary(a => a.Id, a => jobOwner[a.JobPostingId]);
            var appIds = appOwner.Keys.ToHashSet();

            var bookings = appIds.Count == 0
                ? new List<InterviewBooking>()
                : (await _unitOfWork.Repository<InterviewBooking>()
                    .FindAsync(b => appIds.Contains(b.ApplicationId), ct)).ToList();

            // Hồ sơ đã có ít nhất một lịch còn hiệu lực → không còn nằm ở nút "chờ gán lịch".
            var scheduledAppIds = bookings
                .Where(b => !string.Equals(b.Status, "cancelled", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(b.Status, "declined", StringComparison.OrdinalIgnoreCase))
                .Select(b => b.ApplicationId)
                .ToHashSet();

            var evaluations = appIds.Count == 0
                ? new List<Evaluation>()
                : (await _unitOfWork.Repository<Evaluation>()
                    // Buổi THỬ bị loại: nó là không gian riêng của ứng viên, nhân sự không thấy
                    // và cũng không phải duyệt (ADR-051).
                    .FindAsync(e => appIds.Contains(e.ApplicationId) && e.SessionType == "real", ct)).ToList();

            var evalIds = evaluations.Select(e => e.Id).ToHashSet();
            var reviewedEvalIds = evalIds.Count == 0
                ? new HashSet<Guid>()
                : (await _unitOfWork.Repository<HrReview>()
                    .FindAsync(r => evalIds.Contains(r.EvaluationId), ct))
                    .Select(r => r.EvaluationId).ToHashSet();

            int AgeInDays(DateTimeOffset from) => Math.Max(0, (int)(now - from).TotalDays);

            var result = new List<RecruiterOverviewDto>(recruiters.Count);

            foreach (var user in recruiters)
            {
                var ownJobs = jobs.Where(j => j.CreatedByUserId == user.Id).ToList();
                var ownJobIds = ownJobs.Select(j => j.Id).ToHashSet();
                var ownApps = applications.Where(a => ownJobIds.Contains(a.JobPostingId)).ToList();
                var ownAppIds = ownApps.Select(a => a.Id).ToHashSet();

                var drafts = ownJobs.Where(j => string.Equals(j.Status, "draft", StringComparison.OrdinalIgnoreCase)).ToList();

                // Chưa sàng = vẫn ở trạng thái vừa nộp. Sau khi sàng, hồ sơ chuyển sang
                // "screening"/"interview" nên rơi khỏi nhóm này.
                var unscreened = ownApps
                    .Where(a => string.Equals(a.Status, "applied", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(a.Status, "new", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(a.Status))
                    .ToList();

                // Đã qua sàng nhưng chưa có lịch nào → đang chờ recruiter gán khung giờ.
                var awaitingScheduling = ownApps
                    .Where(a => !ClosedStatuses.Contains(a.Status ?? string.Empty)
                                && string.Equals(a.Status, "screening", StringComparison.OrdinalIgnoreCase)
                                && !scheduledAppIds.Contains(a.Id))
                    .ToList();

                var declined = bookings
                    .Where(b => ownAppIds.Contains(b.ApplicationId)
                                && string.Equals(b.ConfirmationStatus, "declined", StringComparison.OrdinalIgnoreCase))
                    .Select(b => b.ApplicationId)
                    .Distinct()
                    // Đã được gán lại lịch mới thì không còn tồn đọng nữa.
                    .Count(id => !scheduledAppIds.Contains(id));

                var pendingEvals = evaluations
                    .Where(e => ownAppIds.Contains(e.ApplicationId) && !reviewedEvalIds.Contains(e.Id))
                    .ToList();

                var draftsOldest = drafts.Count == 0 ? 0 : drafts.Max(j => AgeInDays(j.CreatedAt));
                var unscreenedOldest = unscreened.Count == 0 ? 0 : unscreened.Max(a => AgeInDays(a.CreatedAt));
                var awaitingOldest = awaitingScheduling.Count == 0 ? 0 : awaitingScheduling.Max(a => AgeInDays(a.CreatedAt));
                var pendingOldest = pendingEvals.Count == 0 ? 0 : pendingEvals.Max(e => AgeInDays(e.CreatedAt));

                result.Add(new RecruiterOverviewDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Department = user.Department,
                    IsActive = user.IsActive,
                    LockReason = user.LockReason,
                    LastLoginAt = user.LastLoginAt,

                    JobsTotal = ownJobs.Count,
                    JobsActive = ownJobs.Count(j => string.Equals(j.Status, "active", StringComparison.OrdinalIgnoreCase)),

                    DraftsAwaitingApproval = drafts.Count,
                    DraftsOldestDays = draftsOldest,

                    ApplicationsUnscreened = unscreened.Count,
                    UnscreenedOldestDays = unscreenedOldest,

                    AwaitingScheduling = awaitingScheduling.Count,
                    AwaitingSchedulingOldestDays = awaitingOldest,

                    DeclinedNeedRebooking = declined,

                    PendingReviews = pendingEvals.Count,
                    PendingReviewsOldestDays = pendingOldest,

                    ActivePipeline = ownApps.Count(a => !ClosedStatuses.Contains(a.Status ?? string.Empty)),
                    Hired = ownApps.Count(a => string.Equals(a.Status, "pass", StringComparison.OrdinalIgnoreCase)),

                    OldestBottleneckDays = new[] { draftsOldest, unscreenedOldest, awaitingOldest, pendingOldest }.Max(),
                });
            }

            // Ai đang tắc lâu nhất lên đầu — đó là thứ HR Lead cần nhìn thấy trước.
            return Result.Success(result
                .OrderByDescending(r => r.OldestBottleneckDays)
                .ThenByDescending(r => r.ActivePipeline)
                .ThenBy(r => r.FullName)
                .ToList());
        }
    }
}
