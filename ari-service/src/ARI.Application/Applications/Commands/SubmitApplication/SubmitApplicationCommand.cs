using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Applications.Commands.SubmitApplication
{
    /// <summary>
    /// Nộp hồ sơ từ Job Board: hash MD5 (cache CV-JD analysis) → parse text → lưu file →
    /// tạo Application qua IApplicationService; DB fail thì xoá file đã lưu.
    /// </summary>
    public record SubmitApplicationCommand(
        Guid JobPostingId,
        Guid? CandidateAccountId,
        string CandidateEmail,
        string CandidateName,
        string? CandidatePhone,
        byte[] CvBytes,
        string FileName,
        string Extension) : IRequest<Result<ApplicationResponse>>;

    public class SubmitApplicationCommandHandler : IRequestHandler<SubmitApplicationCommand, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IDocumentParserService _documentParserService;
        private readonly IFileStorageService _fileStorage;

        public SubmitApplicationCommandHandler(
            IApplicationService applicationService,
            IDocumentParserService documentParserService,
            IFileStorageService fileStorage)
        {
            _applicationService = applicationService;
            _documentParserService = documentParserService;
            _fileStorage = fileStorage;
        }

        public async Task<Result<ApplicationResponse>> Handle(SubmitApplicationCommand command, CancellationToken ct)
        {
            var (cvBytes, extension) = (command.CvBytes, command.Extension);

            // Compute CV Hash for Match Analysis Cache
            string cvFileHash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hashBytes = md5.ComputeHash(cvBytes);
                cvFileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // Extract CV text using the real document parser service (parse trước khi tốn công lưu).
            string cvText;
            try
            {
                using (var stream = new MemoryStream(cvBytes))
                {
                    cvText = await _documentParserService.ParseDocumentAsync(stream, extension);
                }

                if (!string.IsNullOrEmpty(cvText))
                {
                    cvText = cvText.Replace("\0", string.Empty);
                }
            }
            catch (Exception ex)
            {
                return Result.Failure<ApplicationResponse>($"Không thể phân tích file CV: {ex.Message}");
            }

            // Lưu file qua abstraction (Local cho dev / S3-compatible cho prod). DB lưu storageKey.
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "text/plain"
            };
            string cvFileUrl;
            try
            {
                cvFileUrl = await _fileStorage.SaveAsync(cvBytes, command.FileName, contentType);
            }
            catch (Exception ex)
            {
                return Result.Failure<ApplicationResponse>($"Không thể lưu file CV: {ex.Message}", CommonErrorCodes.ServerError);
            }

            var serviceRequest = new SubmitApplicationRequest
            {
                JobPostingId = command.JobPostingId,
                CandidateAccountId = command.CandidateAccountId,
                CandidateEmail = command.CandidateEmail,
                CandidateName = command.CandidateName,
                CandidatePhone = command.CandidatePhone,
                CvFileUrl = cvFileUrl,
                CvText = cvText,
                CvFileHash = cvFileHash
            };

            var result = await _applicationService.SubmitApplicationAsync(serviceRequest, "job_board");
            if (result.IsFailure)
            {
                // Clean up file if db write fails
                await _fileStorage.DeleteAsync(cvFileUrl);
                return result;
            }

            return result;
        }
    }
}
