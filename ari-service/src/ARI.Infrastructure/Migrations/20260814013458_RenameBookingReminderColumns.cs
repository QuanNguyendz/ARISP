using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Vá drift cột `interview_bookings.reminder{24h,1h}_sent` — cùng loại bệnh với ADR-056
    /// nhưng ở tầng CỘT (ADR-056 tự giới hạn "0 thao tác chạm cột/dữ liệu" nên không bắt được).
    ///
    /// `20260614175231_InitialCreate` tạo cột theo quy ước snake_case mặc định của EF là
    /// `reminder1h_sent` / `reminder24h_sent`. Sau đó `AriDbContext.OnModelCreating` khai
    /// `HasColumnName("reminder_1h_sent")` / `("reminder_24h_sent")` cho khớp schema Supabase
    /// vốn được sửa TAY bằng SQL — nhưng không ai sinh migration đổi tên. Supabase trùng khớp
    /// nên im lặng nhiều tháng; DB production dựng 100% từ migration (ADR-055) thì ra tên gốc,
    /// nên MỌI query chạm `InterviewBooking` đều nổ `42703: column ... does not exist`
    /// → nhắc lịch phỏng vấn 24h/1h chết hẳn trên production.
    ///
    /// Chọn đổi tên DB theo code (chứ không bỏ `HasColumnName` cho về tên gốc) vì tên có gạch
    /// dưới quanh số dễ đọc hơn và snapshot/Designer của mọi migration cũ đã ghi tên đó — sửa
    /// chiều ngược lại phải viết tay một migration "rỗng nhưng không rỗng" để gỡ lệch snapshot.
    ///
    /// Viết bằng SQL có kiểm tra thay vì `migrationBuilder.RenameColumn` để chạy được trên MỌI
    /// DB đang tồn tại: prod (còn tên gốc → đổi), Supabase test (đã là tên mới → bỏ qua), DB
    /// trắng dựng lại từ đầu (InitialCreate ra tên gốc → đổi). `RenameColumn` thuần sẽ ném lỗi
    /// ở DB nào đã đúng tên rồi, làm kẹt cả chuỗi migration lúc boot.
    /// </summary>
    public partial class RenameBookingReminderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rename("reminder1h_sent", "reminder_1h_sent"));
            migrationBuilder.Sql(Rename("reminder24h_sent", "reminder_24h_sent"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rename("reminder_1h_sent", "reminder1h_sent"));
            migrationBuilder.Sql(Rename("reminder_24h_sent", "reminder24h_sent"));
        }

        /// <summary>
        /// Chỉ đổi tên khi cột nguồn CÓ và cột đích CHƯA có — nên chạy lại nhiều lần vẫn an toàn
        /// và không đụng vào DB đã ở trạng thái đích.
        /// </summary>
        private static string Rename(string from, string to) => $@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'interview_bookings' AND column_name = '{from}'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'interview_bookings' AND column_name = '{to}'
    ) THEN
        ALTER TABLE interview_bookings RENAME COLUMN {from} TO {to};
    END IF;
END $$;";
    }
}
