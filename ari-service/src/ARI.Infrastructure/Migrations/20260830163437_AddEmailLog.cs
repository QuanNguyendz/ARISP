using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "text", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_email = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    was_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sent_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<string>(type: "text", nullable: true),
                    in_reply_to = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "sent"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_logs_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_email_logs_users_sent_by_user_id",
                        column: x => x.sent_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_email_logs_application_created",
                table: "email_logs",
                columns: new[] { "application_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_sent_by_user_id",
                table: "email_logs",
                column: "sent_by_user_id");

            // BẮT BUỘC với mọi bảng MỚI (ADR-057): EF không tự gắn trigger arisp_notify_change().
            // Thiếu câu này thì realtime của bảng im lặng không bao giờ chạy — mà giao diện vẫn
            // "hoạt động" nhờ F5 tay, nên lỗi rất khó phát hiện. Phải chạy SAU CreateTable.
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_logs");
        }
    }
}
