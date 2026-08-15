using System;
using System.Linq;
using ARI.Application.Realtime;
using Xunit;

namespace ARI.Application.UnitTests.Realtime;

/// <summary>
/// ADR-057 — realtime phát từ tầng database. Trigger gắn trên TOÀN BỘ bảng nên router là chốt chặn
/// quyết định dữ liệu nào được rời khỏi server: bảng chưa định tuyến phải im lặng tuyệt đối.
/// </summary>
public class DbChangeRouterTests
{
    private static string Payload(string table, string op = "U", Guid? id = null, string routing = "{}") =>
        $$"""{"t":"{{table}}","op":"{{op}}","id":"{{id ?? Guid.NewGuid()}}","r":{{routing}}}""";

    // ---------- Parse ----------

    [Fact]
    public void Parse_doc_du_khoa_dinh_tuyen()
    {
        var id = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var change = DbChangeRouter.Parse(Payload("applications", "I", id,
            $$"""{"candidate_account_id":"{{candidateId}}","job_posting_id":"{{jobId}}","status":"screening"}"""));

        Assert.NotNull(change);
        Assert.Equal("applications", change!.Table);
        Assert.Equal("I", change.Op);
        Assert.Equal(id, change.Id);
        Assert.Equal(candidateId, change.CandidateAccountId);
        Assert.Equal(jobId, change.JobPostingId);
        Assert.Equal("screening", change.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ khong phai json")]
    [InlineData("""{"op":"U"}""")]          // thiếu tên bảng
    [InlineData("""{"t":"","op":"U"}""")]   // tên bảng rỗng
    public void Parse_payload_hong_tra_null_thay_vi_nem_loi(string? json)
    {
        // Một payload hỏng không được phép làm chết kênh realtime của cả hệ thống.
        Assert.Null(DbChangeRouter.Parse(json));
    }

    [Fact]
    public void Parse_bo_qua_khoa_khong_phai_guid()
    {
        var change = DbChangeRouter.Parse(Payload("notifications", routing: """{"recipient_user_id":"khong-phai-guid"}"""));

        Assert.NotNull(change);
        Assert.Null(change!.RecipientUserId);
    }

    [Fact]
    public void Parse_doc_duoc_su_kien_statement_level()
    {
        var change = DbChangeRouter.Parse("""{"t":"document_chunks","op":"S"}""");

        Assert.NotNull(change);
        Assert.Equal("S", change!.Op);
        Assert.Null(change.Id);
    }

    // ---------- Chốt chặn: bảng chưa định tuyến ----------

    [Theory]
    [InlineData("refresh_tokens")]
    [InlineData("candidate_refresh_tokens")]
    [InlineData("magic_links")]
    [InlineData("audit_logs")]
    [InlineData("document_chunks")]
    [InlineData("questions")]
    [InlineData("answers")]
    [InlineData("bang_la_khong_ton_tai")]
    public void Bang_chua_dinh_tuyen_khong_gui_cho_ai(string table)
    {
        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload(table)));

        Assert.False(dispatch.HasRecipients);
        Assert.Empty(dispatch.UserIds);
        Assert.Empty(dispatch.RoleGroups);
        Assert.False(dispatch.BroadcastAll);
    }

    [Fact]
    public void Change_null_khong_gui_cho_ai()
    {
        Assert.False(DbChangeRouter.Resolve(null).HasRecipients);
    }

    // ---------- Định tuyến từng bảng ----------

    [Fact]
    public void Notifications_uu_tien_nhan_su_roi_moi_den_ung_vien()
    {
        var staffId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("notifications",
            routing: $$"""{"recipient_user_id":"{{staffId}}","candidate_account_id":"{{candidateId}}"}""")));

        Assert.Equal(new[] { staffId }, dispatch.UserIds);
        Assert.Empty(dispatch.RoleGroups);
    }

    [Fact]
    public void Notifications_cua_ung_vien_gui_dung_ung_vien()
    {
        var candidateId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("notifications",
            routing: $$"""{"candidate_account_id":"{{candidateId}}"}""")));

        Assert.Equal(new[] { candidateId }, dispatch.UserIds);
    }

    [Fact]
    public void Applications_gui_ung_vien_chu_tin_va_nhom_hr()
    {
        var candidateId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var appId = Guid.NewGuid();

        var change = DbChangeRouter.Parse(Payload("applications", "U", appId,
            $$"""{"candidate_account_id":"{{candidateId}}"}"""));

        var dispatch = DbChangeRouter.Resolve(change, new DbChangeLookup { JobOwnerUserId = ownerId });

        Assert.Contains(candidateId, dispatch.UserIds);
        Assert.Contains(ownerId, dispatch.UserIds);
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
        Assert.False(dispatch.BroadcastAll);
        Assert.Equal(appId, dispatch.Payload["applicationId"]);
    }

    [Fact]
    public void Job_postings_broadcast_nhung_payload_broadcast_khong_kem_khoa_noi_bo()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("job_postings", "U", jobId,
            $$"""{"created_by_user_id":"{{ownerId}}","status":"active"}""")));

        Assert.Contains(ownerId, dispatch.UserIds);
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
        Assert.True(dispatch.BroadcastAll);

        // Job Board công khai chỉ cần biết "tin nào vừa đổi" — không kèm id chủ tin hay hồ sơ.
        Assert.Equal(new[] { "t", "op", "id" }, dispatch.BroadcastPayload.Keys.ToArray());
    }

    [Fact]
    public void Bang_theo_ho_so_dung_ket_qua_tra_cuu()
    {
        var candidateId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var appId = Guid.NewGuid();

        foreach (var table in new[] { "interview_bookings", "online_test_submissions", "evaluations", "interview_codes", "interview_sessions" })
        {
            Assert.True(DbChangeRouter.NeedsApplicationLookup(table));

            var change = DbChangeRouter.Parse(Payload(table, routing: $$"""{"application_id":"{{appId}}"}"""));
            var dispatch = DbChangeRouter.Resolve(change, new DbChangeLookup
            {
                CandidateAccountId = candidateId,
                JobOwnerUserId = ownerId,
            });

            Assert.Contains(candidateId, dispatch.UserIds);
            Assert.Contains(ownerId, dispatch.UserIds);
            Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
            Assert.Equal(appId, dispatch.Payload["applicationId"]);
        }
    }

    // ---------- Khung giờ / sức chứa ----------

    [Fact]
    public void Availability_slots_gui_chu_tin_va_nhom_hr()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var change = DbChangeRouter.Parse(Payload("availability_slots", routing: $$"""{"job_posting_id":"{{jobId}}"}"""));
        var dispatch = DbChangeRouter.Resolve(change, new DbChangeLookup { JobPostingId = jobId, JobOwnerUserId = ownerId });

        Assert.Equal(new[] { ownerId }, dispatch.UserIds);
        Assert.Equal(new[] { DbChangeRouter.HrAdminGroup }, dispatch.RoleGroups);
        Assert.False(dispatch.BroadcastAll);
        Assert.Equal(jobId, dispatch.Payload["jobPostingId"]);
    }

    /// <summary>
    /// Sức chứa là dữ liệu vận hành nội bộ. Kể cả khi lookup vô tình mang theo id ứng viên thì
    /// router cũng không được gửi cho họ — kênh realtime chạy song song với API nên phải giữ đúng
    /// phạm vi tối thiểu.
    /// </summary>
    [Fact]
    public void Availability_slots_khong_gui_cho_ung_vien()
    {
        var candidateId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("availability_slots")),
            new DbChangeLookup { CandidateAccountId = candidateId, JobOwnerUserId = ownerId });

        Assert.DoesNotContain(candidateId, dispatch.UserIds);
        Assert.Contains(ownerId, dispatch.UserIds);
    }

    [Fact]
    public void Availability_slots_can_tra_cuu_chu_tin_chu_khong_tra_ho_so()
    {
        Assert.True(DbChangeRouter.NeedsJobLookup("availability_slots"));
        Assert.False(DbChangeRouter.NeedsApplicationLookup("availability_slots"));
    }

    /// <summary>Chặn việc mở rộng NeedsJobLookup quá tay: các bảng khác không được đi qua nhánh này.</summary>
    [Theory]
    [InlineData("job_postings")]
    [InlineData("applications")]
    [InlineData("interview_bookings")]
    [InlineData("notifications")]
    [InlineData(null)]
    public void Bang_khac_khong_can_tra_cuu_tin(string? table)
    {
        Assert.False(DbChangeRouter.NeedsJobLookup(table));
    }

    /// <summary>Tin đã bị xoá cứng thì không còn chủ tin — sự kiện vẫn phải tới được nhóm hr_admin.</summary>
    [Fact]
    public void Availability_slots_khong_co_chu_tin_van_gui_nhom_hr()
    {
        var jobId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("availability_slots", routing: $$"""{"job_posting_id":"{{jobId}}"}""")),
            new DbChangeLookup { JobPostingId = jobId });

        Assert.Empty(dispatch.UserIds);
        Assert.Equal(new[] { DbChangeRouter.HrAdminGroup }, dispatch.RoleGroups);
    }

    [Fact]
    public void Account_requests_gui_nguoi_de_xuat_va_super_admin()
    {
        var requesterId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("account_requests",
            routing: $$"""{"requested_by_user_id":"{{requesterId}}"}""")));

        Assert.Equal(new[] { requesterId }, dispatch.UserIds);
        Assert.Equal(new[] { DbChangeRouter.SuperAdminGroup }, dispatch.RoleGroups);
    }

    [Fact]
    public void Users_va_system_settings_chi_gui_super_admin()
    {
        foreach (var table in new[] { "users", "system_settings" })
        {
            var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload(table)));

            Assert.Empty(dispatch.UserIds);
            Assert.Equal(new[] { DbChangeRouter.SuperAdminGroup }, dispatch.RoleGroups);
            Assert.False(dispatch.BroadcastAll);
        }
    }

    [Fact]
    public void Candidate_accounts_gui_dung_chu_tai_khoan()
    {
        var accountId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("candidate_accounts", "U", accountId)));

        Assert.Equal(new[] { accountId }, dispatch.UserIds);
        Assert.Empty(dispatch.RoleGroups);
    }

    // ---------- Chi tiết ----------

    [Fact]
    public void Khong_gui_trung_mot_nguoi_hai_lan()
    {
        // Ứng viên tự ứng tuyển vào tin của chính mình phụ trách là trường hợp không có thật, nhưng
        // chủ tin trùng ứng viên trong dữ liệu seed/test thì có — không được đẩy 2 lần cùng một event.
        var sameId = Guid.NewGuid();

        var change = DbChangeRouter.Parse(Payload("applications", routing: $$"""{"candidate_account_id":"{{sameId}}"}"""));
        var dispatch = DbChangeRouter.Resolve(change, new DbChangeLookup { JobOwnerUserId = sameId });

        Assert.Single(dispatch.UserIds);
    }

    [Fact]
    public void Guid_rong_khong_duoc_coi_la_nguoi_nhan()
    {
        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("notifications",
            routing: $$"""{"recipient_user_id":"{{Guid.Empty}}"}""")));

        Assert.False(dispatch.HasRecipients);
    }

    [Fact]
    public void Payload_gui_di_chi_chua_khoa_khong_chua_noi_dung_ban_ghi()
    {
        var dispatch = DbChangeRouter.Resolve(DbChangeRouter.Parse(Payload("notifications",
            routing: $$"""{"recipient_user_id":"{{Guid.NewGuid()}}","status":"unread"}""")));

        Assert.Equal(new[] { "t", "op", "id", "jobPostingId", "applicationId" }, dispatch.Payload.Keys.ToArray());
    }
}
