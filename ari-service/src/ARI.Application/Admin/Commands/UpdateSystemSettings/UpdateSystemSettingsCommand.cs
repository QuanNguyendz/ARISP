using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.UpdateSystemSettings
{
    public record UpdateSystemSettingsCommand(List<UpdateSettingItem>? Items, Guid? ActorId) : IRequest<Result>;

    public class UpdateSystemSettingsCommandHandler : IRequestHandler<UpdateSystemSettingsCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSystemSettingsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateSystemSettingsCommand request, CancellationToken ct)
        {
            var items = request.Items;
            if (items == null || items.Count == 0)
                return Result.Failure("Danh sách cài đặt trống.");

            var existing = (await _unitOfWork.Repository<SystemSetting>().GetAllAsync(ct)).ToList();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key)) continue;
                var key = item.Key.Trim();
                var current = existing.FirstOrDefault(s => s.Key == key);
                if (current != null)
                {
                    current.Value = item.Value ?? string.Empty;
                    if (item.Description != null) current.Description = item.Description;
                    current.UpdatedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<SystemSetting>().Update(current);
                }
                else
                {
                    await _unitOfWork.Repository<SystemSetting>().AddAsync(new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Value = item.Value ?? string.Empty,
                        Description = item.Description,
                        UpdatedAt = DateTimeOffset.UtcNow
                    }, ct);
                }
            }

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "system_settings_updated", "SystemSetting", null,
                $"{{\"keys\":\"{string.Join(",", items.Where(i => !string.IsNullOrWhiteSpace(i.Key)).Select(i => i.Key.Trim()))}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
