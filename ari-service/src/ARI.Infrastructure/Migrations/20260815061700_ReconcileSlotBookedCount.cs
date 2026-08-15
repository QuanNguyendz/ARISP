using ARI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Đối soát <c>availability_slots.booked_count</c> về đúng số booking đang thật sự giữ chỗ.
    ///
    /// VÌ SAO CẦN: cột này từ trước tới nay chỉ được CỘNG/TRỪ, không bao giờ được tính lại, mà có
    /// hai đường trừ thừa đã tồn tại lâu nay (vá ở cùng đợt thay đổi này):
    ///   1. <c>RejectApplicationAsync</c> lọc booking bằng <c>status != 'cancelled'</c> — tức là gồm
    ///      cả booking đã <c>declined</c> (chỗ vốn đã được trả) — rồi trừ tiếp một lần nữa.
    ///   2. <c>RescheduleBookingAsync</c> trả chỗ ca cũ VÔ ĐIỀU KIỆN, kể cả khi dời một booking đang
    ///      ở trạng thái <c>declined</c> — mà đó chính là công dụng chính của nút "Dời lịch".
    /// Hệ quả: <c>booked_count</c> tụt xuống dưới số chỗ thực sự bị chiếm và không tự phục hồi.
    ///
    /// VỊ TỪ: một booking chiếm chỗ ⟺ <c>status = 'scheduled'</c>. <c>confirmation_status</c> KHÔNG
    /// tham gia — lịch đang chờ ứng viên xác nhận vẫn giữ chỗ (ADR-048). Biểu thức dưới đây phải
    /// khớp từng chữ với <c>InterviewService.GetSlotsForJobAsync</c> và cờ <c>OccupiesSeat</c>.
    ///
    /// PHẢI CHẠY SAU khi hai đường trừ thừa ở trên đã được vá, nếu không lần "từ chối → dời lịch"
    /// kế tiếp sẽ làm lệch lại ngay.
    /// </summary>
    [DbContext(typeof(AriDbContext))]
    [Migration("20260815061700_ReconcileSlotBookedCount")]
    public partial class ReconcileSlotBookedCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // `IS DISTINCT FROM` giữ tính idempotent theo nghĩa mạnh: lần chạy thứ hai đụng 0 dòng
            // nên không churn `updated_at` và không bắn lại một loạt NOTIFY của ADR-057.
            //
            // CỐ Ý KHÔNG đụng tới `capacity`: sau khi đối soát sẽ có ca lộ ra `booked_count > capacity`
            // (do các lần trừ thừa đã cho lọt thêm booking vào). Đó là sự thật hiện hình chứ không
            // phải hồi quy — giao diện hiện chip "Vượt sức chứa" để nhân sự tự quyết nâng sức chứa
            // hay dời bớt người. Tự động nâng `capacity` ở đây là giấu một quyết định nghiệp vụ vào
            // chỗ không ai nhìn thấy.
            migrationBuilder.Sql(@"
                UPDATE availability_slots s
                   SET booked_count = c.n,
                       updated_at   = NOW()
                  FROM (
                        SELECT s2.id,
                               COUNT(b.id) FILTER (WHERE b.status = 'scheduled')::int AS n
                          FROM availability_slots s2
                          LEFT JOIN interview_bookings b ON b.availability_slot_id = s2.id
                         GROUP BY s2.id
                       ) c
                 WHERE s.id = c.id
                   AND s.booked_count IS DISTINCT FROM c.n;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý để rỗng. Đây là đối soát một cột DẪN XUẤT: giá trị trước đó sai theo định nghĩa
            // (lệch khỏi số booking thực tế) và không được ghi lại ở đâu, nên không có phép nghịch
            // nào có ý nghĩa. Rollback migration này chỉ cần không làm gì.
        }
    }
}
