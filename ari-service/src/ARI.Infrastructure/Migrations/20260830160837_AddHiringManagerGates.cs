using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHiringManagerGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hm_sign_off_at",
                table: "job_postings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hm_sign_off_by_user_id",
                table: "job_postings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hm_sign_off_reason",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hm_sign_off_status",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "concerns",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fallback_reason",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hr_fallback",
                table: "hr_reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reviewer_role",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "strengths",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_level",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_salary_currency",
                table: "hr_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "suggested_salary_max",
                table: "hr_reviews",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "suggested_salary_min",
                table: "hr_reviews",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hm_decided_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hm_decision",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hm_decision_by_user_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hm_decision_note",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_hm_sign_off_by_user_id",
                table: "job_postings",
                column: "hm_sign_off_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_hm_decision_by_user_id",
                table: "applications",
                column: "hm_decision_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_applications_users_hm_decision_by_user_id",
                table: "applications",
                column: "hm_decision_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_job_postings_users_hm_sign_off_by_user_id",
                table: "job_postings",
                column: "hm_sign_off_by_user_id",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_users_hm_decision_by_user_id",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "FK_job_postings_users_hm_sign_off_by_user_id",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "IX_job_postings_hm_sign_off_by_user_id",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "IX_applications_hm_decision_by_user_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "hm_sign_off_at",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "hm_sign_off_by_user_id",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "hm_sign_off_reason",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "hm_sign_off_status",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "concerns",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "fallback_reason",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "is_hr_fallback",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "reviewer_role",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "strengths",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "suggested_level",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "suggested_salary_currency",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "suggested_salary_max",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "suggested_salary_min",
                table: "hr_reviews");

            migrationBuilder.DropColumn(
                name: "hm_decided_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "hm_decision",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "hm_decision_by_user_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "hm_decision_note",
                table: "applications");
        }
    }
}
