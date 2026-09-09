using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndRequestPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-065: `department` chuỗi tự do bị thay bằng khoá ngoại tới bảng `departments`.
            //
            // CỐ Ý KHÔNG backfill tự động: "SDC3.BU3" và "SDC3-BU3" không thể tự gộp mà không đoán,
            // mà đoán sai thì tạo ra liên kết tổ chức sai — im lặng và khó phát hiện hơn hẳn ô trống.
            // Super Admin gán lại bằng tay ở màn Quản lý người dùng.
            //
            // Ghi lại giá trị cũ vào log trước khi xoá, để còn biết phải gán lại những ai.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    orphaned int;
BEGIN
    SELECT count(*) INTO orphaned FROM users WHERE department IS NOT NULL AND btrim(department) <> '';
    IF orphaned > 0 THEN
        RAISE NOTICE 'ADR-065: % tài khoản đang có phòng ban dạng chuỗi sẽ mất liên kết đội. Super Admin cần gán lại ở màn Quản lý người dùng. Giá trị cũ: %',
            orphaned,
            (SELECT string_agg(DISTINCT email || ' => ' || department, ', ')
               FROM users WHERE department IS NOT NULL AND btrim(department) <> '');
    END IF;
END $$;");

            migrationBuilder.DropColumn(
                name: "department",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department",
                table: "recruitment_requests");

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "recruitment_requests",
                type: "uuid",
                nullable: true);

            // Mặc định là GIÁ TRỊ THẬT, không phải chuỗi rỗng như EF tự sinh: phiếu lập trước đợt
            // này sẽ có mức ưu tiên hợp lệ thay vì một chuỗi rỗng lọt qua mọi phép so sánh và rơi
            // xuống cuối hàng chờ mà không ai hiểu vì sao (bẫy ADR-060 về mặc định vô nghĩa).
            migrationBuilder.AddColumn<string>(
                name: "priority",
                table: "recruitment_requests",
                type: "text",
                nullable: false,
                defaultValue: "medium");

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "idx_recruitment_requests_department_id",
                table: "recruitment_requests",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ux_departments_name",
                table: "departments",
                column: "name",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_recruitment_requests_departments_department_id",
                table: "recruitment_requests",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id");

            // Realtime tầng database (ADR-057) — EF không tự sinh. Bảng `departments` mới phải có
            // trigger, nếu không màn quản lý đội và ô chọn đội ở biểu mẫu phiếu không bao giờ tự
            // cập nhật khi Super Admin thêm/tắt một đội.
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recruitment_requests_departments_department_id",
                table: "recruitment_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_users_departments_department_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropIndex(
                name: "idx_users_department_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "idx_recruitment_requests_department_id",
                table: "recruitment_requests");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "recruitment_requests");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "recruitment_requests");

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "recruitment_requests",
                type: "text",
                nullable: true);
        }
    }
}
