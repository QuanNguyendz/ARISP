using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.OnlineTest
{
    // ============================================================
    // GET /api/online-test/applications/{applicationId}/answers — bài làm chi tiết (staff)
    // ============================================================

    /// <summary>
    /// Bài làm chi tiết của một ứng viên: từng câu, đáp án họ khoanh, đáp án đúng, đúng/sai.
    ///
    /// <b>Vì sao cần.</b> Điểm tổng chỉ nói "30/100" — nó không trả lời được câu mà người sàng lọc
    /// thật sự hỏi: <i>sai ở đâu</i>. Một ứng viên trượt vì sai hết phần thuật toán khác hẳn một
    /// người trượt rải đều, và khác hẳn người bỏ trắng nửa bài. Không có màn này thì quyết định giữ
    /// hay loại chỉ dựa vào một con số.
    ///
    /// <b>Bộ đề dựng LẠI, không lưu.</b> Đề bốc DETERMINISTIC theo (câu, hồ sơ, vòng) nên gọi lại
    /// <see cref="OnlineTestSupport.DrawQuestions"/> là ra đúng bộ ứng viên đã làm — kể cả câu họ bỏ
    /// trắng (không có khoá trong bài làm đã lưu). Lấy khoá của bài làm làm bộ đề thì câu bỏ trắng sẽ
    /// biến mất khỏi màn xem, và "làm sai" với "không làm" trông giống hệt nhau.
    ///
    /// <b>Chỉ NHÂN SỰ.</b> Đáp án đúng không bao giờ rời server về phía ứng viên (ADR-049); cổng ở
    /// đây là <see cref="OnlineTestSupport.CanManageAsync"/> — chủ tin, quản trị viên, và Hiring
    /// Manager của tin (người ra đề).
    /// </summary>
    public record GetOnlineTestAnswerSheetQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestAnswerSheetDto?>>;

    public class GetOnlineTestAnswerSheetQueryHandler
        : IRequestHandler<GetOnlineTestAnswerSheetQuery, Result<OnlineTestAnswerSheetDto?>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOnlineTestAnswerSheetQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestAnswerSheetDto?>> Handle(
            GetOnlineTestAnswerSheetQuery request, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(request.ApplicationId, ct);
            if (app == null)
                return Result.Failure<OnlineTestAnswerSheetDto?>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            var (ok, job) = await OnlineTestSupport.CanManageAsync(
                _unitOfWork, app.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<OnlineTestAnswerSheetDto?>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok)
                return Result.Failure<OnlineTestAnswerSheetDto?>("Bạn không có quyền xem bài làm của hồ sơ này.", CommonErrorCodes.Forbidden);

            var submission = (await _unitOfWork.Repository<OnlineTestSubmission>()
                    .FindAsync(s => s.ApplicationId == request.ApplicationId, ct))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            // Chưa nộp bài thì KHÔNG phải lỗi — màn gọi tới đây để hỏi "có bài không".
            if (submission == null) return Result.Success<OnlineTestAnswerSheetDto?>(null);

            var bank = (await _unitOfWork.Repository<OnlineTestQuestion>()
                .FindAsync(q => q.JobPostingId == app.JobPostingId, ct)).ToList();

            var drawn = OnlineTestSupport.DrawQuestions(
                bank, app.Id, submission.RoundNumber, job.OnlineTestQuestionsPerTest);

            var answers = OnlineTestSupport.ParseAnswers(submission.SelectedAnswers);

            var items = drawn.Select(q =>
            {
                var correct = OnlineTestSupport.ParseInts(q.CorrectOptions);
                answers.TryGetValue(q.Id, out var picked);
                var selected = (picked ?? new List<int>()).Distinct().OrderBy(v => v).ToList();

                // Cùng vị từ chấm điểm với lúc nộp: đúng khi tập chọn KHỚP HOÀN TOÀN tập đáp án
                // đúng. Viết lại theo cách khác ở đây thì màn xem bài sẽ mâu thuẫn với chính điểm số.
                var isCorrect = correct.Count > 0 && selected.ToHashSet().SetEquals(correct.ToHashSet());

                return new OnlineTestAnswerReviewItemDto(
                    q.Id,
                    q.QuestionText,
                    OnlineTestSupport.ParseOptions(q.Options),
                    q.QuestionType,
                    selected,
                    correct.Distinct().OrderBy(v => v).ToList(),
                    isCorrect);
            }).ToList();

            return Result.Success<OnlineTestAnswerSheetDto?>(new OnlineTestAnswerSheetDto(
                app.Id,
                app.CandidateName,
                submission.RoundNumber,
                submission.Score,
                submission.IsPassed,
                job.OnlineTestPassScore,
                submission.CorrectCount,
                submission.TotalQuestions,
                submission.CreatedAt,
                submission.TabSwitchCount,
                items,
                OnlineTestSubmittedBy.IsSystem(submission.SubmittedBy)));
        }
    }
}
