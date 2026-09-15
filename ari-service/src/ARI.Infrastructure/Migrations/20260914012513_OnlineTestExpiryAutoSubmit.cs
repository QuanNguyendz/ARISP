using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Bài trắc nghiệm HẾT HẠN = hệ thống nộp thay, không còn là "không tham dự".
    ///
    /// <b>1. Cột <c>online_test_submissions.submitted_by</c></b> (<c>candidate</c> | <c>system</c>).
    /// Mặc định <c>candidate</c> cho mọi bài cũ — trước đây chỉ ứng viên mới nộp được bài. Phải là cột
    /// riêng chứ không suy từ "bài trống": người vào làm rồi bỏ trắng hết cũng ra 0 điểm.
    ///
    /// <b>2. Sửa dữ liệu tác vụ nền đã làm hỏng.</b> Tác vụ tự đánh trượt người vắng mặt (ADR-059)
    /// từng quét cả vòng trắc nghiệm như vòng phỏng vấn: quá giờ mà không có phiên phỏng vấn thật
    /// (vòng này không bao giờ có) thì huỷ lịch, trả chỗ trong ca, ghi hồ sơ <c>not_pass</c> và báo
    /// ứng viên "Hồ sơ dừng lại do không tham dự". Kể cả người đã nộp bài.
    ///
    /// Chỉ khôi phục những dòng CHẮC CHẮN là do tác vụ đó gây ra và chưa ai đụng vào sau đó:
    /// <list type="bullet">
    /// <item>lịch <c>cancelled</c> + <c>declined_by = 'system'</c> ở một vòng cấu hình
    /// <c>online_test</c>;</item>
    /// <item>hồ sơ đang <c>not_pass</c> và <c>updated_at</c> khớp (±1 phút) với <c>updated_at</c> của
    /// lịch đó — tác vụ ghi cả hai trong CÙNG một lần lưu. Recruiter đã thao tác gì với hồ sơ sau
    /// đó (kể cả tự tay loại) thì mốc này lệch và dòng bị bỏ qua;</item>
    /// <item>hồ sơ + vòng đó chưa có lịch nào khác đang giữ chỗ.</item>
    /// </list>
    /// Lịch trở lại <c>scheduled</c> (xác nhận về <c>pending</c> — trạng thái trước đó đã bị ghi đè,
    /// không đoán), hồ sơ trở lại <c>interview</c> — trạng thái duy nhất một hồ sơ có lịch giữ chỗ
    /// có thể đang ở. <c>booked_count</c> của các ca liên quan TÍNH LẠI từ dòng booking (ADR-058),
    /// không cộng tay. Thông báo "dừng lại do không tham dự" bị xoá mềm vì nó sai.
    ///
    /// Sau migration, người chưa có bài sẽ được tác vụ nền nộp thay (0 điểm) ở lượt quét kế tiếp;
    /// người đã nộp thì giữ nguyên bài và điểm của mình.
    ///
    /// <c>Down()</c> chỉ gỡ cột: không làm hỏng lại dữ liệu đã sửa.
    /// Không có bảng mới nên không cần gọi lại <c>arisp_attach_change_triggers()</c> (quy tắc 24).
    /// </summary>
    public partial class OnlineTestExpiryAutoSubmit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "submitted_by",
                table: "online_test_submissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "candidate");

            migrationBuilder.Sql(@"
DO $$
DECLARE
    restored_bookings integer;
    restored_apps     integer;
BEGIN
    CREATE TEMP TABLE _online_test_wrong_no_show ON COMMIT DROP AS
    SELECT DISTINCT ON (b.application_id, b.round_number)
           b.id AS booking_id,
           b.application_id,
           b.round_number,
           b.availability_slot_id
      FROM interview_bookings b
      JOIN availability_slots s ON s.id = b.availability_slot_id
      JOIN applications a       ON a.id = b.application_id
     WHERE b.status = 'cancelled'
       AND b.declined_by = 'system'
       AND a.status = 'not_pass'
       AND a.deleted_at IS NULL
       AND a.updated_at BETWEEN b.updated_at - INTERVAL '1 minute' AND b.updated_at + INTERVAL '1 minute'
       AND EXISTS (
           SELECT 1 FROM interview_round_configs r
            WHERE r.job_posting_id = s.job_posting_id
              AND r.round_number   = b.round_number
              AND lower(btrim(r.round_type)) = 'online_test')
       AND NOT EXISTS (
           SELECT 1 FROM interview_bookings o
            WHERE o.application_id = b.application_id
              AND o.round_number   = b.round_number
              AND o.status = 'scheduled')
     ORDER BY b.application_id, b.round_number, b.updated_at DESC;

    UPDATE interview_bookings b
       SET status              = 'scheduled',
           confirmation_status = 'pending',
           declined_by         = NULL,
           decline_reason      = NULL,
           responded_at        = NULL,
           updated_at          = NOW()
      FROM _online_test_wrong_no_show w
     WHERE b.id = w.booking_id;
    GET DIAGNOSTICS restored_bookings = ROW_COUNT;

    UPDATE applications a
       SET status     = 'interview',
           updated_at = NOW()
      FROM _online_test_wrong_no_show w
     WHERE a.id = w.application_id
       AND a.status = 'not_pass';
    GET DIAGNOSTICS restored_apps = ROW_COUNT;

    -- ADR-058: booked_count = số dòng 'scheduled'. Tính lại từ dòng, không cộng tay.
    UPDATE availability_slots s
       SET booked_count = (SELECT count(*) FROM interview_bookings b
                            WHERE b.availability_slot_id = s.id
                              AND b.status = 'scheduled'),
           updated_at   = NOW()
     WHERE s.id IN (SELECT availability_slot_id FROM _online_test_wrong_no_show);

    UPDATE notifications n
       SET deleted_at = NOW(),
           updated_at = NOW()
      FROM _online_test_wrong_no_show w
     WHERE n.dedup_key = 'no_show_closed:' || w.application_id::text || ':' || w.round_number::text
       AND n.deleted_at IS NULL;

    IF restored_bookings > 0 THEN
        RAISE NOTICE 'OnlineTestExpiryAutoSubmit: khoi phuc % lich thi trac nghiem va % ho so bi tac vu no-show danh truot nham.',
            restored_bookings, restored_apps;
    END IF;

    DROP TABLE IF EXISTS _online_test_wrong_no_show;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "submitted_by",
                table: "online_test_submissions");
        }
    }
}
