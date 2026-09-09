using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHmAvailabilityAndInterviewAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "admitted_at",
                table: "interview_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "admitted_by_user_id",
                table: "interview_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hm_joined_at",
                table: "interview_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hm_joined_by_user_id",
                table: "interview_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hiring_manager_availabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    hiring_manager_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hiring_manager_availabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_hiring_manager_availabilities_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hiring_manager_availabilities_users_hiring_manager_user_id",
                        column: x => x.hiring_manager_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_hm_availabilities_job_round",
                table: "hiring_manager_availabilities",
                columns: new[] { "job_posting_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "IX_hiring_manager_availabilities_hiring_manager_user_id",
                table: "hiring_manager_availabilities",
                column: "hiring_manager_user_id");

            // Bảng MỚI phải được gắn trigger realtime (quy tắc 24) — EF không tự sinh lời gọi này.
            // Hàm quét pg_class nên gọi lại là đủ; đã có trigger thì không nhân đôi.
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");

            // ---- Chuẩn hoá sức chứa ca về 1 (ADR-067) --------------------------------------
            // Từ nay một ca chỉ nhận MỘT ứng viên, nên `capacity > 1` là một trạng thái không thể
            // đạt tới nữa — để nguyên thì giao diện hiện "0/3 chỗ" trong khi server chặn ở người thứ
            // hai, đúng kiểu lệch giữa con số hiển thị và luật thật mà ADR-058 đã phải đi chữa.
            //
            // CHỈ hạ những ca không mất mát gì: còn ở tương lai và đang giữ nhiều nhất một chỗ.
            migrationBuilder.Sql(@"
                UPDATE availability_slots s
                SET capacity = 1, updated_at = NOW()
                WHERE s.capacity > 1
                  AND s.start_time > NOW()
                  AND (SELECT COUNT(*) FROM interview_bookings b
                       WHERE b.availability_slot_id = s.id AND b.status = 'scheduled') <= 1;
            ");

            // Ca đang thật sự giữ từ 2 người trở lên thì KHÔNG đụng vào — hạ sức chứa ở đó là đuổi
            // người ra khỏi chỗ họ đã được hẹn mà không ai báo. Liệt kê ra để nhân sự tự dời lịch,
            // giống cách ADR-065 xử lý dữ liệu phòng ban cũ: nói to chỗ cần người quyết định.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE r RECORD; n INT := 0;
                BEGIN
                    FOR r IN
                        SELECT s.id, s.start_time, COUNT(b.id) AS booked
                        FROM availability_slots s
                        JOIN interview_bookings b
                          ON b.availability_slot_id = s.id AND b.status = 'scheduled'
                        GROUP BY s.id, s.start_time
                        HAVING COUNT(b.id) > 1
                    LOOP
                        n := n + 1;
                        RAISE NOTICE 'ADR-067: ca % (bat dau %) dang co % ung vien — can doi bot sang ca khac.',
                            r.id, r.start_time, r.booked;
                    END LOOP;
                    IF n = 0 THEN
                        RAISE NOTICE 'ADR-067: khong co ca nao dang giu tu 2 ung vien tro len.';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hiring_manager_availabilities");

            migrationBuilder.DropColumn(
                name: "admitted_at",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "admitted_by_user_id",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "hm_joined_at",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "hm_joined_by_user_id",
                table: "interview_sessions");
        }
    }
}
