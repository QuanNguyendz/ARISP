using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
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
}
