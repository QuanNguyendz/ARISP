using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ARI.Application.StaffNotifications.Queries.GetStaffNotifications
{
    /// <summary>
    /// Sync thông báo từ sự kiện thực (idempotent theo DedupKey) rồi trả danh sách + số chưa đọc.
    /// Phạm vi theo vai trò: Recruiter chỉ thấy tin mình tạo; HR Admin thấy toàn bộ tin.
    /// </summary>
    public record GetStaffNotificationsQuery(Guid UserId, bool IsRecruiter, bool IsHrAdmin)
        : IRequest<Result<StaffNotificationListDto>>;

    public class GetStaffNotificationsQueryHandler
        : IRequestHandler<GetStaffNotificationsQuery, Result<StaffNotificationListDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetStaffNotificationsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<StaffNotificationListDto>> Handle(GetStaffNotificationsQuery request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, ct);
            var settings = user != null && !string.IsNullOrEmpty(user.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.StaffSettingsDto>(user.SettingsJson) ?? new ARI.Application.DTOs.StaffSettingsDto()
                : new ARI.Application.DTOs.StaffSettingsDto();

            if (settings.ReceivePush)
            {
                try
                {
                    await SyncNotificationsAsync(request.UserId, request.IsRecruiter, request.IsHrAdmin, ct);
                }
                catch { /* sync idempotent — lỗi thoáng qua không được crash toàn endpoint */ }
            }

            var items = (await _unitOfWork.Repository<Notification>()
                    .FindAsync(n => n.RecipientUserId == request.UserId, ct))
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new StaffNotificationItemDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt))
                .ToList();

            return Result.Success(new StaffNotificationListDto(items, items.Count(i => !i.IsRead)));
        }

        /// <summary>Sinh thông báo từ sự kiện thực của nhân sự, idempotent theo DedupKey (giữ trạng thái đã đọc).</summary>
        private async Task SyncNotificationsAsync(Guid userId, bool isRecruiter, bool isHrAdmin, CancellationToken ct)
        {
            // Super Admin không gắn với phễu tuyển dụng theo tin → không sinh thông báo recruitment.
            if (!isRecruiter && !isHrAdmin) return;

            // Tin trong phạm vi của người dùng (projection nhẹ).
            var jobs = await _unitOfWork.Repository<JobPosting>()
                .QueryAsync(q => (isHrAdmin
                        ? q
                        : q.Where(j => j.CreatedByUserId == userId))
                    .Select(j => new { j.Id, j.Title, j.Status }), ct);
            if (jobs.Count == 0) return;

            var jobIds = jobs.Select(j => j.Id).ToList();
            var jobTitle = jobs.ToDictionary(j => j.Id, j => j.Title);
            string Title(Guid jid) => jobTitle.TryGetValue(jid, out var t) ? t : "Vị trí tuyển dụng";

            var linkBase = isRecruiter && !isHrAdmin ? "/recruiter" : "/hr";
            // Trang bảng điểm trắc nghiệm khác route giữa recruiter (/my-jobs) và HR (/jobs).
            string OnlineTestResultsLink(Guid jid) => isRecruiter && !isHrAdmin
                ? $"/recruiter/my-jobs/{jid}/online-test/results"
                : $"/hr/jobs/{jid}/online-test/results";
            var nowUtc = DateTimeOffset.UtcNow;
            var since = nowUtc.AddDays(-30);

            // Tính cả bản đã soft-delete (IgnoreQueryFilters) để thông báo người dùng đã xóa
            // KHÔNG bị sync tạo lại ở lần mở sau.
            var existing = (await _unitOfWork.Repository<Notification>()
                    .QueryAsync(q => q.IgnoreQueryFilters()
                        .Where(n => n.RecipientUserId == userId)
                        .Select(n => n.DedupKey), ct))
                .ToHashSet();
            var toAdd = new List<Notification>();

            void Add(string key, string type, string title, string? body, string? link, DateTimeOffset createdAt)
            {
                if (existing.Contains(key)) return;
                existing.Add(key);
                toAdd.Add(new Notification
                {
                    RecipientUserId = userId,
                    DedupKey = key,
                    Type = type,
                    Title = title,
                    Body = body,
                    Link = link,
                    CreatedAt = createdAt,
                    UpdatedAt = nowUtc,
                });
            }

            // 1. Ứng viên mới ứng tuyển (30 ngày gần nhất) vào tin trong phạm vi.
            var apps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => jobIds.Contains(a.JobPostingId) && a.CreatedAt >= since)
                    .Select(a => new { a.Id, a.JobPostingId, a.CandidateName, a.CreatedAt }), ct);
            foreach (var a in apps)
                Add($"applied:{a.Id}", "applied", "Ứng viên mới ứng tuyển",
                    $"{a.CandidateName} · {Title(a.JobPostingId)}", $"{linkBase}/candidates/{a.Id}", a.CreatedAt);

            // 2. Đánh giá AI chờ HR xác nhận (chưa có HrReview) cho tin trong phạm vi (30 ngày gần nhất).
            var appIds = apps.Select(a => a.Id).ToList();
            if (appIds.Count > 0)
            {
                var evals = await _unitOfWork.Repository<Evaluation>()
                    .QueryAsync(q => q.Where(e => appIds.Contains(e.ApplicationId) && e.CreatedAt >= since)
                        .Select(e => new { e.Id, e.ApplicationId, e.RoundNumber, e.CreatedAt }), ct);
                var evalIds = evals.Select(e => e.Id).ToList();
                var reviewed = evalIds.Count > 0
                    ? (await _unitOfWork.Repository<HrReview>()
                        .QueryAsync(q => q.Where(r => evalIds.Contains(r.EvaluationId)).Select(r => r.EvaluationId), ct)).ToHashSet()
                    : new HashSet<Guid>();
                var appToJob = apps.ToDictionary(a => a.Id, a => a.JobPostingId);
                foreach (var ev in evals.Where(e => !reviewed.Contains(e.Id)))
                {
                    var jid = appToJob.TryGetValue(ev.ApplicationId, out var j) ? j : Guid.Empty;
                    Add($"review:{ev.Id}", "pending", $"Đánh giá vòng {ev.RoundNumber} chờ xác nhận",
                        jid != Guid.Empty ? Title(jid) : null, $"{linkBase}/evaluations", ev.CreatedAt);
                }
            }

            // 4. Ứng viên hoàn thành bài thi trắc nghiệm (30 ngày gần nhất) cho tin trong phạm vi.
            if (appIds.Count > 0)
            {
                var submissions = await _unitOfWork.Repository<OnlineTestSubmission>()
                    .QueryAsync(q => q
                        .Where(s => appIds.Contains(s.ApplicationId) && s.CreatedAt >= since)
                        .Select(s => new { s.Id, s.ApplicationId, s.Score, s.IsPassed, s.RoundNumber, s.CreatedAt }), ct);
                if (submissions.Count > 0)
                {
                    var subAppIds = submissions.Select(s => s.ApplicationId).Distinct().ToList();
                    var subApps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                            .QueryAsync(q => q.Where(a => subAppIds.Contains(a.Id))
                                .Select(a => new { a.Id, a.JobPostingId, a.CandidateName }), ct))
                        .ToDictionary(a => a.Id);
                    foreach (var s in submissions)
                    {
                        subApps.TryGetValue(s.ApplicationId, out var a);
                        var jid = a?.JobPostingId ?? Guid.Empty;
                        var name = a?.CandidateName ?? "Ứng viên";
                        var verdict = s.IsPassed ? "Đạt" : "Chưa đạt";
                        Add($"onlinetest:{s.Id}", "online_test",
                            $"Ứng viên hoàn thành bài thi trắc nghiệm ({verdict})",
                            $"{name} · {Title(jid)} · {(int)Math.Round(s.Score)}/100",
                            jid != Guid.Empty ? OnlineTestResultsLink(jid) : $"{linkBase}/candidates/{s.ApplicationId}",
                            s.CreatedAt);
                    }
                }
            }

            // 5. Ứng viên phản hồi lịch phỏng vấn (xác nhận / báo bận) — 30 ngày gần nhất.
            if (appIds.Count > 0)
            {
                var responses = await _unitOfWork.Repository<InterviewBooking>()
                    .QueryAsync(q => q
                        .Where(b => appIds.Contains(b.ApplicationId)
                            && b.RespondedAt >= since
                            && (b.ConfirmationStatus == "confirmed" || b.ConfirmationStatus == "declined"))
                        .Select(b => new { b.Id, b.ApplicationId, b.RoundNumber, b.ConfirmationStatus, b.DeclineReason, b.RespondedAt }), ct);
                if (responses.Count > 0)
                {
                    var respAppIds = responses.Select(r => r.ApplicationId).Distinct().ToList();
                    var respApps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                            .QueryAsync(q => q.Where(a => respAppIds.Contains(a.Id))
                                .Select(a => new { a.Id, a.JobPostingId, a.CandidateName }), ct))
                        .ToDictionary(a => a.Id);
                    foreach (var r in responses)
                    {
                        respApps.TryGetValue(r.ApplicationId, out var a);
                        var jid = a?.JobPostingId ?? Guid.Empty;
                        var name = a?.CandidateName ?? "Ứng viên";
                        var declined = string.Equals(r.ConfirmationStatus, "declined", StringComparison.OrdinalIgnoreCase);
                        Add($"schedule_response:{r.Id}", "schedule",
                            declined ? "Ứng viên báo bận lịch phỏng vấn" : "Ứng viên xác nhận lịch phỏng vấn",
                            declined
                                ? $"{name} · {Title(jid)} · vòng {r.RoundNumber}: {r.DeclineReason}"
                                : $"{name} · {Title(jid)} · vòng {r.RoundNumber}",
                            $"{linkBase}/candidates/{r.ApplicationId}", r.RespondedAt ?? nowUtc);
                    }
                }
            }

            // 3. Tin chờ duyệt — chỉ HR Admin. (Đã tạo thực tế qua UpdateJobStatusCommand nên bỏ qua tạo ảo để tránh lặp)
            // if (isHrAdmin)
            // {
            //     foreach (var j in jobs.Where(j => j.Status == "pending"))
            //         Add($"jobapproval:{j.Id}", "approval", "Tin tuyển dụng chờ duyệt",
            //             Title(j.Id), "/hr/jobs/pending", nowUtc);
            // }

            if (toAdd.Count > 0)
            {
                foreach (var n in toAdd) await _unitOfWork.Repository<Notification>().AddAsync(n, ct);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}
