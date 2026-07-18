using System;
using ARISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    [DbContext(typeof(ARISPDbContext))]
    [Migration("20260630000000_AddStaffNotifications")]
    public partial class AddStaffNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cho phép candidate_account_id null để bảng dùng chung cho cả nhân sự nội bộ.
            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_account_id",
                table: "notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "recipient_user_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id_dedup_key",
                table: "notifications",
                columns: new[] { "recipient_user_id", "dedup_key" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_recipient_user_id_dedup_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "recipient_user_id",
                table: "notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_account_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
