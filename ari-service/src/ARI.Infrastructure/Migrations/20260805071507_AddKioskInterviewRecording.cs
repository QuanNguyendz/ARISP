using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKioskInterviewRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recording_deleted_at",
                table: "interview_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recording_expires_at",
                table: "interview_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "recording_size_bytes",
                table: "interview_sessions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recording_deleted_at",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "recording_expires_at",
                table: "interview_sessions");

            migrationBuilder.DropColumn(
                name: "recording_size_bytes",
                table: "interview_sessions");
        }
    }
}
