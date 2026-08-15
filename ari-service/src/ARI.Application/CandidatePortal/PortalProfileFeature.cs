using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    // ============================================================
    // GET /api/portal/profile
    // ============================================================

    public record GetMyProfileQuery(Guid CandidateId) : IRequest<Result<CandidateProfileResponse>>;

    public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, Result<CandidateProfileResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetMyProfileQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<CandidateProfileResponse>> Handle(GetMyProfileQuery request, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateId, ct);
            if (acc == null)
                return Result.Failure<CandidateProfileResponse>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.NotFound);

            return Result.Success(await PortalSupport.BuildProfileAsync(acc, _fileStorage));
        }
    }

    // ============================================================
    // PUT /api/portal/profile
    // ============================================================

    public record UpdateMyProfileCommand(Guid CandidateId, CandidateProfileUpdateRequest Request) : IRequest<Result<CandidateProfileResponse>>;

    public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result<CandidateProfileResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public UpdateMyProfileCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<CandidateProfileResponse>> Handle(UpdateMyProfileCommand command, CancellationToken ct)
        {
            var req = command.Request;

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(command.CandidateId, ct);
            if (acc == null)
                return Result.Failure<CandidateProfileResponse>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.NotFound);

            if (!string.IsNullOrWhiteSpace(req.FullName)) acc.FullName = req.FullName.Trim();
            acc.Headline = req.Headline?.Trim();
            acc.Phone = req.Phone?.Trim();

            // Địa giới hành chính (Provinces Open API v2). Lưu code+name có cấu trúc và
            // tự suy ra chuỗi Location "Phường X, Tỉnh Y" để hiển thị/tương thích cũ.
            acc.ProvinceCode = req.ProvinceCode;
            acc.ProvinceName = req.ProvinceName?.Trim();
            acc.WardCode = req.WardCode;
            acc.WardName = req.WardName?.Trim();
            acc.Location = string.Join(", ", new[] { acc.WardName, acc.ProvinceName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(acc.Location)) acc.Location = null;

            acc.DateOfBirth = string.IsNullOrWhiteSpace(req.DateOfBirth) ? null : req.DateOfBirth.Trim();
            acc.About = req.About;
            acc.LinkedinUrl = req.LinkedinUrl?.Trim();
            acc.GithubUrl = req.GithubUrl?.Trim();
            acc.PortfolioUrl = req.PortfolioUrl?.Trim();

            if (req.Skills != null)
                acc.SkillsJson = JsonSerializer.Serialize(req.Skills.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList());
            if (req.Experience != null)
                acc.ExperienceJson = JsonSerializer.Serialize(req.Experience);
            if (req.Education != null)
                acc.EducationJson = JsonSerializer.Serialize(req.Education);

            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(await PortalSupport.BuildProfileAsync(acc, _fileStorage));
        }
    }

    // ============================================================
    // POST /api/portal/profile/cv — upload CV hồ sơ + Gemini review
    // ============================================================

    public record UploadedProfileCvDto(string? ProfileCvUrl, string CvFileName, string? CvDownloadUrl,
        CvReviewResponse? Review, bool AiAvailable, string? AiMessage);

    public record UploadProfileCvCommand(Guid CandidateId, byte[] Bytes, string FileName, string Ext) : IRequest<Result<UploadedProfileCvDto>>;

    public class UploadProfileCvCommandHandler : IRequestHandler<UploadProfileCvCommand, Result<UploadedProfileCvDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IDocumentParserService _documentParserService;
        private readonly IGeminiProvider _geminiProvider;

        public UploadProfileCvCommandHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IDocumentParserService documentParserService,
            IGeminiProvider geminiProvider)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _documentParserService = documentParserService;
            _geminiProvider = geminiProvider;
        }

        public async Task<Result<UploadedProfileCvDto>> Handle(UploadProfileCvCommand command, CancellationToken ct)
        {
            var (bytes, ext) = (command.Bytes, command.Ext);

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(command.CandidateId, ct);
            if (acc == null)
                return Result.Failure<UploadedProfileCvDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.NotFound);

            var mime = ext == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            string fallbackText = string.Empty;
            try
            {
                using var s = new System.IO.MemoryStream(bytes);
                fallbackText = await _documentParserService.ParseDocumentAsync(s, ext);
            }
            catch { /* fallback text best-effort */ }

            // Gemini đánh giá CV
            var reviewResult = await _geminiProvider.ReviewCvAsync(bytes, mime, fallbackText);
            CvReviewResponse? reviewResp = null;
            if (reviewResult.IsSuccess)
            {
                var r = reviewResult.Value;
                if (!r.IsValidCv)
                    return Result.Failure<UploadedProfileCvDto>("Tài liệu tải lên không phải là CV hợp lệ. Vui lòng tải lên CV (PDF/DOCX).", "invalid_cv");

                reviewResp = new CvReviewResponse
                {
                    IsValidCv = true,
                    OverallScore = r.OverallScore,
                    Verdict = r.Verdict,
                    Summary = r.Summary,
                    SuggestedPositions = r.SuggestedPositions,
                    Strengths = r.Strengths,
                    Improvements = r.Improvements,
                    MissingSections = r.MissingSections,
                    ReviewedAt = DateTimeOffset.UtcNow.ToString("o"),
                    ReviewedBy = r.Provider
                };
            }
            // Nếu AI không khả dụng (vd thiếu API key) vẫn lưu CV, chỉ không có đánh giá.

            // Xoá CV cũ (nếu có) để không tích tụ rác trên storage.
            if (!string.IsNullOrEmpty(acc.ProfileCvUrl))
                await _fileStorage.DeleteAsync(acc.ProfileCvUrl);

            var storageKey = await _fileStorage.SaveAsync(bytes, command.FileName, mime, StorageFolder.Cv);
            var originalFileName = System.IO.Path.GetFileName(command.FileName);

            acc.ProfileCvUrl = storageKey;
            acc.ProfileCvFileName = originalFileName;
            if (reviewResp != null)
                acc.CvReviewJson = JsonSerializer.Serialize(reviewResp);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(new UploadedProfileCvDto(
                await _fileStorage.GetUrlAsync(acc.ProfileCvUrl),
                originalFileName,
                await _fileStorage.GetDownloadUrlAsync(acc.ProfileCvUrl, originalFileName),
                reviewResp,
                reviewResult.IsSuccess,
                reviewResult.IsFailure ? reviewResult.Error : null));
        }
    }

    // ============================================================
    // POST /api/portal/profile/avatar
    // ============================================================

    public record UploadedAvatarDto(string AvatarUrl);

    /// <summary>Ảnh đại diện ứng viên tự tải lên. Ext đã chuẩn hoá chữ thường, kèm dấu chấm.</summary>
    public record UploadAvatarCommand(Guid CandidateId, byte[] Bytes, string FileName, string Ext)
        : IRequest<Result<UploadedAvatarDto>>;

    public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, Result<UploadedAvatarDto>>
    {
        /// <summary>Ảnh đại diện không cần lớn — 2MB đủ cho ảnh vuông chất lượng cao.</summary>
        public const int MaxBytes = 2 * 1024 * 1024;

        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public UploadAvatarCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<UploadedAvatarDto>> Handle(UploadAvatarCommand command, CancellationToken ct)
        {
            if (!AllowedTypes.TryGetValue(command.Ext, out var mime))
                return Result.Failure<UploadedAvatarDto>("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");

            if (command.Bytes.Length == 0)
                return Result.Failure<UploadedAvatarDto>("File ảnh rỗng.");

            if (command.Bytes.Length > MaxBytes)
                return Result.Failure<UploadedAvatarDto>("Ảnh vượt quá 2MB. Vui lòng chọn ảnh nhỏ hơn.");

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(command.CandidateId, ct);
            if (acc == null)
                return Result.Failure<UploadedAvatarDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.NotFound);

            // Xoá ảnh cũ để không tích tụ rác trên storage — nhưng CHỈ khi đó là file của ta.
            // Ảnh Google là URL tuyệt đối trỏ sang máy chủ Google, gọi DeleteAsync với nó là vô nghĩa.
            var previous = acc.AvatarUrl;
            var previousIsOurFile = !string.IsNullOrEmpty(previous)
                && !previous!.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !previous.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            var storageKey = await _fileStorage.SaveAsync(command.Bytes, command.FileName, mime, StorageFolder.Avatar, ct);

            acc.AvatarUrl = storageKey;
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync(ct);

            // Xoá sau khi đã lưu bản ghi mới: đổi thứ tự thì upload lỗi giữa chừng là mất luôn ảnh cũ.
            if (previousIsOurFile)
            {
                try { await _fileStorage.DeleteAsync(previous!, ct); }
                catch { /* best-effort — file rác không đáng để hỏng cả thao tác */ }
            }

            var url = await PortalSupport.ResolveAvatarUrlAsync(acc.AvatarUrl, _fileStorage);
            return Result.Success(new UploadedAvatarDto(url ?? string.Empty));
        }
    }

    // ============================================================
    // POST /api/portal/profile/change-password
    // ============================================================

    public record ChangeCandidatePasswordResultDto(string Message);

    public record ChangeCandidatePasswordCommand(Guid CandidateId, string? CurrentPassword, string NewPassword)
        : IRequest<Result<ChangeCandidatePasswordResultDto>>;

    public class ChangeCandidatePasswordCommandHandler
        : IRequestHandler<ChangeCandidatePasswordCommand, Result<ChangeCandidatePasswordResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public ChangeCandidatePasswordCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<ChangeCandidatePasswordResultDto>> Handle(ChangeCandidatePasswordCommand request, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateId, ct);
            if (acc == null)
                return Result.Failure<ChangeCandidatePasswordResultDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.NotFound);

            var hasPw = !string.IsNullOrEmpty(acc.PasswordHash);

            // Tài khoản đã có mật khẩu → bắt buộc xác minh mật khẩu hiện tại trước khi đổi.
            if (hasPw)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                    return Result.Failure<ChangeCandidatePasswordResultDto>("Vui lòng nhập mật khẩu hiện tại.");

                if (!_passwordHasher.Verify(request.CurrentPassword, acc.PasswordHash!))
                    return Result.Failure<ChangeCandidatePasswordResultDto>("Mật khẩu hiện tại không đúng.", "wrong_current_password");
            }

            if (!AuthSupport.IsStrongPassword(request.NewPassword, out var validationError))
                return Result.Failure<ChangeCandidatePasswordResultDto>(validationError);

            // Không cho đặt mật khẩu mới trùng mật khẩu cũ.
            if (hasPw && _passwordHasher.Verify(request.NewPassword, acc.PasswordHash!))
                return Result.Failure<ChangeCandidatePasswordResultDto>("Mật khẩu mới không được trùng mật khẩu hiện tại.");

            acc.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            acc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(acc);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success(new ChangeCandidatePasswordResultDto(
                hasPw ? "Đổi mật khẩu thành công." : "Đặt mật khẩu thành công."));
        }
    }
}
