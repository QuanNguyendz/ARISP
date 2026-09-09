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

namespace ARI.Application.Scheduling
{
    /// <summary>
    /// Khung giờ rảnh của Hiring Manager cho từng vòng của một tin (ADR-067).
    ///
    /// <b>Vì sao nó tồn tại.</b> Buổi phỏng vấn thật nay cần HM ngồi trong phòng cùng AI, nên ca do
    /// Recruiter xếp phải nằm trong giờ HM có mặt được. Trước đây Recruiter xếp ca tuỳ ý và việc khớp
    /// lịch với HM là một cuộc trao đổi ngoài hệ thống — nghĩa là hệ thống <i>cho phép</i> xếp một ca
    /// mà người bắt buộc phải dự không đến được, và chỉ vỡ ra vào đúng hôm phỏng vấn.
    ///
    /// <b>Phần CỐ Ý nằm ngoài hệ thống.</b> Việc chốt giờ với chính ứng viên (SMS, Zalo, gọi điện) vẫn
    /// do Recruiter làm bên ngoài. Hệ thống không mô hình hoá cuộc thương lượng đó; nó chỉ ràng buộc
    /// kết quả — ca chọn ra phải nằm trong giờ HM rảnh.
    /// </summary>
    public static class HmAvailabilitySupport
    {
        /// <summary>Trần số khung giờ mỗi vòng — quá nhiều thì danh sách chọn ca thành vô dụng.</summary>
        public const int MaxWindowsPerRound = 30;

        /// <summary>
        /// Trần độ dài MỘT khung. Dài hơn một ngày làm việc thì ràng buộc "ca phải nằm trong giờ HM
        /// rảnh" mất hết ý nghĩa: khai "rảnh từ 10/09 đến 25/09" nghĩa là Recruiter xếp được cả ca
        /// 3 giờ sáng. Rảnh nhiều ngày thì khai nhiều khung.
        /// </summary>
        public const int MaxWindowHours = 12;

        /// <summary>
        /// Khung giờ CÒN HIỆU LỰC của vòng: chưa kết thúc tính tới thời điểm gọi.
        /// Khung đã trôi qua giữ lại trong DB (dấu vết vì sao ca cũ được xếp như vậy) nhưng không
        /// còn tham gia kiểm tra nào.
        /// </summary>
        public static async Task<List<HiringManagerAvailability>> ActiveWindowsAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await unitOfWork.Repository<HiringManagerAvailability>().FindAsync(
                a => a.JobPostingId == jobPostingId && a.RoundNumber == roundNumber && a.EndTime > now, ct);
            return rows.OrderBy(a => a.StartTime).ToList();
        }

        /// <summary>
        /// Ca <paramref name="slotStart"/>–<paramref name="slotEnd"/> có nằm TRỌN trong một khung giờ
        /// rảnh nào không.
        ///
        /// Đòi nằm trọn chứ không chỉ giao nhau: một ca 14:00–15:00 chồng lên khung rảnh 14:00–14:15
        /// nghĩa là HM phải rời phòng giữa buổi phỏng vấn — về hình thức thì "có giao nhau", về thực tế
        /// thì buổi đó không tổ chức được.
        /// </summary>
        public static bool IsCovered(
            IEnumerable<HiringManagerAvailability> windows, DateTimeOffset slotStart, DateTimeOffset slotEnd)
            => windows.Any(w => w.StartTime <= slotStart && w.EndTime >= slotEnd);

        /// <summary>Mô tả các khung giờ theo giờ VN để đưa thẳng vào thông báo lỗi cho Recruiter.</summary>
        public static string Describe(IEnumerable<HiringManagerAvailability> windows)
        {
            var text = windows
                .OrderBy(w => w.StartTime)
                .Take(5)
                .Select(w =>
                {
                    var s = w.StartTime.ToOffset(TimeSpan.FromHours(7));
                    var e = w.EndTime.ToOffset(TimeSpan.FromHours(7));
                    return s.Date == e.Date
                        ? $"{s:dd/MM} {s:HH:mm}–{e:HH:mm}"
                        : $"{s:dd/MM HH:mm} – {e:dd/MM HH:mm}";
                })
                .ToList();
            return text.Count == 0 ? string.Empty : string.Join("; ", text);
        }

        /// <summary>
        /// Chuẩn hoá + kiểm khung giờ người dùng gửi lên. Trả về danh sách đã lọc, hoặc thông báo lỗi.
        /// Dùng chung giữa lệnh khai lịch của HM và bước duyệt shortlist (nơi HM gửi kèm lịch).
        /// </summary>
        public static (List<HmAvailabilityWindowInput> windows, string? error) Sanitize(
            IReadOnlyList<HmAvailabilityWindowInput>? input)
        {
            var items = (input ?? Array.Empty<HmAvailabilityWindowInput>()).ToList();
            if (items.Count > MaxWindowsPerRound)
                return (new List<HmAvailabilityWindowInput>(),
                    $"Tối đa {MaxWindowsPerRound} khung giờ cho mỗi vòng.");

            var now = DateTimeOffset.UtcNow;
            var cleaned = new List<HmAvailabilityWindowInput>();
            foreach (var w in items)
            {
                // Giờ BẮT ĐẦU phải ở tương lai, không chỉ giờ kết thúc: một khung đã bắt đầu thì
                // phần còn dùng được của nó không còn khớp với thứ người khai đang nghĩ, và ca xếp
                // vào đó có thể rơi vào khoảng đã trôi qua.
                //
                // Khung ĐANG DIỄN RA vẫn còn hiệu lực (xem `ActiveWindowsAsync`) — luật này chỉ áp
                // cho khung MỚI khai, nên giao diện cố ý không nạp khung đang chạy vào ô sửa.
                if (w.StartTime <= now)
                    return (cleaned, "Giờ bắt đầu phải ở tương lai. Hãy chọn khung giờ sắp tới.");
                if (w.EndTime <= w.StartTime)
                    return (cleaned, "Giờ kết thúc phải sau giờ bắt đầu.");
                if ((w.EndTime - w.StartTime).TotalHours > MaxWindowHours)
                    return (cleaned,
                        $"Mỗi khung giờ tối đa {MaxWindowHours} tiếng — rảnh nhiều ngày thì tách thành nhiều khung.");

                // Trùng khít thì bỏ: HM bấm gửi hai lần không nên sinh ra hai dòng y hệt.
                if (cleaned.Any(c => c.StartTime == w.StartTime && c.EndTime == w.EndTime)) continue;
                cleaned.Add(w with { Note = string.IsNullOrWhiteSpace(w.Note) ? null : w.Note.Trim() });
            }

            return (cleaned, null);
        }
    }

    /// <summary>Một khung giờ do Hiring Manager khai.</summary>
    public record HmAvailabilityWindowInput(DateTimeOffset StartTime, DateTimeOffset EndTime, string? Note);

    public record HmAvailabilityDto(
        Guid Id, int RoundNumber, DateTimeOffset StartTime, DateTimeOffset EndTime,
        string? Note, Guid HiringManagerUserId, string? HiringManagerName);

    // ============================================================
    // GET /api/schedules/hm-availability?jobPostingId=&round=
    // ============================================================

    /// <summary>
    /// Recruiter đọc để lọc ca; HM đọc để xem mình đã khai gì. Ngưỡng quyền là ĐỌC dữ liệu của tin
    /// (thành viên đội trở lên) — đây không phải dữ liệu nhạy cảm, nhưng cũng không phải dữ liệu công khai.
    /// </summary>
    public record GetHmAvailabilityQuery(Guid JobPostingId, int? Round, Guid? UserId, string? Role)
        : IRequest<Result<List<HmAvailabilityDto>>>;

    public class GetHmAvailabilityQueryHandler
        : IRequestHandler<GetHmAvailabilityQuery, Result<List<HmAvailabilityDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetHmAvailabilityQueryHandler(IUnitOfWork unitOfWork) { _unitOfWork = unitOfWork; }

        public async Task<Result<List<HmAvailabilityDto>>> Handle(
            GetHmAvailabilityQuery request, CancellationToken ct)
        {
            if (request.JobPostingId == Guid.Empty)
                return Result.Failure<List<HmAvailabilityDto>>("jobPostingId là bắt buộc.");

            var (ok, job) = await JobAccess.CanViewAsync(
                _unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null)
                return Result.Failure<List<HmAvailabilityDto>>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok)
                return Result.Failure<List<HmAvailabilityDto>>("Bạn không có quyền xem lịch của tin này.", CommonErrorCodes.Forbidden);

            var now = DateTimeOffset.UtcNow;
            var rows = (await _unitOfWork.Repository<HiringManagerAvailability>().FindAsync(
                    a => a.JobPostingId == request.JobPostingId
                         && (!request.Round.HasValue || a.RoundNumber == request.Round.Value)
                         && a.EndTime > now, ct))
                .OrderBy(a => a.RoundNumber).ThenBy(a => a.StartTime)
                .ToList();

            // Tên người khai tra một lượt cho cả trang — tin đổi HM giữa chừng thì Recruiter cần
            // nhìn ra khung nào là của ai.
            var userIds = rows.Select(r => r.HiringManagerUserId).Distinct().ToList();
            var users = userIds.Count == 0
                ? new List<User>()
                : (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.Id), ct)).ToList();

            return Result.Success(rows.Select(r => new HmAvailabilityDto(
                r.Id, r.RoundNumber, r.StartTime, r.EndTime, r.Note, r.HiringManagerUserId,
                users.FirstOrDefault(u => u.Id == r.HiringManagerUserId)?.FullName)).ToList());
        }
    }

    // ============================================================
    // PUT /api/schedules/hm-availability  (Hiring Manager của tin)
    // ============================================================

    /// <summary>
    /// HM khai lại TOÀN BỘ khung giờ còn hiệu lực của một vòng.
    ///
    /// Thay cả danh sách chứ không thêm từng dòng: đây là câu trả lời cho "tuần này tôi rảnh những
    /// lúc nào", mà câu trả lời đó thay đổi nguyên khối. Thêm/xoá từng dòng thì giao diện phải giữ
    /// trạng thái trung gian và HM dễ để sót một khung cũ không còn đúng.
    ///
    /// Khung đã TRÔI QUA không bị đụng tới — chúng là dấu vết giải thích vì sao ca cũ được xếp như vậy.
    /// </summary>
    public record SetHmAvailabilityCommand(
        Guid JobPostingId, int RoundNumber, IReadOnlyList<HmAvailabilityWindowInput> Windows,
        Guid? ActorId, string? ActorRole) : IRequest<Result<int>>;

    public class SetHmAvailabilityCommandHandler : IRequestHandler<SetHmAvailabilityCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public SetHmAvailabilityCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<int>> Handle(SetHmAvailabilityCommand request, CancellationToken ct)
        {
            var round = request.RoundNumber > 0 ? request.RoundNumber : 1;

            var gate = await HmAvailabilityWriteGate.EvaluateAsync(
                _unitOfWork, request.JobPostingId, request.ActorId, request.ActorRole, ct);
            if (gate.Error != null) return Result.Failure<int>(gate.Error, gate.Code!);

            var (windows, error) = HmAvailabilitySupport.Sanitize(request.Windows);
            if (error != null) return Result.Failure<int>(error);

            var replaced = await HmAvailabilityWriteGate.ReplaceAsync(
                _unitOfWork, request.JobPostingId, round, gate.OwnerUserId, windows, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            // Recruiter là người đang chờ chính dữ liệu này để xếp ca — báo thẳng, không để họ
            // phải tự tải lại màn xếp lịch mà đoán.
            if (gate.Job != null)
            {
                await _notifications.PublishUserEventAsync(gate.Job.CreatedByUserId, "ReceiveUserNotification",
                    new
                    {
                        Type = "HmAvailabilityUpdated",
                        JobPostingId = gate.Job.Id,
                        RoundNumber = round,
                        WindowCount = replaced,
                    }, ct);
            }

            return Result.Success(replaced);
        }
    }

    /// <summary>
    /// Cổng ghi dùng chung cho hai đường vào (lệnh khai lịch, và bước duyệt shortlist có gửi kèm lịch).
    /// Tách ra vì hai đường đó phải áp CÙNG một luật — chép thành hai bản là mở đường cho một bên
    /// nới lỏng dần.
    /// </summary>
    internal static class HmAvailabilityWriteGate
    {
        internal record GateResult(string? Error, string? Code, JobPosting? Job, Guid OwnerUserId);

        /// <summary>
        /// Ai được khai: Hiring Manager của tin. Quản trị viên khai THAY được (HM nghỉ, tin chưa gán
        /// ai) — nhưng khung giờ vẫn ghi tên người thật sự sẽ dự nếu tin đã có HM, để Recruiter không
        /// tưởng là lịch của quản trị viên.
        /// </summary>
        public static async Task<GateResult> EvaluateAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? actorId, string? actorRole, CancellationToken ct)
        {
            if (actorId is not { } uid || uid == Guid.Empty)
                return new GateResult("Không xác định được người dùng.", CommonErrorCodes.Forbidden, null, Guid.Empty);

            var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return new GateResult("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound, null, Guid.Empty);

            var hm = await JobAccess.PrimaryHiringManagerAsync(unitOfWork, jobPostingId, ct);
            var isHm = hm != null && hm.UserId == uid;
            var isAdmin = RoleNames.IsAdmin(actorRole);

            if (!isHm && !isAdmin)
                return new GateResult(
                    "Chỉ Hiring Manager phụ trách tin này (hoặc quản trị viên) mới khai được lịch rảnh.",
                    CommonErrorCodes.Forbidden, job, Guid.Empty);

            return new GateResult(null, null, job, hm?.UserId ?? uid);
        }

        /// <summary>Xoá khung CÒN HIỆU LỰC của vòng rồi ghi danh sách mới. Chưa <c>SaveChanges</c>.</summary>
        public static async Task<int> ReplaceAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int round, Guid ownerUserId,
            IReadOnlyList<HmAvailabilityWindowInput> windows, CancellationToken ct)
        {
            var existing = await HmAvailabilitySupport.ActiveWindowsAsync(unitOfWork, jobPostingId, round, ct);
            foreach (var row in existing) unitOfWork.Repository<HiringManagerAvailability>().Delete(row);

            foreach (var w in windows)
            {
                await unitOfWork.Repository<HiringManagerAvailability>().AddAsync(new HiringManagerAvailability
                {
                    JobPostingId = jobPostingId,
                    RoundNumber = round,
                    HiringManagerUserId = ownerUserId,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    Note = w.Note,
                }, ct);
            }

            return windows.Count;
        }

        /// <summary>THÊM khung mới, giữ nguyên khung đã khai. Dùng ở bước duyệt shortlist.</summary>
        public static async Task<int> AppendAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int round, Guid ownerUserId,
            IReadOnlyList<HmAvailabilityWindowInput> windows, CancellationToken ct)
        {
            var existing = await HmAvailabilitySupport.ActiveWindowsAsync(unitOfWork, jobPostingId, round, ct);
            var added = 0;

            foreach (var w in windows)
            {
                if (existing.Any(e => e.StartTime == w.StartTime && e.EndTime == w.EndTime)) continue;
                await unitOfWork.Repository<HiringManagerAvailability>().AddAsync(new HiringManagerAvailability
                {
                    JobPostingId = jobPostingId,
                    RoundNumber = round,
                    HiringManagerUserId = ownerUserId,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    Note = w.Note,
                }, ct);
                added++;
            }

            return added;
        }
    }
}
