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

    /// <summary>
    /// Đội tuyển dụng (ADR-061) đọc được hồ sơ, báo cáo AI và thư mời của tin qua API, nên kênh
    /// realtime phải phủ đúng bấy nhiêu. Thiếu họ ở đây thì màn của Hiring Manager đứng im và họ
    /// phải F5 tay để biết có việc mới — đúng thứ ADR-057 sinh ra để xoá bỏ.
    /// </summary>
    [Theory]
    [InlineData("applications")]
    [InlineData("evaluations")]
    [InlineData("offers")]
    [InlineData("email_logs")]
    [InlineData("availability_slots")]
    public void Doi_tuyen_dung_nhan_su_kien_cua_tin_minh_phu_trach(string table)
    {
        var hiringManagerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload(table)),
            new DbChangeLookup
            {
                JobOwnerUserId = ownerId,
                HiringTeamUserIds = new[] { hiringManagerId },
            });

        Assert.Contains(hiringManagerId, dispatch.UserIds);
        Assert.Contains(ownerId, dispatch.UserIds);
    }

    /// <summary>
    /// Chủ tin cũng có thể là thành viên đội của chính tin mình. Gửi hai lần cho cùng một người là
    /// hai thông báo trùng trên chuông.
    /// </summary>
    [Fact]
    public void Nguoi_vua_la_chu_tin_vua_trong_doi_chi_nhan_mot_lan()
    {
        var sameId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("offers")),
            new DbChangeLookup { JobOwnerUserId = sameId, HiringTeamUserIds = new[] { sameId, sameId } });

        Assert.Single(dispatch.UserIds, id => id == sameId);
    }

    /// <summary>
    /// Nhật ký thư là chuyện nội bộ: ứng viên đã nhận chính bức thư đó trong hộp thư của họ. Kể cả
    /// khi lookup mang theo id ứng viên thì router cũng không được gửi.
    /// </summary>
    [Fact]
    public void Email_logs_khong_gui_cho_ung_vien()
    {
        var candidateId = Guid.NewGuid();
        var hiringManagerId = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("email_logs")),
            new DbChangeLookup
            {
                CandidateAccountId = candidateId,
                HiringTeamUserIds = new[] { hiringManagerId },
            });

        Assert.DoesNotContain(candidateId, dispatch.UserIds);
        Assert.Contains(hiringManagerId, dispatch.UserIds);
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

    // ---------- Đội tuyển dụng của tin (ADR-061) ----------

    [Fact]
    public void Job_hiring_team_members_gui_nguoi_duoc_gan_chu_tin_va_nhom_hr()
    {
        var assignedUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var change = DbChangeRouter.Parse(Payload("job_hiring_team_members", "I",
            routing: $$"""{"user_id":"{{assignedUserId}}","job_posting_id":"{{Guid.NewGuid()}}"}"""));
        var dispatch = DbChangeRouter.Resolve(change, new DbChangeLookup { JobOwnerUserId = ownerId });

        Assert.Contains(assignedUserId, dispatch.UserIds);   // danh sách tin của HM vừa đổi
        Assert.Contains(ownerId, dispatch.UserIds);
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
    }

    [Fact]
    public void Recruitment_request_bao_cho_hm_recruiter_duoc_giao_va_hr_leader()
    {
        var hm = Guid.NewGuid();
        var recruiter = Guid.NewGuid();

        // Đi qua Parse để khoá luôn việc payload chở được `assigned_recruiter_id` — khoá này do
        // migration thêm vào `arisp_notify_change()`, quên thì Recruiter được giao việc không nhận
        // được gì mà không có lỗi nào.
        var change = DbChangeRouter.Parse(Payload("recruitment_requests", "U",
            routing: $$"""{"requested_by_user_id":"{{hm}}","assigned_recruiter_id":"{{recruiter}}","status":"approved"}"""));

        var dispatch = DbChangeRouter.Resolve(change, DbChangeLookup.Empty);

        Assert.Contains(hm, dispatch.UserIds);           // người lập phiếu chờ kết quả duyệt
        Assert.Contains(recruiter, dispatch.UserIds);    // người vừa được giao việc
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
        Assert.False(dispatch.BroadcastAll);             // phiếu là việc nội bộ, chưa có gì công khai
        Assert.Equal(2, dispatch.UserIds.Count);         // đúng hai người, không rộng hơn
    }

    [Fact]
    public void Ban_jd_da_soan_chi_gui_nguoi_soan_va_nhom_hr()
    {
        // ADR-064: HM sở hữu phiếu nhưng KHÔNG thao tác gì trên bản nháp JD — thứ họ cần thấy là
        // file JD đã gắn vào tin, lúc ký duyệt, và sự kiện đó đến từ `job_postings`.
        var author = Guid.NewGuid();

        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("jd_documents", "U",
                routing: $$"""{"created_by_user_id":"{{author}}"}""")),
            DbChangeLookup.Empty);

        Assert.Equal(new[] { author }, dispatch.UserIds.ToArray());
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
        Assert.False(dispatch.BroadcastAll);
    }

    [Fact]
    public void Mau_jd_chi_gui_nhom_hr()
    {
        var dispatch = DbChangeRouter.Resolve(
            DbChangeRouter.Parse(Payload("jd_templates")), DbChangeLookup.Empty);

        Assert.Empty(dispatch.UserIds);
        Assert.Contains(DbChangeRouter.HrAdminGroup, dispatch.RoleGroups);
    }

    [Fact]
    public void Job_hiring_team_members_can_tra_chu_tin()
    {
        // Bảng này chỉ mang job_posting_id, không mang created_by_user_id — phải nằm trong nhóm
        // cần tra tin, nếu không chủ tin sẽ không bao giờ nhận được sự kiện.
        Assert.True(DbChangeRouter.NeedsJobLookup("job_hiring_team_members"));
    }

    // ---------- Chốt chặn: bảng mới không được lọt qua mà không ai quyết định ----------

    /// <summary>
    /// Mọi entity trong Domain phải được xếp DỨT KHOÁT vào một trong hai nhóm dưới đây.
    /// Thêm entity mới sẽ làm test này đỏ — buộc người thêm phải trả lời "ai được nhận realtime
    /// của bảng này", thay vì để nó rơi vào nhánh mặc định im lặng và phát hiện ra sau nhiều tháng.
    /// </summary>
    private static readonly string[] EntitiesWithRealtimeRouting =
    {
        "Notification", "Application", "JobPosting", "InterviewBooking", "OnlineTestSubmission",
        "Evaluation", "InterviewCode", "InterviewSession", "AvailabilitySlot", "AccountRequest",
        "User", "SystemSetting", "CandidateAccount", "JobHiringTeamMember", "EmailLog", "Offer",
        "RecruitmentRequest", "JdTemplate", "JdDocument", "Department",
        "HiringManagerAvailability",
    };

    /// <summary>
    /// CỐ Ý không định tuyến — không ai cần biết realtime, hoặc là dữ liệu nhạy cảm/nội bộ:
    /// token đăng nhập, nhật ký kiểm toán, chunk vector do rag-service ghi hàng loạt, nội dung
    /// hỏi–đáp của buổi phỏng vấn (đi qua SessionHub riêng, không qua kênh này).
    /// </summary>
    private static readonly string[] EntitiesIntentionallyUnrouted =
    {
        "RefreshToken", "CandidateRefreshToken", "MagicLink", "AuditLog", "DocumentChunk",
        "Question", "Answer", "PlaybookDocument", "MustAskTracking", "CheatDetectionSignal",
        "WebhookDelivery", "OnlineTestQuestion", "CvJdAnalysis", "SavedJob", "HrReview",
        "InterviewInvite", "InterviewRoundConfig",
    };

    [Fact]
    public void Moi_entity_deu_phai_duoc_quyet_dinh_dinh_tuyen()
    {
        var entityTypes = typeof(ARI.Domain.Entities.JobPosting).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "ARI.Domain.Entities")
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var decided = EntitiesWithRealtimeRouting.Concat(EntitiesIntentionallyUnrouted)
            .ToHashSet(StringComparer.Ordinal);

        var undecided = entityTypes.Except(decided).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(undecided.Count == 0,
            "Entity chưa được quyết định định tuyến realtime: " + string.Join(", ", undecided) +
            ". Thêm vào EntitiesWithRealtimeRouting (kèm case trong DbChangeRouter) hoặc " +
            "EntitiesIntentionallyUnrouted (kèm lý do).");
    }
}
