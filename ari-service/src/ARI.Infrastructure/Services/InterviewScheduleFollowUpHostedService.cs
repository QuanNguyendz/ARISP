using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Offers;
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

                // Thư mời nhận việc quá hạn (ADR-061). Đặt trong CHÍNH vòng lặp này thay vì dựng
                // một BackgroundService thứ hai: cả hai việc đều là "chạy theo một hạn chót trên
                // một văn bản đã gửi ứng viên", và vòng lặp này đã có sẵn delay khởi động, chu kỳ
                // 30 phút, scoped factory và try/catch cô lập từng tác vụ.
                // NHẮC trước khi ĐÓNG, cùng một vòng quét: đóng trước rồi mới nhắc thì thư vừa bị
                // đánh quá hạn ở dòng trên sẽ không còn ở trạng thái `sent` để nhắc nữa.
                try { await RemindOffersAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Nhắc phản hồi thư mời nhận việc thất bại."); }

                try { await ExpireOffersAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Đóng thư mời nhận việc quá hạn thất bại."); }

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

                // Đã đóng rồi thì thôi — quét lại không được ghi đè kết quả đã chốt.
                // Danh sách "đã đóng" phải lấy từ ApplicationStatuses.Terminal chứ không liệt kê
                // tại chỗ: bản liệt kê tại chỗ trước đây chỉ biết pass/not_pass/withdrawn, nên một
                // ứng viên đã "hired" mà còn lịch cũ quá giờ sẽ bị chính vòng quét này đánh trượt.
                if (ApplicationStatuses.IsTerminal(app.Status))
                    continue;

                app.Status = ApplicationStatuses.NotPass;
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
        // ─────────────────── 3. Thư mời nhận việc quá hạn (ADR-061) ───────────────────

        /// <summary>
        /// Đóng các thư mời đã GỬI mà quá hạn phản hồi: offer → <c>expired</c>, hồ sơ →
        /// <c>offer_declined</c>. Không im lặng bỏ qua — một thư mời treo vô thời hạn giữ chỗ
        /// "một offer sống" của hồ sơ, khiến doanh nghiệp không ra được thư mời khác.
        /// </summary>
/// <summary>
        /// Nhắc ứng viên phản hồi thư mời trước khi nó hết hạn.
        ///
        /// Không có bước này thì ứng viên bị <c>ExpireOffersAsync</c> đánh dấu từ chối mà **chưa từng
        /// được cảnh báo** — trong khi luồng phỏng vấn ngay bên cạnh vẫn nhắc ở mốc 24h/3h. Với thư
        /// mời nhận việc thì im lặng còn tệ hơn: hồ sơ khép lại vĩnh viễn ở <c>offer_declined</c>.
        ///
        /// Chống gửi trùng bằng chính <c>DedupKey</c> của thông báo (<c>offer_reminder:{id}:{mốc}</c>)
        /// thay vì thêm cột mới — cùng cách nhánh nhắc lịch đang dùng, và không phải migration.
        /// </summary>
        private async Task RemindOffersAsync(CancellationToken ct)
        {
            var marks = _options.OfferReminderMarks();
            if (marks.Length == 0) return;

            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var candidateBaseUrl = config["Frontend:CandidateBaseUrl"];

            var now = DateTimeOffset.UtcNow;
            var pending = (await unitOfWork.Repository<Offer>().FindAsync(
                o => o.Status == OfferStatus.Sent && o.ExpiresAt != null && o.ExpiresAt > now, ct)).ToList();
            if (pending.Count == 0) return;

            var sent = 0;
            foreach (var offer in pending)
            {
                var hoursLeft = (offer.ExpiresAt!.Value - now).TotalHours;
                var mark = marks.FirstOrDefault(m => hoursLeft <= m);
                if (mark == 0) continue;

                var dedupKey = $"offer_reminder:{offer.Id}:{mark}";
                var already = await unitOfWork.Repository<Notification>()
                    .FindAsync(n => n.DedupKey == dedupKey, ct);
                if (already.Any()) continue;

                var app = await unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .GetByIdAsync(offer.ApplicationId, ct);
                if (app == null) continue;

                var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

                await unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    CandidateAccountId = app.CandidateAccountId,
                    Type = "reminder",
                    Title = "Thư mời nhận việc sắp hết hạn",
                    Body = $"Vị trí \"{offer.Position ?? job?.Title}\" — vui lòng phản hồi trước hạn.",
                    Link = $"/candidate/applications/{app.Id}/offer",
                    DedupKey = dedupKey,
                    IsRead = false,
                }, ct);

                // Thư là best-effort: SMTP hỏng không được làm hỏng cả vòng quét, và thông báo trên
                // chuông ở trên đã ghi rồi nên ứng viên vẫn thấy.
                try
                {
                    var mail = OfferEmail.BuildReminder(offer, app, job, candidateBaseUrl);
                    await email.SendEmailAsync(app.CandidateEmail, mail.Subject, mail.Html);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không gửi được thư nhắc phản hồi offer {OfferId}.", offer.Id);
                }

                sent++;
            }

            if (sent == 0) return;
            await unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation("Đã nhắc {Count} ứng viên phản hồi thư mời nhận việc.", sent);
        }

                private async Task ExpireOffersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeOffset.UtcNow;
            var expired = (await unitOfWork.Repository<Offer>().FindAsync(
                o => o.Status == OfferStatus.Sent && o.ExpiresAt != null && o.ExpiresAt <= now, ct)).ToList();
            if (expired.Count == 0) return;

            var closed = new List<(Guid AppId, Guid? AccountId, string Name, string? JobTitle)>();

            foreach (var offer in expired)
            {
                offer.Status = OfferStatus.Expired;
                offer.UpdatedAt = now;
                unitOfWork.Repository<Offer>().Update(offer);

                var app = await unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .GetByIdAsync(offer.ApplicationId, ct);
                if (app == null) continue;

                // Không ghi đè hồ sơ đã đóng bằng đường khác (rút hồ sơ, thu hồi offer…).
                if (ApplicationStatuses.IsTerminal(app.Status)) continue;

                app.Status = ApplicationStatuses.OfferDeclined;
                app.UpdatedAt = now;
                unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

                var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
                closed.Add((app.Id, app.CandidateAccountId, app.CandidateName, job?.Title));

                await unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = offer.CreatedByUserId,
                    Type = "result",
                    Title = "Thư mời nhận việc đã quá hạn",
                    Body = $"{app.CandidateName} — vị trí \"{job?.Title}\" không phản hồi trước hạn.",
                    Link = "/hr/offers",
                    DedupKey = $"offer_expired:{offer.Id}",
                    IsRead = false,
                }, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);

            foreach (var item in closed)
            {
                try
                {
                    if (item.AccountId is { } accountId)
                    {
                        await notif.PublishUserEventAsync(accountId, "ReceiveApplicationStatusUpdate",
                            new { Id = item.AppId, Status = ApplicationStatuses.OfferDeclined }, ct);
                    }
                }
                catch { /* best-effort */ }
            }

            _logger.LogInformation("Đã đóng {Count} thư mời nhận việc quá hạn.", expired.Count);
        }

    }
}
