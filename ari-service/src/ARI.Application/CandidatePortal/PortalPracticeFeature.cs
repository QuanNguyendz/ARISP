using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    /// <summary>
    /// Xem lại buổi phỏng vấn THỬ — không gian riêng của ứng viên (ADR-051).
    /// Transcript + nhận xét AI lưu vĩnh viễn, xem lại không giới hạn số lần; nhân sự nội bộ
    /// không truy cập được (mọi surface staff đã lọc bỏ <c>session_type = 'practice'</c>).
    /// Transcript buổi THẬT vẫn nằm sau cổng <c>HrReview.ShareTranscript</c> — không mở ở đây.
    /// </summary>

    // ============================================================
    // GET /api/portal/practice/sessions — danh sách buổi thử của ứng viên
    // ============================================================

    public record GetMyPracticeSessionsQuery(Guid? ApplicationId, Guid CandidateAccountId, string? Email)
        : IRequest<Result<object>>;

    public class GetMyPracticeSessionsQueryHandler : IRequestHandler<GetMyPracticeSessionsQuery, Result<object>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetMyPracticeSessionsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<object>> Handle(GetMyPracticeSessionsQuery request, CancellationToken ct)
        {
            var candidateAccountId = request.CandidateAccountId;

            // Hồ sơ của ứng viên (lọc thêm theo 1 hồ sơ nếu client truyền applicationId).
            var myApps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => a.CandidateAccountId == candidateAccountId)
                    .Select(a => new { a.Id, a.JobPostingId }), ct);

            if (request.ApplicationId.HasValue)
                myApps = myApps.Where(a => a.Id == request.ApplicationId.Value).ToList();

            if (myApps.Count == 0)
                return Result.Success<object>(new List<object>());

            var appIds = myApps.Select(a => a.Id).ToList();
            var sessions = (await _unitOfWork.Repository<InterviewSession>()
                    .FindAsync(s => appIds.Contains(s.ApplicationId) && s.SessionType == "practice", ct))
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .ToList();

            if (sessions.Count == 0)
                return Result.Success<object>(new List<object>());

            var jobIds = myApps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobTitleById = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                .ToDictionary(j => j.Id, j => j.Title);
            var jobIdByAppId = myApps.ToDictionary(a => a.Id, a => a.JobPostingId);

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var evalBySession = (await _unitOfWork.Repository<Evaluation>()
                    .QueryAsync(q => q
                        .Where(e => sessionIds.Contains(e.SessionId))
                        .Select(e => new { e.SessionId, e.OverallScore }), ct))
                .GroupBy(e => e.SessionId)
                .ToDictionary(g => g.Key, g => g.First());

            var turnCountBySession = (await _unitOfWork.Repository<Question>()
                    .QueryAsync(q => q.Where(x => sessionIds.Contains(x.SessionId)).Select(x => x.SessionId), ct))
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var items = sessions.Select(s =>
            {
                evalBySession.TryGetValue(s.Id, out var eval);
                string? jobTitle = null;
                if (jobIdByAppId.TryGetValue(s.ApplicationId, out var jobId))
                    jobTitleById.TryGetValue(jobId, out jobTitle);

                return (object)new
                {
                    Id = s.Id.ToString(),
                    ApplicationId = s.ApplicationId.ToString(),
                    JobTitle = jobTitle,
                    s.RoundNumber,
                    s.RoundType,
                    s.Status,
                    s.StartedAt,
                    s.EndedAt,
                    s.DurationSeconds,
                    HasEvaluation = eval != null,
                    OverallScore = eval?.OverallScore,
                    TurnCount = turnCountBySession.TryGetValue(s.Id, out var c) ? c : 0
                };
            }).ToList();

            return Result.Success<object>(items);
        }
    }

    // ============================================================
    // GET /api/portal/practice/sessions/{sessionId} — transcript + nhận xét AI (IDOR)
    // ============================================================

    public record GetMyPracticeReviewQuery(Guid SessionId, Guid CandidateAccountId, string? Email)
        : IRequest<Result<object>>;

    public class GetMyPracticeReviewQueryHandler : IRequestHandler<GetMyPracticeReviewQuery, Result<object>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetMyPracticeReviewQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<object>> Handle(GetMyPracticeReviewQuery request, CancellationToken ct)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(request.SessionId, ct);
            if (session == null)
                return Result.Failure<object>("Không tìm thấy buổi phỏng vấn thử.", CommonErrorCodes.NotFound);

            // Endpoint này CHỈ phục vụ buổi thử — buổi thật đi theo cổng chia sẻ của HR.
            if (session.SessionType != "practice")
                return Result.Failure<object>("Không tìm thấy buổi phỏng vấn thử.", CommonErrorCodes.NotFound);

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            if (app == null)
                return Result.Failure<object>("Không tìm thấy hồ sơ ứng tuyển liên quan.", CommonErrorCodes.NotFound);

            // IDOR Protection + Auto-link
            if (!await PortalSupport.TryEnsureOwnerAsync(app, request.CandidateAccountId, request.Email, _unitOfWork))
                return Result.Failure<object>("Forbidden", CommonErrorCodes.Forbidden);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            // Transcript: câu hỏi theo thứ tự + câu trả lời tương ứng. Nạp answers MỘT lần rồi ghép
            // in-memory (tránh N+1 theo từng câu hỏi).
            var questions = (await _unitOfWork.Repository<Question>()
                    .FindAsync(q => q.SessionId == request.SessionId, ct))
                .OrderBy(q => q.SequenceNumber)
                .ToList();
            var answers = (await _unitOfWork.Repository<Answer>()
                    .FindAsync(a => a.SessionId == request.SessionId, ct))
                .ToList();
            var answerByQuestionId = answers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).First());

            var evaluation = (await _unitOfWork.Repository<Evaluation>()
                .FindAsync(e => e.SessionId == request.SessionId, ct)).FirstOrDefault();

            // Ghép nhận xét AI vào ĐÚNG lượt hỏi–đáp: câu hỏi/câu trả lời luôn lấy từ DB, AI chỉ
            // đóng góp điểm + phân tích + gợi ý (trước đây phụ thuộc model chép lại câu hỏi nên
            // mục "phân tích từng câu" hiện trống trơn — ADR-051).
            var analyses = PortalSupport.ParseQuestionAnalyses(evaluation?.QuestionAnalyses);
            var analysisBySequence = new Dictionary<int, QuestionAnalysisDto>();
            for (var i = 0; i < analyses.Count; i++)
            {
                var a = analyses[i];
                // Model bỏ quên sequence_number → suy theo thứ tự mảng.
                var seq = a.SequenceNumber > 0
                    ? a.SequenceNumber
                    : (i < questions.Count ? questions[i].SequenceNumber : 0);
                var hasContent = !string.IsNullOrWhiteSpace(a.Analysis)
                                 || !string.IsNullOrWhiteSpace(a.Feedback)
                                 || a.Score > 0;
                if (seq > 0 && hasContent) analysisBySequence[seq] = a;
            }

            var turns = questions.Select(q =>
            {
                answerByQuestionId.TryGetValue(q.Id, out var a);
                analysisBySequence.TryGetValue(q.SequenceNumber, out var analysis);
                return (object)new
                {
                    q.SequenceNumber,
                    Question = q.QuestionText,
                    q.QuestionType,
                    Answer = a?.Transcript,
                    AskedAt = q.CreatedAt,
                    AnsweredAt = a?.CreatedAt,
                    ResponseTimeMs = a?.ResponseTimeMs,
                    // Model không trả điểm (báo cáo cũ) → null, KHÔNG phải 0: 0 khiến FE tô đỏ
                    // "Cần cải thiện" trong khi nhận xét lại tích cực.
                    Score = analysis != null && analysis.Score > 0 ? analysis.Score : (decimal?)null,
                    Analysis = analysis?.Analysis,
                    Feedback = analysis?.Feedback
                };
            }).ToList();

            // CỐ Ý không trả AiVerdict: buổi thử không phải kết quả tuyển dụng, chỉ đưa điểm + nhận xét
            // để ứng viên tự luyện tập (ADR-051).
            // Phân tích từng câu đã gắn vào `turns` nên KHÔNG trả lại lần hai ở đây (tránh lặp nội dung
            // + tránh hiển thị bản sao Q&A do model chép sai).
            object? evalData = evaluation == null ? null : new
            {
                evaluation.Id,
                evaluation.OverallScore,
                evaluation.Reasoning,
                evaluation.RecommendedNextStep,
                CriterionScores = PortalSupport.ParseCriterionScores(evaluation.CriterionScores),
                LanguageAssessment = PortalSupport.ParseLanguageAssessment(evaluation.LanguageAssessment),
                AnalyzedTurnCount = analysisBySequence.Count,
                evaluation.CreatedAt
            };

            return Result.Success<object>(new
            {
                Id = session.Id.ToString(),
                ApplicationId = session.ApplicationId.ToString(),
                JobTitle = job?.Title,
                session.RoundNumber,
                session.RoundType,
                session.Status,
                session.InterviewLanguage,
                ReportLanguage = session.ReportLanguage ?? session.InterviewLanguage,
                session.StartedAt,
                session.EndedAt,
                session.DurationSeconds,
                session.ClosingText,
                Turns = turns,
                Evaluation = evalData,
                // Phiên đã đóng nhưng AI chưa chấm xong → FE hiện trạng thái "đang chấm".
                EvaluationPending = evaluation == null && session.Status == "completed"
            });
        }
    }
}
