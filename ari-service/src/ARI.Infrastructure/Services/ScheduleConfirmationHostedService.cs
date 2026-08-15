using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Tự động REJECT lịch phỏng vấn mà ứng viên không xác nhận trong thời hạn (bổ sung ADR-048).
    ///
    /// Quét định kỳ các booking còn <c>scheduled</c> + <c>ConfirmationStatus == "pending"</c> mà đã quá
    /// <c>Scheduling:ConfirmDeadlineHours</c> giờ kể từ lúc gửi email mời (mốc <c>CreatedAt</c>). Với mỗi
    /// booking quá hạn: xử lý y như ứng viên tự "báo bận" — chuyển sang <c>declined</c>, trả chỗ ở khung giờ
    /// để nhân sự xếp lại (giữ mô hình reschedule), thông báo ứng viên + nhân sự.
    ///
    /// Tắt bằng <c>Scheduling:ConfirmDeadlineHours &lt;= 0</c>.
    /// </summary>
    public class ScheduleConfirmationHostedService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulingOptions _options;
        private readonly ILogger<ScheduleConfirmationHostedService> _logger;

        public ScheduleConfirmationHostedService(
            IServiceScopeFactory scopeFactory,
            SchedulingOptions options,
            ILogger<ScheduleConfirmationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.ConfirmDeadlineHours <= 0)
            {
                _logger.LogInformation("Auto-reject lịch chưa xác nhận: TẮT (ConfirmDeadlineHours <= 0).");
                return;
            }

            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RejectStaleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Lỗi tạm (DB) không được làm chết worker — thử lại ở chu kỳ sau.
                    _logger.LogError(ex, "Auto-reject lịch chưa xác nhận thất bại.");
                }

                try { await Task.Delay(ScanInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RejectStaleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeOffset.UtcNow;
            var deadline = now.AddHours(-_options.ConfirmDeadlineHours);

            var stale = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.Status == "scheduled"
                     && b.ConfirmationStatus == "pending"
                     && b.CreatedAt <= deadline, ct)).ToList();

            if (stale.Count == 0) return;

            foreach (var booking in stale)
            {
                booking.ConfirmationStatus = "declined";
                booking.DeclineReason = "[Hệ thống] Ứng viên không xác nhận lịch trong thời hạn.";
                // Nguồn đóng lịch nằm ở CỘT chứ không ở nội dung câu trên: giao diện phải phân biệt
                // được "quá hạn xác nhận" với "ứng viên báo bận" mà không đọc văn xuôi.
                booking.DeclinedBy = BookingDeclinedBy.System;
                booking.RespondedAt = now;
                booking.Status = "declined"; // trả chỗ + gỡ khỏi unique 'scheduled' để nhân sự gán lịch mới
                booking.UpdatedAt = now;
                unitOfWork.Repository<InterviewBooking>().Update(booking);
            }

            // Lưu trạng thái declined TRƯỚC khi trả chỗ: nếu save lỗi, không booking nào bị coi là
            // đã xử lý (vẫn pending) → tránh trả chỗ 2 lần ở chu kỳ sau.
            await unitOfWork.SaveChangesAsync(ct);

            // Giải phóng 1 chỗ ở mỗi khung giờ (best-effort, không âm) — sau khi đã persist declined.
            foreach (var booking in stale)
            {
                try
                {
                    await unitOfWork.ExecuteSqlRawAsync(
                        "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                        new object[] { now, booking.AvailabilitySlotId }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không trả được chỗ khung giờ {SlotId} khi auto-reject booking {BookingId}.",
                        booking.AvailabilitySlotId, booking.Id);
                }
            }

            // Thông báo ứng viên (bell) + nhân sự (realtime) — best-effort, không chặn batch.
            foreach (var booking in stale)
            {
                try { await NotifyAsync(unitOfWork, notif, booking, ct); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không gửi được thông báo auto-reject cho booking {BookingId}.", booking.Id);
                }
            }

            _logger.LogInformation("Auto-reject {Count} lịch chưa xác nhận quá hạn ({Hours} giờ).",
                stale.Count, _options.ConfirmDeadlineHours);
        }

        private static async Task NotifyAsync(
            IUnitOfWork uow, INotificationService notif, InterviewBooking booking, CancellationToken ct)
        {
            var app = await uow.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(booking.ApplicationId, ct);
            if (app == null) return;

            var job = await uow.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            // Bell + realtime cho ứng viên: lịch đã bị huỷ do chưa xác nhận, chờ nhân sự xếp lại.
            if (app.CandidateAccountId.HasValue)
            {
                var notifRepo = uow.Repository<ARI.Domain.Entities.Notification>();
                var dedupKey = $"schedule_auto_rejected:{booking.Id}";
                var already = await notifRepo.FindAsync(
                    n => n.CandidateAccountId == app.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                if (!already.Any())
                {
                    await notifRepo.AddAsync(new ARI.Domain.Entities.Notification
                    {
                        CandidateAccountId = app.CandidateAccountId.Value,
                        DedupKey = dedupKey,
                        Type = "schedule",
                        Title = "Lịch phỏng vấn đã bị huỷ (chưa xác nhận)",
                        Body = $"Bạn chưa xác nhận lịch phỏng vấn (vòng {booking.RoundNumber}) trong thời hạn nên lịch đã tự động bị huỷ. Nhân sự sẽ xếp một khung giờ khác và thông báo lại cho bạn.",
                        Link = $"/portal/schedule/{app.Id}",
                        IsRead = false
                    }, ct);
                    await uow.SaveChangesAsync(ct);
                }

                await notif.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification",
                    new { Type = "InterviewScheduleAutoRejected", ApplicationId = app.Id, RoundNumber = booking.RoundNumber }, ct);
            }

            // Realtime cho nhân sự (chủ tin + nhóm HR admin): ứng viên không phản hồi → cần xếp lại.
            var payload = new
            {
                Type = "ScheduleResponse",
                applicationId = app.Id,
                jobPostingId = app.JobPostingId,
                candidateName = app.CandidateName,
                roundNumber = booking.RoundNumber,
                response = "declined",
                reason = booking.DeclineReason,
                auto = true,
            };
            if (job != null)
                await notif.PublishUserEventAsync(job.CreatedByUserId, "ReceiveScheduleResponse", payload, ct);
            await notif.PublishGroupEventAsync("hr_admin", "ReceiveScheduleResponse", payload, ct);
        }
    }
}
