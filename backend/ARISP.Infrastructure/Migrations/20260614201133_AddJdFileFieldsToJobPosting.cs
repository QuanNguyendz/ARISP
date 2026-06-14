using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARISP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJdFileFieldsToJobPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "jd_file_format",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jd_file_name",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "jd_file_url",
                table: "job_postings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "jd_file_format",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "jd_file_name",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "jd_file_url",
                table: "job_postings");
        }
    }
}
