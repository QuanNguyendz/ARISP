using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using ARI.Application.JdTemplates;
using ARI.Application.RecruitmentRequests;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.JdDocuments
{
    // ===================================================================================
    //  Bản mô tả công việc soạn theo mẫu công ty (ADR-064)
    //
    //  Recruiter được phân công mở phiếu đã duyệt → soạn JD (điền sẵn từ phiếu) → xuất file
    //  → file gắn thẳng vào tin nháp → Hiring Manager mở đúng file đó để ký duyệt.
    //
    //  Phân quyền dùng chung `RecruitmentRequestAccess` với `CreateJobCommand`: soạn được JD
    //  nhưng không dựng được tin (hoặc ngược lại) là trạng thái vô nghĩa.
    // ===================================================================================

    public record JdDocumentDto(
        Guid? Id,
        Guid RecruitmentRequestId,
        string Title,
        string? Department,
        string? EmploymentType,
        string? WorkMode,
        string? Location,
        string? ExperienceLevel,
        int? Vacancies,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,
        DateTimeOffset? ApplicationDeadline,
        IReadOnlyDictionary<string, string> Sections,

        /// <summary>File đã xuất gần nhất — null nếu chưa xuất lần nào.</summary>
        string? GeneratedFileStorageKey,
        string? GeneratedFileName,
        string? GeneratedFormat,
        string? GeneratedFileViewUrl,
        DateTimeOffset? GeneratedAt);

    public record JdDocumentInput(
        string Title,
        string? Department,
        string? EmploymentType,
        string? WorkMode,
        string? Location,
        string? ExperienceLevel,
        int? Vacancies,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,
        DateTimeOffset? ApplicationDeadline,
        Dictionary<string, string> Sections);

    /// <summary>Kết quả xuất file — đủ để màn tạo tin gắn file mà không phải tải lên lại.</summary>
    public record JdGeneratedFileDto(string StorageKey, string ViewUrl, string FileName, string Format);

    internal static class JdDocumentSupport
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static async Task<JdDocument?> GetAsync(IUnitOfWork uow, Guid requestId, CancellationToken ct)
        {
            var rows = await uow.Repository<JdDocument>()
                .FindAsync(d => d.RecruitmentRequestId == requestId && d.DeletedAt == null, ct);
            return rows.FirstOrDefault();
        }

        public static string SerializeSections(IDictionary<string, string> sections) =>
            JsonSerializer.Serialize(
                sections.Where(kv => JdSectionKeys.IsKnown(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        .ToDictionary(kv => kv.Key, kv => kv.Value.Trim()),
                Json);

        /// <summary>
        /// Bản JD khởi tạo từ phiếu khi Recruiter mở trình soạn lần đầu.
        ///
        /// Hai ô Hiring Manager đã điền trên phiếu rơi thẳng vào hai mục đầu — trình soạn mở ra là
        /// đã có nội dung để sửa, không phải một trang trắng. Đó là toàn bộ lý do ô "yêu cầu ứng
        /// viên" được thêm vào phiếu.
        /// </summary>
        public static JdDocument SeedFromRequest(RecruitmentRequest req, Guid actorId, string? departmentName)
        {
            var sections = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(req.Description)) sections[JdSectionKeys.Description] = req.Description!.Trim();
            if (!string.IsNullOrWhiteSpace(req.Requirements)) sections[JdSectionKeys.Requirements] = req.Requirements!.Trim();

            return new JdDocument
            {
                RecruitmentRequestId = req.Id,
                Title = req.Title,
                // Ảnh chụp TÊN đội tại thời điểm soạn — file JD phải bất biến kể cả khi đội
                // đổi tên sau này (cùng lý lẽ `reviewer_role` là ảnh chụp ở ADR-061).
                Department = departmentName,
                EmploymentType = req.EmploymentType,
                WorkMode = req.WorkMode,
                Location = req.Location,
                ExperienceLevel = req.ExperienceLevel,
                Vacancies = req.Headcount,
                SalaryMin = req.SalaryMin,
                SalaryMax = req.SalaryMax,
                SalaryCurrency = req.SalaryCurrency,
                SectionsJson = SerializeSections(sections),
                CreatedByUserId = actorId,
            };
        }

        public static async Task<JdDocumentDto> ToDtoAsync(
            JdDocument doc, IFileStorageService storage, CancellationToken ct)
        {
            var viewUrl = string.IsNullOrWhiteSpace(doc.GeneratedFileStorageKey)
                ? null
                : await storage.GetUrlAsync(doc.GeneratedFileStorageKey!, ct);

            return new JdDocumentDto(
                doc.Id == Guid.Empty ? null : doc.Id,
                doc.RecruitmentRequestId,
                doc.Title, doc.Department, doc.EmploymentType, doc.WorkMode, doc.Location,
                doc.ExperienceLevel, doc.Vacancies, doc.SalaryMin, doc.SalaryMax, doc.SalaryCurrency,
                doc.ApplicationDeadline,
                JdLayout.ParseContent(doc.SectionsJson),
                doc.GeneratedFileStorageKey, doc.GeneratedFileName, doc.GeneratedFormat, viewUrl, doc.GeneratedAt);
        }

        public static void Apply(JdDocument doc, JdDocumentInput input)
        {
            doc.Title = string.IsNullOrWhiteSpace(input.Title) ? doc.Title : input.Title.Trim();
            doc.Department = Trim(input.Department);
            doc.EmploymentType = Trim(input.EmploymentType);
            doc.WorkMode = Trim(input.WorkMode);
            doc.Location = Trim(input.Location);
            doc.ExperienceLevel = Trim(input.ExperienceLevel);
            doc.Vacancies = input.Vacancies is > 0 ? input.Vacancies : null;
            doc.SalaryMin = input.SalaryMin;
            doc.SalaryMax = input.SalaryMax;
            doc.SalaryCurrency = Trim(input.SalaryCurrency) ?? "VND";
            doc.ApplicationDeadline = input.ApplicationDeadline;
            doc.SectionsJson = SerializeSections(input.Sections ?? new Dictionary<string, string>());
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        }

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // ---------------------------------------------------------------------------------
    //  Đọc bản JD của một phiếu (khởi tạo từ phiếu nếu chưa soạn lần nào)
    // ---------------------------------------------------------------------------------

    public record GetJdDocumentQuery(Guid RecruitmentRequestId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<JdDocumentDto>>;

    public class GetJdDocumentQueryHandler : IRequestHandler<GetJdDocumentQuery, Result<JdDocumentDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;

        public GetJdDocumentQueryHandler(IUnitOfWork unitOfWork, IFileStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
        }

        public async Task<Result<JdDocumentDto>> Handle(GetJdDocumentQuery request, CancellationToken ct)
        {
            var (req, error, code) = await RecruitmentRequestAccess.LoadExecutableAsync(
                _unitOfWork, request.RecruitmentRequestId, request.ActorId, request.ActorRole, ct);
            if (req == null) return Fail(error!, code);

            var doc = await JdDocumentSupport.GetAsync(_unitOfWork, req.Id, ct)
                      // KHÔNG lưu bản khởi tạo xuống DB ở đây: đây là truy vấn ĐỌC, mà ghi trong
                      // đường đọc thì mở màn xem cũng đẻ ra dòng dữ liệu. Lưu khi người dùng bấm Lưu.
                      ?? JdDocumentSupport.SeedFromRequest(req, request.ActorId ?? Guid.Empty,
                             await DepartmentLookup.NameForUserAsync(_unitOfWork, req.DepartmentId, ct));

            return Result.Success(await JdDocumentSupport.ToDtoAsync(doc, _storage, ct));
        }

        private static Result<JdDocumentDto> Fail(string error, string? code) =>
            code == null ? Result.Failure<JdDocumentDto>(error) : Result.Failure<JdDocumentDto>(error, code);
    }

    // ---------------------------------------------------------------------------------
    //  Lưu bản nháp
    // ---------------------------------------------------------------------------------

    public record SaveJdDocumentCommand(Guid RecruitmentRequestId, JdDocumentInput Input, Guid? ActorId, string? ActorRole)
        : IRequest<Result>;

    public class SaveJdDocumentCommandHandler : IRequestHandler<SaveJdDocumentCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public SaveJdDocumentCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(SaveJdDocumentCommand request, CancellationToken ct)
        {
            var (req, error, code) = await RecruitmentRequestAccess.LoadExecutableAsync(
                _unitOfWork, request.RecruitmentRequestId, request.ActorId, request.ActorRole, ct);
            if (req == null) return code == null ? Result.Failure(error!) : Result.Failure(error!, code);

            if (string.IsNullOrWhiteSpace(request.Input.Title))
                return Result.Failure("Tiêu đề vị trí là bắt buộc.");

            var doc = await JdDocumentSupport.GetAsync(_unitOfWork, req.Id, ct);
            var isNew = doc == null;
            doc ??= JdDocumentSupport.SeedFromRequest(req, request.ActorId ?? Guid.Empty,
                await DepartmentLookup.NameForUserAsync(_unitOfWork, req.DepartmentId, ct));

            JdDocumentSupport.Apply(doc, request.Input);

            if (isNew) await _unitOfWork.Repository<JdDocument>().AddAsync(doc, ct);
            else _unitOfWork.Repository<JdDocument>().Update(doc);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Xuất file (docx | pdf)
    // ---------------------------------------------------------------------------------

    public record GenerateJdFileCommand(Guid RecruitmentRequestId, string? Format, Guid? ActorId, string? ActorRole)
        : IRequest<Result<JdGeneratedFileDto>>;

    public class GenerateJdFileCommandHandler : IRequestHandler<GenerateJdFileCommand, Result<JdGeneratedFileDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IJdDocumentRenderer _renderer;

        public GenerateJdFileCommandHandler(
            IUnitOfWork unitOfWork, IFileStorageService storage, IJdDocumentRenderer renderer)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _renderer = renderer;
        }

        public async Task<Result<JdGeneratedFileDto>> Handle(GenerateJdFileCommand request, CancellationToken ct)
        {
            var format = (request.Format ?? "pdf").Trim().ToLowerInvariant();
            if (format is not ("pdf" or "docx"))
                return Result.Failure<JdGeneratedFileDto>("Định dạng phải là 'pdf' hoặc 'docx'.");

            var (req, error, code) = await RecruitmentRequestAccess.LoadExecutableAsync(
                _unitOfWork, request.RecruitmentRequestId, request.ActorId, request.ActorRole, ct);
            if (req == null)
                return code == null
                    ? Result.Failure<JdGeneratedFileDto>(error!)
                    : Result.Failure<JdGeneratedFileDto>(error!, code);

            var doc = await JdDocumentSupport.GetAsync(_unitOfWork, req.Id, ct);
            if (doc == null)
                return Result.Failure<JdGeneratedFileDto>(
                    "Chưa có nội dung JD nào để xuất. Hãy soạn và lưu trước.");

            var template = await JdTemplateSupport.GetOrDefaultAsync(_unitOfWork, ct);

            // Logo là phần DUY NHẤT được phép hỏng lặng lẽ: đọc không ra thì xuất file không kèm
            // ảnh, còn hơn để cả thao tác "tải PDF" báo lỗi vì một file ảnh.
            byte[]? logo = null;
            if (!string.IsNullOrWhiteSpace(template.LogoStorageKey))
            {
                try { logo = await _storage.ReadAllBytesAsync(template.LogoStorageKey!, ct); }
                catch { logo = null; }
            }

            var bytes = format == "docx"
                ? await _renderer.RenderDocxAsync(template, doc, logo, ct)
                : await _renderer.RenderPdfAsync(template, doc, logo, ct);

            var safeTitle = SafeFileName(doc.Title);
            var fileName = $"JD-{safeTitle}.{format}";
            var contentType = format == "docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/pdf";

            // Lưu cùng thư mục với file JD tải tay: từ góc nhìn của tin tuyển dụng thì đây CHÍNH LÀ
            // file JD, chỉ khác đường sinh ra.
            var key = await _storage.SaveAsync(bytes, fileName, contentType, StorageFolder.Jd, ct);

            // Xoá bản CŨ sau khi bản mới đã nằm chắc trên storage. Mỗi lượt xuất file trước đây để
            // lại một object mồ côi: soạn qua vài vòng sửa là kho phình ra hàng chục bản JD của cùng
            // một phiếu, mà chỉ bản cuối được tham chiếu.
            //
            // Xoá SAU chứ không xoá trước: hỏng ở bước `SaveAsync` thì phiếu vẫn còn file cũ để mở,
            // còn hơn mất cả hai. Và xoá là **best-effort** — object đã mất hoặc storage chập chờn
            // không được phép làm hỏng thao tác xuất file mà người dùng vừa thực hiện thành công.
            var staleKey = doc.GeneratedFileStorageKey;
            if (!string.IsNullOrWhiteSpace(staleKey) && staleKey != key)
            {
                try { await _storage.DeleteAsync(staleKey!, ct); }
                catch { /* file rác còn lại thì dọn sau, không đánh đổi lấy một lỗi 500 */ }
            }

            doc.GeneratedFileStorageKey = key;
            doc.GeneratedFileName = fileName;
            doc.GeneratedFormat = format;
            doc.GeneratedAt = DateTimeOffset.UtcNow;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JdDocument>().Update(doc);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "jd_document_generated",
                nameof(JdDocument), doc.Id,
                AuditMetadata.Serialize(new { recruitmentRequestId = req.Id, format }), ct);

            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new JdGeneratedFileDto(
                key, await _storage.GetUrlAsync(key, ct), fileName, format));
        }

        /// <summary>Bỏ ký tự không hợp lệ trong tên file; giữ lại chữ, số, gạch.</summary>
        private static string SafeFileName(string title)
        {
            var cleaned = new string((title ?? string.Empty)
                .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
                .ToArray()).Trim('-');

            while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
            return string.IsNullOrWhiteSpace(cleaned) ? "mo-ta-cong-viec" : cleaned;
        }
    }
}
