using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Offers
{
    /// <summary>
    /// Tự đóng tin khi đã tuyển đủ số lượng trên phiếu yêu cầu (ADR-074 — hoàn thiện offer).
    ///
    /// Trước đây ứng viên cuối cùng nhận việc xong, tin vẫn <c>active</c> trên Job Board: người ngoài tiếp tục
    /// nộp hồ sơ vào một vị trí đã hết chỗ, và AI tiếp tục chấm CV — tốn tiền cho một phễu không còn đích.
    ///
    /// Chỉ ĐÓNG tin, không đụng tới hồ sơ nào: ứng viên đang giữa phễu hay đang cầm thư mời là chuyện con người
    /// phải quyết (từ chối kèm thư cảm ơn, hoặc giữ lại vì còn chỗ khác). Hệ thống chỉ báo cho đúng người, kèm
    /// con số, để không ai bị bỏ quên trong một tin đã đóng.
    /// </summary>
    public static class JobHeadcountCloser
    {
        public const string AuditAction = "job_auto_closed_headcount";

        public record Outcome(int Headcount, int Hired, int OpenApplications, int LiveOffers, IReadOnlyList<Guid> Notified);

        /// <summary>
        /// Đóng tin nếu số hồ sơ <c>hired</c> ĐÃ LƯU đạt số lượng trên phiếu. Gọi SAU khi trạng thái <c>hired</c>
        /// của người vừa nhận việc đã được lưu — đếm bằng truy vấn nên thay đổi chưa lưu không được tính.
        /// Trả <c>null</c> khi không đóng (tin không còn <c>active</c>, không có phiếu, hoặc chưa đủ người).
        /// Người gọi tự <c>SaveChangesAsync</c> rồi phát realtime cho <see cref="Outcome.Notified"/>.
        /// </summary>
        public static async Task<Outcome?> CloseIfFilledAsync(IUnitOfWork uow, JobPosting job, CancellationToken ct)
        {
            if (!string.Equals(job.Status, "active", StringComparison.OrdinalIgnoreCase)) return null;

            // Tin không từ phiếu (dữ liệu trước ADR-063) không có "số lượng cần tuyển" — không đoán.
            if (job.RecruitmentRequestId is not { } requestId) return null;
            var request = await uow.Repository<RecruitmentRequest>().GetByIdAsync(requestId, ct);
            if (request == null || request.Headcount <= 0) return null;

            var jobId = job.Id;
            var hired = await uow.Repository<ARI.Domain.Entities.Application>()
                .CountAsync(a => a.JobPostingId == jobId && a.Status == ApplicationStatuses.Hired, ct);
            if (hired < request.Headcount) return null;

            job.Status = "closed";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            uow.Repository<JobPosting>().Update(job);

            var open = await uow.Repository<ARI.Domain.Entities.Application>()
                .CountAsync(a => a.JobPostingId == jobId && !ApplicationStatuses.Terminal.Contains(a.Status), ct);
            var liveOffers = await uow.Repository<Offer>()
                .CountAsync(o => o.JobPostingId == jobId
                                 && o.Status != OfferStatus.Accepted
                                 && !OfferStatus.Closed.Contains(o.Status), ct);

            await AdminSupport.WriteAuditAsync(uow, null, AuditAction, nameof(JobPosting), job.Id,
                AuditMetadata.Serialize(new
                {
                    headcount = request.Headcount,
                    hired,
                    open_applications = open,
                    live_offers = liveOffers,
                    recruitment_request_id = request.Id,
                }), ct);

            // Người cần biết: HM chính (chủ nhu cầu), Recruiter (vận hành phễu), HR Leader (chủ quy trình +
            // thư mời còn đang chạy). Link theo workspace của từng người (StaffLinks).
            var hm = await JobAccess.PrimaryHiringManagerAsync(uow, jobId, ct);
            var hrLeaders = await uow.Repository<User>().QueryAsync(
                q => q.Where(u => u.IsActive && u.Role == RoleNames.HrAdmin).Select(u => u.Id), ct);
            var recipients = new[] { hm?.UserId ?? Guid.Empty, job.CreatedByUserId }
                .Concat(hrLeaders)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var body = $"Đã tuyển đủ {request.Headcount} người cho \"{job.Title}\" — tin đã tự đóng trên Job Board.";
            if (open > 0) body += $" Còn {open} hồ sơ đang xử lý cần khép lại.";
            if (liveOffers > 0) body += $" {liveOffers} thư mời khác vẫn đang hiệu lực — hệ thống không tự thu hồi.";

            // Mỗi lần đóng là một sự kiện riêng: tin mở lại (tăng số lượng) rồi đủ người lần nữa vẫn phải báo.
            var stamp = DateTimeOffset.UtcNow.Ticks;
            foreach (var recipient in recipients)
            {
                await OfferSupport.NotifyStaffAsync(uow, recipient, "system",
                    "Tin đã tuyển đủ người và tự đóng", body,
                    await StaffLinks.JobAsync(uow, recipient, jobId, ct),
                    $"job_filled:{jobId}:{stamp}", ct);
            }

            return new Outcome(request.Headcount, hired, open, liveOffers, recipients);
        }
    }
}
