using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Theo đuôi lịch phỏng vấn đã gán (ADR-059). Hai việc, cùng một nhịp quét:
    ///
    /// 1. <b>Nhắc ứng viên chưa phản hồi</b> tại các mốc <c>Scheduling:ReminderHoursBeforeSlot</c>
    ///    (mặc định 24h rồi 3h trước giờ hẹn) — chuông trong Portal + email <b>trả lời vào chính
    ///    luồng thư mời</b> của vòng đó, không đẻ thư mới.
    /// 2. <b>Tự đánh trượt người không tham dự</b>: qua giờ hẹn (cộng thêm khoảng ân hạn) mà không có
    ///    phiên phỏng vấn THẬT nào của vòng → hồ sơ <c>not_pass</c>, lịch đóng lại.
    ///
    /// Thay cho cơ chế cũ "quá hạn xác nhận 48h thì tự huỷ lịch rồi trả chỗ cho nhân sự xếp lại":
    /// cách đó đối xử với người <i>phớt lờ</i> thư mời y hệt người <i>chủ động báo bận</i>, trong khi
    /// nghiệp vụ muốn phân biệt rõ — xếp lại lịch là đặc quyền của người có phản hồi.
    /// </summary>
    public class InterviewScheduleFollowUpHostedService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulingOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InterviewScheduleFollowUpHostedService> _logger;

        public InterviewScheduleFollowUpHostedService(
            IServiceScopeFactory scopeFactory,
            SchedulingOptions options,
            IConfiguration configuration,
            ILogger<InterviewScheduleFollowUpHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                // Hai việc tách nhau: nhắc lỗi thì vẫn phải đánh trượt được và ngược lại.
                try { await RemindPendingAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Nhắc lịch phỏng vấn thất bại."); }

                try { await FailNoShowsAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Tự đánh trượt ứng viên không tham dự thất bại."); }

                try { await Task.Delay(ScanInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        // ────────────────────────────── 1. Nhắc lịch ──────────────────────────────

        private async Task RemindPendingAsync(CancellationToken ct)
        {
            var marks = _options.ReminderMarks();
            if (marks.Length == 0) return;

            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeOffset.UtcNow;

            var pending = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.Status == BookingStatus.Scheduled
                     && b.ConfirmationStatus == BookingConfirmationStatus.Pending, ct)).ToList();
            if (pending.Count == 0) return;

            var slotIds = pending.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slotById = (await unitOfWork.Repository<AvailabilitySlot>()
                    .FindAsync(s => slotIds.Contains(s.Id), ct))
                .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

            foreach (var booking in pending)
            {
                if (!slotById.TryGetValue(booking.AvailabilitySlotId, out var slot)) continue;
                if (slot.StartTime <= now) continue; // đã tới giờ — việc của nhánh đánh trượt

                var hoursLeft = (slot.StartTime - now).TotalHours;
                // Mốc lớn nhất đã "chạm" (còn ít hơn mốc đó) và chưa từng gửi.
                var mark = marks.FirstOrDefault(m => hoursLeft <= m
                                                     && (booking.LastAutoReminderHours == null
                                                         || m < booking.LastAutoReminderHours.Value));
                if (mark == 0) continue;

                var app = await unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .GetByIdAsync(booking.ApplicationId, ct);
                if (app == null) continue;

                var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

                // Chuông trong Portal — dedup theo mốc để quét lại không đẻ thêm dòng.
                if (app.CandidateAccountId.HasValue)
                {
                    var dedupKey = $"schedule_reminder:{booking.Id}:{mark}";
                    var already = await unitOfWork.Repository<Notification>().FindAsync(
                        n => n.CandidateAccountId == app.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                    if (!already.Any())
                    {
                        var local = slot.StartTime.ToOffset(TimeSpan.FromHours(7));
                        await unitOfWork.Repository<Notification>().AddAsync(new Notification
                        {
                            CandidateAccountId = app.CandidateAccountId.Value,
                            DedupKey = dedupKey,
                            Type = "schedule",
                            Title = "Bạn chưa phản hồi lịch phỏng vấn",
                            Body = $"Buổi phỏng vấn vòng {booking.RoundNumber} lúc {local:HH:mm dd/MM/yyyy} sắp diễn ra. "
                                   + "Vui lòng xác nhận tham dự, hoặc báo bận kèm lý do để được xếp lịch khác. "
                                   + "Không phản hồi và không tham dự thì hồ sơ sẽ dừng lại ở vòng này.",
                            Link = $"/portal/schedule/{app.Id}",
                            IsRead = false,
                        }, ct);
                    }

                    try
                    {
                        await notif.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification",
                            new { Type = "ScheduleReminder", ApplicationId = app.Id, BookingId = booking.Id }, ct);
                    }
                    catch { /* realtime hỏng không được chặn việc nhắc */ }
                }

                // Email nhắc — trả lời vào đúng thư mời đã gửi (nếu có Message-Id).
                try
                {
                    var mail = await InterviewInviteEmail.BuildReminderAsync(
                        unitOfWork, _configuration, app, job, booking.RoundNumber, booking.Id, slot.StartTime, ct);
                    await notif.SendThreadedEmailAsync(
                        app.CandidateEmail, mail.Subject, mail.Html, booking.InviteEmailMessageId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không gửi được email nhắc lịch cho booking {BookingId}.", booking.Id);
                }

                booking.LastAutoReminderHours = mark;
                booking.UpdatedAt = now;
                unitOfWork.Repository<InterviewBooking>().Update(booking);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }

        // ────────────────────── 2. Không tham dự = trượt ──────────────────────

        private async Task FailNoShowsAsync(CancellationToken ct)
        {
            if (!_options.AutoFailNoShow) return;

            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeOffset.UtcNow;
            var grace = TimeSpan.FromHours(Math.Max(0, _options.NoShowGraceHours));

            var live = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.Status == BookingStatus.Scheduled, ct)).ToList();
            if (live.Count == 0) return;

            var slotIds = live.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slotById = (await unitOfWork.Repository<AvailabilitySlot>()
                    .FindAsync(s => slotIds.Contains(s.Id), ct))
                .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

            var overdue = live
                .Where(b => slotById.TryGetValue(b.AvailabilitySlotId, out var s) && s.EndTime.Add(grace) <= now)
                .ToList();
            if (overdue.Count == 0) return;

            var appIds = overdue.Select(b => b.ApplicationId).Distinct().ToList();
            var sessions = (await unitOfWork.Repository<InterviewSession>().FindAsync(
                s => appIds.Contains(s.ApplicationId) && s.SessionType == "real", ct)).ToList();

            var noShows = overdue
                .Where(b => !sessions.Any(s => s.ApplicationId == b.ApplicationId && s.RoundNumber == b.RoundNumber))
                .ToList();
            if (noShows.Count == 0) return;

            var closedApps = new List<(Guid AppId, Guid? AccountId, string Email, string Name, int Round)>();

            foreach (var booking in noShows)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.ConfirmationStatus = BookingConfirmationStatus.Declined;
                booking.DeclinedBy = BookingDeclinedBy.System;
                booking.DeclineReason = "[Hệ thống] Ứng viên không tham dự buổi phỏng vấn đã hẹn.";
                booking.RespondedAt = now;
                booking.UpdatedAt = now;
                unitOfWork.Repository<InterviewBooking>().Update(booking);

                var app = await unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .GetByIdAsync(booking.ApplicationId, ct);
                if (app == null) continue;

                // Đã đóng rồi thì thôi — quét lại không được ghi đè kết quả HR đã chốt.
                if (string.Equals(app.Status, "not_pass", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(app.Status, "pass", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(app.Status, "withdrawn", StringComparison.OrdinalIgnoreCase))
                    continue;

                app.Status = "not_pass";
                app.UpdatedAt = now;
                unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);
                closedApps.Add((app.Id, app.CandidateAccountId, app.CandidateEmail, app.CandidateName, booking.RoundNumber));
            }

            // Lưu TRƯỚC khi trả chỗ: save lỗi thì không booking nào bị coi là đã xử lý, chu kỳ sau
            // chạy lại từ đầu thay vì trả chỗ hai lần (cùng thứ tự với các luồng trả chỗ khác).
            await unitOfWork.SaveChangesAsync(ct);

            foreach (var booking in noShows)
            {
                try
                {
                    await unitOfWork.ExecuteSqlRawAsync(
                        "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                        new object[] { now, booking.AvailabilitySlotId }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không trả được chỗ của booking {BookingId} sau khi đánh trượt.", booking.Id);
                }
            }

            foreach (var closed in closedApps)
            {
                if (closed.AccountId.HasValue)
                {
                    var dedupKey = $"no_show_closed:{closed.AppId}:{closed.Round}";
                    var already = await unitOfWork.Repository<Notification>().FindAsync(
                        n => n.CandidateAccountId == closed.AccountId.Value && n.DedupKey == dedupKey, ct);
                    if (!already.Any())
                    {
                        await unitOfWork.Repository<Notification>().AddAsync(new Notification
                        {
                            CandidateAccountId = closed.AccountId.Value,
                            DedupKey = dedupKey,
                            Type = "result",
                            Title = "Hồ sơ dừng lại do không tham dự phỏng vấn",
                            Body = $"Bạn không tham dự buổi phỏng vấn vòng {closed.Round} đã hẹn và cũng không báo bận, "
                                   + "nên hồ sơ dừng lại ở vòng này.",
                            Link = $"/candidate/applications/{closed.AppId}",
                            IsRead = false,
                        }, ct);
                    }

                    try
                    {
                        await notif.PublishUserEventAsync(closed.AccountId.Value, "ReceiveUserNotification",
                            new { Type = "ApplicationClosedNoShow", ApplicationId = closed.AppId }, ct);
                    }
                    catch { /* best-effort */ }
                }

                try
                {
                    await notif.PublishGroupEventAsync("hr_admin", "ReceiveApplicationStatusUpdate",
                        new { ApplicationId = closed.AppId, Status = "not_pass", Reason = "no_show" }, ct);
                }
                catch { /* best-effort */ }
            }

            await unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation("Đã đánh trượt {Count} hồ sơ không tham dự phỏng vấn.", closedApps.Count);
        }
    }
}
