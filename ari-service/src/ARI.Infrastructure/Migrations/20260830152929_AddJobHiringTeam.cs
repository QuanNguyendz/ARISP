using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobHiringTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_hiring_team_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    // Default ở TẦNG DB (EF không tự sinh): entity có default trong C# nên mọi
                    // insert qua EF đều đúng, nhưng script vá dữ liệu hay seed bằng SQL thuần sẽ
                    // vỡ nếu cột NOT NULL mà không có default.
                    role_on_job = table.Column<string>(type: "text", nullable: false, defaultValue: "hiring_manager"),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_hiring_team_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_hiring_team_members_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_hiring_team_members_users_added_by_user_id",
                        column: x => x.added_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_job_hiring_team_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_hiring_team_members_added_by_user_id",
                table: "job_hiring_team_members",
                column: "added_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_hiring_team_members_user_id",
                table: "job_hiring_team_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_job_hiring_team_members_job_user",
                table: "job_hiring_team_members",
                columns: new[] { "job_posting_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_job_hiring_team_members_primary",
                table: "job_hiring_team_members",
                column: "job_posting_id",
                unique: true,
                filter: "is_primary = true AND deleted_at IS NULL");

            // BẮT BUỘC — hàm quét pg_class rồi gắn trigger arisp_notify_change() cho mọi bảng
            // (ADR-057). Bảng mới KHÔNG tự có trigger, mà thiếu trigger thì realtime im lặng
            // không bao giờ chạy: giao diện vẫn "hoạt động" nhờ F5 tay nên lỗi rất khó thấy.
            // Phải chạy SAU CreateTable. Idempotent (DROP TRIGGER IF EXISTS cho từng bảng).
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_hiring_team_members");
        }
    }
}
