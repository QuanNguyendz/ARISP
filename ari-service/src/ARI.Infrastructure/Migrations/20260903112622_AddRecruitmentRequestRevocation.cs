using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentRequestRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-066: hai cột cho thao tác thu hồi phê duyệt. Cả hai NULLABLE nên không cần giá trị
            // mặc định — phiếu cũ chưa từng bị thu hồi thì để trống là đúng nghĩa, khác hẳn bẫy
            // ADR-060 (cột NOT NULL mà EF tự điền `""`).
            //
            // KHÔNG gọi `arisp_attach_change_triggers()` ở đây: quy tắc 24 áp cho BẢNG MỚI, mà
            // `recruitment_requests` đã có trigger từ ADR-063. Hàm `arisp_notify_change()` đọc cột
            // qua `to_jsonb(rec)->>` nên thêm cột không phải gắn lại trigger.
            migrationBuilder.AddColumn<Guid>(
                name: "revoked_by_user_id",
                table: "recruitment_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revoked_reason",
                table: "recruitment_requests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revoked_by_user_id",
                table: "recruitment_requests");

            migrationBuilder.DropColumn(
                name: "revoked_reason",
                table: "recruitment_requests");
        }
    }
}
