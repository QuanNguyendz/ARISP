using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Lưu lại kết quả bước "Xác thực thông tin" của màn ứng tuyển.
    ///
    /// Trước đây hệ thống đối chiếu họ tên + số điện thoại ứng viên vừa gõ với nội dung CV, cảnh báo
    /// cho chính ứng viên rồi vứt kết quả đi. Nhân sự đọc hồ sơ không có cách nào biết người này đã
    /// được báo là lệch thông tin mà vẫn bấm nộp — nên hai cột này đi vào hồ sơ, hiện ở khối
    /// "Thông tin ứng tuyển" của cả ba vai.
    /// </summary>
    public partial class ApplicationContactVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_verification_status",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_verification_details",
                table: "applications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_verification_details",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "contact_verification_status",
                table: "applications");
        }
    }
}
