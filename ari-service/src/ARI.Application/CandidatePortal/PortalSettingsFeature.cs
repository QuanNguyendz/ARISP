using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    // ============================================================
    // GET /api/portal/settings
    // ============================================================

    public record GetPortalSettingsQuery(Guid CandidateId) : IRequest<Result<CandidateSettingsDto>>;

    public class GetPortalSettingsQueryHandler : IRequestHandler<GetPortalSettingsQuery, Result<CandidateSettingsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetPortalSettingsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<CandidateSettingsDto>> Handle(GetPortalSettingsQuery request, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateId, ct);
            if (acc == null)
                return Result.Failure<CandidateSettingsDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            return Result.Success(PortalSupport.DeserializeOrEmpty<CandidateSettingsDto>(acc.SettingsJson));
        }
    }

    // ============================================================
    // PUT /api/portal/settings
    // ============================================================

    public record UpdatePortalSettingsCommand(Guid CandidateId, CandidateSettingsDto? Settings) : IRequest<Result<CandidateSettingsDto>>;

    public class UpdatePortalSettingsCommandHandler : IRequestHandler<UpdatePortalSettingsCommand, Result<CandidateSettingsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePortalSettingsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<CandidateSettingsDto>> Handle(UpdatePortalSettingsCommand request, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateId, ct);
            if (acc == null)
                return Result.Failure<CandidateSettingsDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            var settings = request.Settings ?? new CandidateSettingsDto();
            if (settings.Language != "en") settings.Language = "vi";

            acc.SettingsJson = JsonSerializer.Serialize(settings, PortalSupport.JsonOpts);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();
            return Result.Success(settings);
        }
    }

    // ============================================================
    // GET /api/portal/settings/export — file JSON tải về
    // ============================================================

    public record ExportFileDto(byte[] Bytes, string ContentType, string FileName);

    public record ExportMyDataQuery(Guid CandidateId) : IRequest<Result<ExportFileDto>>;

    public class ExportMyDataQueryHandler : IRequestHandler<ExportMyDataQuery, Result<ExportFileDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ExportMyDataQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<ExportFileDto>> Handle(ExportMyDataQuery request, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateId, ct);
            if (acc == null)
                return Result.Failure<ExportFileDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            var apps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => a.CandidateAccountId == request.CandidateId, ct)).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = (await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct))
                .ToDictionary(j => j.Id, j => j.Title);

            var export = new
            {
                exportedAt = DateTimeOffset.UtcNow,
                profile = new
                {
                    acc.Email,
                    acc.FullName,
                    acc.Phone,
                    acc.Headline,
                    acc.Location,
                    acc.DateOfBirth,
                    acc.About,
                    acc.LinkedinUrl,
                    acc.GithubUrl,
                    acc.PortfolioUrl,
                    Skills = PortalSupport.DeserializeOrEmpty<List<string>>(acc.SkillsJson),
                    Experience = PortalSupport.DeserializeOrEmpty<List<CandidateExperienceItem>>(acc.ExperienceJson),
                    Education = PortalSupport.DeserializeOrEmpty<List<CandidateEducationItem>>(acc.EducationJson),
                    CreatedAt = acc.CreatedAt,
                },
                applications = apps.OrderByDescending(a => a.CreatedAt).Select(a => new
                {
                    a.Id,
                    JobTitle = jobs.TryGetValue(a.JobPostingId, out var t) ? t : null,
                    a.Status,
                    a.DesiredLocation,
                    a.CoverLetter,
                    a.NoticePeriod,
                    a.CreatedAt,
                }),
            };

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Result.Success(new ExportFileDto(bytes, "application/json", $"arisp-data-{DateTime.UtcNow:yyyyMMdd}.json"));
        }
    }

    // ============================================================
    // POST /api/portal/settings/logout-all — thu hồi mọi refresh token
    // ============================================================

    public record LogoutAllDevicesCommand(Guid CandidateId) : IRequest<Result<int>>;

    public class LogoutAllDevicesCommandHandler : IRequestHandler<LogoutAllDevicesCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LogoutAllDevicesCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(LogoutAllDevicesCommand request, CancellationToken ct)
        {
            var tokens = (await _unitOfWork.Repository<CandidateRefreshToken>()
                .FindAsync(t => t.CandidateAccountId == request.CandidateId && t.RevokedAt == null, ct)).ToList();
            foreach (var t in tokens)
            {
                t.RevokedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<CandidateRefreshToken>().Update(t);
            }
            if (tokens.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Result.Success(tokens.Count);
        }
    }
}
