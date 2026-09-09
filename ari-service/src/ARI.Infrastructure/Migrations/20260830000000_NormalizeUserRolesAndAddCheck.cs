using ARI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Chuẩn hoá <c>users.role</c> về snake_case chữ thường rồi khoá lại bằng ràng buộc CHECK.
    ///
    /// VÌ SAO CẦN: hệ thống có hai bộ từ vựng cho vai trò — giá trị DB (<c>hr_admin</c>) và giá trị
    /// claim JWT (<c>Hr_admin</c>) — và việc lẫn lộn chúng đã gây lỗi thật, im lặng:
    /// <c>GetRecruitersQuery</c> so <c>u.Role == AppRoles.Recruiter</c> ("Recruiter") ngay trong một
    /// biểu thức EF nên dịch ra SQL <c>=</c> phân biệt hoa thường; màn "Phân công &amp; tải tuyển dụng"
    /// của HR Lead luôn trống trơn mà không có lỗi nào. <c>SeedInterviewJobCommand</c> thì gieo thẳng
    /// <c>"Hr_admin"</c> vào DB, nên dữ liệu lệch là có thật chứ không chỉ là rủi ro lý thuyết.
    ///
    /// Ràng buộc CHECK là lớp cuối: sau khi mã nguồn đã dùng <c>RoleNames</c>, nó biến việc tái phạm
    /// từ "lỗi im lặng phát hiện sau vài tháng" thành "ghi DB thất bại ngay".
    ///
    /// Chuẩn bị sẵn cho vai trò <c>hiring_manager</c> (ADR-061) để không phải sửa ràng buộc lần nữa;
    /// chưa có tài khoản nào mang vai trò đó cho tới khi màn cấp tài khoản mở nó ra.
    /// </summary>
    [DbContext(typeof(AriDbContext))]
    [Migration("20260830000000_NormalizeUserRolesAndAddCheck")]
    public partial class NormalizeUserRolesAndAddCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Lệch hoa thường — phép sửa an toàn tuyệt đối, giữ nguyên ý nghĩa vai trò.
            //    `IS DISTINCT FROM` để lần chạy lại đụng 0 dòng (không churn updated_at, không bắn
            //    lại một loạt NOTIFY của ADR-057).
            migrationBuilder.Sql(@"
                UPDATE users
                   SET role = lower(btrim(role)),
                       updated_at = NOW()
                 WHERE role IS NOT NULL
                   AND role IS DISTINCT FROM lower(btrim(role));
            ");

            // 2) Giá trị KHÔNG nhận ra được (ví dụ 'Pending' của luồng đăng ký ngoài đã bỏ theo
            //    ADR-029). Không thể đoán chủ ý, mà cũng không được để migration chết giữa chừng
            //    trên production.
            //
            //    Xử lý: KHOÁ tài khoản và hạ về vai trò thấp nhất — giữ nguyên hành vi hiện tại
            //    (nhánh `user.Role == "Pending"` trong CompleteExternalStaffSignIn vốn đã chặn
            //    những tài khoản này đăng nhập), đồng thời GHI LẠI giá trị cũ vào `lock_reason`
            //    nên không mất dữ liệu và Super Admin thấy được vì sao tài khoản bị khoá.
            //    Cố ý KHÔNG xoá tài khoản: đây có thể là người thật.
            migrationBuilder.Sql(@"
                UPDATE users
                   -- COALESCE(role, ...) BẮT BUỘC: trong Postgres, nối chuỗi với một toán hạng NULL
                   -- cho ra NULL. Nhánh `role IS NULL` ở WHERE bên dưới vì thế sẽ gán lock_reason =
                   -- NULL, tức XOÁ luôn lý do khoá cũ nếu có — đúng ngược với điều chú thích trên hứa.
                   SET lock_reason = COALESCE(lock_reason || ' | ', '')
                                     || 'Vai trò cũ không hợp lệ: ''' || COALESCE(role, '(null)')
                                     || ''' (chuẩn hoá 2026-08-30)',
                       is_active   = false,
                       role        = 'recruiter',
                       updated_at  = NOW()
                 WHERE role IS NULL
                    OR role NOT IN ('super_admin', 'hr_admin', 'recruiter', 'hiring_manager');
            ");

            // 3) Khoá lại. Đặt tên tường minh để Down() gỡ đúng ràng buộc.
            migrationBuilder.Sql(@"
                ALTER TABLE users
                  ADD CONSTRAINT ck_users_role
                  CHECK (role IN ('super_admin', 'hr_admin', 'recruiter', 'hiring_manager'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ gỡ được ràng buộc. Hai bước dữ liệu ở trên KHÔNG có phép nghịch có ý nghĩa:
            // dạng chữ thường mới là dạng đúng (mã nguồn đọc/ghi theo dạng này), còn giá trị lạ
            // thì đã được chép nguyên văn vào `lock_reason` chứ không mất đi.
            migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_role;");
        }
    }
}
