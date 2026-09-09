using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Departments
{
    // ===================================================================================
    //  Đội/bộ phận của công ty (ADR-065)
    //
    //  Super Admin quản lý danh sách; tài khoản nhân viên gắn vào đúng một đội. Đây là dữ liệu
    //  TỔ CHỨC, không phải trục phân quyền — ADR-061 giữ nguyên, ai quyết định về một tin vẫn do
    //  `job_hiring_team_members` trả lời.
    // ===================================================================================

    public record DepartmentDto(
        Guid Id, string Name, string? Code, string? Description, bool IsActive,

        /// <summary>Số tài khoản đang thuộc đội — để cảnh báo trước khi tắt một đội còn người.</summary>
        int MemberCount,

        DateTimeOffset CreatedAt);

    public record DepartmentInput(string Name, string? Code, string? Description, bool IsActive);

    /// <summary>Tra tên đội theo khoá, dùng chung cho mọi màn cần hiển thị tên.</summary>
    public static class DepartmentLookup
    {
        public static async Task<Dictionary<Guid, string>> NamesAsync(
            IUnitOfWork uow, IEnumerable<Guid> ids, CancellationToken ct)
        {
            var wanted = ids.Distinct().ToList();
            if (wanted.Count == 0) return new Dictionary<Guid, string>();

            // Đội GIẢI THỂ thì tắt (`IsActive = false`) chứ không xoá mềm — chính là để phiếu và tài
            // khoản cũ vẫn tra được tên. Nên ở đây không lọc theo `IsActive`, và cũng không cần
            // `IgnoreQueryFilters` (tầng Application cố ý không tham chiếu EF Core).
            var rows = await uow.Repository<Department>().QueryAsync(
                q => q.Where(d => wanted.Contains(d.Id)).Select(d => new { d.Id, d.Name }), ct);

            return rows.ToDictionary(d => d.Id, d => d.Name);
        }

        /// <summary>Tên đội của một tài khoản, hoặc <c>null</c> nếu chưa được gán.</summary>
        public static async Task<string?> NameForUserAsync(IUnitOfWork uow, Guid? departmentId, CancellationToken ct)
        {
            if (departmentId is not { } id) return null;
            var names = await NamesAsync(uow, new[] { id }, ct);
            return names.TryGetValue(id, out var name) ? name : null;
        }
    }

    // ---------------------------------------------------------------------------------

    public record GetDepartmentsQuery(bool ActiveOnly) : IRequest<Result<List<DepartmentDto>>>;

    public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, Result<List<DepartmentDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetDepartmentsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<DepartmentDto>>> Handle(GetDepartmentsQuery request, CancellationToken ct)
        {
            var rows = await _unitOfWork.Repository<Department>().QueryAsync(
                q =>
                {
                    var scoped = request.ActiveOnly ? q.Where(d => d.IsActive) : q;
                    return scoped.OrderBy(d => d.Name)
                        .Select(d => new { d.Id, d.Name, d.Code, d.Description, d.IsActive, d.CreatedAt });
                }, ct);

            if (rows.Count == 0) return Result.Success(new List<DepartmentDto>());

            // Đếm nhân sự mỗi đội bằng MỘT truy vấn gộp, không phải mỗi đội một lần.
            var counts = await _unitOfWork.Repository<User>().QueryAsync(
                q => q.Where(u => u.DepartmentId != null)
                      .GroupBy(u => u.DepartmentId!.Value)
                      .Select(g => new { DepartmentId = g.Key, Count = g.Count() }), ct);

            var byId = counts.ToDictionary(c => c.DepartmentId, c => c.Count);

            return Result.Success(rows.Select(d => new DepartmentDto(
                d.Id, d.Name, d.Code, d.Description, d.IsActive,
                byId.TryGetValue(d.Id, out var n) ? n : 0,
                d.CreatedAt)).ToList());
        }
    }

    // ---------------------------------------------------------------------------------

    public record CreateDepartmentCommand(DepartmentInput Input, Guid? ActorId) : IRequest<Result<Guid>>;

    public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateDepartmentCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<Guid>> Handle(CreateDepartmentCommand request, CancellationToken ct)
        {
            var name = (request.Input.Name ?? string.Empty).Trim();
            if (name.Length == 0) return Result.Failure<Guid>("Tên đội là bắt buộc.");

            if (await DepartmentSupport.NameTakenAsync(_unitOfWork, name, excludeId: null, ct))
                return Result.Failure<Guid>($"Đã có đội tên \"{name}\".", CommonErrorCodes.Conflict);

            var entity = new Department
            {
                Name = name,
                Code = DepartmentSupport.Trim(request.Input.Code),
                Description = DepartmentSupport.Trim(request.Input.Description),
                IsActive = request.Input.IsActive,
            };

            await _unitOfWork.Repository<Department>().AddAsync(entity, ct);
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "department_created",
                nameof(Department), entity.Id, AuditMetadata.Serialize(new { entity.Name }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(entity.Id);
        }
    }

    // ---------------------------------------------------------------------------------

    public record UpdateDepartmentCommand(Guid Id, DepartmentInput Input, Guid? ActorId) : IRequest<Result>;

    public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateDepartmentCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(UpdateDepartmentCommand request, CancellationToken ct)
        {
            var entity = await _unitOfWork.Repository<Department>().GetByIdAsync(request.Id, ct);
            if (entity == null) return Result.Failure("Không tìm thấy đội.", CommonErrorCodes.NotFound);

            var name = (request.Input.Name ?? string.Empty).Trim();
            if (name.Length == 0) return Result.Failure("Tên đội là bắt buộc.");

            if (await DepartmentSupport.NameTakenAsync(_unitOfWork, name, excludeId: entity.Id, ct))
                return Result.Failure($"Đã có đội tên \"{name}\".", CommonErrorCodes.Conflict);

            entity.Name = name;
            entity.Code = DepartmentSupport.Trim(request.Input.Code);
            entity.Description = DepartmentSupport.Trim(request.Input.Description);
            entity.IsActive = request.Input.IsActive;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<Department>().Update(entity);
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "department_updated",
                nameof(Department), entity.Id,
                AuditMetadata.Serialize(new { entity.Name, entity.IsActive }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }

    internal static class DepartmentSupport
    {
        public static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Trùng tên KHÔNG phân biệt hoa thường. Postgres so sánh phân biệt hoa thường, nên nếu chỉ
        /// dựa vào unique index thì "Backend Team" và "backend team" là hai đội khác nhau — mọi thống
        /// kê theo đội sẽ tách đôi mà không ai để ý.
        /// </summary>
        public static async Task<bool> NameTakenAsync(
            IUnitOfWork uow, string name, Guid? excludeId, CancellationToken ct)
        {
            var lowered = name.ToLowerInvariant();
            var hits = await uow.Repository<Department>().QueryAsync(
                q => q.Where(d => d.Name.ToLower() == lowered).Select(d => d.Id), ct);

            return hits.Any(id => excludeId == null || id != excludeId.Value);
        }

        /// <summary>Đội đang hoạt động, dùng khi gán cho tài khoản hoặc cho phiếu mới.</summary>
        public static async Task<Department?> GetActiveAsync(IUnitOfWork uow, Guid id, CancellationToken ct)
        {
            var rows = await uow.Repository<Department>().FindAsync(d => d.Id == id && d.IsActive, ct);
            return rows.FirstOrDefault();
        }
    }
}
