using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.JdDocuments;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.JdTemplates
{
    // ===================================================================================
    //  Mẫu bản mô tả công việc của công ty (ADR-064)
    //
    //  Single-tenant → chỉ MỘT mẫu đang dùng. Chưa cấu hình lần nào thì trả về bộ mặc định
    //  (không lưu DB) để trình soạn JD vẫn chạy được ngay từ ngày đầu — bắt HR Leader phải
    //  cấu hình xong mới dùng được tính năng là dựng thêm một cánh cửa khoá không cần thiết.
    // ===================================================================================

    public record JdTemplateDto(
        Guid? Id,
        string CompanyName,
        string? CompanyAddress,
        string? CompanyWebsite,
        string? CompanyEmail,
        string? LogoStorageKey,

        /// <summary>URL mở được trên trình duyệt — tính ở server, FE không dựng từ storageKey.</summary>
        string? LogoUrl,

        string AccentColor,
        string FontFamily,
        string? FooterNote,
        IReadOnlyList<JdTemplateSection> Sections,
        DateTimeOffset? UpdatedAt);

    public record UpdateJdTemplateInput(
        string CompanyName,
        string? CompanyAddress,
        string? CompanyWebsite,
        string? CompanyEmail,
        string AccentColor,
        string FontFamily,
        string? FooterNote,
        List<JdTemplateSection> Sections);

    internal static class JdTemplateSupport
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        /// <summary>Mẫu đang dùng, hoặc <c>null</c> nếu chưa ai cấu hình.</summary>
        public static async Task<JdTemplate?> GetCurrentAsync(IUnitOfWork uow, CancellationToken ct)
        {
            var rows = await uow.Repository<JdTemplate>().QueryAsync(
                q => q.OrderByDescending(t => t.UpdatedAt).Take(1), ct);
            return rows.FirstOrDefault();
        }

        /// <summary>
        /// Mẫu đang dùng, hoặc một bản mặc định TRONG BỘ NHỚ khi chưa cấu hình. Renderer luôn có
        /// thứ để dựng — không có nhánh "chưa cấu hình thì không xuất được file".
        /// </summary>
        public static async Task<JdTemplate> GetOrDefaultAsync(IUnitOfWork uow, CancellationToken ct) =>
            await GetCurrentAsync(uow, ct) ?? new JdTemplate
            {
                CompanyName = "Công ty",
                SectionsJson = JsonSerializer.Serialize(JdTemplateSection.Defaults(), Json),
            };

        public static string SerializeSections(IEnumerable<JdTemplateSection> sections) =>
            JsonSerializer.Serialize(sections, Json);

        /// <summary>
        /// Kiểm danh sách mục. Hai luật này lọt xuống là hỏng ngầm: khoá lạ thì renderer không tìm
        /// thấy nội dung đã soạn, còn tắt hết mục thì file JD xuất ra chỉ có mỗi đầu trang.
        /// </summary>
        public static Result ValidateSections(List<JdTemplateSection>? sections)
        {
            if (sections is not { Count: > 0 })
                return Result.Failure("Mẫu JD phải có ít nhất một mục.");

            foreach (var s in sections)
            {
                if (!JdSectionKeys.IsKnown(s.Key))
                    return Result.Failure(
                        $"Mục \"{s.Key}\" không hợp lệ. Khoá của mục là cố định, chỉ đổi được tiêu đề và thứ tự.");

                if (string.IsNullOrWhiteSpace(s.Title))
                    return Result.Failure("Mỗi mục phải có tiêu đề.");
            }

            var duplicate = sections.GroupBy(s => s.Key, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                return Result.Failure($"Mục \"{duplicate.Key}\" bị khai hai lần.");

            if (sections.All(s => !s.Enabled))
                return Result.Failure("Phải bật ít nhất một mục, nếu không file JD xuất ra sẽ trống.");

            return Result.Success();
        }

        public static async Task<JdTemplateDto> ToDtoAsync(
            JdTemplate? template, IFileStorageService storage, CancellationToken ct)
        {
            var sections = JdLayout.ParseSections(template?.SectionsJson);
            var logoUrl = string.IsNullOrWhiteSpace(template?.LogoStorageKey)
                ? null
                : await storage.GetUrlAsync(template!.LogoStorageKey!, ct);

            return new JdTemplateDto(
                template?.Id,
                template?.CompanyName ?? string.Empty,
                template?.CompanyAddress,
                template?.CompanyWebsite,
                template?.CompanyEmail,
                template?.LogoStorageKey,
                logoUrl,
                JdLayout.NormalizeHex(template?.AccentColor),
                string.IsNullOrWhiteSpace(template?.FontFamily) ? "Arial" : template!.FontFamily,
                template?.FooterNote,
                sections,
                template?.UpdatedAt);
        }
    }

    // ---------------------------------------------------------------------------------

    public record GetJdTemplateQuery : IRequest<Result<JdTemplateDto>>;

    public class GetJdTemplateQueryHandler : IRequestHandler<GetJdTemplateQuery, Result<JdTemplateDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;

        public GetJdTemplateQueryHandler(IUnitOfWork unitOfWork, IFileStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
        }

        public async Task<Result<JdTemplateDto>> Handle(GetJdTemplateQuery request, CancellationToken ct)
        {
            var template = await JdTemplateSupport.GetCurrentAsync(_unitOfWork, ct);
            return Result.Success(await JdTemplateSupport.ToDtoAsync(template, _storage, ct));
        }
    }

    // ---------------------------------------------------------------------------------

    public record UpdateJdTemplateCommand(UpdateJdTemplateInput Input, Guid? ActorId) : IRequest<Result>;

    public class UpdateJdTemplateCommandHandler : IRequestHandler<UpdateJdTemplateCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateJdTemplateCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(UpdateJdTemplateCommand request, CancellationToken ct)
        {
            var input = request.Input;

            if (string.IsNullOrWhiteSpace(input.CompanyName))
                return Result.Failure("Tên công ty là bắt buộc — nó nằm ở đầu mọi bản JD.");

            var sectionCheck = JdTemplateSupport.ValidateSections(input.Sections);
            if (sectionCheck.IsFailure) return sectionCheck;

            var template = await JdTemplateSupport.GetCurrentAsync(_unitOfWork, ct);
            var isNew = template == null;
            template ??= new JdTemplate();

            template.CompanyName = input.CompanyName.Trim();
            template.CompanyAddress = Trim(input.CompanyAddress);
            template.CompanyWebsite = Trim(input.CompanyWebsite);
            template.CompanyEmail = Trim(input.CompanyEmail);
            template.AccentColor = JdLayout.NormalizeHex(input.AccentColor);
            template.FontFamily = string.IsNullOrWhiteSpace(input.FontFamily) ? "Arial" : input.FontFamily.Trim();
            template.FooterNote = Trim(input.FooterNote);
            template.SectionsJson = JdTemplateSupport.SerializeSections(input.Sections);
            template.UpdatedByUserId = request.ActorId;
            template.UpdatedAt = DateTimeOffset.UtcNow;

            if (isNew) await _unitOfWork.Repository<JdTemplate>().AddAsync(template, ct);
            else _unitOfWork.Repository<JdTemplate>().Update(template);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "jd_template_updated",
                nameof(JdTemplate), template.Id,
                AuditMetadata.Serialize(new { template.CompanyName, sections = input.Sections.Count }), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // ---------------------------------------------------------------------------------

    public record UploadCompanyLogoCommand(byte[] Content, string FileName, string ContentType, Guid? ActorId)
        : IRequest<Result<string>>;

    public class UploadCompanyLogoCommandHandler : IRequestHandler<UploadCompanyLogoCommand, Result<string>>
    {
        /// <summary>Logo in ra khổ ~140x40pt nên vài trăm KB là thừa; chặn ở 2MB.</summary>
        private const int MaxBytes = 2 * 1024 * 1024;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;

        public UploadCompanyLogoCommandHandler(IUnitOfWork unitOfWork, IFileStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
        }

        public async Task<Result<string>> Handle(UploadCompanyLogoCommand request, CancellationToken ct)
        {
            if (request.Content is not { Length: > 0 })
                return Result.Failure<string>("File logo rỗng.");

            if (request.Content.Length > MaxBytes)
                return Result.Failure<string>("Logo không được vượt quá 2MB.");

            // Chỉ PNG/JPEG: đây là hai định dạng mà CẢ hai bộ dựng (OpenXML và PdfSharpCore) đọc
            // được. Nhận thêm SVG/WEBP thì file .docx sẽ hỏng ảnh mà không có lỗi nào lúc tải lên.
            var type = (request.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (type is not ("image/png" or "image/jpeg" or "image/jpg"))
                return Result.Failure<string>("Logo phải là ảnh PNG hoặc JPG.");

            var key = await _storage.SaveAsync(
                request.Content, request.FileName, type, StorageFolder.Branding, ct);

            var template = await JdTemplateSupport.GetCurrentAsync(_unitOfWork, ct);
            var isNew = template == null;
            template ??= new JdTemplate
            {
                CompanyName = "Công ty",
                SectionsJson = JdTemplateSupport.SerializeSections(JdTemplateSection.Defaults()),
            };

            template.LogoStorageKey = key;
            template.UpdatedByUserId = request.ActorId;
            template.UpdatedAt = DateTimeOffset.UtcNow;

            if (isNew) await _unitOfWork.Repository<JdTemplate>().AddAsync(template, ct);
            else _unitOfWork.Repository<JdTemplate>().Update(template);

            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(await _storage.GetUrlAsync(key, ct));
        }
    }
}
