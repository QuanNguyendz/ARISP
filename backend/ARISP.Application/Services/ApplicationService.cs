using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Application.Services
{
    public class ApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IEmailService _emailService;
        // Define valid status transitions in a static dictionary
        private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "invited", new(StringComparer.OrdinalIgnoreCase) { "cv_submitted", "withdrawn" } },
            { "cv_submitted", new(StringComparer.OrdinalIgnoreCase) { "screening", "withdrawn" } },
            { "screening", new(StringComparer.OrdinalIgnoreCase) { "interview", "not_pass", "withdrawn" } },
            { "interview", new(StringComparer.OrdinalIgnoreCase) { "pass", "not_pass", "withdrawn" } },
            { "pass", new(StringComparer.OrdinalIgnoreCase) { "withdrawn" } },
            { "not_pass", new(StringComparer.OrdinalIgnoreCase) { "screening", "interview" } },
            { "withdrawn", new(StringComparer.OrdinalIgnoreCase) } // terminal state
        };

        public ApplicationService(IUnitOfWork unitOfWork, IEmbeddingProvider embeddingProvider, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _embeddingProvider = embeddingProvider;
            _emailService = emailService;
        }

        /// <summary>
        /// Hàm tiện ích dùng chung để Map Entity sang Response (Tránh lặp code)
        /// </summary>
        private static ApplicationResponse MapToResponse(ARISP.Domain.Entities.Application application, JobPosting? jobPosting)
        {
            return new ApplicationResponse
            {
                Id = application.Id,
                JobPostingId = application.JobPostingId,
                JobTitle = jobPosting?.Title ?? "Unknown Job",
                CandidateEmail = application.CandidateEmail,
                CandidateName = application.CandidateName,
                CandidatePhone = application.CandidatePhone,
                CvFileUrl = application.CvFileUrl,
                CvText = application.CvText,
                Source = application.Source,
                Status = application.Status,
                PracticeSessionUsed = application.PracticeSessionUsed,
                CreatedAt = application.CreatedAt,
                CvJdAnalysisId = application.CvJdAnalysisId
            };
        }

        public async Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<ApplicationResponse>("Job posting not found.");

            if (jobPosting.Status != "active")
                return Result.Failure<ApplicationResponse>("Tin tuyển dụng này hiện không hoạt động hoặc đã bị đóng.");

            if (jobPosting.ApplicationDeadline.HasValue && jobPosting.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
                return Result.Failure<ApplicationResponse>("Tin tuyển dụng này đã hết hạn nộp hồ sơ.");

            var application = new ARISP.Domain.Entities.Application
            {
                JobPostingId = request.JobPostingId,
                CandidateAccountId = request.CandidateAccountId,
                CandidateEmail = request.CandidateEmail,
                CandidateName = request.CandidateName,
                CandidatePhone = request.CandidatePhone,
                CvFileUrl = request.CvFileUrl,
                CvText = request.CvText,
                Source = source,
                Status = "cv_submitted"
            };

            // Auto-link CvJdAnalysis if it exists
            if (!string.IsNullOrEmpty(request.CvFileHash))
            {
                var analyses = await _unitOfWork.Repository<CvJdAnalysis>()
                    .FindAsync(x => x.JobPostingId == request.JobPostingId && x.CvHash == request.CvFileHash, ct);
                var analysis = System.Linq.Enumerable.FirstOrDefault(analyses);
                if (analysis != null)
                {
                    application.CvJdAnalysisId = analysis.Id;
                }
            }

            await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().AddAsync(application, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Chunk & Embed CV text for candidate personalization
            if (!string.IsNullOrEmpty(request.CvText))
            {
                var chunks = ChunkText(request.CvText);
                int chunkIndex = 0;
                foreach (var chunkText in chunks)
                {
                    var embedding = await _embeddingProvider.EmbedAsync(chunkText, ct);
                    var embeddingString = $"[{string.Join(",", embedding)}]";
                    var chunkId = Guid.NewGuid();
                    var createdAt = DateTimeOffset.UtcNow;

                    await _unitOfWork.ExecuteSqlRawAsync(
                        "INSERT INTO document_chunks (id, source_type, source_id, chunk_index, chunk_text, embedding, metadata, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}::vector, {6}::jsonb, {7})",
                        new object[] { chunkId, "cv", application.Id, chunkIndex++, chunkText, embeddingString, "{}", createdAt },
                        ct
                    );
                }
            }

            var response = new ApplicationResponse
            {
                Id = application.Id,
                JobPostingId = application.JobPostingId,
                JobTitle = jobPosting?.Title ?? "Unknown Job",
                CandidateEmail = application.CandidateEmail,
                CandidateName = application.CandidateName,
                CandidatePhone = application.CandidatePhone,
                CvFileUrl = application.CvFileUrl,
                CvText = application.CvText,
                Source = application.Source,
                Status = application.Status,
                PracticeSessionUsed = application.PracticeSessionUsed,
                CreatedAt = application.CreatedAt,
                CvJdAnalysisId = application.CvJdAnalysisId
            };

            return Result.Success(response);
        }

        public async Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default)
        {
            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetAllAsync(ct);
            var jobs = await _unitOfWork.Repository<JobPosting>().GetAllAsync(ct);
            var jobDict = jobs.ToDictionary(j => j.Id, j => j.Title);

            var responseList = applications.Select(app => new ApplicationResponse
            {
                Id = app.Id,
                JobPostingId = app.JobPostingId,
                JobTitle = jobDict.TryGetValue(app.JobPostingId, out var title) ? title : "Unknown Job",
                CandidateEmail = app.CandidateEmail,
                CandidateName = app.CandidateName,
                CandidatePhone = app.CandidatePhone,
                CvFileUrl = app.CvFileUrl,
                CvText = app.CvText,
                Source = app.Source,
                Status = app.Status,
                PracticeSessionUsed = app.PracticeSessionUsed,
                CreatedAt = app.CreatedAt,
                CvJdAnalysisId = app.CvJdAnalysisId
            }).ToList();

            return Result.Success(responseList);
        }

        /// <summary>
        /// Retrieves detailed application by its Guid ID.
        /// </summary>
        public async Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(id, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>("Application not found.");

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            return Result.Success(MapToResponse(application, jobPosting));
        }

        /// <summary>
        /// Updates the application status after validating transition rules.
        /// </summary>
        public async Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
                return Result.Failure<ApplicationResponse>("Target status cannot be empty.");

            var repository = _unitOfWork.Repository<ARISP.Domain.Entities.Application>();
            var application = await repository.GetByIdAsync(id, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>("Application not found.");

            string currentStatus = application.Status?.Trim() ?? string.Empty;
            newStatus = newStatus.Trim().ToLowerInvariant();

            // 1. Tối ưu: Lấy danh sách trạng thái hợp lệ trực tiếp từ bộ Keys của Dictionary tĩnh
            if (!AllowedStatusTransitions.ContainsKey(newStatus))
            {
                return Result.Failure<ApplicationResponse>($"Status '{newStatus}' is invalid. Allowed values: {string.Join(", ", AllowedStatusTransitions.Keys)}");
            }

            // 2. Prevent updating if status is unchanged
            if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<ApplicationResponse>($"Application is already in '{newStatus}' status.");
            }

            // 3. Validate status transition
            if (AllowedStatusTransitions.TryGetValue(currentStatus, out var allowedNextStates))
            {
                if (!allowedNextStates.Contains(newStatus))
                {
                    return Result.Failure<ApplicationResponse>($"Cannot transition application status from '{currentStatus}' to '{newStatus}'.");
                }
            }
            else
            {
                return Result.Failure<ApplicationResponse>($"Transition mapping for current status '{currentStatus}' is not configured.");
            }

            // 4. Update status and save
            application.Status = newStatus;
            application.UpdatedAt = DateTimeOffset.UtcNow;

            repository.Update(application);
            await _unitOfWork.SaveChangesAsync(ct);

            // Get job info for the response
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            return Result.Success(MapToResponse(application, jobPosting));
        }

        public async Task<Result<bool>> SendInterviewInviteAsync(Guid applicationId, CancellationToken ct = default)
        {
            // 1. Lấy thông tin đơn ứng tuyển từ DB
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
            {
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");
            }

            // 2. Tạo Magic Link hướng tới trang làm bài test của Frontend Portal
            var magicLink = $"http://localhost:3000/portal/practice/{applicationId}";

            // 3. Chuẩn bị nội dung HTML cho Email
            var subject = "[ARISP] - Lời mời tham gia vòng kiểm tra năng lực";
            var htmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
            <h3 style='color: #333;'>Chào {application.CandidateName},</h3>
            <p>Chúc mừng bạn! Hồ sơ ứng tuyển của bạn đã thông qua vòng duyệt hồ sơ (CV Review).</p>
            <p>Chúng tôi trân trọng mời bạn tham gia vòng đánh giá tiếp theo bằng cách truy cập vào đường dẫn (Magic Link) dưới đây để tiến hành làm bài test năng lực:</p>
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{magicLink}' style='padding: 12px 25px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Bắt đầu làm bài đánh giá</a>
            </p>
            <p style='color: #666; font-size: 12px;'><i>Lưu ý: Đường dẫn này dành riêng cho bạn và không nên chia sẻ cho người khác.</i></p>
            <br/>
            <p>Trân trọng,</p>
            <p><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            // 4. Gọi Service gửi Mail có sẵn (Không lo gạch đỏ, không cần cài MailKit vào Application nữa)
            try
            {
                await _emailService.SendEmailAsync(application.CandidateEmail, subject, htmlMessage);

                // Thêm logic cập nhật trạng thái nếu cần
                application.Status = "Invited";
                await _unitOfWork.SaveChangesAsync(ct);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Lỗi khi gọi dịch vụ gửi email: {ex.Message}");
            }
        }

        public async Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result.Failure<bool>("Application not found.");

            return Result.Success(!application.PracticeSessionUsed);
        }

        private List<string> ChunkText(string text)
        {
            var chunks = new List<string>();
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string currentChunk = "";

            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length > 600)
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = line + " ";
                }
                else
                {
                    currentChunk += line + " ";
                }
            }

            if (!string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
            }

            return chunks;
        }
    }
}
