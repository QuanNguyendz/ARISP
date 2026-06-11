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

        public ApplicationService(IUnitOfWork unitOfWork, IEmbeddingProvider embeddingProvider)
        {
            _unitOfWork = unitOfWork;
            _embeddingProvider = embeddingProvider;
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
                CreatedAt = application.CreatedAt
            };
        }

        public async Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<ApplicationResponse>("Job posting not found.");

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
                CreatedAt = application.CreatedAt
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
                CreatedAt = app.CreatedAt
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
