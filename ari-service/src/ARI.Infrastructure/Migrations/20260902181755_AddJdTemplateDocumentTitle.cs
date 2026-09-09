using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJdTemplateDocumentTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mặc định là TIÊU ĐỀ THẬT, không phải chuỗi rỗng như EF tự sinh: mẫu đã cấu hình trước
            // đợt này sẽ nhận đúng tiêu đề thay vì một dòng trắng ở giữa đầu văn bản.
            // (`JdLayout` vẫn có lớp rơi-về mặc định, nhưng dữ liệu đúng ngay từ DB thì không phải
            // dựa vào lớp đó — đúng bài học ADR-060 về cột mới có mặc định vô nghĩa.)
            migrationBuilder.AddColumn<string>(
                name: "document_title",
                table: "jd_templates",
                type: "text",
                nullable: false,
                defaultValue: "THÔNG TIN TUYỂN DỤNG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_title",
                table: "jd_templates");
        }
    }
}
