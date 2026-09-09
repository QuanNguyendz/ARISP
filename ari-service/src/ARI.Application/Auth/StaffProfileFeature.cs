using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth
{
    public record GetStaffSettingsQuery(Guid UserId) : IRequest<Result<StaffSettingsDto>>;

    public class GetStaffSettingsQueryHandler : IRequestHandler<GetStaffSettingsQuery, Result<StaffSettingsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetStaffSettingsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffSettingsDto>> Handle(GetStaffSettingsQuery request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null) return Result.Failure<StaffSettingsDto>("Không tìm thấy tài khoản", CommonErrorCodes.NotFound);

            var settings = !string.IsNullOrEmpty(user.SettingsJson)
                ? JsonSerializer.Deserialize<StaffSettingsDto>(user.SettingsJson) ?? new StaffSettingsDto()
                : new StaffSettingsDto();

            return Result.Success(settings);
        }
    }

    public record UpdateStaffSettingsCommand(Guid UserId, StaffSettingsDto Settings) : IRequest<Result<StaffSettingsDto>>;

    public class UpdateStaffSettingsCommandHandler : IRequestHandler<UpdateStaffSettingsCommand, Result<StaffSettingsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateStaffSettingsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffSettingsDto>> Handle(UpdateStaffSettingsCommand request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null) return Result.Failure<StaffSettingsDto>("Không tìm thấy tài khoản", CommonErrorCodes.NotFound);

            user.SettingsJson = JsonSerializer.Serialize(request.Settings);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(request.Settings);
        }
    }

    // ============================================================
    // Hồ sơ cá nhân của nhân sự nội bộ.
    // Trước đây màn Cài đặt của HR/Recruiter chỉ có endpoint settings; tab "Hồ sơ" vẽ cứng
    // "HR Admin"/"hr@arisp.com" và tab "Bảo mật" là 2 ô mật khẩu không nối đi đâu.
    // ============================================================

    public record GetStaffProfileQuery(Guid UserId) : IRequest<Result<StaffProfileDto>>;

    public class GetStaffProfileQueryHandler : IRequestHandler<GetStaffProfileQuery, Result<StaffProfileDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetStaffProfileQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffProfileDto>> Handle(GetStaffProfileQuery request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null) return Result.Failure<StaffProfileDto>("Không tìm thấy tài khoản", CommonErrorCodes.NotFound);

            return Result.Success(new StaffProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Department = await DepartmentLookup.NameForUserAsync(_unitOfWork, user.DepartmentId, ct),
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                // Tài khoản đăng nhập bằng Google chưa từng đặt mật khẩu — FE dựa vào cờ này để
                // đổi nhãn thành "Đặt mật khẩu" và không đòi mật khẩu hiện tại.
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
            });
        }
    }

    /// <summary>
    /// Sửa hồ sơ cá nhân. CỐ Ý chỉ cho đổi HỌ TÊN.
    ///
    /// Email là danh tính đăng nhập (khớp `allowed_email_domains` + tài khoản Google) và vai trò do
    /// Super Admin cấp — cho nhân sự tự đổi hai thứ đó là mở đường nâng quyền.
    ///
    /// <b>Phòng ban đã bị GỠ khỏi đây (ADR-065).</b> Trước đây nhân viên tự sửa được, nên ô "đội"
    /// khoá cứng trên phiếu yêu cầu tuyển dụng chỉ là hình thức: Hiring Manager của đội A chỉ cần
    /// vào Cài đặt đổi sang đội B rồi quay ra lập phiếu. Gỡ ở giao diện thôi là chưa đủ — lệnh này
    /// không được NHẬN tham số đó, nếu không một request tự dựng vẫn đổi được.
    /// </summary>
    public record UpdateStaffProfileCommand(Guid UserId, string FullName)
        : IRequest<Result<StaffProfileDto>>;

    public class UpdateStaffProfileCommandHandler : IRequestHandler<UpdateStaffProfileCommand, Result<StaffProfileDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateStaffProfileCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffProfileDto>> Handle(UpdateStaffProfileCommand request, CancellationToken ct)
        {
            var fullName = (request.FullName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fullName))
                return Result.Failure<StaffProfileDto>("Họ tên không được để trống.");
            if (fullName.Length > 120)
                return Result.Failure<StaffProfileDto>("Họ tên không được quá 120 ký tự.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null) return Result.Failure<StaffProfileDto>("Không tìm thấy tài khoản", CommonErrorCodes.NotFound);

            user.FullName = fullName;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(new StaffProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Department = await DepartmentLookup.NameForUserAsync(_unitOfWork, user.DepartmentId, ct),
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
            });
        }
    }

    /// <summary>Đổi mật khẩu nhân sự — cùng luật với ứng viên (xem `ChangeCandidatePasswordCommand`).</summary>
    public record ChangeStaffPasswordCommand(Guid UserId, string? CurrentPassword, string NewPassword)
        : IRequest<Result<string>>;

    public class ChangeStaffPasswordCommandHandler : IRequestHandler<ChangeStaffPasswordCommand, Result<string>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public ChangeStaffPasswordCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<string>> Handle(ChangeStaffPasswordCommand request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            if (user == null) return Result.Failure<string>("Không tìm thấy tài khoản", CommonErrorCodes.NotFound);

            var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);

            // Đã có mật khẩu → buộc xác minh mật khẩu hiện tại. Tài khoản chỉ đăng nhập bằng
            // Google thì chưa có gì để xác minh, đây là lần ĐẶT mật khẩu đầu tiên.
            if (hasPassword)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                    return Result.Failure<string>("Vui lòng nhập mật khẩu hiện tại.");

                if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash!))
                    return Result.Failure<string>("Mật khẩu hiện tại không đúng.", "wrong_current_password");
            }

            if (!AuthSupport.IsStrongPassword(request.NewPassword, out var validationError))
                return Result.Failure<string>(validationError);

            if (hasPassword && _passwordHasher.Verify(request.NewPassword, user.PasswordHash!))
                return Result.Failure<string>("Mật khẩu mới không được trùng mật khẩu hiện tại.");

            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(hasPassword ? "Đổi mật khẩu thành công." : "Đặt mật khẩu thành công.");
        }
    }
}
