using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recruitment_request_id",
                table: "job_postings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recruitment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    headcount = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    employment_type = table.Column<string>(type: "text", nullable: true),
                    work_mode = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    experience_level = table.Column<string>(type: "text", nullable: true),
                    expected_start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    salary_min = table.Column<decimal>(type: "numeric", nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric", nullable: true),
                    salary_currency = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    review_reason = table.Column<string>(type: "text", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_recruiter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_recruitment_requests_users_assigned_recruiter_id",
                        column: x => x.assigned_recruiter_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recruitment_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recruitment_requests_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ux_job_postings_recruitment_request_id",
                table: "job_postings",
                column: "recruitment_request_id",
                unique: true,
                filter: "recruitment_request_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_recruitment_requests_active_deleted",
                table: "recruitment_requests",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "idx_recruitment_requests_assigned_recruiter",
                table: "recruitment_requests",
                column: "assigned_recruiter_id");

            migrationBuilder.CreateIndex(
                name: "idx_recruitment_requests_requested_by",
                table: "recruitment_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_recruitment_requests_status",
                table: "recruitment_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_requests_reviewed_by_user_id",
                table: "recruitment_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_job_postings_recruitment_requests_recruitment_request_id",
                table: "job_postings",
                column: "recruitment_request_id",
                principalTable: "recruitment_requests",
                principalColumn: "id");

            // === Realtime tầng database (ADR-057) — EF KHÔNG tự sinh phần này ===
            //
            // Hai việc, thiếu việc nào cũng hỏng im lặng:
            //
            // 1. Thêm `assigned_recruiter_id` vào payload của `arisp_notify_change()`. Không có nó
            //    thì Recruiter vừa được phân công là người DUY NHẤT không nhận được sự kiện — đúng
            //    lỗi ADR-061 gặp khi `DbChangeLookup` thiếu `HiringTeamUserIds` và màn Hiring
            //    Manager đứng im. Hàm đọc cột qua `to_jsonb(rec) ->>` nên bảng không có cột này chỉ
            //    trả NULL, và `jsonb_strip_nulls` loại luôn — 29 bảng còn lại không đổi payload.
            //
            // 2. Gắn trigger cho bảng mới. `arisp_attach_change_triggers()` quét `pg_class` nên chỉ
            //    cần gọi lại là xong; quên gọi thì `recruitment_requests` không phát NOTIFY nào và
            //    hàng chờ duyệt của HR Leader không bao giờ tự cập nhật.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION arisp_notify_change() RETURNS trigger AS $$
DECLARE
    rec     jsonb;
    payload text;
BEGIN
    IF (TG_OP = 'DELETE') THEN
        rec := to_jsonb(OLD);
    ELSE
        rec := to_jsonb(NEW);
    END IF;

    payload := json_build_object(
        't',  TG_TABLE_NAME,
        'op', left(TG_OP, 1),
        'id', rec ->> 'id',
        'r',  jsonb_strip_nulls(jsonb_build_object(
                  'candidate_account_id',  rec ->> 'candidate_account_id',
                  'recipient_user_id',     rec ->> 'recipient_user_id',
                  'job_posting_id',        rec ->> 'job_posting_id',
                  'application_id',        rec ->> 'application_id',
                  'user_id',               rec ->> 'user_id',
                  'created_by_user_id',    rec ->> 'created_by_user_id',
                  'requested_by_user_id',  rec ->> 'requested_by_user_id',
                  'assigned_recruiter_id', rec ->> 'assigned_recruiter_id',
                  'status',                rec ->> 'status'
              ))
    )::text;

    PERFORM pg_notify('arisp_changes', payload);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;");

            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Trả `arisp_notify_change()` về đúng bản trước (không có `assigned_recruiter_id`).
            // Trigger trên bảng bị xoá tự biến mất theo `DROP TABLE`, không cần gỡ tay.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION arisp_notify_change() RETURNS trigger AS $$
DECLARE
    rec     jsonb;
    payload text;
BEGIN
    IF (TG_OP = 'DELETE') THEN
        rec := to_jsonb(OLD);
    ELSE
        rec := to_jsonb(NEW);
    END IF;

    payload := json_build_object(
        't',  TG_TABLE_NAME,
        'op', left(TG_OP, 1),
        'id', rec ->> 'id',
        'r',  jsonb_strip_nulls(jsonb_build_object(
                  'candidate_account_id', rec ->> 'candidate_account_id',
                  'recipient_user_id',    rec ->> 'recipient_user_id',
                  'job_posting_id',       rec ->> 'job_posting_id',
                  'application_id',       rec ->> 'application_id',
                  'user_id',              rec ->> 'user_id',
                  'created_by_user_id',   rec ->> 'created_by_user_id',
                  'requested_by_user_id', rec ->> 'requested_by_user_id',
                  'status',               rec ->> 'status'
              ))
    )::text;

    PERFORM pg_notify('arisp_changes', payload);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;");

            migrationBuilder.DropForeignKey(
                name: "FK_job_postings_recruitment_requests_recruitment_request_id",
                table: "job_postings");

            migrationBuilder.DropTable(
                name: "recruitment_requests");

            migrationBuilder.DropIndex(
                name: "ux_job_postings_recruitment_request_id",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "recruitment_request_id",
                table: "job_postings");
        }
    }
}
