# Postgres production + truy cập DBeaver cho cả nhóm

> Áp dụng từ ADR-055: production bỏ Supabase, chạy Postgres container trên chính VPS.
> Supabase nay chỉ còn là môi trường **test/integration**.

---

## Vì sao DB không mở cổng ra Internet

Trước đây DB nghe ở `161.248.147.38:8443` — ai trên Internet cũng chạm được, bằng tài khoản
`postgres` (superuser). Bot quét cổng dò mật khẩu Postgres chạy liên tục, và cổng "lạ" như
8443 không giấu được gì: máy quét thử hết 65535 cổng chứ không đoán.

Repo đã dính đúng lỗi này một lần rồi. `.ai/tasks.md` ghi lại: `docker-compose.prod.yml` reset
`ports` cho redis/rag/backend/frontend nhưng **bỏ sót postgres**, nên base compose bind
`0.0.0.0:5433` kèm mật khẩu dev mặc định — quét từ ngoài xác nhận `5433 open`.

Điểm mấu chốt hay bị hiểu nhầm: **`ufw` không chặn được cổng do Docker publish.** Docker chèn
rule iptables ở tầng thấp hơn ufw, nên `ufw deny 5433` trông như đã chặn mà thực tế vẫn mở.
Chỉ có gỡ mapping ở Docker mới thật sự đóng.

Cách xử lý hiện tại: `docker-compose.prod.yml` bind `127.0.0.1:5432:5432`. Docker chỉ tạo rule
DNAT trên loopback → cổng **không** ra Internet, nhưng chính VPS vẫn gọi được → SSH tunnel
hoạt động. Cả nhóm giữ nguyên cách làm việc bằng DBeaver mà không hở gì.

```
Máy bạn ─── SSH (cổng 22, xác thực bằng key) ──▶ VPS ──▶ 127.0.0.1:5432 ──▶ container postgres
             ▲ chỉ cổng duy nhất mở ra Internet
```

---

## Phần 1 — Quản trị VPS làm một lần

### 1.1 Tạo user riêng cho tunnel

Không dùng `root` cho việc truy cập DB hằng ngày:

```bash
adduser --disabled-password --gecos "" arisp
mkdir -p /home/arisp/.ssh && chmod 700 /home/arisp/.ssh
touch /home/arisp/.ssh/authorized_keys && chmod 600 /home/arisp/.ssh/authorized_keys
chown -R arisp:arisp /home/arisp/.ssh
```

### 1.2 Thêm public key của từng thành viên

Mỗi người chạy trên máy mình:

```bash
ssh-keygen -t ed25519 -C "ten-cua-ban"
cat ~/.ssh/id_ed25519.pub    # gửi nội dung DÒNG NÀY cho người quản trị
```

> **Chỉ gửi file `.pub`.** Private key (`id_ed25519`, không có đuôi `.pub`) không bao giờ rời
> máy của bạn, không gửi qua chat, không commit.

Người quản trị dán mỗi key thành **một dòng** vào `/home/arisp/.ssh/authorized_keys`.
Gỡ quyền một người = xoá đúng dòng của người đó, không ảnh hưởng ai khác và không phải đổi
mật khẩu DB cho cả nhóm.

### 1.3 Đóng cổng cũ

```bash
# Xác nhận không còn mapping nào ra 0.0.0.0
docker ps --format '{{.Names}}\t{{.Ports}}' | grep -i postgres
ss -tlnp | grep -E ':(8443|5432|5433)'
```

Cổng 5432 phải hiện `127.0.0.1:5432`, **không** phải `0.0.0.0:5432` hay `*:5432`.

Kiểm chứng từ **máy khác**, không phải từ VPS:

```bash
nmap -Pn -p 22,80,443,5432,5433,8443 161.248.147.38
```

Chỉ 22/80/443 được `open`. Còn lại phải `closed` hoặc `filtered`.

---

## Phần 2 — Cấu hình DBeaver (mỗi thành viên tự làm)

Trong cửa sổ *Connection settings* của kết nối `arisp_db`:

### Tab Main

| Trường | Giá trị |
|---|---|
| Connect by | Host |
| Host | `localhost` |
| Port | `5432` |
| Database | `arisp_db` |
| Authentication | Username/password |
| Username | `arisp_dev` (hoặc `postgres` nếu chưa tách quyền) |
| Password | lấy từ `docker/.env` trên VPS |

### Tab SSH

Bấm nút **`SSH, SSL, ...`** ở góc trên bên phải cửa sổ, chọn tab **SSH**:

| Trường | Giá trị |
|---|---|
| Use SSH Tunnel | ✅ bật |
| Host/IP | `161.248.147.38` |
| Port | `22` |
| User Name | `arisp` |
| Authentication Method | `Public Key` |
| Private Key | đường dẫn tới key trên máy bạn (`C:\Users\<ten>\.ssh\id_ed25519`) |
| Passphrase | nếu lúc `ssh-keygen` bạn có đặt |

Bấm **Test Connection** để xác nhận.

### Chỗ dễ nhầm nhất

`localhost:5432` ở tab Main **không phải máy bạn**. DBeaver mở SSH tới VPS trước, rồi từ bên
trong VPS mới nối tới Postgres. Vì thế:

- Host để `localhost` chứ **không** phải `161.248.147.38` — điền IP vào tab Main là sai, kết
  nối sẽ treo rồi timeout.
- Port là `5432` (cổng trong VPS), không phải `8443` như cấu hình cũ.

---

## Phần 3 — Tách quyền

> **Đã áp dụng trên production (2026-08-13).** Role `arisp_dev` đã tồn tại; cả nhóm dùng role này
> trong DBeaver, `postgres` superuser chỉ dành cho quản trị. Đã kiểm chứng: `arisp_dev` đọc/ghi được
> dữ liệu nhưng `DROP TABLE` bị chặn (`must be owner of table`).
>
> Vật liệu phát cho nhóm (private key + hướng dẫn từng người + mật khẩu `arisp_dev`) nằm ở
> `E:\ARISP-team-keys\PHAT-KEY-CHO-NHOM.md` trên máy trưởng nhóm — **ngoài repo, không commit**.
>
> `arisp_app` cho ứng dụng thì **chưa áp** — backend vẫn chạy bằng `postgres` vì nó cần quyền tạo
> bảng để chạy EF migration lúc boot.

SQL đã dùng (giữ lại để dựng lại môi trường khác):

```sql
-- Tài khoản cho ứng dụng (cần quyền tạo bảng: backend chạy EF migration lúc boot)
CREATE ROLE arisp_app LOGIN PASSWORD '<mat-khau-app>';
GRANT ALL PRIVILEGES ON DATABASE arisp_db TO arisp_app;
GRANT ALL ON SCHEMA public TO arisp_app;

-- Tài khoản cho người trong nhóm: đọc/ghi dữ liệu, KHÔNG sửa được cấu trúc
CREATE ROLE arisp_dev LOGIN PASSWORD '<mat-khau-nhom>';
GRANT CONNECT ON DATABASE arisp_db TO arisp_dev;
GRANT USAGE ON SCHEMA public TO arisp_dev;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO arisp_dev;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO arisp_dev;
```

Dòng `ALTER DEFAULT PRIVILEGES` là cần thiết: thiếu nó thì bảng do migration sau này tạo ra
sẽ không cấp quyền cho `arisp_dev`, và cả nhóm sẽ báo "bảng mới không xem được".

Sau đó đổi `docker/.env` sang `arisp_app` và giữ `postgres` chỉ để quản trị.

---

## Phần 4 — Backup

Xem `scripts/backup-db.sh`. Cài cron trên VPS:

```bash
chmod +x /var/www/ARISP/scripts/backup-db.sh
crontab -e
# thêm dòng:
0 3 * * * /var/www/ARISP/scripts/backup-db.sh >> /var/log/arisp-backup.log 2>&1
```

Ba điều cần nhớ:

1. **Backup nằm cùng máy với DB không cứu được khi mất VPS.** Phải copy định kỳ ra ngoài.
2. **Snapshot volume lúc Postgres đang chạy không đảm bảo nhất quán** — không thay được `pg_dump`.
3. **Bản dump chưa thử restore thì chưa phải backup.** Script đã tự đọc mục lục sau khi dump,
   nhưng nên restore thật vào một database tạm ít nhất một lần.

---

## Phần 5 — Sự cố thường gặp

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| Backend crash lúc boot, log báo lỗi SSL | Chuỗi kết nối còn `SSL Mode=Require` từ thời Supabase | Đổi thành `SSL Mode=Disable` trong `docker/.env` |
| Backend báo không nối được DB ở lần deploy đầu | `initdb` khởi tạo thư mục rỗng lâu hơn cửa sổ retry ~9 giây | Đã xử lý bằng `depends_on: postgres: service_healthy`; nếu vẫn gặp thì `docker compose restart backend` |
| rag-service trả câu hỏi vô nghĩa nhưng HTTP 200 | Thiếu `OPENAI_API_KEY` → chạy mock mode âm thầm | Kiểm tra nhóm biến UPPER_SNAKE trong `.env` |
| rag-service không nối được DB dù backend nối được | Nhóm biến `DATABASE_*` chưa sửa (hai hệ tên biến khác nhau) | Sửa `DATABASE_HOST=postgres`, `DATABASE_SSLMODE=disable` |
| DBeaver treo rồi timeout | Tab Main điền IP VPS thay vì `localhost` | Host = `localhost`, IP chỉ điền ở tab SSH |
| `docker compose down -v` đã chạy nhầm | Dữ liệu nằm ở bind mount `/var/lib/arisp/pgdata` nên **không** bị xoá | Chỉ cần `up -d` lại |
