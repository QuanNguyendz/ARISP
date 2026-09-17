using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ARI.Application.HiringTeam
{
    // ============================================================
    // DTO
    // ============================================================

    /// <summary>Một thành viên trong đội tuyển dụng của tin, kèm thông tin người dùng để hiển thị.</summary>
    public class HiringTeamMemberDto
    {
        public Guid Id { get; set; }
        public Guid JobPostingId { get; set; }
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string RoleOnJob { get; set; } = JobTeamRoles.HiringManager;
        public bool IsPrimary { get; set; }
        public string? AddedByName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>Ứng viên cho vị trí Hiring Manager khi nhân sự bấm nút gán.</summary>
    public class HiringManagerOptionDto
    {
        public Guid Id { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Department { get; set; }

        /// <summary>Khoá đội (ADR-065) — so khớp bằng khoá thay vì chuỗi nên hết trượt do khác cách gõ.</summary>
        public Guid? DepartmentId { get; set; }

        /// <summary>Phòng ban trùng với phòng ban của tin — chỉ để xếp gợi ý lên đầu, KHÔNG phải quyền.</summary>
        public bool MatchesJobDepartment { get; set; }
    }

    // ============================================================
    // GET /api/jobs/{id}/hiring-team
    // ============================================================

    public record GetHiringTeamQuery(Guid JobPostingId, Guid? UserId, string? Role)
        : IRequest<Result<List<HiringTeamMemberDto>>>;

    public class GetHiringTeamQueryHandler : IRequestHandler<GetHiringTeamQuery, Result<List<HiringTeamMemberDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetHiringTeamQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<HiringTeamMemberDto>>> Handle(GetHiringTeamQuery request, CancellationToken ct)
        {
            var (_, job, level) = await JobAccess.EvaluateAsync(
                _unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<List<HiringTeamMemberDto>>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            // Thành viên đội phải thấy được chính đội của mình.
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<List<HiringTeamMemberDto>>(
                    "Bạn không có quyền xem đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            return Result.Success(await LoadAsync(_unitOfWork, request.JobPostingId, ct));
        }

        internal static async Task<List<HiringTeamMemberDto>> LoadAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, CancellationToken ct)
        {
            var members = (await unitOfWork.Repository<JobHiringTeamMember>()
                .FindAsync(m => m.JobPostingId == jobPostingId, ct)).ToList();
            if (members.Count == 0) return new List<HiringTeamMemberDto>();

            var userIds = members.Select(m => m.UserId)
                .Concat(members.Select(m => m.AddedByUserId))
                .Distinct().ToList();

            var users = (await unitOfWork.Repository<User>()
                    .QueryAsync(q => q.Where(u => userIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.FullName, u.Email, u.DepartmentId }), ct))
                .ToDictionary(u => u.Id);

            // Tên đội tra một lượt (ADR-065) — `users.department` chuỗi đã bị thay bằng khoá ngoại.
            var departmentNames = await ARI.Application.Departments.DepartmentLookup.NamesAsync(
                unitOfWork, users.Values.Where(u => u.DepartmentId.HasValue).Select(u => u.DepartmentId!.Value), ct);

            return members
                // Hiring Manager chính lên đầu — đó là người nhân sự cần nhìn thấy trước.
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.CreatedAt)
                .Select(m =>
                {
                    users.TryGetValue(m.UserId, out var u);
                    users.TryGetValue(m.AddedByUserId, out var by);
                    return new HiringTeamMemberDto
                    {
                        Id = m.Id,
                        JobPostingId = m.JobPostingId,
                        UserId = m.UserId,
                        FullName = u?.FullName,
                        Email = u?.Email ?? string.Empty,
                        Department = u?.DepartmentId is { } dId && departmentNames.TryGetValue(dId, out var dName) ? dName : null,
                        RoleOnJob = m.RoleOnJob,
                        IsPrimary = m.IsPrimary,
                        AddedByName = string.IsNullOrWhiteSpace(by?.FullName) ? by?.Email : by!.FullName,
                        CreatedAt = m.CreatedAt,
                    };
                })
                .ToList();
        }
    }

    // ============================================================
    // POST /api/jobs/{id}/hiring-team
    // ============================================================

    /// <summary>
    /// Thêm một thành viên PHỤ vào đội tuyển dụng (người phỏng vấn, người theo dõi, hoặc HM phụ chỉ đọc).
    ///
    /// Lệnh này KHÔNG đặt được Hiring Manager chính (ADR-068). Vị trí đó là người kiểm Recruiter ở mọi
    /// cổng duyệt, nên đổi nó là việc của HR Leader qua <see cref="SetPrimaryHiringManagerCommand"/> —
    /// trước đây chủ tin gửi <c>isPrimary: true</c> cho một "observer" là hạ được HM thật và tự mở cổng.
    /// </summary>
    public record AddHiringTeamMemberCommand(
        Guid JobPostingId, Guid UserId, string? RoleOnJob, Guid? ActorId, string? ActorRole)
        : IRequest<Result<HiringTeamMemberDto>>;

    public class AddHiringTeamMemberCommandHandler
        : IRequestHandler<AddHiringTeamMemberCommand, Result<HiringTeamMemberDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public AddHiringTeamMemberCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<HiringTeamMemberDto>> Handle(AddHiringTeamMemberCommand request, CancellationToken ct)
        {
            // GÁN người vào tin là thao tác quản lý tin → cần Owner trở lên. Thành viên đội không
            // tự thêm người khác vào đội (nếu không, một HM tự kéo đồng nghiệp vào xem hồ sơ).
            // added_by_user_id là khoá ngoại NOT NULL sang users — không có danh tính thì không ghi
            // được dòng nào, và dấu vết "ai đã gán" cũng mất.
            if (request.ActorId is not { } actorId || actorId == Guid.Empty)
                return Result.Failure<HiringTeamMemberDto>("Không xác định được người thực hiện.", CommonErrorCodes.Forbidden);

            var (_, job, level) = await JobAccess.EvaluateAsync(
                _unitOfWork, request.JobPostingId, request.ActorId, request.ActorRole, ct);
            if (job == null)
                return Result.Failure<HiringTeamMemberDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result.Failure<HiringTeamMemberDto>(
                    "Bạn không có quyền sửa đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            // Mặc định là người phỏng vấn: thêm người vào đội thường là để họ ngồi phỏng vấn cùng, và vị
            // trí Hiring Manager chính không còn được đặt ở đây nữa.
            var roleOnJob = JobTeamRoles.Normalize(request.RoleOnJob) ?? JobTeamRoles.Interviewer;

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null || user.DeletedAt != null)
                return Result.Failure<HiringTeamMemberDto>("Không tìm thấy tài khoản này.", CommonErrorCodes.NotFound);
            if (!user.IsActive)
                return Result.Failure<HiringTeamMemberDto>("Tài khoản này đang bị khoá, không gán vào tin được.");

            // Gán vai trò Hiring Manager trong tin thì tài khoản phải THẬT SỰ là Hiring Manager:
            // ngược lại cổng duyệt sẽ chờ một người không có màn hình nào để duyệt.
            if (roleOnJob == JobTeamRoles.HiringManager && !RoleNames.Is(user.Role, RoleNames.HiringManager))
                return Result.Failure<HiringTeamMemberDto>(
                    "Chỉ gán được tài khoản có vai trò Hiring Manager vào vị trí này.");

            var existing = (await _unitOfWork.Repository<JobHiringTeamMember>()
                .FindAsync(m => m.JobPostingId == request.JobPostingId && m.UserId == request.UserId, ct))
                .FirstOrDefault();

            // Ràng buộc UNIQUE (job, user) CỐ Ý không lọc deleted_at — gán lại người đã gỡ phải
            // hồi sinh đúng dòng cũ, chèn dòng thứ hai sẽ vi phạm ràng buộc.
            if (existing == null)
            {
                var softDeleted = await _unitOfWork.Repository<JobHiringTeamMember>().QueryAsync(
                    q => q.IgnoreQueryFilters()
                        .Where(m => m.JobPostingId == request.JobPostingId && m.UserId == request.UserId),
                    ct);
                existing = softDeleted.FirstOrDefault();
            }

            // Viết lại dòng của HM chính qua lệnh này là hạ họ xuống thành viên phụ — đúng lỗ hổng ADR-068
            // vá. Đổi HM chính chỉ đi qua lệnh chuyển HM của HR Leader.
            if (existing is { IsPrimary: true, DeletedAt: null })
                return Result.Failure<HiringTeamMemberDto>(
                    "Người này đang là Hiring Manager chính của tin. Đổi Hiring Manager chính qua thao tác chuyển HM của HR Admin.",
                    CommonErrorCodes.Conflict);

            JobHiringTeamMember member;
            if (existing != null)
            {
                existing.DeletedAt = null;
                existing.RoleOnJob = roleOnJob;
                existing.IsPrimary = false;
                existing.AddedByUserId = actorId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<JobHiringTeamMember>().Update(existing);
                member = existing;
            }
            else
            {
                member = new JobHiringTeamMember
                {
                    JobPostingId = request.JobPostingId,
                    UserId = request.UserId,
                    RoleOnJob = roleOnJob,
                    IsPrimary = false,
                    AddedByUserId = actorId,
                };
                await _unitOfWork.Repository<JobHiringTeamMember>().AddAsync(member, ct);
            }

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "hiring_team_member_added",
                nameof(JobPosting), request.JobPostingId,
                AuditMetadata.Serialize(new
                {
                    jobTitle = job.Title, userId = request.UserId, userEmail = user.Email,
                    roleOnJob, isPrimary = false,
                }), ct);

            // Dùng helper idempotent: gán lại người đã gỡ (hoặc chỉ đổi cờ IsPrimary) là đi lại
            // đúng đường này với cùng dedupKey — thêm thẳng sẽ vỡ UNIQUE và rollback cả thao tác,
            // đúng cái nhánh "hồi sinh dòng cũ" mà handler này được viết ra để hỗ trợ.
            await ARI.Application.Offers.OfferSupport.NotifyStaffAsync(
                _unitOfWork, request.UserId,
                "system", "Bạn được thêm vào đội tuyển dụng",
                $"Bạn tham gia tin \"{job.Title}\" với vai trò {RoleLabel(roleOnJob)}.",
                $"/hm/jobs/{job.Id}", $"hiring_team:{job.Id}:{request.UserId}", ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(request.UserId, "ReceiveUserNotification",
                new { Type = "HiringTeamAssigned", JobPostingId = job.Id, JobTitle = job.Title }, ct);

            var dto = (await GetHiringTeamQueryHandler.LoadAsync(_unitOfWork, request.JobPostingId, ct))
                .First(m => m.UserId == request.UserId);
            return Result.Success(dto);
        }

        private static string RoleLabel(string roleOnJob) => roleOnJob switch
        {
            JobTeamRoles.HiringManager => "Hiring Manager",
            JobTeamRoles.Interviewer => "Người phỏng vấn",
            _ => "Người theo dõi",
        };
    }

    // ============================================================
    // DELETE /api/jobs/{id}/hiring-team/{memberId}
    // ============================================================

    public record RemoveHiringTeamMemberCommand(Guid JobPostingId, Guid MemberId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<bool>>;

    public class RemoveHiringTeamMemberCommandHandler
        : IRequestHandler<RemoveHiringTeamMemberCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public RemoveHiringTeamMemberCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<bool>> Handle(RemoveHiringTeamMemberCommand request, CancellationToken ct)
        {
            var (_, job, level) = await JobAccess.EvaluateAsync(
                _unitOfWork, request.JobPostingId, request.ActorId, request.ActorRole, ct);
            if (job == null)
                return Result<bool>.Failure("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result<bool>.Failure(
                    "Bạn không có quyền sửa đội tuyển dụng của tin này.", CommonErrorCodes.Forbidden);

            var member = await _unitOfWork.Repository<JobHiringTeamMember>().GetByIdAsync(request.MemberId, ct);
            if (member == null || member.JobPostingId != request.JobPostingId)
                return Result<bool>.Failure("Không tìm thấy thành viên này trong đội.", CommonErrorCodes.NotFound);

            // ADR-068: tin không bao giờ được rơi vào tình trạng không có Hiring Manager chính. Gỡ HM chính
            // trước đây là cách một Recruiter tự mở mọi cổng đang kiểm chính mình — nay chỉ CHUYỂN được,
            // và chuyển là việc của HR Leader.
            if (member.IsPrimary)
                return Result<bool>.Failure(
                    "Không gỡ được Hiring Manager chính của tin. HR Admin cần chuyển tin cho Hiring Manager khác trước.",
                    CommonErrorCodes.Conflict);

            // Xoá mềm: dòng này là dấu vết ai từng duyệt shortlist / chốt kết quả của tin.
            member.DeletedAt = DateTimeOffset.UtcNow;
            member.IsPrimary = false; // nhường lại cờ để index UNIQUE có lọc không chặn người kế tiếp
            member.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobHiringTeamMember>().Update(member);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "hiring_team_member_removed",
                nameof(JobPosting), request.JobPostingId,
                AuditMetadata.Serialize(new
                {
                    jobTitle = job.Title, userId = member.UserId, roleOnJob = member.RoleOnJob,
                }), ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(member.UserId, "ReceiveUserNotification",
                new { Type = "HiringTeamRemoved", JobPostingId = job.Id, JobTitle = job.Title }, ct);

            return Result.Success(true);
        }
    }

    // ============================================================
    // PUT /api/jobs/{id}/hiring-manager  (HR Leader / Super Admin)
    // ============================================================

    /// <summary>
    /// Gán hoặc CHUYỂN Hiring Manager chính của tin (ADR-068).
    ///
    /// Đây là đường DUY NHẤT đổi được vị trí đó. Ba ràng buộc, mỗi cái có lý do riêng:
    /// <list type="bullet">
    ///   <item><b>Chỉ HR Leader / Super Admin.</b> HM chính là người kiểm Recruiter ở mọi cổng — để chủ tin
    ///   tự chọn người kiểm mình là mất nghĩa của cổng.</item>
    ///   <item><b>Lý do ≥10 ký tự + audit + báo cho cả HM cũ lẫn HM mới.</b> Đổi người duyệt giữa chừng mà
    ///   im lặng thì HM cũ không biết mình đã hết việc, HM mới không biết mình có việc.</item>
    ///   <item><b>Người nhận phải là Hiring Manager đang hoạt động.</b> Nếu không, cổng chờ một người không
    ///   có màn hình nào để duyệt.</item>
    /// </list>
    /// Việc đang chờ (chữ ký JD, hồ sơ ở <c>hm_review</c>, chốt kết quả) tự sang người mới vì mọi cổng
    /// đọc HM chính HIỆN TẠI; lịch có mặt của HM cũ hết hiệu lực vì luật khớp giờ lọc theo người.
    /// </summary>
    public record SetPrimaryHiringManagerCommand(
        Guid JobPostingId, Guid UserId, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result<HiringTeamMemberDto>>;

    public class SetPrimaryHiringManagerCommandHandler
        : IRequestHandler<SetPrimaryHiringManagerCommand, Result<HiringTeamMemberDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public SetPrimaryHiringManagerCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<HiringTeamMemberDto>> Handle(SetPrimaryHiringManagerCommand request, CancellationToken ct)
        {
            if (!RoleNames.IsAdmin(request.ActorRole))
                return Result.Failure<HiringTeamMemberDto>(
                    "Chỉ HR Admin hoặc Super Admin mới gán / chuyển được Hiring Manager chính của tin.",
                    CommonErrorCodes.Forbidden);
            if (request.ActorId is not { } actorId || actorId == Guid.Empty)
                return Result.Failure<HiringTeamMemberDto>("Không xác định được người thực hiện.", CommonErrorCodes.Forbidden);

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (reason == null || reason.Length < ARI.Application.RecruitmentRequests.RecruitmentRequestSupport.MinReasonLength)
                return Result.Failure<HiringTeamMemberDto>(
                    $"Vui lòng nêu lý do gán / chuyển Hiring Manager (tối thiểu {ARI.Application.RecruitmentRequests.RecruitmentRequestSupport.MinReasonLength} ký tự).");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (job == null)
                return Result.Failure<HiringTeamMemberDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase))
                return Result.Failure<HiringTeamMemberDto>("Tin đã lưu trữ, không đổi Hiring Manager được nữa.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null || user.DeletedAt != null)
                return Result.Failure<HiringTeamMemberDto>("Không tìm thấy tài khoản này.", CommonErrorCodes.NotFound);
            if (!user.IsActive)
                return Result.Failure<HiringTeamMemberDto>("Tài khoản này đang bị khoá, không giao tin được.");
            if (!RoleNames.Is(user.Role, RoleNames.HiringManager))
                return Result.Failure<HiringTeamMemberDto>("Chỉ giao được cho tài khoản có vai trò Hiring Manager.");

            var current = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
            if (current?.UserId == user.Id)
                return Result.Failure<HiringTeamMemberDto>("Người này đã là Hiring Manager chính của tin.", CommonErrorCodes.Conflict);

            // Hạ cờ người đang giữ TRƯỚC khi nâng người mới — index UNIQUE có lọc (một primary/tin) sẽ
            // từ chối hai người cùng cờ. Dòng cũ GIỮ LẠI làm HM phụ: đó là dấu vết ai từng duyệt tin này.
            if (current != null)
            {
                current.IsPrimary = false;
                current.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<JobHiringTeamMember>().Update(current);
            }

            // UNIQUE (job, user) cố ý không lọc deleted_at → người từng ở trong đội phải hồi sinh đúng dòng cũ.
            var existing = (await _unitOfWork.Repository<JobHiringTeamMember>().QueryAsync(
                    q => q.IgnoreQueryFilters().Where(m => m.JobPostingId == job.Id && m.UserId == user.Id), ct))
                .FirstOrDefault();

            if (existing != null)
            {
                existing.DeletedAt = null;
                existing.RoleOnJob = JobTeamRoles.HiringManager;
                existing.IsPrimary = true;
                existing.AddedByUserId = actorId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<JobHiringTeamMember>().Update(existing);
            }
            else
            {
                await _unitOfWork.Repository<JobHiringTeamMember>().AddAsync(new JobHiringTeamMember
                {
                    JobPostingId = job.Id,
                    UserId = user.Id,
                    RoleOnJob = JobTeamRoles.HiringManager,
                    IsPrimary = true,
                    AddedByUserId = actorId,
                }, ct);
            }

            await AdminSupport.WriteAuditAsync(_unitOfWork, actorId, "hiring_manager_transferred",
                nameof(JobPosting), job.Id,
                AuditMetadata.Serialize(new
                {
                    jobTitle = job.Title, fromUserId = current?.UserId, toUserId = user.Id, toEmail = user.Email, reason,
                }), ct);

            // Nói luôn cho người mới biết đang có việc gì chờ họ — nếu không, thứ đầu tiên họ thấy là một
            // danh sách "chờ bạn duyệt" không rõ từ đâu ra.
            var pendingShortlists = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().CountAsync(
                a => a.JobPostingId == job.Id
                     && a.Status == ApplicationStatuses.HmReview
                     && a.HmDecision == HmDecision.Pending, ct);
            var awaitingSignOff = string.Equals(job.Status, "pending", StringComparison.OrdinalIgnoreCase)
                                  && HmSignOffStatus.Is(job.HmSignOffStatus, HmSignOffStatus.Pending);
            var backlog = new List<string>();
            if (awaitingSignOff) backlog.Add("mô tả công việc đang chờ bạn ký duyệt");
            if (pendingShortlists > 0) backlog.Add($"{pendingShortlists} hồ sơ đang chờ bạn duyệt");

            var stamp = DateTimeOffset.UtcNow.Ticks;
            await ARI.Application.Offers.OfferSupport.NotifyStaffAsync(
                _unitOfWork, user.Id, "system", "Bạn được giao làm Hiring Manager của tin",
                $"Tin \"{job.Title}\"." + (backlog.Count > 0 ? $" Việc đang chờ: {string.Join(", ", backlog)}." : string.Empty)
                    + $" Lý do: {reason}",
                $"/hm/jobs/{job.Id}", $"hm_transferred_in:{job.Id}:{user.Id}:{stamp}", ct);

            if (current != null)
            {
                await ARI.Application.Offers.OfferSupport.NotifyStaffAsync(
                    _unitOfWork, current.UserId, "system", "Tin đã được chuyển cho Hiring Manager khác",
                    $"Tin \"{job.Title}\" nay do {user.FullName ?? user.Email} phụ trách. Lý do: {reason}",
                    $"/hm/jobs/{job.Id}", $"hm_transferred_out:{job.Id}:{current.UserId}:{stamp}", ct);
            }

            if (job.CreatedByUserId != actorId)
            {
                await ARI.Application.Offers.OfferSupport.NotifyStaffAsync(
                    _unitOfWork, job.CreatedByUserId, "system", "Hiring Manager của tin đã thay đổi",
                    $"Tin \"{job.Title}\" nay do {user.FullName ?? user.Email} phụ trách.",
                    await StaffLinks.JobAsync(_unitOfWork, job.CreatedByUserId, job.Id, ct),
                    $"hm_transferred_owner:{job.Id}:{stamp}", ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(user.Id, "ReceiveUserNotification",
                new { Type = "HiringTeamAssigned", JobPostingId = job.Id, JobTitle = job.Title }, ct);

            var dto = (await GetHiringTeamQueryHandler.LoadAsync(_unitOfWork, job.Id, ct))
                .First(m => m.UserId == user.Id);
            return Result.Success(dto);
        }
    }

    // ============================================================
    // GET /api/staff/hiring-managers?jobPostingId=
    // ============================================================

    /// <summary>
    /// Danh sách tài khoản Hiring Manager để nhân sự chọn khi gán vào tin.
    /// Nếu truyền <paramref name="JobPostingId"/>, người cùng phòng ban với tin được xếp lên đầu —
    /// đó là TOÀN BỘ công dụng của <c>department</c> trong ADR-061: xếp hạng gợi ý, không phải quyền.
    /// </summary>
    public record GetHiringManagerOptionsQuery(Guid? JobPostingId) : IRequest<Result<List<HiringManagerOptionDto>>>;

    public class GetHiringManagerOptionsQueryHandler
        : IRequestHandler<GetHiringManagerOptionsQuery, Result<List<HiringManagerOptionDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetHiringManagerOptionsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<HiringManagerOptionDto>>> Handle(
            GetHiringManagerOptionsQuery request, CancellationToken ct)
        {
            string? jobDepartment = null;
            if (request.JobPostingId is { } jobId && jobId != Guid.Empty)
            {
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobId, ct);
                jobDepartment = job?.Department;
            }

            var managers = await _unitOfWork.Repository<User>().QueryAsync(
                q => q.Where(u => u.Role == RoleNames.HiringManager && u.IsActive)
                      .Select(u => new HiringManagerOptionDto
                      {
                          Id = u.Id, FullName = u.FullName, Email = u.Email, DepartmentId = u.DepartmentId,
                          Department = null,
                      }), ct);

            // Điền tên đội để hiển thị, và so khớp gợi ý bằng TÊN đã chuẩn hoá.
            //
            // Tin tuyển dụng vẫn giữ `Department` dạng chuỗi (Gemini trích từ JD), nên không so được
            // bằng khoá ở đây. So bằng tên đã chuẩn hoá là chính xác nhất có thể — và đây chỉ là
            // XẾP GỢI Ý lên đầu, sai thì cùng lắm người dùng phải cuộn thêm một dòng.
            var deptNames = await ARI.Application.Departments.DepartmentLookup.NamesAsync(
                _unitOfWork, managers.Where(m => m.DepartmentId.HasValue).Select(m => m.DepartmentId!.Value), ct);

            foreach (var m in managers)
            {
                if (m.DepartmentId is { } id && deptNames.TryGetValue(id, out var name)) m.Department = name;

                m.MatchesJobDepartment = !string.IsNullOrWhiteSpace(jobDepartment)
                    && !string.IsNullOrWhiteSpace(m.Department)
                    && string.Equals(m.Department!.Trim(), jobDepartment.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return Result.Success(managers
                .OrderByDescending(m => m.MatchesJobDepartment)
                .ThenBy(m => m.FullName ?? m.Email)
                .ToList());
        }
    }
}
