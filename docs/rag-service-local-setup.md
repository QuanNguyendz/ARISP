# Chạy RAG service ở máy local (database = Supabase)

> **Đọc mục 1 trước.** Máy bạn đang có một cái bẫy: API và rag-service **mặc định trỏ vào hai
> database khác nhau**. Không xử lý thì RAG chạy mà không truy hồi được gì, **và không báo lỗi**.
>
> Tham chiếu: ADR-039 (rag-service), ADR-002/055 (Supabase là môi trường test, VPS là production).

---

## 1 · Cái bẫy: hai database

Cấu hình hiện tại trên máy bạn:

| Thành phần | Lấy cấu hình DB từ | Đang trỏ tới |
|---|---|---|
| **API (.NET)** khi `dotnet run` | `dotnet user-secrets` → `ConnectionStrings:DefaultConnection` | **Supabase** (`aws-1-ap-northeast-1.pooler.supabase.com`, SSL Require) |
| **rag-service** khi `docker compose up` | `docker/.env` → `DATABASE_HOST` | **container `postgres`** (`Host=postgres`, SSL Disable) |

Hai cái này **không phải một database**. Hậu quả khi để nguyên:

1. Bạn tải playbook lên → .NET ghi `playbook_documents` vào **Supabase**, rồi gọi `/ingest`.
2. rag-service cắt chunk + sinh vector → ghi `document_chunks` vào **container**.
3. Lúc phỏng vấn, rag-service truy hồi trong **container**, lọc theo `job_posting_id` — mà tin đó
   chỉ tồn tại ở **Supabase**.
4. Kết quả: **truy hồi trả về rỗng**. AI vẫn hỏi (nó có JD + CV trong payload), báo cáo vẫn sinh —
   **không lỗi nào hiện ra**. Bạn chỉ phát hiện khi để ý câu hỏi chẳng liên quan gì tới playbook.

> Đây đúng loại hỏng mà ADR-062 gọi là *"thể hiện trong luồng nhưng không hoạt động"*. Mục 3 kiểm
> đúng chỗ này.

**Cách xử lý:** cho rag-service trỏ vào **chính Supabase mà API đang dùng**. Nó đã được viết sẵn để
làm việc đó — `database_sslmode` mặc định `require`, và tầng kết nối đã xử lý giới hạn
prepared-statement của Supabase pooler (`statement_cache_size=0`).

---

## 2 · Cách chạy (khuyến nghị cho máy bạn)

Chạy **thẳng trên máy bằng venv**, không qua Docker. Đơn giản nhất vì API của bạn cũng chạy trên máy,
và cả hai cùng đọc một database.

### 2.1 Tạo `rag-service/.env`

File này **đã nằm trong `.gitignore`** nên không lo lộ lên git.

```ini
# ---- Database: DÙNG CHUNG với API (.NET) ----
# Lấy đúng các giá trị trong chuỗi kết nối của user-secrets:
#   dotnet user-secrets list --project ari-service/src/ARI.API
DATABASE_HOST=aws-1-ap-northeast-1.pooler.supabase.com
DATABASE_PORT=5432
DATABASE_NAME=postgres
DATABASE_USER=<Username trong chuỗi kết nối>
DATABASE_PASSWORD=<Password trong chuỗi kết nối>
DATABASE_SSLMODE=require

# ---- OpenAI: nhúng vector + sinh câu hỏi/chấm điểm ----
OPENAI_API_KEY=<key của bạn>
EMBEDDING_MODEL=text-embedding-3-small
EMBEDDING_DIM=1536
CHAT_MODEL=gpt-4o
JSON_MODEL=gpt-4o-mini

# ---- Truy hồi ----
RETRIEVE_TOP_K=8
RRF_K=60
```

> **Không copy `docker/.env` sang đây.** File đó có `DATABASE_HOST=postgres` — đúng cho môi trường
> Docker, sai cho máy bạn. Chép nguyên là rơi lại vào đúng cái bẫy ở mục 1.
>
> **`DATABASE_SSLMODE` phải là `require`**: Supabase từ chối kết nối không SSL.

### 2.2 Khởi động

```bash
cd rag-service
./.venv/Scripts/python.exe -m uvicorn app.main:app --port 8000 --reload
```

*(Linux/macOS: `./.venv/bin/python -m uvicorn app.main:app --port 8000 --reload`)*

Venv đã cài sẵn đủ `uvicorn · asyncpg · fastapi · langchain · langgraph · openai` — không cần
`pip install` gì thêm.

### 2.3 Chạy API trỏ vào rag-service

**Chạy bằng Visual Studio** (bấm ▶ / F5) — không phải làm gì thêm.
`Properties/launchSettings.json` đã khai sẵn biến cần thiết cho profile `http` và `https`:

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "RAG_SERVICE_URL": "http://localhost:8000"
}
```

> ⚠️ **Chọn profile `http` hoặc `https`, KHÔNG chọn `IIS Express`** — profile đó không khai
> `RAG_SERVICE_URL`, nên backend sẽ đi tìm hostname `rag-service` (tên nội bộ mạng Docker) và mọi
> thao tác AI hỏng. Đổi ở dropdown ngay cạnh nút ▶ trên thanh công cụ.

> **Không còn cờ `AI:Provider`.** Từ ADR-062, RAG service là đường DUY NHẤT — không có nhánh dự
> phòng nào để rơi vào, nên cũng không còn cấu hình nào có thể đặt sai.

**Chạy bằng terminal:**

```bash
# bash / Git Bash
RAG_SERVICE_URL=http://localhost:8000 dotnet run --project ari-service/src/ARI.API
```

```powershell
# PowerShell — KHÔNG có cú pháp gán biến ngay trước lệnh như bash
$env:RAG_SERVICE_URL = "http://localhost:8000"
dotnet run --project ari-service/src/ARI.API
```

> **`RAG_SERVICE_URL` bắt buộc khi chạy trên máy.** Bỏ trống thì backend dùng mặc định
> `http://rag-service:8000` — tên nội bộ mạng Docker, máy bạn không phân giải được, và mọi thao tác
> AI sẽ lỗi kết nối.
>
> **Cách biết chắc đã chạy đúng:** tải một playbook lên, terminal `uvicorn` phải hiện `POST /ingest`.

### 2.4 Frontend

```bash
cd ari-web && npm run dev:candidate   # 3000
cd ari-web && npm run dev:staff       # 3001
```

---

## 3 · Kiểm TRƯỚC khi test — 4 bước, làm hết

```bash
# 1. rag-service sống chưa
curl http://localhost:8000/health                    # phải 200

# 2. Nó nối được đúng database chưa (quan trọng nhất)
curl http://localhost:8000/docs                      # Swagger mở được là app khởi động OK
```

**3. Chứng minh hai bên dùng CHUNG một database.** Chạy đối chiếu:

```bash
# Số tin tuyển dụng mà API nhìn thấy
curl -s -H "Authorization: Bearer <token HR>" http://localhost:5000/api/jobs/admin | jq length
```

Rồi tải một playbook lên qua giao diện (`/hr/playbooks`) và xem log terminal rag-service: phải có
dòng `POST /ingest`. Nếu `/ingest` trả lỗi kết nối → `.env` sai. Nếu nó **thành công** nhưng bước 4
ra 0 → đang ghi nhầm database.

**4. Chunk đã vào đúng chỗ chưa.** Mở Supabase SQL Editor (hoặc DBeaver qua Supabase) chạy:

```sql
SELECT count(*) AS so_chunk,
       count(embedding) AS co_vector
  FROM document_chunks;
```

> **✅ Phải đạt:** `so_chunk > 0` **và** `co_vector = so_chunk`.
> - `so_chunk = 0` → rag-service đang ghi vào database khác. Xem lại `rag-service/.env`.
> - `co_vector < so_chunk` → cắt chunk chạy được nhưng sinh vector hỏng, thường do `OPENAI_API_KEY`.

---

## 4 · Điều kiện phía Supabase

Hai thứ phải có sẵn, đều do migration EF tạo — chạy API một lần là nó tự áp:

| Cần gì | Migration | Dùng cho |
|---|---|---|
| Extension `vector` + cột `embedding` | schema gốc | nhánh **dense** (`embedding <=> $1::vector`) |
| Index GIN `to_tsvector('simple', chunk_text)` | `20260626000000_AddDocumentChunksFtsIndex` | nhánh **sparse** (`ts_rank`) |

Kiểm nhanh:

```sql
SELECT extname FROM pg_extension WHERE extname = 'vector';
SELECT indexname FROM pg_indexes
 WHERE tablename = 'document_chunks' AND indexdef ILIKE '%to_tsvector%';
```

Thiếu cái thứ hai thì hybrid retrieval **vẫn chạy** nhưng nhánh sparse quét tuần tự — chậm dần theo
số chunk, không sai kết quả.

---

## 5 · Vì sao KHÔNG dùng `docker compose up` cho máy bạn

Vẫn dùng được, nhưng phải **ghi đè** `DATABASE_*` cho riêng service `rag-service`, vì `env_file: .env`
sẽ nạp `docker/.env` (trỏ container). Thêm vào `docker/docker-compose.override.yml`:

```yaml
services:
  rag-service:
    environment:
      DATABASE_HOST: aws-1-ap-northeast-1.pooler.supabase.com
      DATABASE_PORT: "5432"
      DATABASE_NAME: postgres
      DATABASE_SSLMODE: require
      # DATABASE_USER / DATABASE_PASSWORD: đặt trong docker/.env, đừng viết thẳng vào file này
```

Nhưng khi đó container `postgres` chạy mà **không ai dùng** — thừa. Chạy bằng venv (mục 2) gọn hơn
cho tình huống của bạn.

---

## 6 · Khác biệt local vs. deploy — đừng nhầm

| | Local (máy bạn) | Deploy (VPS) |
|---|---|---|
| Database | **Supabase** (pooler, SSL Require) | **Postgres tự host** trong container (SSL Disable) — ADR-055 |
| rag-service | venv trên máy, cổng 8000 | container trong mạng Docker, **không expose ra Internet** |
| `RAG_SERVICE_URL` | `http://localhost:8000` | `http://rag-service:8000` |
| `DATABASE_SSLMODE` | `require` | `disable` |
| Endpoint `/api/dev/*` | có | **404** (chỉ bật ở Development) |

> **Trước khi push để CI/CD chạy:** không cần đổi gì trong repo — `rag-service/.env` đã gitignore, và
> `docker/.env` trên VPS là file riêng của máy chủ. Chỉ cần bảo đảm trên VPS
> `DATABASE_SSLMODE=disable` (container nội bộ không phục vụ certificate — ADR-055).
