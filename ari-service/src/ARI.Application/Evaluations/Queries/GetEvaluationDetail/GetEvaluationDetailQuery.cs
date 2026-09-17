using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations.Queries.GetEvaluationDetail
{
    /// <summary>
    /// Tra cứu chi tiết đánh giá theo EvaluationId, fallback theo SessionId (dùng chung cho 2 endpoint).
    /// Trả về transcript, bảng điểm từng tiêu chí và <b>link video buổi phỏng vấn</b>, nên bắt buộc
    /// kiểm quyền trên tin mà hồ sơ thuộc về.
    /// </summary>
    public record GetEvaluationDetailQuery(Guid Id, Guid? UserId, string? Role)
        : IRequest<Result<EvaluationDetailResponse>>;

    public class GetEvaluationDetailQueryHandler
        : IRequestHandler<GetEvaluationDetailQuery, Result<EvaluationDetailResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetEvaluationDetailQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<EvaluationDetailResponse>> Handle(GetEvaluationDetailQuery request, CancellationToken ct)
        {
            // First check by evaluation ID
            var evaluation = await _unitOfWork.Repository<Evaluation>().GetByIdAsync(request.Id, ct);

            // If not found, try search by SessionId
            if (evaluation == null)
            {
                var evals = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == request.Id, ct);
                evaluation = evals.FirstOrDefault();
            }

            if (evaluation == null)
                return Result.Failure<EvaluationDetailResponse>("Evaluation not found.");

            // Buổi thử chỉ thuộc về ứng viên — với nhân sự nội bộ thì coi như không tồn tại (ADR-051).
            if (evaluation.SessionType == "practice")
                return Result.Failure<EvaluationDetailResponse>("Evaluation not found.");

            var (application, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, evaluation.ApplicationId, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<EvaluationDetailResponse>("Application associated with this evaluation was not found.");
            if (job == null)
                return Result.Failure<EvaluationDetailResponse>("Job posting associated with this evaluation was not found.");
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<EvaluationDetailResponse>(JobAccessErrors.EvaluationForbidden, CommonErrorCodes.Forbidden);

            var hrReviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id, ct);
            var hrReview = hrReviews.FirstOrDefault();

            var response = EvaluationDetailResponse.FromEntity(evaluation, application, job, hrReview);

            // Video buổi phỏng vấn thật (ADR-052) — resolve storageKey thành URL xem được, kèm hạn lưu.
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(evaluation.SessionId, ct);
            if (session != null)
            {
                response.RecordingExpiresAt = session.RecordingExpiresAt;
                response.RecordingDeletedAt = session.RecordingDeletedAt;
                if (!string.IsNullOrEmpty(session.RecordingUrl))
                    response.RecordingUrl = await _fileStorage.GetUrlAsync(session.RecordingUrl, ct);

                response.RoundType = session.RoundType;
                response.SessionStartedAt = session.StartedAt;
                response.SessionEndedAt = session.EndedAt;
                response.DurationSeconds = session.DurationSeconds;
            }

            // Ca đã gán cho vòng này — người duyệt cần biết đang xem buổi nào của tin nào.
            var booking = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => b.ApplicationId == application.Id
                         && b.RoundNumber == evaluation.RoundNumber
                         && b.Status != BookingStatus.Cancelled
                         && b.Status != BookingStatus.Declined, ct))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();
            if (booking != null)
            {
                var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(booking.AvailabilitySlotId, ct);
                response.SlotStartTime = slot?.StartTime;
                response.SlotEndTime = slot?.EndTime;
            }

            response.Transcript = await LoadTranscriptAsync(evaluation.SessionId, ct);

            // Ai là người có thẩm quyền chốt kết quả này (ADR-061). Giao diện cần biết để hiện đúng
            // một trong hai thứ: nút chốt, hay banner "đang chờ Hiring Manager" kèm nút chốt thay.
            var (primaryHm, hmUser, hmState) = await JobAccess.HiringManagerStatusAsync(_unitOfWork, job.Id, ct);
            response.HiringManagerState = HiringManagerStateNames.Of(hmState);
            if (primaryHm != null)
            {
                response.HiringManagerUserId = primaryHm.UserId;
                if (hmUser != null)
                    response.HiringManagerName =
                        string.IsNullOrWhiteSpace(hmUser.FullName) ? hmUser.Email : hmUser.FullName;
            }

            // Điểm khớp CV-JD đã chấm sẵn lúc ứng tuyển (ADR-030) — chỉ đọc lại, KHÔNG gọi Gemini.
            if (application.CvJdAnalysisId.HasValue)
            {
                var analysis = await _unitOfWork.Repository<CvJdAnalysis>()
                    .GetByIdAsync(application.CvJdAnalysisId.Value, ct);
                // Chỉ điểm chấm theo bộ tiêu chí (ADR-070) — không hiện điểm AI tự cho hay "0" của file không phải CV.
                if (analysis != null && ARI.Application.CvScoring.CvScoreState.IsDisplayable(analysis.Status, analysis.RubricDocumentId))
                {
                    response.CvMatchScore = analysis.MatchScore;
                    response.CvMatchSummary = analysis.Summary;
                }
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Câu hỏi theo thứ tự + câu trả lời tương ứng (cùng cách ghép với màn xem lại buổi thử của ứng
        /// viên). Nạp câu trả lời MỘT lần rồi ghép trong bộ nhớ — không truy vấn theo từng câu hỏi.
        /// </summary>
        private async Task<List<TranscriptTurnDto>> LoadTranscriptAsync(Guid sessionId, CancellationToken ct)
        {
            var questions = (await _unitOfWork.Repository<Question>()
                    .FindAsync(q => q.SessionId == sessionId, ct))
                .OrderBy(q => q.SequenceNumber)
                .ToList();
            if (questions.Count == 0) return new List<TranscriptTurnDto>();

            var answerByQuestion = (await _unitOfWork.Repository<Answer>()
                    .FindAsync(a => a.SessionId == sessionId, ct))
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).First());

            return questions.Select(q =>
            {
                answerByQuestion.TryGetValue(q.Id, out var a);
                return new TranscriptTurnDto
                {
                    SequenceNumber = q.SequenceNumber,
                    Question = q.QuestionText,
                    QuestionType = q.QuestionType,
                    Answer = string.IsNullOrWhiteSpace(a?.Transcript) ? null : a!.Transcript,
                    AskedAt = q.CreatedAt,
                    AnsweredAt = a?.CreatedAt,
                    ResponseTimeMs = a?.ResponseTimeMs,
                };
            }).ToList();
        }
    }
}
