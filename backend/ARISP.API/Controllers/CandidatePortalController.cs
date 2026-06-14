using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidatePortalController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CandidatePortalController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// GET /api/portal/applications
        /// Lấy danh sách hồ sơ ứng tuyển của ứng viên hiện tại (Lọc theo CandidateAccountId hoặc Email chưa liên kết)
        /// </summary>
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            // Fetch applications matching by CandidateAccountId or CandidateEmail (if CandidateAccountId is null)
            IEnumerable<ARISP.Domain.Entities.Application> apps;
            if (!string.IsNullOrEmpty(emailClaim))
            {
                apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => 
                    a.CandidateAccountId == candidateAccountId || 
                    (a.CandidateAccountId == null && a.CandidateEmail == emailClaim));
            }
            else
            {
                apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => 
                    a.CandidateAccountId == candidateAccountId);
            }

            var appsList = apps.ToList();

            // Link applications that are matched by email but don't have CandidateAccountId set
            bool hasUpdates = false;
            foreach (var app in appsList)
            {
                if (app.CandidateAccountId == null)
                {
                    app.CandidateAccountId = candidateAccountId;
                    _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                    hasUpdates = true;
                }
            }

            if (hasUpdates)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            // Optimize N+1 query: Fetch job titles for all related job postings at once
            var jobIds = appsList.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id));
            var jobsDict = jobs.ToDictionary(j => j.Id, j => j.Title);

            var response = appsList
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.JobPostingId,
                    JobTitle = jobsDict.TryGetValue(a.JobPostingId, out var title) ? title : null,
                    a.CandidateEmail,
                    a.CandidateName,
                    a.Status,
                    a.CreatedAt
                })
                .ToList();

            return Ok(response);
        }

        /// <summary>
        /// GET /api/portal/applications/{id}
        /// Chi tiết hồ sơ ứng tuyển + Danh sách buổi phỏng vấn & Kết quả đánh giá được share (Bảo mật IDOR)
        /// </summary>
        [HttpGet("applications/{id:guid}")]
        public async Task<IActionResult> GetApplicationDetail(Guid id)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            var app = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(id);
            if (app == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng tuyển." });
            }

            // IDOR Protection + Auto-link
            bool isOwner = app.CandidateAccountId == candidateAccountId;
            if (!isOwner && !app.CandidateAccountId.HasValue && !string.IsNullOrEmpty(emailClaim) &&
                string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                app.CandidateAccountId = candidateAccountId;
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                await _unitOfWork.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId);

            var sessionsResult = await _unitOfWork.Repository<InterviewSession>().FindAsync(s => s.ApplicationId == id);
            var sessions = sessionsResult.OrderBy(s => s.RoundNumber).ToList();

            // Optimize query: Fetch all evaluations and HR reviews in batch
            var sessionIds = sessions.Select(s => s.Id).ToList();
            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => sessionIds.Contains(e.SessionId));
            var evalDict = evaluations.ToDictionary(e => e.SessionId, e => e);

            var evalIds = evaluations.Select(e => e.Id).ToList();
            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId));
            var reviewDict = reviews.ToDictionary(r => r.EvaluationId, r => r);

            var sessionDetails = new List<object>();
            foreach (var s in sessions)
            {
                object? evalData = null;

                if (evalDict.TryGetValue(s.Id, out var evaluation))
                {
                    if (reviewDict.TryGetValue(evaluation.Id, out var review))
                    {
                        if (review.ShareEvaluation)
                        {
                            evalData = new
                            {
                                evaluation.Id,
                                evaluation.RoundNumber,
                                evaluation.AiVerdict,
                                evaluation.OverallScore,
                                evaluation.Reasoning,
                                evaluation.RecommendedNextStep,
                                evaluation.LanguageAssessment
                            };
                        }
                    }
                }

                sessionDetails.Add(new
                {
                    s.Id,
                    s.RoundNumber,
                    s.RoundType,
                    s.SessionType,
                    s.Status,
                    s.StartedAt,
                    s.EndedAt,
                    s.DurationSeconds,
                    s.RecordingUrl,
                    Evaluation = evalData
                });
            }

            return Ok(new
            {
                app.Id,
                app.JobPostingId,
                JobTitle = job?.Title,
                JobDescription = job?.JobDescription,
                app.CandidateEmail,
                app.CandidateName,
                app.CandidatePhone,
                app.CvFileUrl,
                app.Status,
                app.CreatedAt,
                Sessions = sessionDetails
            });
        }

        /// <summary>
        /// GET /api/portal/evaluations/{sessionId}
        /// Xem chi tiết đánh giá của vòng phỏng vấn nếu được HR cấu hình Share (Bảo mật IDOR)
        /// </summary>
        [HttpGet("evaluations/{sessionId:guid}")]
        public async Task<IActionResult> GetSessionEvaluation(Guid sessionId)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var candidateAccountId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });
            }

            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { message = "Không tìm thấy buổi phỏng vấn." });
            }

            var app = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId);
            if (app == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng tuyển liên quan." });
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

            // IDOR Protection + Auto-link
            bool isOwner = app.CandidateAccountId == candidateAccountId;
            if (!isOwner && !app.CandidateAccountId.HasValue && !string.IsNullOrEmpty(emailClaim) &&
                string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                app.CandidateAccountId = candidateAccountId;
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(app);
                await _unitOfWork.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == sessionId);
            var evaluation = evaluations.FirstOrDefault();
            if (evaluation == null)
            {
                return NotFound(new { message = "Báo cáo đánh giá chưa được khởi tạo." });
            }

            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id);
            var review = reviews.FirstOrDefault();
            if (review == null || !review.ShareEvaluation)
            {
                return BadRequest(new { message = "Kết quả đánh giá chi tiết chưa được chia sẻ cho vòng phỏng vấn này." });
            }

            return Ok(new
            {
                evaluation.Id,
                evaluation.SessionId,
                evaluation.ApplicationId,
                evaluation.RoundNumber,
                evaluation.SessionType,
                evaluation.AiVerdict,
                evaluation.OverallScore,
                evaluation.CriterionScores,
                evaluation.Reasoning,
                evaluation.RecommendedNextStep,
                evaluation.QuestionAnalyses,
                evaluation.LanguageAssessment,
                evaluation.CreatedAt
            });
        }
    }
}
