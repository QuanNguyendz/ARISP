using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
                    position = table.Column<string>(type: "text", nullable: true),
                    salary_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    salary_currency = table.Column<string>(type: "text", nullable: true, defaultValue: "VND"),
                    salary_period = table.Column<string>(type: "text", nullable: true, defaultValue: "month"),
                    bonus = table.Column<string>(type: "text", nullable: true),
                    benefits = table.Column<string>(type: "text", nullable: true),
                    employment_type = table.Column<string>(type: "text", nullable: true),
                    work_location = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    offer_letter_file_url = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval_note = table.Column<string>(type: "text", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    candidate_response_note = table.Column<string>(type: "text", nullable: true),
                    withdrawn_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.id);
                    table.ForeignKey(
                        name: "FK_offers_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_offers_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalTable: "job_postings",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_offers_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_offers_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_offers_users_withdrawn_by_user_id",
                        column: x => x.withdrawn_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_offers_expiry_sweep",
                table: "offers",
                columns: new[] { "status", "expires_at" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_offers_approved_by_user_id",
                table: "offers",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_offers_created_by_user_id",
                table: "offers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_offers_job_posting_id",
                table: "offers",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_offers_withdrawn_by_user_id",
                table: "offers",
                column: "withdrawn_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_offers_application_live",
                table: "offers",
                column: "application_id",
                unique: true,
                filter: "deleted_at IS NULL AND status NOT IN ('withdrawn', 'declined', 'expired')");

            // Bảng MỚI → phải gắn trigger realtime (ADR-057). Lần thứ ba trong ADR-061 gặp bẫy này:
            // EF không bao giờ tự gọi hàm đó, mà thiếu nó thì kênh realtime im lặng không chạy.
            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offers");
        }
    }
}
