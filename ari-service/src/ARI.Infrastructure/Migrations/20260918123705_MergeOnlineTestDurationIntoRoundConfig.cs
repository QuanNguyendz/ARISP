using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Thời lượng bài thi trắc nghiệm về MỘT nguồn: <c>interview_round_configs.max_duration_minutes</c>
    /// của vòng <c>online_test</c> (ADR-072).
    ///
    /// Trước đây có hai ô cùng tên "thời lượng": "Số phút" của vòng ở màn tạo tin
    /// (<c>interview_round_configs.max_duration_minutes</c>) và "Thời lượng" ở màn ngân hàng đề
    /// (<c>job_postings.online_test_duration_minutes</c>). Bài thi chỉ đọc cột thứ hai, nên số phút khai
    /// lúc tạo tin không có tác dụng gì, còn màn tin lại hiện chính con số vô tác dụng đó.
    ///
    /// <b>1. Chép giá trị ĐANG CÓ HIỆU LỰC sang vòng.</b> Cột trên <c>job_postings</c> là thứ đồng hồ bài
    /// thi thật sự đã dùng, nên nó thắng con số trên vòng. Giá trị ngoài 1–300 (không cửa nào ghi ra được,
    /// nhưng không đoán) về mặc định 30.
    ///
    /// <b>2. Giờ kết thúc của ca thi = giờ đóng bài.</b> Bài thi nay đóng lúc giờ hẹn + thời lượng; giờ kết
    /// thúc gõ tay trên ca thi (vd. 09:00–17:00) chưa từng có tác dụng gì với bài thi — trước đây cửa vào
    /// luôn là 1 tiếng kể từ giờ hẹn — nhưng vẫn được in lên màn xếp lịch và Portal. Đồng bộ lại để mọi
    /// nơi hiển thị đúng khung giờ bài thi mở. Chỉ chạm ca của vòng <c>online_test</c>.
    ///
    /// <b>3. Bỏ cột</b> <c>job_postings.online_test_duration_minutes</c> — hai nguồn cho một sự thật là trôi
    /// lệch (bài học ADR-058/065).
    ///
    /// <c>Down()</c> dựng lại cột và chép ngược từ vòng trắc nghiệm đầu tiên của mỗi tin; giờ kết thúc cũ
    /// của ca thi không khôi phục được (không còn dấu vết) — vô hại vì chúng chưa từng có tác dụng.
    /// Không có bảng mới nên không cần gọi lại <c>arisp_attach_change_triggers()</c> (quy tắc 24).
    /// </summary>
    public partial class MergeOnlineTestDurationIntoRoundConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    copied_rounds integer;
    synced_slots  integer;
BEGIN
    UPDATE interview_round_configs r
       SET max_duration_minutes = CASE
               WHEN j.online_test_duration_minutes BETWEEN 1 AND 300 THEN j.online_test_duration_minutes
               ELSE 30
           END
      FROM job_postings j
     WHERE r.job_posting_id = j.id
       AND lower(btrim(r.round_type)) = 'online_test';
    GET DIAGNOSTICS copied_rounds = ROW_COUNT;

    UPDATE availability_slots s
       SET end_time   = s.start_time + make_interval(mins => r.max_duration_minutes),
           updated_at = now()
      FROM interview_round_configs r
     WHERE r.job_posting_id = s.job_posting_id
       AND r.round_number   = s.round_number
       AND lower(btrim(r.round_type)) = 'online_test'
       AND s.end_time IS DISTINCT FROM s.start_time + make_interval(mins => r.max_duration_minutes);
    GET DIAGNOSTICS synced_slots = ROW_COUNT;

    RAISE NOTICE 'ADR-072: da chep thoi luong bai thi cho % vong trac nghiem, dong bo gio ket thuc % ca thi.',
        copied_rounds, synced_slots;
END $$;");

            migrationBuilder.DropColumn(
                name: "online_test_duration_minutes",
                table: "job_postings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "online_test_duration_minutes",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.Sql(@"
UPDATE job_postings j
   SET online_test_duration_minutes = r.max_duration_minutes
  FROM (
        SELECT DISTINCT ON (job_posting_id) job_posting_id, max_duration_minutes
          FROM interview_round_configs
         WHERE lower(btrim(round_type)) = 'online_test'
         ORDER BY job_posting_id, round_number
       ) r
 WHERE j.id = r.job_posting_id
   AND r.max_duration_minutes BETWEEN 1 AND 300;");
        }
    }
}
