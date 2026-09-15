using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-068 — mọi tin luôn có ĐÚNG MỘT Hiring Manager chính. Migration CHỈ sửa dữ liệu, không đổi
    /// cấu trúc (không có bảng mới nên không cần gọi lại <c>arisp_attach_change_triggers()</c> — quy tắc 24).
    ///
    /// Trước ADR-068, "tin không có HM" là một đường chạy hợp lệ (mọi cổng duyệt MỞ), và HM mất khỏi tin
    /// bằng nhiều cách: tin dựng trước khi có luật tự gán người lập phiếu, Recruiter gỡ HM khỏi đội, hoặc
    /// đặt một "observer" làm primary (hạ HM thật xuống). Nay thiếu HM là cổng ĐÓNG, nên phải đưa dữ liệu
    /// về đúng bất biến ở mức làm được mà KHÔNG đoán:
    /// <list type="number">
    /// <item><b>Primary không phải HM</b> (observer / interviewer mang cờ primary) → hạ cờ. Cờ đó chỉ còn
    /// là tàn dư của lỗ hổng, và nó chặn index UNIQUE <c>ux_job_hiring_team_members_primary</c> khi gán HM thật.</item>
    /// <item><b>Tin dựng từ phiếu mà không có HM chính</b> → người LẬP PHIẾU làm HM chính: đúng luật đã có
    /// từ ADR-063, và là suy luận duy nhất không cần đoán. Người đó từng ở trong đội (bị gỡ) thì HỒI SINH
    /// dòng cũ — UNIQUE <c>(job, user)</c> cố ý không lọc <c>deleted_at</c>.</item>
    /// <item><b>Tin không có phiếu và không có HM</b> (dữ liệu có trước ADR-063) → KHÔNG tự gán ai: bịa HM
    /// là bịa người chịu trách nhiệm tuyển dụng. <c>RAISE NOTICE</c> liệt kê để HR Leader gán qua lệnh chuyển
    /// HM (tiền lệ ADR-065); cho tới lúc đó cổng của tin đóng.</item>
    /// </list>
    /// Người lập phiếu đã bị khoá vẫn được gán — bất biến "có HM" giữ nguyên, và màn tin sẽ báo "HM không
    /// còn hoạt động" để HR Leader chuyển. Chạy lại lần hai không đổi gì (mọi bước lọc theo "chưa có HM").
    ///
    /// <c>Down()</c> để trống: gỡ HM khỏi tin là mở lại đúng lỗ hổng ADR-068 vá, và không phân biệt được dòng
    /// nào do migration này tạo với dòng HR Leader gán sau đó.
    /// </summary>
    public partial class BackfillPrimaryHiringManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    demoted   integer;
    revived   integer;
    inserted  integer;
    orphans   integer;
BEGIN
    -- 1) Cờ primary chỉ thuộc về vai trò hiring_manager.
    UPDATE job_hiring_team_members
       SET is_primary = false, updated_at = NOW()
     WHERE is_primary = true
       AND deleted_at IS NULL
       AND role_on_job <> 'hiring_manager';
    GET DIAGNOSTICS demoted = ROW_COUNT;

    -- Tin dựng từ phiếu, còn sống, chưa có HM chính.
    CREATE TEMP TABLE _jobs_missing_hm ON COMMIT DROP AS
    SELECT j.id AS job_id, j.created_by_user_id, r.requested_by_user_id
      FROM job_postings j
      JOIN recruitment_requests r ON r.id = j.recruitment_request_id
     WHERE j.deleted_at IS NULL
       AND NOT EXISTS (
           SELECT 1 FROM job_hiring_team_members p
            WHERE p.job_posting_id = j.id
              AND p.is_primary = true
              AND p.deleted_at IS NULL
              AND p.role_on_job = 'hiring_manager');

    -- 2a) Người lập phiếu đã có dòng trong đội (kể cả đã gỡ) → nâng / hồi sinh đúng dòng đó.
    UPDATE job_hiring_team_members m
       SET deleted_at  = NULL,
           role_on_job = 'hiring_manager',
           is_primary  = true,
           updated_at  = NOW()
      FROM _jobs_missing_hm x
     WHERE m.job_posting_id = x.job_id
       AND m.user_id = x.requested_by_user_id;
    GET DIAGNOSTICS revived = ROW_COUNT;

    -- 2b) Chưa từng có dòng nào → thêm mới; người gán ghi là chủ tin (người dựng tin từ phiếu).
    INSERT INTO job_hiring_team_members
           (id, job_posting_id, user_id, role_on_job, is_primary, added_by_user_id, created_at, updated_at)
    SELECT gen_random_uuid(), x.job_id, x.requested_by_user_id, 'hiring_manager', true,
           x.created_by_user_id, NOW(), NOW()
      FROM _jobs_missing_hm x
     WHERE NOT EXISTS (
           SELECT 1 FROM job_hiring_team_members m
            WHERE m.job_posting_id = x.job_id
              AND m.user_id = x.requested_by_user_id);
    GET DIAGNOSTICS inserted = ROW_COUNT;

    IF demoted + revived + inserted > 0 THEN
        RAISE NOTICE 'ADR-068: ha co primary cua % thanh vien khong phai HM; gan nguoi lap phieu lam HM chinh cho % tin (% dong hoi sinh, % dong moi).',
            demoted, revived + inserted, revived, inserted;
    END IF;

    -- 3) Tin không có phiếu và không có HM: không đoán, chỉ liệt kê.
    SELECT count(*) INTO orphans
      FROM job_postings j
     WHERE j.deleted_at IS NULL
       AND NOT EXISTS (
           SELECT 1 FROM job_hiring_team_members p
            WHERE p.job_posting_id = j.id
              AND p.is_primary = true
              AND p.deleted_at IS NULL
              AND p.role_on_job = 'hiring_manager');

    IF orphans > 0 THEN
        RAISE NOTICE 'ADR-068: % tin chua co Hiring Manager (tin co truoc ADR-063, khong co phieu) - cong duyet cua cac tin nay DONG cho toi khi HR Leader gan HM: %',
            orphans,
            (SELECT string_agg(j.title || ' [' || j.status || '] ' || j.id::text, '; ' ORDER BY j.created_at)
               FROM job_postings j
              WHERE j.deleted_at IS NULL
                AND NOT EXISTS (
                    SELECT 1 FROM job_hiring_team_members p
                     WHERE p.job_posting_id = j.id
                       AND p.is_primary = true
                       AND p.deleted_at IS NULL
                       AND p.role_on_job = 'hiring_manager'));
    END IF;

    DROP TABLE IF EXISTS _jobs_missing_hm;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý để trống — xem chú thích lớp.
        }
    }
}
