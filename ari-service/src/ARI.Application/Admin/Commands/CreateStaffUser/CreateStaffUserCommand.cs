using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Admin.Commands.CreateStaffUser
{
    /// <summary>
    /// Super Admin tạo tài khoản HR Admin/Recruiter (pre-provisioning): sinh mật khẩu tạm,
    /// gửi email chào mừng. Guard giữ trong handler theo đúng thứ tự check gốc.
    /// </summary>
    public record CreateStaffUserCommand(string Email, string FullName, string? Role, Guid? DepartmentId, Guid? ActorId)
        : IRequest<Result<CreatedStaffUserDto>>;

    public class CreateStaffUserCommandHandler : IRequestHandler<CreateStaffUserCommand, Result<CreatedStaffUserDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public CreateStaffUserCommandHandler(
            IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IEmailService emailService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<Result<CreatedStaffUserDto>> Handle(CreateStaffUserCommand request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Failure<CreatedStaffUserDto>("Email là bắt buộc.");

            if (string.IsNullOrWhiteSpace(request.FullName))
                return Result.Failure<CreatedStaffUserDto>("Họ và tên là bắt buộc.");

            var normalizedRole = RoleNames.NormalizeDbRole(request.Role);
            if (normalizedRole == null || !RoleNames.AssignableStaff.Contains(normalizedRole))
                return Result.Failure<CreatedStaffUserDto>(
                    $"Role phải là một trong: {string.Join(", ", RoleNames.AssignableStaff)}.");

            // Kiểm tra email đã tồn tại chưa
            var existingUsers = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == request.Email.Trim().ToLower(), ct);
            var existingUser = existingUsers.FirstOrDefault();

            if (existingUser != null)
                return Result.Failure<CreatedStaffUserDto>("Email này đã được sử dụng bởi tài khoản khác.", CommonErrorCodes.Conflict);

            // Sinh mật khẩu tạm (12 ký tự, bao gồm chữ hoa, thường, số, ký tự đặc biệt)
            var tempPassword = AdminSupport.GenerateTemporaryPassword();

            // Đội phải đang HOẠT ĐỘNG: gán vào một đội đã tắt thì tài khoản đó không lập được phiếu
            // mà cũng không có lỗi nào chỉ ra vì sao.
            string? departmentName = null;
            if (request.DepartmentId is { } deptId)
            {
                var dept = await DepartmentSupport.GetActiveAsync(_unitOfWork, deptId, ct);
                if (dept == null)
                    return Result.Failure<CreatedStaffUserDto>("Đội được chọn không tồn tại hoặc đã ngừng hoạt động.");
                departmentName = dept.Name;
            }

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim().ToLower(),
                PasswordHash = _passwordHasher.Hash(tempPassword),
                Role = normalizedRole,
                FullName = request.FullName.Trim(),
                DepartmentId = request.DepartmentId,
                IsActive = true
            };

            await _unitOfWork.Repository<User>().AddAsync(newUser, ct);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "staff_account_created", "User", newUser.Id,
                $"{{\"email\":\"{newUser.Email}\",\"role\":\"{newUser.Role}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            // Gửi email thông báo tài khoản cho staff mới
            await AdminSupport.SendStaffWelcomeEmailAsync(_emailService, _configuration, newUser, tempPassword);

            return Result.Success(new CreatedStaffUserDto(
                newUser.Id, newUser.Email, newUser.FullName, newUser.Role, departmentName, newUser.IsActive, newUser.CreatedAt));
        }
    }
}
