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

        public async Task<Result<ApplicationResponse>> SubmitApplicationAsync(Guid organizationId, SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<ApplicationResponse>("Job posting not found.");

            var application = new ARISP.Domain.Entities.Application
            {
                OrganizationId = organizationId,
                JobPostingId = request.JobPostingId,
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
                    var chunk = new DocumentChunk
                    {
                        OrganizationId = organizationId,
                        SourceType = "cv",
                        SourceId = application.Id,
                        ChunkIndex = chunkIndex++,
                        ChunkText = chunkText,
                        Embedding = embedding
                    };
                    await _unitOfWork.Repository<DocumentChunk>().AddAsync(chunk, ct);
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var response = new ApplicationResponse
            {
                Id = application.Id,
                JobPostingId = application.JobPostingId,
                CandidateEmail = application.CandidateEmail,
                CandidateName = application.CandidateName,
                Source = application.Source,
                Status = application.Status,
                PracticeSessionUsed = application.PracticeSessionUsed,
                CreatedAt = application.CreatedAt
            };

            return Result.Success(response);
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
