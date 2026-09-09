using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJdTemplateAndJdDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jd_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruitment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    employment_type = table.Column<string>(type: "text", nullable: true),
                    work_mode = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    experience_level = table.Column<string>(type: "text", nullable: true),
                    vacancies = table.Column<int>(type: "integer", nullable: true),
                    salary_min = table.Column<decimal>(type: "numeric", nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric", nullable: true),
                    salary_currency = table.Column<string>(type: "text", nullable: true),
                    application_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sections_json = table.Column<string>(type: "text", nullable: false),
                    generated_file_storage_key = table.Column<string>(type: "text", nullable: true),
                    generated_file_name = table.Column<string>(type: "text", nullable: true),
                    generated_format = table.Column<string>(type: "text", nullable: true),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jd_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_jd_documents_recruitment_requests_recruitment_request_id",
                        column: x => x.recruitment_request_id,
                        principalTable: "recruitment_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jd_documents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "jd_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "text", nullable: false),
                    company_address = table.Column<string>(type: "text", nullable: true),
                    company_website = table.Column<string>(type: "text", nullable: true),
                    company_email = table.Column<string>(type: "text", nullable: true),
                    logo_storage_key = table.Column<string>(type: "text", nullable: true),
                    accent_color = table.Column<string>(type: "text", nullable: false),
                    font_family = table.Column<string>(type: "text", nullable: false),
                    footer_note = table.Column<string>(type: "text", nullable: true),
                    sections_json = table.Column<string>(type: "text", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jd_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_jd_templates_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_jd_documents_created_by_user_id",
                table: "jd_documents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_jd_documents_recruitment_request_id",
                table: "jd_documents",
                column: "recruitment_request_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_jd_templates_updated_by_user_id",
                table: "jd_templates",
                column: "updated_by_user_id");

            // Realtime tầng database (ADR-057) — EF KHÔNG tự sinh dòng này.
            //
            // `arisp_attach_change_triggers()` quét `pg_class` nên chỉ cần gọi lại là hai bảng mới
            // có trigger. Quên gọi thì `jd_templates`/`jd_documents` không phát NOTIFY nào: HR Leader
            // đổi mẫu mà trình soạn JD đang mở không hay biết, và vẫn xuất file theo bố cục cũ.
            //
            // Payload không cần thêm khoá mới: `jd_documents` đã có sẵn `created_by_user_id`, còn
            // `jd_templates` chỉ gửi cho nhóm hr_admin nên không cần khoá định tuyến nào.
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jd_documents");

            migrationBuilder.DropTable(
                name: "jd_templates");
        }
    }
}
