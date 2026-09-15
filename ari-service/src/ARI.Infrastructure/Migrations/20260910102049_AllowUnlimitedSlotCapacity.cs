using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// <c>availability_slots.capacity</c> nhận <c>NULL</c> = <b>KHÔNG giới hạn số ứng viên</b>.
    ///
    /// Vòng TRẮC NGHIỆM là bài thi trực tuyến: ai cũng làm được trong cùng một khung giờ, không có
    /// ghế nào để đếm và không có Hiring Manager nào phải chia mình ra. Luật "một ca một ứng viên"
    /// của ADR-067 sinh ra từ sự CÓ MẶT của Hiring Manager nên chỉ đúng với vòng hội thoại.
    ///
    /// Một con số lớn giả (999) thì phải giải thích ở mọi chỗ hiển thị ("Đã đặt 3/999") mà vẫn sai
    /// bản chất; <c>NULL</c> nói đúng thứ nó là.
    ///
    /// <b>Sửa lại hệ quả của migration trước.</b> <c>AddHmAvailabilityAndInterviewAdmission</c> hạ
    /// mọi ca tương lai về <c>capacity = 1</c>, kể cả ca của vòng trắc nghiệm — nên ca thi chỉ nhận
    /// được đúng một người. Migration này trả các ca đó về "không giới hạn". Chỉ đụng ca TƯƠNG LAI
    /// và chỉ NỚI RỘNG (không có booking nào đang giữ chỗ bị ảnh hưởng).
    ///
    /// Không có bảng mới nên không cần gọi lại <c>arisp_attach_change_triggers()</c> (quy tắc 24).
    /// </summary>
    public partial class AllowUnlimitedSlotCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "capacity",
                table: "availability_slots",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(@"
DO $$
DECLARE
    freed integer;
BEGIN
    UPDATE availability_slots s
       SET capacity = NULL,
           updated_at = NOW()
      FROM interview_round_configs r
     WHERE r.job_posting_id = s.job_posting_id
       AND r.round_number   = s.round_number
       AND lower(btrim(r.round_type)) = 'online_test'
       AND s.start_time > NOW()
       AND s.capacity IS NOT NULL;

    GET DIAGNOSTICS freed = ROW_COUNT;
    IF freed > 0 THEN
        RAISE NOTICE 'AllowUnlimitedSlotCapacity: da bo tran suc chua cho % ca thi trac nghiem trong tuong lai.', freed;
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Trả cột về NOT NULL: các ca "không giới hạn" phải nhận một con số. Dùng 1 chứ KHÔNG
            // dùng mặc định 0 mà EF sinh ra — capacity 0 nghĩa là không ai đặt được ca đó nữa, tức
            // là một lần rollback sẽ âm thầm khoá toàn bộ lịch thi.
            migrationBuilder.Sql(
                "UPDATE availability_slots SET capacity = GREATEST(booked_count, 1) WHERE capacity IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "capacity",
                table: "availability_slots",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
