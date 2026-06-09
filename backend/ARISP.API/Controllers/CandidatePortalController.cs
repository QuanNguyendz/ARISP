using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARISP.Infrastructure.Data;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/portal")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidatePortalController : ControllerBase
    {
        private readonly ARISPDbContext _dbContext;

        public CandidatePortalController(ARISPDbContext dbContext)
        {
            _dbContext = dbContext;
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
            var query = _dbContext.Applications.AsQueryable();
            if (!string.IsNullOrEmpty(emailClaim))
            {
                query = query.Where(a => a.CandidateAccountId == candidateAccountId || 
                                         (a.CandidateAccountId == null && a.CandidateEmail == emailClaim));
            }
            else
            {
                query = query.Where(a => a.CandidateAccountId == candidateAccountId);
            }

            var apps = await query.ToListAsync();

            // Link applications that are matched by email but don't have CandidateAccountId set
            bool hasUpdates = false;
            foreach (var app in apps)
            {
                if (app.CandidateAccountId == null)
                {
                    app.CandidateAccountId = candidateAccountId;
                    _dbContext.Applications.Update(app);
                    hasUpdates = true;
                }
            }

            if (hasUpdates)
            {
                await _dbContext.SaveChangesAsync();
            }

            var response = apps
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.JobPostingId,
                    JobTitle = _dbContext.JobPostings.Where(j => j.Id == a.JobPostingId).Select(j => j.Title).FirstOrDefault(),
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

            var app = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == id);
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
                _dbContext.Applications.Update(app);
                await _dbContext.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == app.JobPostingId);

            var sessions = await _dbContext.InterviewSessions
                .Where(s => s.ApplicationId == id)
                .OrderBy(s => s.RoundNumber)
                .ToListAsync();

            var sessionDetails = new List<object>();
            foreach (var s in sessions)
            {
                var evaluation = await _dbContext.Evaluations.FirstOrDefaultAsync(e => e.SessionId == s.Id);
                object? evalData = null;

                if (evaluation != null)
                {
                    var review = await _dbContext.HrReviews.FirstOrDefaultAsync(r => r.EvaluationId == evaluation.Id);
                    if (review != null && review.ShareEvaluation)
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

            var session = await _dbContext.InterviewSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null)
            {
                return NotFound(new { message = "Không tìm thấy buổi phỏng vấn." });
            }

            var app = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == session.ApplicationId);
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
                _dbContext.Applications.Update(app);
                await _dbContext.SaveChangesAsync();
                isOwner = true;
            }

            if (!isOwner)
            {
                return Forbid();
            }

            var evaluation = await _dbContext.Evaluations.FirstOrDefaultAsync(e => e.SessionId == sessionId);
            if (evaluation == null)
            {
                return NotFound(new { message = "Báo cáo đánh giá chưa được khởi tạo." });
            }

            var review = await _dbContext.HrReviews.FirstOrDefaultAsync(r => r.EvaluationId == evaluation.Id);
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
