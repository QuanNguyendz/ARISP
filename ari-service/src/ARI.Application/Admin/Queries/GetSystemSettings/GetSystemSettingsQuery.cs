using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetSystemSettings
{
    public record GetSystemSettingsQuery : IRequest<Result<List<SettingDto>>>;

    public class GetSystemSettingsQueryHandler : IRequestHandler<GetSystemSettingsQuery, Result<List<SettingDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetSystemSettingsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<SettingDto>>> Handle(GetSystemSettingsQuery request, CancellationToken ct)
        {
            var settings = (await _unitOfWork.Repository<SystemSetting>().GetAllAsync(ct)).ToList();
            return Result.Success(settings
                .OrderBy(s => s.Key)
                .Select(s => new SettingDto(s.Key, s.Value, s.Description, s.UpdatedAt))
                .ToList());
        }
    }
}
