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
    internal static class OnlineTestMapping
    {
        public static OnlineTestQuestionDto ToDto(OnlineTestQuestion q) => new(
            q.Id,
            q.QuestionText,
            OnlineTestSupport.ParseOptions(q.Options),
            string.IsNullOrWhiteSpace(q.QuestionType) ? "single" : q.QuestionType,
            OnlineTestSupport.ParseInts(q.CorrectOptions));
    }

    // ============================================================
    // GET /api/online-test/jobs/{jobId} — ngân hàng câu hỏi + cấu hình (staff)
    // ============================================================

    public record GetOnlineTestBankQuery(Guid JobPostingId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestBankDto>>;

    public class GetOnlineTestBankQueryHandler : IRequestHandler<GetOnlineTestBankQuery, Result<OnlineTestBankDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOnlineTestBankQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestBankDto>> Handle(GetOnlineTestBankQuery request, CancellationToken ct)
        {
            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<OnlineTestBankDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestBankDto>("Bạn không có quyền quản lý câu hỏi của tin này.", CommonErrorCodes.Forbidden);

            var questions = (await _unitOfWork.Repository<OnlineTestQuestion>()
                    .FindAsync(q => q.JobPostingId == request.JobPostingId, ct))
                .OrderBy(q => q.CreatedAt)
                .Select(OnlineTestMapping.ToDto)
                .ToList();

            return Result.Success(new OnlineTestBankDto(
                job.Id, job.Title, job.OnlineTestPassScore, job.OnlineTestQuestionsPerTest, job.OnlineTestDurationMinutes, questions));
        }
    }

    // ============================================================
    // POST /api/online-test/jobs/{jobId}/questions — thêm câu hỏi
    // ============================================================

    public record CreateOnlineTestQuestionCommand(Guid JobPostingId, UpsertOnlineTestQuestionRequest Request, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestQuestionDto>>;

    public class CreateOnlineTestQuestionCommandHandler : IRequestHandler<CreateOnlineTestQuestionCommand, Result<OnlineTestQuestionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateOnlineTestQuestionCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestQuestionDto>> Handle(CreateOnlineTestQuestionCommand command, CancellationToken ct)
        {
            var validation = ValidateQuestion(command.Request);
            if (validation is not null) return Result.Failure<OnlineTestQuestionDto>(validation);

            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, command.JobPostingId, command.UserId, command.Role, ct);
            if (job == null) return Result.Failure<OnlineTestQuestionDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestQuestionDto>("Bạn không có quyền thêm câu hỏi cho tin này.", CommonErrorCodes.Forbidden);

            var correct = NormalizeCorrect(command.Request);
            var entity = new OnlineTestQuestion
            {
                JobPostingId = command.JobPostingId,
                QuestionText = command.Request.QuestionText.Trim(),
                Options = OnlineTestSupport.SerializeOptions(command.Request.Options),
                QuestionType = NormalizeType(command.Request.QuestionType),
                CorrectOptions = OnlineTestSupport.SerializeInts(correct),
                CorrectOption = correct.First(),
            };
            await _unitOfWork.Repository<OnlineTestQuestion>().AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(OnlineTestMapping.ToDto(entity));
        }

        internal static string NormalizeType(string? type) =>
            string.Equals(type, "multiple", StringComparison.OrdinalIgnoreCase) ? "multiple" : "single";

        /// <summary>Đáp án đúng đã lọc trùng + sắp xếp. Gọi sau khi ValidateQuestion đã pass.</summary>
        internal static List<int> NormalizeCorrect(UpsertOnlineTestQuestionRequest r) =>
            (r.CorrectOptions ?? new List<int>()).Distinct().OrderBy(v => v).ToList();

        internal static string? ValidateQuestion(UpsertOnlineTestQuestionRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.QuestionText))
                return "Nội dung câu hỏi không được để trống.";
            var options = (r.Options ?? new List<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
            if (options.Count < 2)
                return "Câu hỏi phải có ít nhất 2 phương án trả lời.";
            if (options.Count > 6)
                return "Câu hỏi tối đa 6 phương án trả lời.";

            var correct = (r.CorrectOptions ?? new List<int>()).Distinct().ToList();
            if (correct.Count == 0)
                return "Vui lòng chọn ít nhất 1 đáp án đúng.";
            if (correct.Any(c => c < 0 || c >= options.Count))
                return "Đáp án đúng không hợp lệ (nằm ngoài danh sách phương án).";

            var type = NormalizeType(r.QuestionType);
            if (type == "single" && correct.Count != 1)
                return "Câu hỏi 1 đáp án chỉ được chọn đúng 1 đáp án đúng.";
            return null;
        }
    }

    // ============================================================
    // PUT /api/online-test/questions/{id} — sửa câu hỏi
    // ============================================================

    public record UpdateOnlineTestQuestionCommand(Guid Id, UpsertOnlineTestQuestionRequest Request, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestQuestionDto>>;

    public class UpdateOnlineTestQuestionCommandHandler : IRequestHandler<UpdateOnlineTestQuestionCommand, Result<OnlineTestQuestionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateOnlineTestQuestionCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestQuestionDto>> Handle(UpdateOnlineTestQuestionCommand command, CancellationToken ct)
        {
            var validation = CreateOnlineTestQuestionCommandHandler.ValidateQuestion(command.Request);
            if (validation is not null) return Result.Failure<OnlineTestQuestionDto>(validation);

            var entity = await _unitOfWork.Repository<OnlineTestQuestion>().GetByIdAsync(command.Id, ct);
            if (entity == null) return Result.Failure<OnlineTestQuestionDto>("Không tìm thấy câu hỏi.", CommonErrorCodes.NotFound);

            var (ok, _) = await OnlineTestSupport.CanManageAsync(_unitOfWork, entity.JobPostingId, command.UserId, command.Role, ct);
            if (!ok) return Result.Failure<OnlineTestQuestionDto>("Bạn không có quyền sửa câu hỏi này.", CommonErrorCodes.Forbidden);

            var correct = CreateOnlineTestQuestionCommandHandler.NormalizeCorrect(command.Request);
            entity.QuestionText = command.Request.QuestionText.Trim();
            entity.Options = OnlineTestSupport.SerializeOptions(command.Request.Options);
            entity.QuestionType = CreateOnlineTestQuestionCommandHandler.NormalizeType(command.Request.QuestionType);
            entity.CorrectOptions = OnlineTestSupport.SerializeInts(correct);
            entity.CorrectOption = correct.First();
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<OnlineTestQuestion>().Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(OnlineTestMapping.ToDto(entity));
        }
    }

    // ============================================================
    // DELETE /api/online-test/questions/{id} — xoá câu hỏi
    // ============================================================

    public record DeleteOnlineTestQuestionCommand(Guid Id, Guid? UserId, string? Role) : IRequest<Result>;

    public class DeleteOnlineTestQuestionCommandHandler : IRequestHandler<DeleteOnlineTestQuestionCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteOnlineTestQuestionCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(DeleteOnlineTestQuestionCommand command, CancellationToken ct)
        {
            var entity = await _unitOfWork.Repository<OnlineTestQuestion>().GetByIdAsync(command.Id, ct);
            if (entity == null) return Result.Failure("Không tìm thấy câu hỏi.", CommonErrorCodes.NotFound);

            var (ok, _) = await OnlineTestSupport.CanManageAsync(_unitOfWork, entity.JobPostingId, command.UserId, command.Role, ct);
            if (!ok) return Result.Failure("Bạn không có quyền xoá câu hỏi này.", CommonErrorCodes.Forbidden);

            _unitOfWork.Repository<OnlineTestQuestion>().Delete(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ============================================================
    // PUT /api/online-test/jobs/{jobId}/settings — điểm sàn + số câu/bài + thời lượng
    // ============================================================

    public record UpdateOnlineTestSettingsCommand(
        Guid JobPostingId, int PassScore, int QuestionsPerTest, int DurationMinutes, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestBankDto>>;

    public class UpdateOnlineTestSettingsCommandHandler : IRequestHandler<UpdateOnlineTestSettingsCommand, Result<OnlineTestBankDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateOnlineTestSettingsCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestBankDto>> Handle(UpdateOnlineTestSettingsCommand command, CancellationToken ct)
        {
            if (command.PassScore < 0 || command.PassScore > 100)
                return Result.Failure<OnlineTestBankDto>("Điểm sàn phải nằm trong khoảng 0–100.");
            if (command.QuestionsPerTest < 1 || command.QuestionsPerTest > 200)
                return Result.Failure<OnlineTestBankDto>("Số câu mỗi bài phải từ 1 đến 200.");
            if (command.DurationMinutes < 1 || command.DurationMinutes > 300)
                return Result.Failure<OnlineTestBankDto>("Thời lượng phải từ 1 đến 300 phút.");

            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, command.JobPostingId, command.UserId, command.Role, ct);
            if (job == null) return Result.Failure<OnlineTestBankDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestBankDto>("Bạn không có quyền cấu hình bài thi của tin này.", CommonErrorCodes.Forbidden);

            job.OnlineTestPassScore = command.PassScore;
            job.OnlineTestQuestionsPerTest = command.QuestionsPerTest;
            job.OnlineTestDurationMinutes = command.DurationMinutes;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            var questions = (await _unitOfWork.Repository<OnlineTestQuestion>()
                    .FindAsync(q => q.JobPostingId == job.Id, ct))
                .OrderBy(q => q.CreatedAt)
                .Select(OnlineTestMapping.ToDto)
                .ToList();

            return Result.Success(new OnlineTestBankDto(
                job.Id, job.Title, job.OnlineTestPassScore, job.OnlineTestQuestionsPerTest, job.OnlineTestDurationMinutes, questions));
        }
    }

    // ============================================================
    // GET /api/online-test/applications/{applicationId}/result — kết quả 1 ứng viên (staff)
    // ============================================================

    public record GetOnlineTestResultForStaffQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestResultDto?>>;

    public class GetOnlineTestResultForStaffQueryHandler : IRequestHandler<GetOnlineTestResultForStaffQuery, Result<OnlineTestResultDto?>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOnlineTestResultForStaffQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestResultDto?>> Handle(GetOnlineTestResultForStaffQuery request, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(request.ApplicationId, ct);
            if (app == null) return Result.Failure<OnlineTestResultDto?>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, app.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<OnlineTestResultDto?>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestResultDto?>("Bạn không có quyền xem kết quả của hồ sơ này.", CommonErrorCodes.Forbidden);

            var submission = (await _unitOfWork.Repository<OnlineTestSubmission>()
                    .FindAsync(s => s.ApplicationId == request.ApplicationId, ct))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            if (submission == null) return Result.Success<OnlineTestResultDto?>(null);

            return Result.Success<OnlineTestResultDto?>(new OnlineTestResultDto(
                submission.Score, submission.IsPassed, job.OnlineTestPassScore,
                submission.CorrectCount, submission.TotalQuestions, submission.CreatedAt));
        }
    }

    // ============================================================
    // GET /api/online-test/jobs/{jobId}/results — bảng tổng hợp điểm theo job (staff)
    // ============================================================

    public record GetOnlineTestResultsByJobQuery(Guid JobPostingId, Guid? UserId, string? Role)
        : IRequest<Result<OnlineTestJobResultsDto>>;

    public class GetOnlineTestResultsByJobQueryHandler : IRequestHandler<GetOnlineTestResultsByJobQuery, Result<OnlineTestJobResultsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOnlineTestResultsByJobQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OnlineTestJobResultsDto>> Handle(GetOnlineTestResultsByJobQuery request, CancellationToken ct)
        {
            var (ok, job) = await OnlineTestSupport.CanManageAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<OnlineTestJobResultsDto>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<OnlineTestJobResultsDto>("Bạn không có quyền xem điểm của tin này.", CommonErrorCodes.Forbidden);

            var dto = await OnlineTestResultsBuilder.BuildAsync(_unitOfWork, job, ct);
            return Result.Success(dto);
        }
    }

    /// <summary>Dựng bảng tổng hợp điểm — dùng chung cho endpoint JSON và export Excel.</summary>
    internal static class OnlineTestResultsBuilder
    {
        public static async Task<OnlineTestJobResultsDto> BuildAsync(IUnitOfWork uow, JobPosting job, CancellationToken ct)
        {
            var bankCount = await uow.Repository<OnlineTestQuestion>().CountAsync(q => q.JobPostingId == job.Id, ct);

            var apps = await uow.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q.Where(a => a.JobPostingId == job.Id)
                    .Select(a => new { a.Id, a.CandidateName, a.CandidateEmail }), ct);
            var appMap = apps.ToDictionary(a => a.Id);
            var appIds = apps.Select(a => a.Id).ToList();

            var submissions = appIds.Count == 0
                ? new List<OnlineTestSubmission>()
                : (await uow.Repository<OnlineTestSubmission>()
                    .FindAsync(s => appIds.Contains(s.ApplicationId), ct)).ToList();

            var rows = submissions
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.CreatedAt)
                .Select(s =>
                {
                    appMap.TryGetValue(s.ApplicationId, out var a);
                    return new OnlineTestScoreRowDto(
                        s.ApplicationId,
                        a?.CandidateName ?? "Ẩn danh",
                        a?.CandidateEmail ?? "",
                        s.RoundNumber,
                        s.Score,
                        s.IsPassed,
                        s.CorrectCount,
                        s.TotalQuestions,
                        s.CreatedAt,
                        s.TabSwitchCount);
                })
                .ToList();

            var passed = rows.Count(r => r.IsPassed);
            var avg = rows.Count > 0 ? Math.Round(rows.Average(r => r.Score), 1) : 0m;
            var highest = rows.Count > 0 ? rows.Max(r => r.Score) : 0m;
            var lowest = rows.Count > 0 ? rows.Min(r => r.Score) : 0m;

            return new OnlineTestJobResultsDto(
                job.Id, job.Title, job.OnlineTestPassScore, bankCount,
                rows.Count, passed, rows.Count - passed, avg, highest, lowest, rows);
        }
    }
}
