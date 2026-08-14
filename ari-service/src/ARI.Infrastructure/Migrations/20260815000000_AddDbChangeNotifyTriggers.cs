using ARI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-057 — Realtime ở TẦNG DATABASE: trigger + LISTEN/NOTIFY.
    ///
    /// Trước đây mọi sự kiện realtime do tầng ứng dụng tự phát: mỗi command sau khi ghi DB phải nhớ
    /// gọi PublishUserEventAsync. Đường nào quên push thì UI cũ đi âm thầm (lỗi thật đã gặp:
    /// ReassignJobCommand phát "JobReassigned" mà FE không bắt). Nay chính Postgres phát sự kiện khi
    /// dữ liệu đổi, nên đã COMMIT là chắc chắn có event — bất kể ai ghi (EF, SQL tay, rag-service
    /// Python, hosted service).
    ///
    /// NOTIFY là TRANSACTIONAL: chỉ gửi khi transaction commit, rollback thì không sinh event giả.
    ///
    /// Chỉ tạo function + trigger — KHÔNG đụng cột hay dữ liệu nào, nên không cần cập nhật
    /// ModelSnapshot (cùng kiểu với migration AddDocumentChunksFtsIndex). Idempotent, chạy lại được.
    /// </summary>
    [DbContext(typeof(AriDbContext))]
    [Migration("20260815000000_AddDbChangeNotifyTriggers")]
    public partial class AddDbChangeNotifyTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. Hàm trigger dùng chung cho MỌI bảng (row-level) ---
            //
            // Payload chỉ chứa KHOÁ + vài cột định tuyến, không bao giờ chứa nội dung row: giới hạn
            // cứng của pg_notify là 8000 byte, và kênh này không được phép trở thành đường rò dữ liệu.
            // Đọc cột qua to_jsonb(rec) ->> 'ten_cot' nên bảng không có cột đó chỉ trả NULL thay vì
            // lỗi — nhờ vậy MỘT hàm chạy được cho cả 30 bảng, không phải viết 30 hàm.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION arisp_notify_change() RETURNS trigger AS $$
DECLARE
    rec     jsonb;
    payload text;
BEGIN
    IF (TG_OP = 'DELETE') THEN
        rec := to_jsonb(OLD);
    ELSE
        rec := to_jsonb(NEW);
    END IF;

    payload := json_build_object(
        't',  TG_TABLE_NAME,
        'op', left(TG_OP, 1),
        'id', rec ->> 'id',
        'r',  jsonb_strip_nulls(jsonb_build_object(
                  'candidate_account_id', rec ->> 'candidate_account_id',
                  'recipient_user_id',    rec ->> 'recipient_user_id',
                  'job_posting_id',       rec ->> 'job_posting_id',
                  'application_id',       rec ->> 'application_id',
                  'user_id',              rec ->> 'user_id',
                  'created_by_user_id',   rec ->> 'created_by_user_id',
                  'requested_by_user_id', rec ->> 'requested_by_user_id',
                  'status',               rec ->> 'status'
              ))
    )::text;

    PERFORM pg_notify('arisp_changes', payload);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;");

            // --- 2. Bản STATEMENT-level cho bảng ghi hàng loạt ---
            //
            // rag-service xoá sạch rồi nạp lại hàng trăm chunk mỗi lần ingest (rag-service/app/rag/
            // ingest.py). Row-level sẽ bắn hàng trăm NOTIFY cho một thao tác; statement-level cho
            // đúng 1 event "bảng này vừa đổi".
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION arisp_notify_change_stmt() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('arisp_changes', json_build_object('t', TG_TABLE_NAME, 'op', 'S')::text);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;");

            // --- 3. Hàm gắn trigger cho toàn bộ bảng trong schema public ---
            //
            // Quét pg_class thay vì liệt kê cứng 30 tên bảng: migration sau này thêm bảng mới chỉ cần
            // gọi lại SELECT arisp_attach_change_triggers(); là bảng đó có realtime, không phải sửa
            // code C#. DROP IF EXISTS trước khi CREATE nên chạy lại bao nhiêu lần cũng sạch.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION arisp_attach_change_triggers() RETURNS void AS $$
DECLARE
    t            text;
    trg          text;
    bulk_tables  text[] := ARRAY['document_chunks'];
    skip_tables  text[] := ARRAY['ef_migrations_history'];
BEGIN
    FOR t IN
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
          AND NOT (c.relname = ANY(skip_tables))
        ORDER BY c.relname
    LOOP
        trg := 'arisp_notify_' || t;
        EXECUTE format('DROP TRIGGER IF EXISTS %I ON public.%I', trg, t);

        -- Viết mỗi lệnh trên MỘT chuỗi liền: Postgres có nối hai hằng chuỗi cách nhau bởi xuống dòng,
        -- nhưng đó là luật dễ vỡ khi ai đó chỉnh thụt lề — không đáng để dựa vào ở đây.
        IF t = ANY(bulk_tables) THEN
            EXECUTE format('CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON public.%I FOR EACH STATEMENT EXECUTE FUNCTION arisp_notify_change_stmt()', trg, t);
        ELSE
            EXECUTE format('CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION arisp_notify_change()', trg, t);
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;");

            migrationBuilder.Sql("SELECT arisp_attach_change_triggers();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CASCADE gỡ luôn mọi trigger đang dùng hai hàm này.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS arisp_attach_change_triggers();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS arisp_notify_change() CASCADE;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS arisp_notify_change_stmt() CASCADE;");
        }
    }
}
