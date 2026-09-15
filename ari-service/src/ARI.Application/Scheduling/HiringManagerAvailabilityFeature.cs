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
    ///
    /// <b>Khai ở ĐÚNG MỘT nơi</b> (sửa 2026-09-14): mục "Lịch tôi có mặt được" ở màn tin của HM, theo
    /// từng vòng. Lệnh duyệt shortlist không còn mang lịch theo. <b>Vòng trắc nghiệm không có lịch
    /// rảnh</b> — ứng viên làm bài trực tuyến, không ai ngồi cùng, và luật khớp giờ không áp ở đó.
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
        /// Khung giờ CÒN HIỆU LỰC của vòng: chưa kết thúc tính tới thời điểm gọi, và do Hiring Manager
        /// chính HIỆN TẠI của tin khai.
        ///
        /// Khung đã trôi qua giữ lại trong DB (dấu vết vì sao ca cũ được xếp như vậy) nhưng không còn
        /// tham gia kiểm tra nào. Khung của HM CŨ cũng vậy (ADR-068): lịch rảnh là lịch của MỘT NGƯỜI,
        /// nên sau khi HR Leader chuyển tin, giờ rảnh của người cũ không nói gì về việc người mới có mặt
        /// được hay không — để lại thì Recruiter xếp được ca vào lịch của một người không còn dự.
        /// </summary>
        public static async Task<List<HiringManagerAvailability>> ActiveWindowsAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, CancellationToken ct)
        {
            var hm = await JobAccess.PrimaryHiringManagerAsync(unitOfWork, jobPostingId, ct);
            if (hm == null) return new List<HiringManagerAvailability>();

            var now = DateTimeOffset.UtcNow;
            var rows = await unitOfWork.Repository<HiringManagerAvailability>().FindAsync(
                a => a.JobPostingId == jobPostingId && a.RoundNumber == roundNumber && a.EndTime > now
                     && a.HiringManagerUserId == hm.UserId, ct);
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

        /// <summary>
        /// Các buổi ĐÃ HẸN của vòng mà nay không còn nằm trong khung giờ nào của Hiring Manager.
        ///
        /// <b>Vì sao phải trả lời câu này.</b> Luật "ca phải nằm trọn trong khung HM rảnh" (ADR-067)
        /// chỉ chạy lúc GÁN ca. Nên khi HM sửa lại lịch — đúng tình huống có việc đột xuất — những
        /// buổi đã hẹn trước đó không bị chặn ở đâu cả: chúng lặng lẽ trở thành buổi mà người bắt
        /// buộc phải dự đã báo là không dự được, và chỉ vỡ ra vào đúng hôm phỏng vấn. Chính là kiểu
        /// hỏng mà ADR-067 sinh ra để xoá.
        ///
        /// Hàm này KHÔNG tự huỷ gì: dời lịch của một ứng viên đã hẹn là quyết định của Recruiter
        /// (họ còn phải báo lại ứng viên ngoài hệ thống). Việc của hệ thống là **nói ra**.
        ///
        /// Gọi SAU khi đã ghi thay đổi vào <c>IUnitOfWork</c> để đọc đúng danh sách khung còn lại.
        /// </summary>
        public static async Task<List<InterviewBooking>> BookingsOutsideWindowsAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, CancellationToken ct)
        {
            var slots = (await unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                    s => s.JobPostingId == jobPostingId && s.RoundNumber == roundNumber, ct))
                .ToList();
            if (slots.Count == 0) return new List<InterviewBooking>();

            var slotIds = slots.Select(s => s.Id).ToList();
            var now = DateTimeOffset.UtcNow;

            // Chỉ buổi CÒN Ở PHÍA TRƯỚC mới có gì để sửa. Buổi đã diễn ra thì lịch rảnh khai lại
            // hôm nay không nói được điều gì về nó.
            var live = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => slotIds.Contains(b.AvailabilitySlotId) && b.Status == BookingStatus.Scheduled, ct))
                .ToList();
            if (live.Count == 0) return new List<InterviewBooking>();

            var windows = await ActiveWindowsAsync(unitOfWork, jobPostingId, roundNumber, ct);

            return live
                .Where(b =>
                {
                    var slot = slots.FirstOrDefault(s => s.Id == b.AvailabilitySlotId);
                    if (slot == null || slot.StartTime <= now) return false;
                    return !IsCovered(windows, slot.StartTime, slot.EndTime);
                })
                .ToList();
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

            // Chỉ khung của HM chính HIỆN TẠI — đúng tập mà luật khớp giờ dùng (xem `ActiveWindowsAsync`).
            // Hiện cả khung của HM cũ thì Recruiter thấy "giờ rảnh" mà xếp vào lại bị từ chối.
            var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, request.JobPostingId, ct);
            if (hm == null) return Result.Success(new List<HmAvailabilityDto>());

            var now = DateTimeOffset.UtcNow;
            var rows = (await _unitOfWork.Repository<HiringManagerAvailability>().FindAsync(
                    a => a.JobPostingId == request.JobPostingId
                         && (!request.Round.HasValue || a.RoundNumber == request.Round.Value)
                         && a.EndTime > now
                         && a.HiringManagerUserId == hm.UserId, ct))
                .OrderBy(a => a.RoundNumber).ThenBy(a => a.StartTime)
                .ToList();

            // Tên người khai tra một lượt cho cả trang.
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
    /// <summary>
    /// Kết quả một lần khai lại lịch.
    ///
    /// <see cref="AffectedBookings"/> là con số quan trọng nhất và là lý do lệnh này không trả về
    /// một số trần: sửa lịch có thể làm những buổi ĐÃ HẸN rơi ra ngoài giờ HM có mặt được, mà luật
    /// khớp giờ chỉ chạy lúc gán ca nên không có chốt chặn nào bắt được. Người sửa phải nhìn thấy
    /// hậu quả ngay tại chỗ họ vừa bấm.
    /// </summary>
    public record HmAvailabilitySaveResult(int WindowCount, int AffectedBookings);

    public record SetHmAvailabilityCommand(
        Guid JobPostingId, int RoundNumber, IReadOnlyList<HmAvailabilityWindowInput> Windows,
        Guid? ActorId, string? ActorRole) : IRequest<Result<HmAvailabilitySaveResult>>;

    public class SetHmAvailabilityCommandHandler
        : IRequestHandler<SetHmAvailabilityCommand, Result<HmAvailabilitySaveResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public SetHmAvailabilityCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<HmAvailabilitySaveResult>> Handle(
            SetHmAvailabilityCommand request, CancellationToken ct)
        {
            var round = request.RoundNumber > 0 ? request.RoundNumber : 1;

            var gate = await HmAvailabilityWriteGate.EvaluateAsync(
                _unitOfWork, request.JobPostingId, request.ActorId, request.ActorRole, ct);
            if (gate.Error != null) return Result.Failure<HmAvailabilitySaveResult>(gate.Error, gate.Code!);

            // Vòng TRẮC NGHIỆM không nhận lịch rảnh: ứng viên làm bài trực tuyến, HM không phải có mặt,
            // và luật khớp giờ bỏ qua vòng đó. Nhận vào thì khung giờ nằm trong DB — và hiện trên màn
            // xếp lịch của Recruiter — như thể nó có tác dụng gì.
            var roundType = await SchedulingSupport.RoundTypeAsync(_unitOfWork, request.JobPostingId, round, ct);
            if (InterviewInviteEmail.IsOnlineTest(roundType))
                return Result.Failure<HmAvailabilitySaveResult>(
                    "Vòng trắc nghiệm làm bài trực tuyến, không cần Hiring Manager có mặt nên không khai lịch rảnh.");

            var (windows, error) = HmAvailabilitySupport.Sanitize(request.Windows);
            if (error != null) return Result.Failure<HmAvailabilitySaveResult>(error);

            var replaced = await HmAvailabilityWriteGate.ReplaceAsync(
                _unitOfWork, request.JobPostingId, round, gate.OwnerUserId, windows, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            var result = await HmAvailabilityWriteGate.AnnounceAsync(
                _unitOfWork, _notifications, gate.Job, round, replaced, ct);

            return Result.Success(result);
        }
    }

    // ============================================================
    // DELETE /api/schedules/hm-availability/{id}  (Hiring Manager của tin)
    // ============================================================

    /// <summary>
    /// Bỏ MỘT khung giờ đã khai — kể cả khung đang diễn ra.
    ///
    /// <b>Vì sao cần một lệnh riêng bên cạnh lệnh khai lại cả danh sách.</b> Lệnh khai lại chỉ đụng
    /// tới khung CHƯA BẮT ĐẦU (khung đang chạy không gửi lại được vì giờ bắt đầu đã ở quá khứ). Mà
    /// tình huống có thật lại chính là: HM đang trong khung 9h–17h thì có việc đột xuất lúc 14h.
    /// Không có đường này thì họ không có cách nào rút phần còn lại của khung đó.
    /// </summary>
    public record DeleteHmAvailabilityCommand(Guid Id, Guid? ActorId, string? ActorRole)
        : IRequest<Result<HmAvailabilitySaveResult>>;

    public class DeleteHmAvailabilityCommandHandler
        : IRequestHandler<DeleteHmAvailabilityCommand, Result<HmAvailabilitySaveResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public DeleteHmAvailabilityCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<HmAvailabilitySaveResult>> Handle(
            DeleteHmAvailabilityCommand request, CancellationToken ct)
        {
            var row = await _unitOfWork.Repository<HiringManagerAvailability>().GetByIdAsync(request.Id, ct);
            if (row == null)
                return Result.Failure<HmAvailabilitySaveResult>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);

            var gate = await HmAvailabilityWriteGate.EvaluateAsync(
                _unitOfWork, row.JobPostingId, request.ActorId, request.ActorRole, ct);
            if (gate.Error != null) return Result.Failure<HmAvailabilitySaveResult>(gate.Error, gate.Code!);

            var round = row.RoundNumber;
            _unitOfWork.Repository<HiringManagerAvailability>().Delete(row);
            await _unitOfWork.SaveChangesAsync(ct);

            var remaining = (await HmAvailabilitySupport.ActiveWindowsAsync(
                _unitOfWork, row.JobPostingId, round, ct)).Count;

            var result = await HmAvailabilityWriteGate.AnnounceAsync(
                _unitOfWork, _notifications, gate.Job, round, remaining, ct);

            return Result.Success(result);
        }
    }

    /// <summary>
    /// Cổng ghi dùng chung cho hai lệnh sửa lịch (khai lại cả danh sách, rút một khung). Tách ra vì
    /// hai lệnh đó phải áp CÙNG một luật quyền — chép thành hai bản là mở đường cho một bên nới lỏng dần.
    /// </summary>
    public static class HmAvailabilityWriteGate
    {
        public record GateResult(string? Error, string? Code, JobPosting? Job, Guid OwnerUserId);

        /// <summary>
        /// Ai được khai: Hiring Manager của tin. Quản trị viên khai THAY được (HM bận) — khung giờ vẫn
        /// ghi tên HM, người thật sự sẽ dự, để Recruiter không tưởng là lịch của quản trị viên.
        ///
        /// ADR-068: tin thiếu HM hoặc HM đã bị khoá thì không khai thay được nữa — lịch rảnh của một
        /// người không tồn tại / không còn dự là vô nghĩa; việc cần làm là HR Leader chuyển HM.
        /// </summary>
        public static async Task<GateResult> EvaluateAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? actorId, string? actorRole, CancellationToken ct)
        {
            if (actorId is not { } uid || uid == Guid.Empty)
                return new GateResult("Không xác định được người dùng.", CommonErrorCodes.Forbidden, null, Guid.Empty);

            var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return new GateResult("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound, null, Guid.Empty);

            var isHm = await JobAccess.IsPrimaryHiringManagerAsync(unitOfWork, jobPostingId, uid, ct);
            var isAdmin = RoleNames.IsAdmin(actorRole);

            if (!isHm && !isAdmin)
                return new GateResult(
                    "Chỉ Hiring Manager phụ trách tin này (hoặc quản trị viên) mới khai được lịch rảnh.",
                    CommonErrorCodes.Forbidden, job, Guid.Empty);

            var (hm, hmError) = await JobAccess.RequireActiveHiringManagerAsync(unitOfWork, jobPostingId, ct);
            if (hm == null)
                return new GateResult(hmError, CommonErrorCodes.Conflict, job, Guid.Empty);

            return new GateResult(null, null, job, hm.UserId);
        }

        /// <summary>
        /// Thay danh sách khung giờ CHƯA BẮT ĐẦU của vòng. Chưa <c>SaveChanges</c>.
        ///
        /// <b>Khung ĐANG DIỄN RA không bị đụng tới.</b> `Sanitize` đòi giờ bắt đầu ở tương lai, nên
        /// một khung đã chạy không thể gửi lại qua lệnh này — nếu vẫn xoá nó thì mỗi lần HM sửa lịch
        /// sẽ âm thầm huỷ đúng cái khung họ đang ở trong đó, kèm theo mọi ca đã xếp vào. Muốn bỏ một
        /// khung đang chạy thì dùng lệnh xoá từng khung (<see cref="DeleteHmAvailabilityCommand"/>),
        /// nơi việc đó là một hành động có chủ đích chứ không phải tác dụng phụ.
        /// </summary>
        public static async Task<int> ReplaceAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int round, Guid ownerUserId,
            IReadOnlyList<HmAvailabilityWindowInput> windows, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var existing = (await HmAvailabilitySupport.ActiveWindowsAsync(unitOfWork, jobPostingId, round, ct))
                .Where(w => w.StartTime > now)
                .ToList();
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

        /// <summary>
        /// Đếm hậu quả rồi báo cho Recruiter. Gọi SAU <c>SaveChanges</c> để đọc đúng danh sách còn lại.
        ///
        /// Recruiter là người đang chờ chính dữ liệu này để xếp ca — báo thẳng, không để họ phải tự
        /// tải lại màn xếp lịch mà đoán. Và nếu có buổi đã hẹn rơi ra ngoài lịch mới thì họ là người
        /// duy nhất xử lý được (dời ca, báo lại ứng viên), nên con số đó phải đi kèm.
        /// </summary>
        public static async Task<HmAvailabilitySaveResult> AnnounceAsync(
            IUnitOfWork unitOfWork, INotificationService notifications,
            JobPosting? job, int round, int windowCount, CancellationToken ct)
        {
            if (job == null) return new HmAvailabilitySaveResult(windowCount, 0);

            var affected = await HmAvailabilitySupport.BookingsOutsideWindowsAsync(
                unitOfWork, job.Id, round, ct);

            await notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveUserNotification",
                new
                {
                    Type = "HmAvailabilityUpdated",
                    JobPostingId = job.Id,
                    RoundNumber = round,
                    WindowCount = windowCount,
                    AffectedBookings = affected.Count,
                }, ct);

            return new HmAvailabilitySaveResult(windowCount, affected.Count);
        }
    }
}
