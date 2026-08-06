using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.OnlineTest
{
    // ============================================================
    // GET /api/portal/online-test/{applicationId} — đề thi cho ứng viên (bốc ngẫu nhiên N câu)
    // ============================================================

    public record GetCandidateOnlineTestQuery(Guid ApplicationId, Guid CandidateAccountId, string? Email)
        : IRequest<Result<CandidateOnlineTestDto>>;

    public class GetCandidateOnlineTestQueryHandler : IRequestHandler<GetCandidateOnlineTestQuery, Result<CandidateOnlineTestDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCandidateOnlineTestQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<CandidateOnlineTestDto>> Handle(GetCandidateOnlineTestQuery request, CancellationToken ct)
        {
            var (ok, app) = await OnlineTestSupport.AuthorizeCandidateAsync(
                _unitOfWork, request.ApplicationId, request.CandidateAccountId, request.Email, ct);
            if (app == null) return Result.Failure<CandidateOnlineTestDto>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<CandidateOnlineTestDto>("Bạn không có quyền làm bài thi của hồ sơ này.", CommonErrorCodes.Forbidden);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            if (job == null) return Result.Failure<CandidateOnlineTestDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var round = await OnlineTestSupport.ResolveRoundAsync(_unitOfWork, app.JobPostingId, ct);

            var bank = (await _unitOfWork.Repository<OnlineTestQuestion>()
                .FindAsync(q => q.JobPostingId == app.JobPostingId, ct)).ToList();

            // Bộ đề đã bốc (deterministic theo hồ sơ + vòng) — cùng ứng viên luôn nhận cùng bộ đề.
            var drawnQuestions = OnlineTestSupport.DrawQuestions(bank, app.Id, round, job.OnlineTestQuestionsPerTest);

            // Chỉ trả câu hỏi khi hồ sơ ĐÃ qua vòng duyệt CV. Chưa pass → trả metadata (số câu, điểm sàn…)
            // để FE hiện ô "Chờ duyệt CV", nhưng KHÔNG lộ câu hỏi/đáp án.
            var cvPassed = OnlineTestSupport.IsCvPassed(app.Status);
            var questions = cvPassed
                ? drawnQuestions.Select(q => new CandidateTestQuestionDto(
                        q.Id,
                        q.QuestionText,
                        OnlineTestSupport.ParseOptions(q.Options),
                        string.IsNullOrWhiteSpace(q.QuestionType) ? "single" : q.QuestionType))
                    .ToList()
                : new List<CandidateTestQuestionDto>();

            var submission = (await _unitOfWork.Repository<OnlineTestSubmission>()
                    .FindAsync(s => s.ApplicationId == app.Id && s.RoundNumber == round, ct))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            var dto = new CandidateOnlineTestDto(
                app.Id,
                app.JobPostingId,
                job.Title,
                round,
                job.OnlineTestPassScore,
                job.OnlineTestDurationMinutes,
                drawnQuestions.Count,   // tổng số câu của bài — luôn có để FE biết job có đề (kể cả khi chưa pass)
                questions,              // rỗng khi chưa duyệt CV
                submission != null,
                submission?.Score,
                submission?.IsPassed,
                submission?.CreatedAt,
                cvPassed);

            return Result.Success(dto);
        }
    }

    // ============================================================
    // POST /api/portal/online-test/{applicationId}/submit — nộp bài + tự chấm
    // ============================================================

    public record SubmitOnlineTestCommand(Guid ApplicationId, Guid CandidateAccountId, string? Email, Dictionary<Guid, List<int>> Answers)
        : IRequest<Result<OnlineTestResultDto>>;

    public class SubmitOnlineTestCommandHandler : IRequestHandler<SubmitOnlineTestCommand, Result<OnlineTestResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public SubmitOnlineTestCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result<OnlineTestResultDto>> Handle(SubmitOnlineTestCommand command, CancellationToken ct)
        {
            var (ok, app) = await OnlineTestSupport.AuthorizeCandidateAsync(
                _unitOfWork, command.ApplicationId, command.CandidateAccountId, command.Email, ct);
            if (app == null) return Result.Failure<OnlineTestResultDto>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestResultDto>("Bạn không có quyền nộp bài của hồ sơ này.", CommonErrorCodes.Forbidden);

            if (string.Equals(app.Status, "withdrawn", StringComparison.OrdinalIgnoreCase))
                return Result.Failure<OnlineTestResultDto>("Hồ sơ đã rút — không thể làm bài thi.");

            // Chỉ cho nộp bài khi hồ sơ đã qua vòng duyệt CV (chặn cv_submitted/cv_rejected).
            if (!OnlineTestSupport.IsCvPassed(app.Status))
                return Result.Failure<OnlineTestResultDto>("Hồ sơ của bạn cần được duyệt qua vòng CV trước khi làm bài thi trắc nghiệm.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            if (job == null) return Result.Failure<OnlineTestResultDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var round = await OnlineTestSupport.ResolveRoundAsync(_unitOfWork, app.JobPostingId, ct);

            var existing = await _unitOfWork.Repository<OnlineTestSubmission>()
                .CountAsync(s => s.ApplicationId == app.Id && s.RoundNumber == round, ct);
            if (existing > 0)
                return Result.Failure<OnlineTestResultDto>("Bạn đã nộp bài thi trắc nghiệm cho vòng này rồi.", CommonErrorCodes.Conflict);

            var bank = (await _unitOfWork.Repository<OnlineTestQuestion>()
                .FindAsync(q => q.JobPostingId == app.JobPostingId, ct)).ToList();
            if (bank.Count == 0)
                return Result.Failure<OnlineTestResultDto>("Bài thi chưa có câu hỏi. Vui lòng liên hệ nhân sự.");

            // Chấm ĐÚNG bộ đề đã bốc (deterministic) — không tính câu ngoài bộ đề của ứng viên.
            var drawn = OnlineTestSupport.DrawQuestions(bank, app.Id, round, job.OnlineTestQuestionsPerTest);
            var answers = command.Answers ?? new Dictionary<Guid, List<int>>();

            int correct = drawn.Count(q =>
            {
                var correctSet = OnlineTestSupport.ParseInts(q.CorrectOptions).ToHashSet();
                if (correctSet.Count == 0) return false;
                answers.TryGetValue(q.Id, out var picked);
                var pickedSet = (picked ?? new List<int>()).ToHashSet();
                // Đúng khi tập chọn KHỚP HOÀN TOÀN tập đáp án đúng (áp dụng cả single lẫn multiple).
                return pickedSet.SetEquals(correctSet);
            });

            int total = drawn.Count;
            decimal score = total > 0 ? Math.Round((decimal)correct / total * 100m, 2) : 0m;
            bool isPassed = score >= job.OnlineTestPassScore;

            var submission = new OnlineTestSubmission
            {
                ApplicationId = app.Id,
                RoundNumber = round,
                SelectedAnswers = OnlineTestSupport.SerializeAnswers(answers),
                Score = score,
                IsPassed = isPassed,
                CorrectCount = correct,
                TotalQuestions = total,
            };
            await _unitOfWork.Repository<OnlineTestSubmission>().AddAsync(submission, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Đẩy realtime để chuông thông báo của ứng viên refetch → SyncNotificationsAsync tạo
            // notification "online_test:{submissionId}". Best-effort — lỗi SignalR không làm hỏng việc nộp bài.
            if (app.CandidateAccountId.HasValue)
            {
                try
                {
                    await _notificationService.PublishUserEventAsync(
                        app.CandidateAccountId.Value,
                        "ReceiveUserNotification",
                        new { Type = "OnlineTestGraded", applicationId = app.Id, roundNumber = round, isPassed },
                        ct);
                }
                catch { /* best-effort */ }
            }

            // Realtime cho STAFF: recruiter chủ tin + nhóm hr_admin nhận toast + refetch bảng điểm/chuông
            // (đồng bộ chuông staff qua SyncNotificationsAsync, dedupKey "onlinetest:{submissionId}").
            try
            {
                var staffPayload = new
                {
                    Type = "OnlineTestSubmitted",
                    applicationId = app.Id,
                    jobPostingId = app.JobPostingId,
                    candidateName = app.CandidateName,
                    roundNumber = round,
                    score,
                    isPassed,
                };
                await _notificationService.PublishUserEventAsync(job.CreatedByUserId, "ReceiveOnlineTestSubmitted", staffPayload, ct);
                await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveOnlineTestSubmitted", staffPayload, ct);
            }
            catch { /* best-effort */ }

            return Result.Success(new OnlineTestResultDto(
                score, isPassed, job.OnlineTestPassScore, correct, total, submission.CreatedAt));
        }
    }
}
