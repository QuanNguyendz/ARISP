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

        public ApplicationService(IUnitOfWork unitOfWork, IEmbeddingProvider embeddingProvider)
        {
            _unitOfWork = unitOfWork;
            _embeddingProvider = embeddingProvider;
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
