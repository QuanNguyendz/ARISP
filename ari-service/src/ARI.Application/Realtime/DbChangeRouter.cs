using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ARI.Application.Realtime
{
    /// <summary>Một thay đổi dữ liệu do Postgres phát qua NOTIFY (ADR-057).</summary>
    public sealed class DbChangeNotification
    {
        /// <summary>Tên bảng, ví dụ "applications".</summary>
        public string Table { get; init; } = string.Empty;

        /// <summary>I = insert, U = update, D = delete, S = đổi hàng loạt (statement-level).</summary>
        public string Op { get; init; } = string.Empty;

        public Guid? Id { get; init; }
        public Guid? CandidateAccountId { get; init; }
        public Guid? RecipientUserId { get; init; }
        public Guid? JobPostingId { get; init; }
        public Guid? ApplicationId { get; init; }
        public Guid? UserId { get; init; }
        public Guid? CreatedByUserId { get; init; }
        public Guid? RequestedByUserId { get; init; }

        /// <summary>
        /// Recruiter được phân công trên <c>recruitment_requests</c> (ADR-063). Thiếu khoá này thì
        /// người vừa được giao việc là người DUY NHẤT không được báo — đúng lỗi ADR-061 đã gặp khi
        /// realtime hẹp hơn quyền đọc và màn của Hiring Manager đứng im.
        /// </summary>
        public Guid? AssignedRecruiterId { get; init; }

        public string? Status { get; init; }
    }

    /// <summary>
    /// Dữ liệu mà listener tra thêm từ DB cho những bảng chỉ có <c>application_id</c> (booking, bài thi,
    /// đánh giá...) — cần biết hồ sơ đó của ứng viên nào và tin tuyển dụng do ai phụ trách.
    /// </summary>
    public sealed class DbChangeLookup
    {
        public static readonly DbChangeLookup Empty = new();

        public Guid? CandidateAccountId { get; init; }
        public Guid? JobPostingId { get; init; }
        public Guid? JobOwnerUserId { get; init; }

        /// <summary>
        /// Thành viên đội tuyển dụng của tin (ADR-061) — Hiring Manager và những người được mời vào
        /// đội. Họ ĐỌC được hồ sơ, báo cáo AI và thư mời của tin qua API, nên kênh realtime phải phủ
        /// đúng bấy nhiêu: thiếu danh sách này thì màn của Hiring Manager không bao giờ tự cập nhật
        /// và họ phải F5 tay để biết có việc mới — đúng thứ ADR-057 sinh ra để xoá bỏ.
        /// </summary>
        public IReadOnlyList<Guid> HiringTeamUserIds { get; init; } = Array.Empty<Guid>();
    }

    /// <summary>Kết quả định tuyến: gửi cho ai, kèm payload đã lọc.</summary>
    public sealed class DbChangeDispatch
    {
        public static readonly DbChangeDispatch None = new();

        /// <summary>Nhóm SignalR "user_{id}".</summary>
        public IReadOnlyList<Guid> UserIds { get; init; } = Array.Empty<Guid>();

        /// <summary>Nhóm SignalR "role_{ten}" — ví dụ "hr_admin", "super_admin".</summary>
        public IReadOnlyList<string> RoleGroups { get; init; } = Array.Empty<string>();

        /// <summary>Phát cho mọi kết nối (chỉ dùng cho tin tuyển dụng công khai).</summary>
        public bool BroadcastAll { get; init; }

        /// <summary>Payload gửi cho user/nhóm cụ thể.</summary>
        public IReadOnlyDictionary<string, object?> Payload { get; init; }
            = new Dictionary<string, object?>();

        /// <summary>Payload gửi khi broadcast — rút gọn hơn, không kèm khoá định danh nội bộ.</summary>
        public IReadOnlyDictionary<string, object?> BroadcastPayload { get; init; }
            = new Dictionary<string, object?>();

        public bool HasRecipients => UserIds.Count > 0 || RoleGroups.Count > 0 || BroadcastAll;
    }

    /// <summary>
    /// Dịch payload NOTIFY thành "gửi cho ai" (ADR-057).
    ///
    /// Tách khỏi listener và không phụ thuộc Npgsql/SignalR để unit test được — đây là phần nhạy cảm
    /// nhất của tính năng: trigger gắn trên TOÀN BỘ bảng nên router phải là chốt chặn quyết định dữ
    /// liệu nào được phép ra khỏi server. Bảng không nằm trong bảng định tuyến sẽ KHÔNG gửi cho ai.
    /// </summary>
    public static class DbChangeRouter
    {
        /// <summary>Tên event đẩy xuống FE (FE bắt trong useAppNotifications).</summary>
        public const string EventType = "ReceiveDbChange";

        /// <summary>Op đặc biệt: listener vừa nối lại, FE nạp lại toàn bộ cache.</summary>
        public const string ResyncOp = "resync";

        public const string HrAdminGroup = "hr_admin";
        public const string SuperAdminGroup = "super_admin";

        /// <summary>Bảng chỉ mang <c>application_id</c> — cần tra hồ sơ để biết ứng viên + chủ tin.</summary>
        private static readonly HashSet<string> ApplicationScopedTables = new(StringComparer.Ordinal)
        {
            "interview_bookings",
            "online_test_submissions",
            "evaluations",
            "interview_codes",
            "interview_sessions",
            "email_logs",
            "offers",
        };

        /// <summary>
        /// Bảng đã có sẵn <c>job_posting_id</c> trong payload nhưng vẫn phải tra tin để biết CHỦ TIN
        /// (<c>job_postings.created_by_user_id</c> không nằm trên dòng của bảng này).
        /// </summary>
        private static readonly HashSet<string> JobScopedTables = new(StringComparer.Ordinal)
        {
            "availability_slots",
            "hiring_manager_availabilities",
            "job_hiring_team_members",
        };

        public static bool NeedsApplicationLookup(string? table) =>
            table is not null && ApplicationScopedTables.Contains(table);

        public static bool NeedsJobLookup(string? table) =>
            table is not null && JobScopedTables.Contains(table);

        /// <summary>
        /// Đọc payload JSON của pg_notify. Trả null nếu không parse được — listener bỏ qua thay vì chết,
        /// vì một payload hỏng không được phép làm sập kênh realtime của cả hệ thống.
        /// </summary>
        public static DbChangeNotification? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var table = GetString(root, "t");
                if (string.IsNullOrWhiteSpace(table)) return null;

                var routing = root.TryGetProperty("r", out var r) && r.ValueKind == JsonValueKind.Object
                    ? r
                    : default;

                return new DbChangeNotification
                {
                    Table = table!,
                    Op = GetString(root, "op") ?? string.Empty,
                    Id = GetGuid(root, "id"),
                    CandidateAccountId = GetGuid(routing, "candidate_account_id"),
                    RecipientUserId = GetGuid(routing, "recipient_user_id"),
                    JobPostingId = GetGuid(routing, "job_posting_id"),
                    ApplicationId = GetGuid(routing, "application_id"),
                    UserId = GetGuid(routing, "user_id"),
                    CreatedByUserId = GetGuid(routing, "created_by_user_id"),
                    RequestedByUserId = GetGuid(routing, "requested_by_user_id"),
                    AssignedRecruiterId = GetGuid(routing, "assigned_recruiter_id"),
                    Status = GetString(routing, "status"),
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Quyết định người nhận. <paramref name="lookup"/> chỉ cần cho các bảng
        /// <see cref="NeedsApplicationLookup"/>; các bảng khác truyền <see cref="DbChangeLookup.Empty"/>.
        /// </summary>
        public static DbChangeDispatch Resolve(DbChangeNotification? change, DbChangeLookup? lookup = null)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Table)) return DbChangeDispatch.None;
            lookup ??= DbChangeLookup.Empty;

            var users = new List<Guid>();
            var groups = new List<string>();
            var broadcast = false;
            var jobPostingId = change.JobPostingId ?? lookup.JobPostingId;
            var applicationId = change.ApplicationId ?? (change.Table == "applications" ? change.Id : null);

            switch (change.Table)
            {
                // Chuông thông báo dùng chung một bảng cho cả ứng viên lẫn nhân sự:
                // recipient_user_id = nhân sự, candidate_account_id = ứng viên.
                case "notifications":
                    Add(users, change.RecipientUserId ?? change.CandidateAccountId);
                    break;

                case "applications":
                    Add(users, change.CandidateAccountId ?? lookup.CandidateAccountId);
                    Add(users, lookup.JobOwnerUserId);
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Tin tuyển dụng đổi trạng thái ảnh hưởng cả Job Board công khai — giữ đúng phạm vi
                // broadcast mà UpdateJobStatusCommand đang dùng, nhưng payload broadcast chỉ có id.
                case "job_postings":
                    Add(users, change.CreatedByUserId);
                    groups.Add(HrAdminGroup);
                    broadcast = true;
                    jobPostingId ??= change.Id;
                    break;

                case "interview_bookings":
                case "online_test_submissions":
                case "evaluations":
                case "interview_codes":
                case "interview_sessions":
                    Add(users, lookup.CandidateAccountId);
                    Add(users, lookup.JobOwnerUserId);
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Nhật ký thư (ADR-061): tab "Lịch sử email" của nhân sự cập nhật ngay khi thư rời
                // hệ thống. CỐ Ý không gửi ứng viên — họ nhận chính bức thư đó trong hộp thư rồi,
                // còn việc hệ thống ghi log là chuyện nội bộ.
                case "email_logs":
                    Add(users, lookup.JobOwnerUserId);
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Thư mời nhận việc (ADR-061): ứng viên CÓ nhận — họ cần thấy ngay khi thư được
                // gửi hoặc bị thu hồi. Payload chỉ mang khoá, KHÔNG có mức lương.
                case "offers":
                    Add(users, lookup.CandidateAccountId);
                    Add(users, lookup.JobOwnerUserId);
                    // Hiring Manager là NGƯỜI DUYỆT thư mời — thiếu họ ở đây thì hàng chờ duyệt
                    // trên màn của họ đứng im cho tới khi tự tải lại trang.
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Khung giờ + sức chứa là dữ liệu VẬN HÀNH nội bộ: nhân sự sửa sức chứa ở màn cấu
                // hình lịch thì màn Phỏng vấn phải thấy ngay. CỐ Ý không gửi cho ứng viên — họ không
                // có việc gì với sức chứa của ca, và đây là kênh song song với API nên phải giữ
                // đúng phạm vi tối thiểu.
                case "availability_slots":
                    Add(users, lookup.JobOwnerUserId);
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Khung giờ rảnh của Hiring Manager (ADR-067). Người PHẢI biết ngay là Recruiter:
                // họ đang đợi lịch này để xếp ca cho ứng viên, còn màn xếp lịch thì lọc theo nó.
                case "hiring_manager_availabilities":
                    Add(users, lookup.JobOwnerUserId);
                    AddRange(users, lookup.HiringTeamUserIds);
                    groups.Add(HrAdminGroup);
                    break;

                // Đội tuyển dụng của tin (ADR-061). Người vừa được thêm/gỡ phải thấy ngay danh
                // sách tin của mình đổi — đó là toàn bộ phạm vi dữ liệu của một Hiring Manager.
                // CỐ Ý không gửi cho ứng viên: ai duyệt hồ sơ của họ là thông tin nội bộ.
                case "job_hiring_team_members":
                    Add(users, change.UserId);
                    Add(users, lookup.JobOwnerUserId);
                    groups.Add(HrAdminGroup);
                    break;

                case "account_requests":
                    Add(users, change.RequestedByUserId);
                    groups.Add(SuperAdminGroup);
                    break;

                // Phiếu yêu cầu tuyển dụng (ADR-063). Ba bên cùng theo dõi một dòng: HM lập phiếu
                // chờ kết quả duyệt, HR Leader ôm hàng chờ, và Recruiter vừa được phân công cần
                // thấy việc mới xuất hiện ngay. CỐ Ý không có ứng viên — phiếu tồn tại trước khi
                // có tin, chưa gì công khai.
                case "recruitment_requests":
                    Add(users, change.RequestedByUserId);
                    Add(users, change.AssignedRecruiterId);
                    groups.Add(HrAdminGroup);
                    break;

                // Mẫu JD của công ty (ADR-064): cấu hình dùng chung, HR Leader sở hữu. Đổi mẫu thì
                // trình soạn JD đang mở phải thấy ngay — nếu không, Recruiter soạn theo bố cục cũ
                // rồi xuất ra file theo bố cục mới.
                case "jd_templates":
                    groups.Add(HrAdminGroup);
                    break;

                // Bản JD đã soạn cho một phiếu (ADR-064). Chỉ người SOẠN và nhóm HR Leader —
                // `created_by_user_id` vốn đã nằm trong payload nên không phải tra thêm bảng nào.
                //
                // CỐ Ý không gửi cho Hiring Manager dù họ sở hữu phiếu: HM không thao tác gì trên
                // bản nháp JD. Thứ HM cần thấy là file JD đã gắn vào TIN, lúc ký duyệt — sự kiện đó
                // đến từ `job_postings`, không phải bảng này. Gửi rộng hơn phạm vi hành động chỉ tạo
                // ra thông báo không dẫn tới việc gì.
                case "jd_documents":
                    Add(users, change.CreatedByUserId);
                    groups.Add(HrAdminGroup);
                    break;

                // Danh sách đội (ADR-065): Super Admin quản lý, nhưng HR Leader cũng cần thấy ngay —
                // họ chọn đội khi lập phiếu hộ, mà danh sách cũ thì chọn phải một đội vừa bị tắt.
                case "departments":
                    groups.Add(SuperAdminGroup);
                    groups.Add(HrAdminGroup);
                    break;

                case "users":
                case "system_settings":
                    groups.Add(SuperAdminGroup);
                    break;

                // Hồ sơ cá nhân của chính ứng viên (id của row chính là id tài khoản).
                case "candidate_accounts":
                    Add(users, change.Id);
                    break;

                // Bảng chưa định tuyến (refresh_tokens, magic_links, audit_logs, document_chunks,
                // questions, answers...) dừng lại ở đây: KHÔNG gửi cho ai. Mặc định đóng là chủ ý —
                // trigger gắn trên toàn bộ bảng nên đây là chốt chặn cuối.
                default:
                    return DbChangeDispatch.None;
            }

            if (users.Count == 0 && groups.Count == 0 && !broadcast) return DbChangeDispatch.None;

            return new DbChangeDispatch
            {
                UserIds = users,
                RoleGroups = groups,
                BroadcastAll = broadcast,
                Payload = new Dictionary<string, object?>
                {
                    ["t"] = change.Table,
                    ["op"] = change.Op,
                    ["id"] = change.Id,
                    ["jobPostingId"] = jobPostingId,
                    ["applicationId"] = applicationId,
                },
                BroadcastPayload = new Dictionary<string, object?>
                {
                    ["t"] = change.Table,
                    ["op"] = change.Op,
                    ["id"] = change.Id,
                },
            };
        }

        private static void Add(List<Guid> target, Guid? value)
        {
            if (value is { } id && id != Guid.Empty && !target.Contains(id)) target.Add(id);
        }

        /// <summary>Thêm cả danh sách, bỏ trùng — chủ tin cũng có thể nằm trong đội tuyển dụng.</summary>
        private static void AddRange(List<Guid> target, IReadOnlyList<Guid>? values)
        {
            if (values is null) return;
            foreach (var value in values) Add(target, value);
        }

        private static string? GetString(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static Guid? GetGuid(JsonElement parent, string name) =>
            Guid.TryParse(GetString(parent, name), out var g) ? g : null;
    }
}
