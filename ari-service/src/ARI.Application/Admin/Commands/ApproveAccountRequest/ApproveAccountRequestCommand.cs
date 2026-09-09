using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Admin.Commands.ApproveAccountRequest
{
    /// <summary>Super Admin duyệt yêu cầu tạo tài khoản của HR: tạo user active + gửi email + thông báo realtime.</summary>
    public record ApproveAccountRequestCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class ApproveAccountRequestCommandHandler : IRequestHandler<ApproveAccountRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;

        public ApproveAccountRequestCommandHandler(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IEmailService emailService,
            INotificationService notificationService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        public async Task<Result> Handle(ApproveAccountRequestCommand request, CancellationToken ct)
        {
            var req = await _unitOfWork.Repository<AccountRequest>().GetByIdAsync(request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy yêu cầu.", CommonErrorCodes.NotFound);

            if (req.Status != "pending")
                return Result.Failure("Yêu cầu này đã được xử lý.");

            var email = req.Email.Trim().ToLower();
            var existing = (await _unitOfWork.Repository<User>().FindAsync(u => u.Email == email, ct)).FirstOrDefault();
            if (existing != null)
                return Result.Failure("Email này đã có tài khoản. Hãy từ chối yêu cầu.", CommonErrorCodes.Conflict);

            // Tạo tài khoản staff active
            var tempPw = AdminSupport.GenerateTemporaryPassword();
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = _passwordHasher.Hash(tempPw),
                Role = req.Role,
                FullName = req.FullName.Trim(),
                // ADR-065: phòng ban trên phiếu XIN TÀI KHOẢN chỉ là ĐỀ XUẤT dạng text, không
                // phải khoá đội. Super Admin gán đội thật ở màn Users sau khi duyệt — gán mù theo
                // một chuỗi gõ tay sẽ tạo ra liên kết sai mà không ai kiểm.
                DepartmentId = null,
                IsActive = true
            };
            await _unitOfWork.Repository<User>().AddAsync(newUser, ct);

            req.Status = "approved";
            req.ReviewedByUserId = request.ActorId;
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.CreatedUserId = newUser.Id;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AccountRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "account_request_approved", "AccountRequest", req.Id,
                $"{{\"email\":\"{newUser.Email}\",\"role\":\"{newUser.Role}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            await AdminSupport.SendStaffWelcomeEmailAsync(_emailService, _configuration, newUser, tempPw);

            // Notify HR Leader (requester)
            await _notificationService.PublishUserEventAsync(req.RequestedByUserId, "ReceiveAccountRequestUpdate",
                new { RequestId = req.Id, Status = "approved", Email = req.Email });

            return Result.Success();
        }
    }
}
