using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Thêm <c>interview_bookings.declined_by</c>: ai đã đóng lịch (candidate | system | staff).
    ///
    /// Vì sao cần cột này: <c>status</c> + <c>confirmation_status</c> KHÔNG phân biệt được
    /// "ứng viên báo bận" với "hệ thống tự huỷ do quá hạn xác nhận" — cả hai đều ghi
    /// <c>declined</c>. Giao diện vì thế phải đoán bằng cách dò chuỗi tiếng Việt trong
    /// <c>decline_reason</c>, nên gắn nhãn sai cho trường hợp quá hạn, và một ứng viên chỉ cần
    /// gõ đúng từ khoá vào lý do báo bận là đổi được trạng thái hiển thị của chính mình.
    /// </summary>
    public partial class AddBookingDeclinedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "declined_by",
                table: "interview_bookings",
                type: "text",
                nullable: true);

            // Backfill dữ liệu lịch sử. ĐÂY LÀ LẦN DUY NHẤT được phép suy ra nguồn đóng lịch từ
            // nội dung `decline_reason` — một lần, trên dữ liệu đã có, với đúng chuỗi mà
            // ScheduleConfirmationHostedService đang sinh. Từ nay `declined_by` là nguồn sự thật và
            // được ghi tường minh tại 3 nơi (DeclineScheduleCommand / hosted service / RejectApplication).
            //
            // Idempotent nhờ `declined_by IS NULL`: chạy lại không đụng dòng nào.
            // Thứ tự 3 câu có ý nghĩa — câu cuối là nhánh vét, phải chạy sau cùng.
            migrationBuilder.Sql(@"
                UPDATE interview_bookings SET declined_by = 'staff'
                 WHERE declined_by IS NULL AND status = 'cancelled';

                UPDATE interview_bookings SET declined_by = 'system'
                 WHERE declined_by IS NULL AND status = 'declined' AND decline_reason LIKE '[Hệ thống]%';

                UPDATE interview_bookings SET declined_by = 'candidate'
                 WHERE declined_by IS NULL AND status = 'declined';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "declined_by",
                table: "interview_bookings");
        }
    }
}
