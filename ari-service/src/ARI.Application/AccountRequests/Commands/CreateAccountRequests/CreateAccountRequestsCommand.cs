using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.AccountRequests.Commands.CreateAccountRequests
{
    /// <summary>Tạo một hoặc nhiều yêu cầu tạo tài khoản. Nhiều mục → cùng một BatchId.</summary>
    public record CreateAccountRequestsCommand(List<AccountRequestItem>? Items, Guid ActorId)
        : IRequest<Result<CreateAccountRequestsResultDto>>;

    public class CreateAccountRequestsCommandHandler
        : IRequestHandler<CreateAccountRequestsCommand, Result<CreateAccountRequestsResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public CreateAccountRequestsCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result<CreateAccountRequestsResultDto>> Handle(CreateAccountRequestsCommand request, CancellationToken ct)
        {
            var items = request.Items;
            if (items == null || items.Count == 0)
                return Result.Failure<CreateAccountRequestsResultDto>("Danh sách yêu cầu trống.");

            // Chuẩn hóa + validate
            var cleaned = new List<AccountRequestItem>();
            foreach (var item in items)
            {
                var email = item.Email?.Trim().ToLower() ?? string.Empty;
                var fullName = item.FullName?.Trim() ?? string.Empty;
                var role = RoleNames.NormalizeDbRole(item.Role);

                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    return Result.Failure<CreateAccountRequestsResultDto>($"Email không hợp lệ: '{item.Email}'.");
                if (string.IsNullOrWhiteSpace(fullName))
                    return Result.Failure<CreateAccountRequestsResultDto>($"Thiếu họ tên cho '{email}'.");
                if (role == null || !RoleNames.AssignableStaff.Contains(role))
                    return Result.Failure<CreateAccountRequestsResultDto>(
                        $"Vai trò phải là một trong: {string.Join(", ", RoleNames.AssignableStaff)} (email {email}).");

                cleaned.Add(new AccountRequestItem { Email = email, FullName = fullName, Role = role, Department = item.Department?.Trim() });
            }

            // Chặn trùng email trong cùng request
            var dup = cleaned.GroupBy(c => c.Email).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                return Result.Failure<CreateAccountRequestsResultDto>($"Email bị lặp trong yêu cầu: {dup.Key}.");

            // Chặn email đã có tài khoản hoặc đã có yêu cầu đang chờ
            var emails = cleaned.Select(c => c.Email).ToList();
            var existingUsers = await _unitOfWork.Repository<User>().FindAsync(u => emails.Contains(u.Email), ct);
            var takenByUser = existingUsers.Select(u => u.Email).ToHashSet();
            var pendingReqs = await _unitOfWork.Repository<AccountRequest>()
                .FindAsync(r => r.Status == "pending" && emails.Contains(r.Email), ct);
            var takenByPending = pendingReqs.Select(r => r.Email).ToHashSet();

            var conflicts = cleaned
                .Where(c => takenByUser.Contains(c.Email) || takenByPending.Contains(c.Email))
                .Select(c => c.Email)
                .ToList();
            if (conflicts.Count > 0)
                return Result.Failure<CreateAccountRequestsResultDto>(
                    $"Các email sau đã có tài khoản hoặc đang chờ duyệt: {string.Join(", ", conflicts)}.", CommonErrorCodes.Conflict);

            Guid? batchId = cleaned.Count > 1 ? Guid.NewGuid() : null;
            var now = DateTimeOffset.UtcNow;

            foreach (var c in cleaned)
            {
                await _unitOfWork.Repository<AccountRequest>().AddAsync(new AccountRequest
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    RequestedByUserId = request.ActorId,
                    Email = c.Email,
                    FullName = c.FullName,
                    Role = c.Role,
                    Department = c.Department,
                    Status = "pending",
                    CreatedAt = now,
                    UpdatedAt = now
                }, ct);
            }

            // Audit
            await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = request.ActorId,
                Action = "account_request_created",
                EntityType = "AccountRequest",
                EntityId = null,
                Metadata = $"{{\"count\":{cleaned.Count},\"batch\":\"{batchId}\"}}",
                CreatedAt = now
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            // Notify Super Admin
            await _notificationService.PublishGroupEventAsync("super_admin", "ReceiveAccountRequest", new { BatchId = batchId, Count = cleaned.Count }, ct);

            // Real-time Notification for super admin
            await _notificationService.PublishGroupEventAsync("super_admin", "ReceiveNewAccountRequest", new { Count = cleaned.Count, BatchId = batchId }, ct);

            return Result.Success(new CreateAccountRequestsResultDto(cleaned.Count, batchId));
        }
    }
}
