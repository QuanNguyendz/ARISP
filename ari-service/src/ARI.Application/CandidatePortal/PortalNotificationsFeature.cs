using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.StaffNotifications;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ARI.Application.CandidatePortal
{
    // ============================================================
    // GET /api/portal/notifications — sync sự kiện thực rồi trả danh sách + số chưa đọc
    // ============================================================

    public record GetPortalNotificationsQuery(Guid CandidateId) : IRequest<Result<StaffNotificationListDto>>;

    public class GetPortalNotificationsQueryHandler : IRequestHandler<GetPortalNotificationsQuery, Result<StaffNotificationListDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetPortalNotificationsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffNotificationListDto>> Handle(GetPortalNotificationsQuery request, CancellationToken ct)
        {
            await SyncNotificationsAsync(request.CandidateId, ct);

            var items = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == request.CandidateId, ct))
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new StaffNotificationItemDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt))
                .ToList();

            return Result.Success(new StaffNotificationListDto(items, items.Count(i => !i.IsRead)));
        }

        /// <summary>
        /// Sinh thông báo từ các sự kiện thực tế của ứng viên (lời mời PV, kết quả đã chia sẻ,
        /// lịch sắp tới, nộp hồ sơ). Idempotent theo DedupKey — không tạo trùng, giữ trạng thái đã đọc.
        /// </summary>
        private async Task SyncNotificationsAsync(Guid candidateId, CancellationToken ct)
        {
            var apps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q.Where(a => a.CandidateAccountId == candidateId)
                    .Select(a => new { a.Id, a.JobPostingId, a.CreatedAt }), ct);
            if (apps.Count == 0) return;

            var appIds = apps.Select(a => a.Id).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                .ToDictionary(j => j.Id, j => j.Title);
            string JobTitle(Guid jid) => jobs.TryGetValue(jid, out var t) ? t : "Vị trí tuyển dụng";

            var nowUtc = DateTimeOffset.UtcNow;
            // Tính cả bản đã soft-delete (IgnoreQueryFilters) để thông báo người dùng đã xóa
            // KHÔNG bị sync tạo lại ở lần mở sau.
            var existing = (await _unitOfWork.Repository<Notification>()
                .QueryAsync(q => q.IgnoreQueryFilters()
                    .Where(n => n.CandidateAccountId == candidateId)
                    .Select(n => n.DedupKey), ct))
                .ToHashSet();
            var toAdd = new List<Notification>();

            void Add(string key, string type, string title, string? body, string? link, DateTimeOffset createdAt)
            {
                if (existing.Contains(key)) return;
                existing.Add(key);
                toAdd.Add(new Notification
                {
                    CandidateAccountId = candidateId,
                    DedupKey = key,
                    Type = type,
                    Title = title,
                    Body = body,
                    Link = link,
                    CreatedAt = createdAt,
                    UpdatedAt = nowUtc
                });
            }

            // 1. Đã nộp hồ sơ
            foreach (var a in apps)
                Add($"applied:{a.Id}", "applied", "Đã nộp hồ sơ ứng tuyển",
                    JobTitle(a.JobPostingId), $"/candidate/applications/{a.Id}", a.CreatedAt);

            // 2. Lời mời phỏng vấn (mã On-site còn hiệu lực)
            var codes = (await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => appIds.Contains(c.ApplicationId) && c.UsedAt == null && c.ExpiresAt > nowUtc, ct)).ToList();
            foreach (var c in codes)
            {
                var app = apps.FirstOrDefault(a => a.Id == c.ApplicationId);
                Add($"invite:{c.Id}", "invite", $"Lời mời phỏng vấn vòng {c.RoundNumber}",
                    $"{(app != null ? JobTitle(app.JobPostingId) : "")} · On-site — mã phỏng vấn đã sẵn sàng tại Hồ sơ ứng tuyển.",
                    app != null ? $"/candidate/applications/{app.Id}" : "/candidate/applications", c.CreatedAt);
            }

            // 3. Kết quả vòng đã được HR chia sẻ
            var sessionIds = await _unitOfWork.Repository<InterviewSession>()
                .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId)).Select(s => s.Id), ct);
            // Evaluation projection nhẹ (bỏ cột JSON lớn)
            var evals = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Where(e => sessionIds.Contains(e.SessionId))
                    .Select(e => new { e.Id, e.AiVerdict, e.OverallScore, e.RoundNumber, e.ApplicationId }), ct);
            var evalIds = evals.Select(e => e.Id).ToList();
            var reviews = evalIds.Any()
                ? (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct)).ToList()
                : new List<HrReview>();
            var reviewByEval = reviews.ToDictionary(r => r.EvaluationId, r => r);
            foreach (var ev in evals)
            {
                if (!reviewByEval.TryGetValue(ev.Id, out var rv) || !rv.ShareEvaluation) continue;
                var verdict = string.IsNullOrEmpty(rv.FinalVerdict) ? ev.AiVerdict : rv.FinalVerdict;
                var pass = verdict == "pass";
                var scoreText = ev.OverallScore.HasValue ? $" với điểm {Math.Round(ev.OverallScore.Value)}/100" : "";
                Add($"result:{ev.Id}", "result",
                    $"Kết quả vòng {ev.RoundNumber}: {(pass ? "Pass" : "Not Pass")}",
                    $"{JobTitle(apps.FirstOrDefault(a => a.Id == ev.ApplicationId)?.JobPostingId ?? Guid.Empty)}{scoreText}.",
                    $"/candidate/applications/{ev.ApplicationId}", rv.UpdatedAt);
            }

            // 4. Lịch phỏng vấn sắp tới
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => appIds.Contains(b.ApplicationId) && b.Status == "scheduled", ct)).ToList();
            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Any()
                ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s)
                : new Dictionary<Guid, AvailabilitySlot>();
            foreach (var b in bookings)
            {
                if (!slots.TryGetValue(b.AvailabilitySlotId, out var slot) || slot.StartTime <= nowUtc) continue;
                var app = apps.FirstOrDefault(a => a.Id == b.ApplicationId);
                Add($"schedule:{b.Id}", "schedule", $"Lịch phỏng vấn vòng {b.RoundNumber} sắp tới",
                    $"{(app != null ? JobTitle(app.JobPostingId) : "")} · {slot.StartTime:dd/MM HH:mm}",
                    $"/portal/schedule/{b.ApplicationId}", b.CreatedAt);
            }

            // 5. Kết quả bài thi trắc nghiệm online (Online Test)
            var submissions = (await _unitOfWork.Repository<OnlineTestSubmission>()
                .FindAsync(s => appIds.Contains(s.ApplicationId), ct)).ToList();
            foreach (var s in submissions)
            {
                var app = apps.FirstOrDefault(a => a.Id == s.ApplicationId);
                // CỐ Ý không nói đạt/trượt và không kèm điểm: kết quả chỉ công bố khi cả vòng
                // đã chốt. Chuông này chỉ xác nhận bài đã vào hệ thống.
                Add($"online_test:{s.Id}", "result",
                    "Đã nhận bài trắc nghiệm của bạn",
                    $"{(app != null ? JobTitle(app.JobPostingId) : "")} · Bộ phận tuyển dụng sẽ thông báo kết quả sau.",
                    $"/candidate/applications/{s.ApplicationId}", s.CreatedAt);
            }

            if (toAdd.Count > 0)
            {
                foreach (var n in toAdd) await _unitOfWork.Repository<Notification>().AddAsync(n, ct);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }

    // ============================================================
    // POST read-all / POST {id}/read / DELETE {id} / DELETE all
    // ============================================================

    public record MarkAllPortalNotificationsReadCommand(Guid CandidateId) : IRequest<Result<int>>;

    public class MarkAllPortalNotificationsReadCommandHandler : IRequestHandler<MarkAllPortalNotificationsReadCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkAllPortalNotificationsReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(MarkAllPortalNotificationsReadCommand request, CancellationToken ct)
        {
            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == request.CandidateId && !n.IsRead, ct)).ToList();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
            }
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Result.Success(list.Count);
        }
    }

    public record MarkPortalNotificationReadCommand(Guid CandidateId, Guid NotificationId) : IRequest<Result>;

    public class MarkPortalNotificationReadCommandHandler : IRequestHandler<MarkPortalNotificationReadCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkPortalNotificationReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(MarkPortalNotificationReadCommand request, CancellationToken ct)
        {
            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(request.NotificationId, ct);
            if (n == null || n.CandidateAccountId != request.CandidateId)
                return Result.Failure("Không tìm thấy thông báo.", CommonErrorCodes.NotFound);
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
                await _unitOfWork.SaveChangesAsync();
            }
            return Result.Success();
        }
    }

    public record DeletePortalNotificationCommand(Guid CandidateId, Guid NotificationId) : IRequest<Result>;

    public class DeletePortalNotificationCommandHandler : IRequestHandler<DeletePortalNotificationCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeletePortalNotificationCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeletePortalNotificationCommand request, CancellationToken ct)
        {
            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(request.NotificationId, ct);
            if (n == null || n.CandidateAccountId != request.CandidateId)
                return Result.Failure("Không tìm thấy thông báo.", CommonErrorCodes.NotFound);

            _unitOfWork.Repository<Notification>().Delete(n);
            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }
    }

    public record DeleteAllPortalNotificationsCommand(Guid CandidateId) : IRequest<Result<int>>;

    public class DeleteAllPortalNotificationsCommandHandler : IRequestHandler<DeleteAllPortalNotificationsCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAllPortalNotificationsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(DeleteAllPortalNotificationsCommand request, CancellationToken ct)
        {
            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.CandidateAccountId == request.CandidateId, ct)).ToList();
            foreach (var n in list)
                _unitOfWork.Repository<Notification>().Delete(n);
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Result.Success(list.Count);
        }
    }
}
