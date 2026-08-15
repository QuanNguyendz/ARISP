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
        };

        public static bool NeedsApplicationLookup(string? table) =>
            table is not null && ApplicationScopedTables.Contains(table);

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
                    Add(users, lookup.CandidateAccountId);
                    Add(users, lookup.JobOwnerUserId);
                    groups.Add(HrAdminGroup);
                    break;

                case "account_requests":
                    Add(users, change.RequestedByUserId);
                    groups.Add(SuperAdminGroup);
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
