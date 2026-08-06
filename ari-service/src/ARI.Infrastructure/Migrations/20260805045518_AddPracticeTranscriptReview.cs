using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// Xem lại buổi phỏng vấn thử (ADR-051): lưu câu chào kết thúc + ngôn ngữ viết báo cáo,
    /// thêm index cho đường đọc transcript theo session.
    ///
    /// Viết bằng SQL có IF NOT EXISTS thay vì AddColumn/CreateIndex: một bản migration trước đó
    /// (cùng tên, timestamp 20260805022000) đã kịp chạy trên DB dev rồi mới được gộp lại thành
    /// bản này — nên DB dev đã có sẵn closing_text + 2 index, còn DB sạch thì chưa. Cách này chạy
    /// đúng trên cả hai trạng thái.
    /// </summary>
    public partial class AddPracticeTranscriptReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE interview_sessions ADD COLUMN IF NOT EXISTS closing_text text;");
            migrationBuilder.Sql("ALTER TABLE interview_sessions ADD COLUMN IF NOT EXISTS report_language text;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_questions_session_id ON questions (session_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_answers_session_id ON answers (session_id);");

            // Dọn bản ghi lịch sử mồ côi của bản migration đã bị gộp (chỉ tồn tại trên DB đã chạy nó).
            migrationBuilder.Sql(
                "DELETE FROM ef_migrations_history WHERE \"MigrationId\" = '20260805022000_AddPracticeTranscriptReview';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_questions_session_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_answers_session_id;");
            migrationBuilder.Sql("ALTER TABLE interview_sessions DROP COLUMN IF EXISTS closing_text;");
            migrationBuilder.Sql("ALTER TABLE interview_sessions DROP COLUMN IF EXISTS report_language;");
        }
    }
}
