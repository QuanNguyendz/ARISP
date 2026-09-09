using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.UpdateUserDepartment
{
    /// <summary>
    /// Gán/đổi đội của một tài khoản nhân sự (ADR-065) — chỉ Super Admin.
    ///
    /// Đây là đường DUY NHẤT đổi được đội của một người: <c>UpdateStaffProfileCommand</c> cố ý không
    /// nhận tham số đó nữa, vì khi nhân viên tự sửa được thì ô "Đội" khoá cứng trên phiếu yêu cầu
    /// tuyển dụng chỉ là hình thức — Hiring Manager sửa hồ sơ rồi quay ra lập phiếu ghi đội khác.
    /// </summary>
    public record UpdateUserDepartmentCommand(Guid Id, Guid? DepartmentId, Guid? ActorId) : IRequest<Result>;

    public class UpdateUserDepartmentCommandHandler : IRequestHandler<UpdateUserDepartmentCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserDepartmentCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateUserDepartmentCommand request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("User not found.", CommonErrorCodes.NotFound);

            // Gỡ khỏi đội thì cho phép (người rời công ty, đội giải thể). Gán MỚI thì đội phải còn
            // hoạt động: gán vào đội đã tắt là dựng sẵn một tài khoản không lập được phiếu, mà chỗ
            // báo lỗi lại nằm tận màn phiếu chứ không phải ở đây.
            if (request.DepartmentId is { } departmentId)
            {
                var department = await DepartmentSupport.GetActiveAsync(_unitOfWork, departmentId, ct);
                if (department == null)
                    return Result.Failure("Đội được chọn không tồn tại hoặc đã ngừng hoạt động.");
            }

            user.DepartmentId = request.DepartmentId;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_department_updated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"department_id\":\"{request.DepartmentId?.ToString() ?? "null"}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
