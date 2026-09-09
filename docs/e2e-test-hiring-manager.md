# Kịch bản test tay E2E — luồng Hiring Manager, Offer và trình soạn thư (ADR-061 + ADR-063)

> **Đọc trước khi bắt đầu.** Tài liệu này đi trọn phễu từ lúc Hiring Manager **lập phiếu yêu cầu
> tuyển dụng** tới lúc ứng viên nhận việc. Mỗi bước ghi rõ: **đăng nhập bằng ai · bấm nút nào ·
> điền gì · phải thấy gì**.
>
> Ô **✅ Kiểm** là điểm chốt — sai ở đó thì dừng lại, đừng đi tiếp, vì các bước sau dựa vào nó.
>
> Sơ đồ luồng: `docs/diagrams/arisp-e2e-test-flow.drawio` (mở bằng draw.io / diagrams.net).
>
> Tham chiếu: ADR-061 (vai trò HM, cổng duyệt, trình soạn thư, Offer), **ADR-063 (phiếu yêu cầu
> tuyển dụng, HM ký JD là đăng tin, HR Leader chốt thư mời)**, ADR-052/054/062 (Kiosk),
> ADR-048/059 (xếp lịch + thư mời), ADR-053 (đạt chỉ khi qua hết vòng).

> ### ⚠️ Ba thay đổi của ADR-063 làm khác hẳn lượt test trước
>
> 1. **Không tạo tin "trần" được nữa.** Mọi tin phải bắt nguồn từ một **phiếu yêu cầu tuyển dụng đã
>    duyệt**. Vào thẳng màn tạo tin sẽ bị chặn — xem **Phần R**.
> 2. **HM ký duyệt JD là tin lên `active` luôn.** Không còn bước "HR Leader bấm Duyệt đăng" — bước 5
>    của bản trước nay đổi thành *kiểm chứng tin đã tự đăng*.
> 3. **HM KHÔNG còn chốt được thư mời nhận việc.** HM đề xuất mức lương, **HR Leader chốt** — bước
>    16 đổi người đăng nhập, và có thêm một ô kiểm "phải bị chặn".
>
> ### ⚠️ ADR-064 thêm một bước nữa giữa phiếu và tin
>
> Nút trên phiếu **không còn đi thẳng tới màn tạo tin**. Nay là: phiếu → **soạn JD theo mẫu công ty**
> → xuất file → file tự đi kèm sang màn tạo tin → **HM mở đúng file đó để ký duyệt**. Xem **Phần J**.

---

## 0 · Chuẩn bị (~10 phút)

### 0.1 Khởi động — `rag-service` là thành phần BẮT BUỘC

RAG service (ADR-039) sở hữu **toàn bộ** phần chunk / embed / truy hồi / sinh câu hỏi / chấm điểm.
Nó được gọi ở **sáu điểm** trong kịch bản này, nên phải chạy từ đầu:

| Điểm gọi | Endpoint rag-service | Xuất hiện ở bước |
|---|---|---|
| Nhận diện ngôn ngữ từ JD | `POST /detect-language` | **R8** (dựng tin từ phiếu) · tin seed thì đã có sẵn |
| Nạp JD của tin mới vào RAG | `POST /ingest` | **R8** — dựng tin xong, terminal `uvicorn` phải hiện thêm một dòng `POST /ingest` |
| Nạp playbook → chunk + embed | `POST /ingest` | **A0** |
| Sinh câu hỏi phỏng vấn | `POST /next-question` | 12 |
| Phân tích từng câu trả lời | `POST /analyze-answer` | 12 |
| Sinh báo cáo đánh giá | `POST /evaluate` | 13–14 |
| Chấm năng lực ngôn ngữ | `POST /assess-language` | 13–14 |

> ⚠️ **Máy này đang dùng Supabase làm database local.** API lấy chuỗi kết nối từ `dotnet user-secrets`
> (trỏ `…pooler.supabase.com`), trong khi `docker/.env` trỏ container `postgres`. Chạy rag-service
> bằng Docker mà không ghi đè là **hai bên dùng hai database khác nhau** — truy hồi trả rỗng và
> **không báo lỗi gì**. Hướng dẫn đầy đủ: **[rag-service-local-setup.md](rag-service-local-setup.md)**.

**Cách chạy — mọi thứ trên máy, cùng một database (Supabase):**

```bash
# 1. RAG service (venv đã cài sẵn đủ thư viện)
#    Cần rag-service/.env trỏ ĐÚNG Supabase mà API dùng — xem rag-service-local-setup.md mục 2.1
cd rag-service
./.venv/Scripts/python.exe -m uvicorn app.main:app --port 8000 --reload

# 2. API — chạy bằng Visual Studio: chọn profile "http" hoặc "https" rồi bấm ▶ là xong
#    (launchSettings.json đã khai sẵn RAG_SERVICE_URL). ĐỪNG chọn "IIS Express".
#    Chạy bằng terminal thì biến này BẮT BUỘC:
RAG_SERVICE_URL=http://localhost:8000 dotnet run --project ari-service/src/ARI.API

# 3. Hai site — mỗi cái một terminal
cd ari-web && npm run dev:candidate   # http://localhost:3000  ứng viên + Kiosk
cd ari-web && npm run dev:staff       # http://localhost:3001  HR / Recruiter / HM
```

| Dịch vụ | Địa chỉ |
|---|---|
| Site ứng viên + Kiosk | `http://localhost:3000` |
| Site nhân sự | `http://localhost:3001` |
| API | `http://localhost:5000` |
| **RAG service** (Swagger) | `http://localhost:8000/docs` |
| Database | Supabase (dùng chung cho cả API lẫn rag-service) |

### 0.1b Kiểm RAG service TRƯỚC khi bắt đầu

Làm ngay, đừng để tới bước 12 mới phát hiện:

```bash
# 1. Sống chưa
curl http://localhost:8000/health          # phải 200

# 2. Backend có gọi tới RAG không — tải 1 playbook rồi xem terminal uvicorn:
#    phải hiện "POST /ingest". Không có dòng nào = backend đang trỏ sai địa chỉ.
```

> **✅ Kiểm 0.1b** — `/health` trả 200 **và** terminal `uvicorn` hiện `POST /ingest` khi bạn tải
> playbook ở bước A0. Từ ADR-062 **không còn nhánh dự phòng nào** để rơi vào: backend không gọi
> được RAG service thì lỗi hiện ra ngay, không xuống cấp im lặng.
>
> **✅ Kiểm 0.1b-2 — CÙNG MỘT DATABASE** (bẫy riêng của máy này): sau bước A0, chạy trên Supabase
> `SELECT count(*) FROM document_chunks;` phải **> 0**. Bằng 0 nghĩa là rag-service đang ghi vào
> database khác — sửa `rag-service/.env`.

**Theo dõi RAG trong lúc test** — để terminal `uvicorn` ở mục 0.1 hiện ra màn hình suốt buổi. Mỗi
thao tác có AI thì log phải nhảy. Không có dòng nào = RAG không được gọi.

### 0.1c Cách chạy thay thế — toàn bộ bằng Docker

Dùng được, nhưng phải **ghi đè `DATABASE_*` cho riêng `rag-service`**, vì `env_file: .env` sẽ nạp
`docker/.env` (trỏ container `postgres`) trong khi API của bạn dùng Supabase. Cách làm ở
[rag-service-local-setup.md](rag-service-local-setup.md) mục 5.

Khi đó container `postgres` chạy mà **không ai dùng** — thừa. Với máy bạn, cách ở 0.1 gọn hơn.

> ⚠️ **Hai cạm bẫy hostname**, cả hai đều hỏng im lặng hoặc khó đoán:
> - `docker/.env` khai `RAG_SERVICE_URL=http://rag-service:8000` — **tên nội bộ mạng Docker**, máy
>   bạn không phân giải được. Chạy API trên máy thì phải là `http://localhost:8000`.
> - `docker/.env` khai `DATABASE_HOST=postgres` — cũng là tên nội bộ, và **trỏ sai database** so với
>   Supabase mà API đang dùng.

### 0.1d Test trên môi trường đã deploy

Cùng kịch bản, chỉ đổi tên miền (ADR-047): ứng viên `https://arisp.io.vn` · nhân sự
`https://staff.arisp.io.vn`.

> **Khác biệt so với local — đừng nhầm:** local dùng **Supabase** (SSL `require`), deploy dùng
> **Postgres tự host trong container** (SSL `disable` — container không phục vụ certificate, ADR-055).
> Trên VPS `RAG_SERVICE_URL` là `http://rag-service:8000` (tên nội bộ mạng Docker), không phải
> `localhost`. Không cần đổi gì trong repo khi push: `rag-service/.env` đã gitignore và `docker/.env`
> trên máy chủ là file riêng của nó.

Trên VPS, **rag-service không expose ra Internet** — nó nằm sau mạng Docker nội bộ và không đi qua
Nginx (đúng thiết kế ADR-039). Muốn kiểm thì SSH vào máy chủ:

```bash
ssh arisp@<vps>
cd /opt/arisp/docker                       # hoặc thư mục deploy của bạn
docker compose ps rag-service
docker compose exec rag-service curl -s localhost:8000/health
docker compose logs -f rag-service         # để chạy trong lúc demo
```

> **Lưu ý khi demo bản deploy:** bỏ qua toàn bộ mục `/api/dev/*` — các endpoint đó **trả 404 ngoài
> môi trường Development**. Trên bản deploy phải tạo tin, tài khoản và hồ sơ bằng giao diện thật.

### 0.2 Dựng job 3 vòng + tài khoản test

```bash
curl -X POST "http://localhost:5000/api/dev/seed-interview-job?fresh=true"
```

Ghi lại từ kết quả trả về:

| Trường | Dùng ở bước |
|---|---|
| `candidate.email` = `practice.dev@arisp.local` / `Practice123!` | 6, 18 |
| `staff.email` = `hr.dev@arisp.local` / `Hr123456!` (HR Admin) | R, 1–5, 13, 16 |
| `jobPostingId` | 1 |
| `applicationId` | 7 |

> **Vì sao vẫn dùng seed dù ADR-063 bắt mọi tin phải có phiếu:** `seed-interview-job` ghi thẳng vào
> DB, không đi qua `CreateJobCommand`, nên nó tạo ra **đúng loại dữ liệu lịch sử** mà cột
> `recruitment_request_id` được phép để null. Tin seed có sẵn 3 vòng + ngân hàng đề + mã Kiosk nên
> phần phỏng vấn (Phần C) dùng nó cho nhanh. **Phần R** dựng một tin thứ hai qua đúng đường phiếu để
> chứng minh cổng mới — tin đó chỉ cần đi tới bước đăng là đủ, không cần phỏng vấn.

### 0.3a Tạo đội/bộ phận (ADR-065)

Phải làm **trước** khi tạo tài khoản Hiring Manager: tài khoản chưa có đội thì không lập được phiếu.

1. Đăng nhập Super Admin tại `http://localhost:3001/admin/login`.
2. Thanh bên → **Đội / Bộ phận** → **Thêm đội**. Tạo hai đội:
   - `Backend Team` · mã `BE`
   - `QA Team` · mã `QA`

> **✅ Kiểm 0.3a** — Tạo thêm một đội tên `backend team` (viết thường) → **phải bị chặn vì trùng tên**.
> Postgres so sánh phân biệt hoa thường, nên nếu chỉ dựa vào unique index thì đây là hai đội khác nhau và
> mọi thống kê theo đội sẽ tách đôi mà không ai để ý.
>
> **✅ Kiểm 0.3a-b** — Thẻ mỗi đội hiện **số người đang thuộc đội**, và **không có nút xoá** — chỉ có ô
> tích *Đang hoạt động*. Đội giải thể thì tắt, vì phiếu và tài khoản cũ vẫn phải tra được tên.

### 0.3 Tạo tài khoản Hiring Manager

Seed **không** tạo sẵn vai này — phải tạo tay, và đây cũng chính là bài test đầu tiên.

1. Vẫn là Super Admin → **Quản lý Users → Tất cả người dùng** → nút **Thêm staff**.
2. Điền: email `hm.dev@arisp.local` · họ tên `Trần Hiring Manager` · **Vai trò: `Hiring Manager`** ·
   **Đội: `Backend Team`** (ô chọn, không còn gõ tay — ADR-065).
3. Lưu → hệ thống gửi mật khẩu tạm qua email. Không có SMTP thì lấy mật khẩu ở bảng `users` hoặc đặt lại tay.

> **✅ Kiểm 0.3** — Dropdown vai trò **phải có mục "Hiring Manager"**. Trước ADR-061 chỉ có Recruiter
> và HR Admin. Ô **Đội / Bộ phận** phải là **danh sách chọn**, không phải ô nhập chữ.
>
> **✅ Kiểm 0.3c — QUAN TRỌNG** — Đăng nhập `hm.dev@arisp.local` → **Cài đặt cá nhân**: ô phòng ban phải
> **khoá, không sửa được**. Đây là lỗ hổng chính mà ADR-065 đi vá: trước đây nhân viên tự đổi được phòng ban
> của chính mình, nên khoá ô đội trên phiếu chỉ là hình thức. Kiểm tận gốc bằng chính lệnh sửa hồ sơ:
>
> ```bash
> curl -i -X PUT "http://localhost:5000/api/staff/profile" \
>   -H "Authorization: Bearer <TOKEN_CUA_HM>" \
>   -H "Content-Type: application/json" \
>   -d '{"fullName":"Trần Hiring Manager","department":"QA Team"}'
> ```
>
> Trả **200** nhưng đội **không đổi** — lệnh không còn **nhận** tham số đó nữa. Đổi được là hỏng cổng ADR-065.
>
> **✅ Kiểm 0.3b** — Đăng nhập `hm.dev@arisp.local` tại `/admin/login` → phải vào thẳng
> **`/hm/dashboard`**, thanh bên chỉ có 5 mục: Tổng quan · Tin tuyển dụng của tôi · Hồ sơ chờ duyệt ·
> Kết quả phỏng vấn · Thư mời nhận việc. **Không** có mục xếp lịch, cấp mã hay playbook.

> ⚠️ **Đừng dùng `?dev=hm`.** Tham số đó gắn một user giả với token giả — giao diện hiện ra nhưng
> **mọi lời gọi API đều 401**. Chỉ dùng để ngắm giao diện, không dùng để test E2E.

---

## R · Phiếu yêu cầu tuyển dụng — điểm bắt đầu bắt buộc (ADR-063)

> Phần này **mới hoàn toàn**. Nó chứng minh bốn điều: nhu cầu tuyển bắt đầu từ HM, HR Leader kiểm
> soát dải lương, **không ai tự duyệt phiếu của mình**, và tin chỉ dựng được từ phiếu đã duyệt bởi
> đúng người được phân công.

### Bước R1 — HM lập phiếu
**Đăng nhập:** `hm.dev@arisp.local`

1. Thanh bên → **Phiếu yêu cầu tuyển dụng** → nút **Lập phiếu**.
2. Điền: vị trí `Senior Backend Engineer` · số lượng `2` · **mức độ ưu tiên `Ưu tiên cao`** ·
   **ngày dự kiến bắt đầu** một ngày trong tương lai · thời gian `Toàn thời gian` · cấp bậc `Senior` ·
   hình thức `Tại văn phòng` · nơi làm việc `Hà Nội` ·
   **lương tối thiểu `60000000`** · **tối đa `90000000`** (cố ý đặt cao để HR Leader trả lại ở R4) ·
   lý do `Mở rộng đội cho dự án mới` · mô tả sơ bộ vài dòng.

   > Ô **Hiring Manager** và **Đội / Bộ phận** điền sẵn và **không sửa được** — đội phải hiện
   > `Backend Team` lấy từ tài khoản, không phải do gõ.
3. Ô **Yêu cầu ứng viên** — gõ **mỗi dòng một tiêu chí** (đây là nguyên liệu chính để Recruiter dựng
   JD, nên cố ý gõ nhiều dòng để kiểm ở R8c):

   ```
   Thành thạo C# / ASP.NET MVC
   Nắm chắc OOP, biết áp dụng trong dự án
   Tối thiểu 3 tháng kinh nghiệm làm việc thực tế
   ```

4. **Gửi phiếu**.

> **✅ Kiểm R1** — Phiếu hiện trong danh sách với chip **"Chờ duyệt"**, dòng phụ ghi đúng số lượng và
> dải lương.
>
> **✅ Kiểm R1b** — Đăng nhập `hr.dev@arisp.local` → **chuông có thông báo mới** và phiếu xuất hiện ở
> màn **Phiếu yêu cầu tuyển dụng** của HR. Không thấy = bảng mới chưa gắn trigger realtime
> (`SELECT arisp_attach_change_triggers();` — ADR-057).

> **✅ Kiểm R1c — đội do SERVER quyết định (ADR-065)** — Lập phiếu bằng API và cố tình khai đội khác:
>
> ```bash
> curl -i -X POST "http://localhost:5000/api/recruitment-requests" \
>   -H "Authorization: Bearer <TOKEN_CUA_HM>" \
>   -H "Content-Type: application/json" \
>   -d '{"title":"Thử đổi đội","departmentId":"<ID_CUA_QA_TEAM>","headcount":1,"priority":"medium",
>        "expectedStartDate":"2026-12-01","reason":"thử","description":"thử","requirements":"thử",
>        "salaryNegotiable":true}'
> ```
>
> Phiếu tạo ra phải mang đội **`Backend Team`**, không phải QA. Đây là toàn bộ mục đích của ADR-065 —
> khoá ô ở giao diện là chưa đủ, một request tự dựng vẫn đi qua. Xong nhớ **rút phiếu thử** này.
>
> **✅ Kiểm R1d — các ô bắt buộc** — Bỏ trống lần lượt *Ngày dự kiến bắt đầu* / *Lý do tuyển* /
> *Mô tả sơ bộ* / *Yêu cầu ứng viên* → mỗi ô phải **chặn riêng**, không gộp một thông báo chung chung.
>
> **✅ Kiểm R1e — ô tích "Thoả thuận"** — Tích vào → hai ô lương **khoá và trắng** (không được giữ lại
> con số cũ dưới ô đã khoá — nhìn thấy "thoả thuận" mà server vẫn nhận được lương là hai thứ mâu thuẫn
> trên cùng một phiếu). Bỏ tích rồi để trống cả hai ô → **phải bị chặn**, vì "thoả thuận" và "quên điền"
> không được lẫn vào nhau.
>
> **✅ Kiểm R1f — hàng chờ theo mức độ ưu tiên** — Lập thêm một phiếu mức **Ưu tiên thấp**. Ở màn của
> HR Leader, phiếu **Ưu tiên cao phải nằm trên**, và bộ lọc *Mức độ ưu tiên* lọc đúng.

### Bước R2 — HM thử tự duyệt phiếu của mình → phải KHÔNG có nút
**Vẫn là:** `hm.dev@arisp.local`

1. Mở lại phiếu vừa lập.

> **✅ Kiểm R2 — QUAN TRỌNG** — **Không có** khối "Phân công Recruiter", **không có** nút *Duyệt &
> phân công* hay *Trả lại*. Chỉ còn **Sửa phiếu** và **Rút phiếu**.
>
> Cờ quyền do **server** tính (`canReview`), không phải giao diện tự đoán theo vai trò. Muốn kiểm tận
> gốc thì gọi thẳng API bằng token của HM:
>
> ```bash
> curl -i -X POST "http://localhost:5000/api/recruitment-requests/<REQUEST_ID>/approve" \
>   -H "Authorization: Bearer <TOKEN_CUA_HM>" \
>   -H "Content-Type: application/json" \
>   -d '{"assignedRecruiterId":"<ID_RECRUITER>"}'
> ```
>
> Phải trả **403**. Qua được là hỏng cổng chính của ADR-063.

### Bước R3 — HR Leader thử trả lại mà không nêu lý do → phải bị chặn
**Đăng nhập:** `hr.dev@arisp.local`

1. **Phiếu yêu cầu tuyển dụng** → mở phiếu → bấm **Trả lại**.
2. Để trống ô lý do, hoặc gõ vài ký tự như `sai`.

> **✅ Kiểm R3** — Nút **Xác nhận trả lại bị tắt** cho tới khi lý do đủ **10 ký tự**. Phiếu **vẫn ở
> "Chờ duyệt"**. (Server cũng kiểm lại — gọi thẳng API với lý do rỗng phải trả 400.)

### Bước R4 — HR Leader trả phiếu về kèm lý do
**Vẫn là:** `hr.dev@arisp.local`

1. Nhập lý do: `Dải lương vượt khung của cấp bậc này, đề nghị hạ xuống mức 45-60 triệu` →
   **Xác nhận trả lại**.

> **✅ Kiểm R4** — Chip đổi thành **"Bị trả lại"** (nền đỏ), thẻ phiếu hiện nguyên văn lý do.
>
> **✅ Kiểm R4b** — Đăng nhập HM → chuông báo *"Phiếu yêu cầu tuyển dụng cần chỉnh sửa"*.

### Bước R5 — HM sửa dải lương rồi gửi lại
**Đăng nhập:** `hm.dev@arisp.local`

1. Mở phiếu → **Sửa phiếu** → đổi lương tối thiểu `45000000`, tối đa `60000000` → **Gửi phiếu**.
2. Quay lại phiếu → bấm **Gửi lại**.

> **✅ Kiểm R5** — Chip về **"Chờ duyệt"**, xuất hiện nhãn **"Lần gửi thứ 2"**.
>
> **✅ Kiểm R5b** — **Lý do bị trả lại lần trước vẫn còn hiển thị.** Đây là chủ ý: HR Leader cần biết
> vòng này sửa gì so với vòng trước. Xoá đi thì mỗi vòng duyệt lại bắt đầu từ con số không.

### Bước R6 — HR Leader duyệt kèm phân công Recruiter
**Đăng nhập:** `hr.dev@arisp.local`

1. Mở phiếu → khối **Phân công Recruiter** → chọn một Recruiter → **Duyệt & phân công**.

> **✅ Kiểm R6** — Chip sang **"Đã duyệt"**, dòng phụ hiện *"Phụ trách: <tên Recruiter>"*.
>
> **✅ Kiểm R6b** — Dropdown chỉ liệt kê **tài khoản Recruiter còn hoạt động**, kèm số tin đang tuyển
> của mỗi người; người **cùng phòng ban** với phiếu được chọn sẵn.
>
> **✅ Kiểm R6c** — Duyệt **luôn kèm phân công**: nút tắt khi chưa chọn ai. Duyệt xong mà không có
> người phụ trách thì phiếu nằm im — đúng lỗi ADR-059 đã chữa cho "duyệt CV mà chưa gán ca".
>
> **✅ Kiểm R6d** — Recruiter được chọn nhận **chuông báo việc mới**.

### Bước R7 — Thử tạo tin KHÔNG qua phiếu → phải bị chặn
**Đăng nhập:** tài khoản Recruiter vừa được phân công

1. Vào thẳng `http://localhost:3001/recruiter/jobs/create` (**không** kèm `?requestId=`).

> **✅ Kiểm R7a — ô chọn phiếu phải HIỆN RA** — Ngay trên cùng, trước cả ô tải JD, có khối
> **"Phiếu yêu cầu tuyển dụng \*"** kèm ô chọn. Danh sách chỉ gồm phiếu **đã duyệt, giao cho bạn,
> và chưa dựng tin nào** — phiếu đã có tin không được hiện, vì chọn trúng thì lúc lưu bị 409.
>
> Không có phiếu nào đủ điều kiện thì khối này hiện lời nhắc màu vàng kèm liên kết mở màn Phiếu yêu
> cầu tuyển dụng — chứ không phải một ô chọn rỗng để người dùng tự đoán.

2. **Không chọn phiếu**, điền qua loa vị trí + mô tả rồi bấm **Tạo tin**.

> **✅ Kiểm R7b — QUAN TRỌNG** — Bị chặn **ngay**, thông báo: *"Tin tuyển dụng phải được dựng từ một
> phiếu yêu cầu tuyển dụng đã duyệt…"*. Đây là ràng buộc trung tâm của ADR-063; qua được là mọi cổng
> phía trên trở thành trang trí.
>
> **✅ Kiểm R7c — HR Leader cũng không có ngoại lệ** — Đăng nhập `hr.dev@arisp.local`, vào
> `http://localhost:3001/hr/jobs/create` và làm y hệt: cũng **bị chặn**. Khác biệt duy nhất của
> quản trị viên là họ dựng được tin từ phiếu giao cho Recruiter khác, chứ không phải được bỏ qua phiếu.

### Bước R7b — HR Leader KHÔNG lập được phiếu
**Đăng nhập:** `hr.dev@arisp.local`

1. Mở màn **Phiếu yêu cầu tuyển dụng**.

> **✅ Kiểm R7b-1** — **Không có nút "Lập phiếu"**. Chỉ Hiring Manager lập phiếu, và lý do nằm ngoài
> chuyện phân quyền: `CreateJobCommand` gán **người lập phiếu làm Hiring Manager của tin** (ADR-063), nên
> HR Leader lập phiếu là tự đặt mình vào **cả hai đầu** của các cổng mà ADR-061/063 dựng lên để tách nhau.
>
> **✅ Kiểm R7b-2 — kiểm tận gốc** — Ẩn nút không phải là chặn. Gọi thẳng API bằng token HR Leader:
>
> ```bash
> curl -i -X POST "http://localhost:5000/api/recruitment-requests" \
>   -H "Authorization: Bearer <TOKEN_CUA_HR_LEADER>" \
>   -H "Content-Type: application/json" \
>   -d '{"title":"Thử lập hộ","headcount":1,"priority":"medium","expectedStartDate":"2026-12-01T00:00:00Z",
>        "reason":"thử","description":"thử","requirements":"thử","salaryNegotiable":true}'
> ```
>
> Phải trả **403** (policy `RecruitmentRequestAuthoring`). Thử lại bằng token Super Admin và Recruiter —
> cũng **403**.
>
> **✅ Kiểm R7b-3** — Tắt `QA Team` ở màn **Đội / Bộ phận**, rồi đăng nhập HM thuộc đội đó → phiếu cũ của
> họ **vẫn hiện đúng tên đội** — tắt là ngăn gán mới, không phải xoá lịch sử. Nhớ **bật lại**.
>
> **✅ Kiểm R7b-4** — Cổng "không ai tự duyệt phiếu của chính mình" (ADR-063) **vẫn còn** dù đường trên đã
> đóng: nó chặn theo NGƯỜI, nên vẫn kín với phiếu cũ hoặc tài khoản đổi vai từ HM sang HR Leader.

### Bước R7c — Nhu cầu đổi sau khi phiếu đã duyệt (ADR-066)
**Đăng nhập:** `hm.dev@arisp.local`

Phiếu ở bước R6 đã duyệt nhưng **chưa dựng thành tin** — đúng cửa sổ mà phần này kiểm.

1. Mở phiếu đã duyệt.

> **✅ Kiểm R7c-1** — Có khối **"Nhu cầu tuyển đã thay đổi?"** với hai nút **Mở lại để sửa** và
> **Đóng phiếu**. Trước ADR-066 phiếu duyệt xong là đóng băng — không có đường nào ngoài việc lập phiếu mới.
>
> **✅ Kiểm R7c-2 — lý do bắt buộc** — Bấm **Mở lại để sửa**, gõ dưới 10 ký tự → nút xác nhận **vẫn mờ**.
> Thao tác này lấy mất việc đã giao cho Recruiter, nên họ phải đọc được vì sao.

2. Gõ lý do thật (ví dụ *"Đội đổi hướng, cần thêm một người nữa"*) → **Mở lại phiếu**.

> **✅ Kiểm R7c-3 — QUAN TRỌNG** — Phiếu quay về **Chờ duyệt**, **mất tên Recruiter được phân công**,
> và HM **sửa được ngay**. Phân công phải đi theo chữ ký: ADR-063 chốt "duyệt kèm phân công trong cùng
> một thao tác", để lại Recruiter trên phiếu đang chờ duyệt là tự tạo ngoại lệ cho chính bất biến đó.
>
> **✅ Kiểm R7c-4 — người mất việc phải được báo** — Đăng nhập tài khoản Recruiter vừa bị gỡ: **chuông có
> thông báo** kèm lý do, và phiếu **không còn** trong danh sách việc của họ. Đây là ô kiểm dễ hỏng nhất:
> trigger realtime (ADR-057) **không** thay được, vì payload mang `assigned_recruiter_id` của hàng MỚI —
> đúng cột vừa bị xoá. HR Leader đã duyệt cũng phải nhận được thông báo.

3. HM sửa số lượng thành `3` → HR Leader duyệt lại kèm phân công (như bước R6).

> **✅ Kiểm R7c-5** — Phiếu hiện **số vòng gửi tăng thêm một**: mở lại là một vòng sửa–gửi lại thật.

4. Thử **Đóng phiếu** trên chính phiếu vừa được duyệt lại, kèm lý do.

> **✅ Kiểm R7c-6** — Phiếu sang **Đã huỷ**, hiện banner *"Phiếu đã đóng. Lý do: …"* kèm tên người
> thực hiện, và **vẫn giữ tên Recruiter + người duyệt** — phiếu đóng là hồ sơ lịch sử, khác hẳn mở lại.
> Ghi chú của người duyệt **không bị ghi đè** bởi lý do đóng.
>
> **✅ Kiểm R7c-7** — Phiếu đã đóng thì khối hai nút **biến mất**, và nút dựng tin cũng không còn.

5. **Ca phải bị chặn — làm sau bước R8** (khi đã có một phiếu dựng thành tin): mở phiếu đó.

> **✅ Kiểm R7c-8 — QUAN TRỌNG NHẤT** — Khối "Nhu cầu tuyển đã thay đổi?" **không hiện**. Kiểm tận gốc:
>
> ```bash
> curl -i -X POST "http://localhost:5000/api/recruitment-requests/<REQUEST_ID_DA_CO_TIN>/reopen" \
>   -H "Authorization: Bearer <TOKEN_CUA_HM>" \
>   -H "Content-Type: application/json" \
>   -d '{"reason":"thử mở lại phiếu đã có tin"}'
> ```
>
> Phải **bị từ chối** kèm câu chỉ sang thao tác trên chính tin. Qua được là tạo ra một tin đang chạy mà phiếu
> nguồn của nó đang "chờ duyệt", rồi vòng dựng tin lần hai chết bằng 409 của unique index một-phiếu-một-tin.
>
> **✅ Kiểm R7c-9 — Recruiter không thu hồi được** — Gọi chính endpoint trên bằng token của Recruiter được
> phân công, trên một phiếu chưa có tin → **403**. Họ thực thi nhu cầu chứ không huỷ bỏ nó.
>
> **✅ Kiểm R7c-10 — Rút phiếu chỉ đường, không làm thay** — Trên một phiếu đã duyệt, gọi
> `POST /api/recruitment-requests/<id>/cancel` → bị chặn kèm câu *"dùng Đóng phiếu (kèm lý do)"*.
> Đường `cancel` không đòi lý do và không báo cho ai — đúng cho phiếu chưa ai duyệt, sai khi đã có người cầm việc.

---

## J · Mẫu JD công ty và trình soạn JD (ADR-064)

> Vì sao có phần này: phiếu của HM chỉ có vài dòng gõ vội, nên tin dựng thẳng từ đó quá mỏng để đăng
> ra career site. Và chính **file JD** mới là thứ Hiring Manager mở ra để ký duyệt ở bước 4.

### Bước J1 — HR Leader cấu hình mẫu JD
**Đăng nhập:** `hr.dev@arisp.local`

1. Thanh bên → **Mẫu JD**.
2. **Tải logo lên** (PNG/JPG, ≤2MB) · điền tên công ty `Eastern Sun` · địa chỉ · website.
3. Chọn một **màu nhấn** khác mặc định (vd đỏ) để dễ nhận ra ở bước J4.
4. Ở khối **Các mục của JD**: đổi tiêu đề mục `benefits` thành `Chế độ đãi ngộ`, **tắt** mục
   `workingTime`, và bấm mũi tên đưa `requirements` **lên trên** `description`.
5. **Lưu mẫu**.

> **✅ Kiểm J1 — xem trước đổi theo ngay** — Cột phải hiện logo vừa tải, tên công ty, màu nhấn mới,
> và các mục **đúng thứ tự vừa đổi**; mục `workingTime` biến mất.
>
> **✅ Kiểm J1b — khoá của mục không sửa được** — Ô `key` (`description`, `benefits`…) hiện dạng chữ
> xám, không phải ô nhập. Đây là chủ ý: nội dung JD đã soạn tra theo đúng khoá đó, đổi khoá là làm
> mất nội dung của mọi bản JD đã có **mà không sinh ra lỗi nào**.
>
> **✅ Kiểm J1c — tắt hết mục phải bị chặn** — Bỏ chọn toàn bộ mục rồi Lưu: phải báo lỗi
> *"Phải bật ít nhất một mục…"*. Lọt xuống thì file JD xuất ra chỉ có mỗi đầu trang.

### Bước J2 — Recruiter mở trình soạn JD từ phiếu
**Đăng nhập:** Recruiter được phân công ở bước R6

1. **Phiếu yêu cầu tuyển dụng** → chọn phiếu → cột phải → nút **"Soạn JD theo mẫu công ty"**.

> **✅ Kiểm J2 — nút đã ĐỔI** — Không còn *"Dựng tin tuyển dụng từ phiếu này"*. Bấm vào mở
> `/recruiter/recruitment-requests/<id>/jd`.
>
> **✅ Kiểm J2b — điền sẵn từ phiếu, không phải trang trắng** — Ô vị trí, bộ phận, số lượng, dải lương
> đã có sẵn. Quan trọng hơn: **hai ô HM đã điền trên phiếu rơi đúng vào hai mục đầu** — phần "Mô tả
> sơ bộ" vào mục *Mô tả công việc*, phần "Yêu cầu ứng viên" vào mục *Yêu cầu*. Đó là toàn bộ lý do ô
> "yêu cầu ứng viên" tồn tại trên phiếu.
>
> **✅ Kiểm J2c — chỉ hiện mục ĐANG BẬT, đúng thứ tự của mẫu** — Mục `workingTime` (đã tắt ở J1)
> không xuất hiện; *Yêu cầu* nằm **trên** *Mô tả công việc*; mục quyền lợi mang tiêu đề mới
> `Chế độ đãi ngộ`.

### Bước J3 — Soạn nội dung rồi lưu nháp
1. Viết thêm vài dòng vào mỗi mục — **mỗi ý một dòng**.
2. Bấm **Lưu nháp**, tải lại trang.

> **✅ Kiểm J3** — Nội dung vừa gõ **còn nguyên** sau khi tải lại. Không lưu được nội dung thì mỗi lần
> HM trả JD về, Recruiter phải soạn lại từ đầu.

### Bước J4 — Xuất file và kiểm mẫu có thật sự được áp dụng
1. Bấm **PDF** → file mở ở tab mới. Bấm tiếp **DOCX** → mở bằng Word.

> **✅ Kiểm J4 — QUAN TRỌNG NHẤT CỦA ADR-064** — Cả hai file phải có:
> - **logo công ty** ở đầu trang;
> - tên/địa chỉ công ty và **màu nhấn** đúng như cấu hình ở J1;
> - các mục **đúng thứ tự và đúng tiêu đề đã đổi** (`Chế độ đãi ngộ`, không có `workingTime`);
> - mỗi dòng đã gõ thành **một gạch đầu dòng**;
> - dải lương in kiểu Việt Nam: `20.000.000 – 30.000.000 VND` (dấu **chấm**, không phải dấu phẩy).
>
> **✅ Kiểm J4b — hai bản xuất giống nhau** — DOCX và PDF phải cùng nội dung, cùng thứ tự mục. Lệch
> nhau nghĩa là ai đó đã sửa một bộ xuất mà quên bộ kia.

### Bước J5 — Dựng tin từ JD
1. Bấm **"Dựng tin từ JD này"**.

> **✅ Kiểm J5 — file ĐI KÈM, không phải tải lên lại** — Màn tạo tin mở ra và ô JD **đã có sẵn file**,
> kèm dòng *"File JD này lấy từ bản soạn theo mẫu công ty…"*. Không phải bấm "Upload & phân tích JD".
>
> **✅ Kiểm J5b — các trường điền từ nội dung có cấu trúc** — Tiêu đề, bộ phận, dải lương, số lượng và
> phần mô tả công việc đã điền theo bản JD vừa soạn (không phải Gemini đoán lại từ file).

### Bước J6 — Đường upload tay vẫn còn

> **✅ Kiểm J6** — Ở màn tạo tin, bấm **Upload & phân tích JD** rồi chọn một file PDF bất kỳ: vẫn
> chạy như cũ (Gemini phân tích, điền các trường). ADR-064 **không** bỏ đường này — tin không đi qua
> trình soạn vẫn phải tạo được.

---

### Bước R8 — Recruiter dựng tin từ phiếu
**Vẫn là:** Recruiter được phân công

> **ADR-064 đã đổi bước này:** nút trên phiếu nay là *"Soạn JD theo mẫu công ty"* và đi qua **Phần J**
> ở trên. Phần R8 dưới đây mô tả trạng thái sau khi bấm *"Dựng tin từ JD này"* ở bước J5, hoặc khi vào
> thẳng màn tạo tin.

1. Từ bước **J5** (hoặc mở thẳng màn tạo tin rồi chọn phiếu ở ô trên cùng).
2. Biểu mẫu ở `/recruiter/jobs/create?requestId=…` (HR Leader thì là `/hr/jobs/create?requestId=…` — route trong StaffSite khoá chặt theo vai trò).

> **✅ Kiểm R8a — ô chọn phiếu đã chọn sẵn đúng phiếu vừa bấm.** Đổi sang phiếu khác trong ô chọn thì
> toàn bộ phần điền sẵn (tiêu đề, phòng ban, dải lương, số lượng, bản nháp JD) **đổi theo** — cố ý
> ghi đè, vì đổi phiếu nghĩa là vừa nhận ra chọn nhầm, giữ lại nội dung phiếu cũ thì tin mang nửa nọ
> nửa kia.

> **✅ Kiểm R8 — điền sẵn** — Vị trí, bộ phận, **dải lương 45–60 triệu** (con số *sau khi sửa*, không
> phải mức HM đề xuất ban đầu), số lượng, hình thức làm việc **đã điền sẵn từ phiếu**.
>
> **✅ Kiểm R8c — bản nháp JD dựng từ phiếu** — Ô **Mô tả công việc** không được để trắng. Nó phải có
> **hai mục có tiêu đề**:
>
> - **Mô tả công việc** — nội dung ô "Mô tả sơ bộ công việc" của phiếu
> - **Yêu cầu ứng viên** — ba dòng gõ ở R1, mỗi dòng thành **một gạch đầu dòng**
>
> Đây là lý do ô "Yêu cầu ứng viên" tồn tại trên phiếu: Recruiter mở ra đã có khung để sửa thay vì
> một ô trắng, và **không phải tự đoán yêu cầu kỹ thuật** — chỉ trưởng bộ phận biết vế đó.
> Gõ một dòng duy nhất thì ra đoạn văn, nhiều dòng thì ra danh sách.

3. Bổ sung JD (tải file PDF/DOCX), cấu hình **1 vòng** là đủ cho phần này → **Tạo tin**.

> **✅ Kiểm R8b — CỔNG QUAN TRỌNG NHẤT CỦA ADR-063** — Mở tin vừa tạo → thẻ **"Đội tuyển dụng"** phải
> **đã có sẵn `Trần Hiring Manager`** kèm chip *"Người quyết định"*, **mà không ai gán tay**.
>
> Không có bước này thì cổng ký duyệt JD **không tồn tại** (ADR-061: cờ bật cổng suy ra từ việc có ai
> được gán) — tin sẽ ra job board mà không ai ký duyệt.

### Bước R9 — Thử dựng tin thứ hai từ cùng phiếu → phải bị chặn
**Vẫn là:** Recruiter đó

1. Quay lại phiếu.

> **✅ Kiểm R9** — Nút *"Dựng tin tuyển dụng từ phiếu này"* **biến mất**, thay bằng dòng *"Phiếu này
> đã có tin tuyển dụng."* Gọi thẳng API với cùng `requestId` phải trả **409 Conflict** (DB còn một
> unique index chặn cả hai request đồng thời).

### Bước R10 — Recruiter khác không nhận được việc của người được phân công *(cần 2 Recruiter)*

> **✅ Kiểm R10** — Đăng nhập Recruiter **không** được phân công → màn Phiếu yêu cầu tuyển dụng
> **không thấy phiếu này**. Gọi API tạo tin với `requestId` đó phải trả **403**.
> Thiếu bước kiểm này thì việc HR Leader chọn người lúc duyệt chỉ còn là gợi ý.

---

## A · Nạp playbook và ký duyệt mô tả công việc

### Bước A0 — Nạp Playbook và bộ tiêu chí vào RAG *(bước chứng minh RAG rõ nhất)*
**Đăng nhập:** `hr.dev@arisp.local` → vào **Playbook** (`/hr/playbooks`)

Đây là chỗ pipeline RAG hiện ra tường minh nhất: tài liệu đi vào → rag-service **cắt thành chunk,
sinh embedding, ghi vào pgvector** → buổi phỏng vấn và bước chấm điểm về sau truy hồi từ chính các
chunk đó. Ba tài liệu dưới đây phục vụ ba mục đích khác nhau — **đừng bỏ cái nào**.

---

#### A0a — Bộ tiêu chí chấm phỏng vấn ⚠️ **BẮT BUỘC**

> **Không có bước này thì bước 14 KHÔNG sinh được báo cáo đánh giá.** Từ ADR-062, tin chưa khai bộ
> tiêu chí thì hệ thống **không chấm** — thay vì bịa một điểm tổng do model tự nghĩ ra. Hệ thống ghi
> log lỗi và dừng, phiên vẫn đóng bình thường (transcript và bản ghi hình đã lưu xong).

1. Bấm **Tải file mẫu** với loại **`Tiêu chí chấm phỏng vấn`** → tải về `mau-tieu-chi-cham-phong-van.xlsx`.
2. Mở file, sửa đè lên ví dụ có sẵn. File mẫu có 4 cột:

   | Mã tiêu chí (a-z, _) | Tên hiển thị | Trọng số (%) — tổng phải = 100 | Chuẩn chấm (tuỳ chọn) |
   |---|---|---|---|
   | `technical` | Chuyên môn | 60 | Nêu được đánh đổi, không chỉ liệt kê công nghệ |
   | `communication` | Giao tiếp | 40 | Trình bày mạch lạc, có ví dụ cụ thể |

3. **Thử sai trước cho chắc:** đổi 40 thành `50` (tổng = 110) rồi tải lên.

   > **✅ Kiểm A0a-1** — **Bị từ chối ngay tại cổng upload**, thông báo *"Tổng trọng số phải bằng 100,
   > hiện là 110"*. Lọt được xuống dưới thì mọi điểm chấm sau đó đều sai mà không có tín hiệu nào.

4. Sửa lại cho tổng = 100 → tải lên với **Phạm vi: `Theo tin`** → chọn `[DEV] Kiosk Sandbox`,
   **Loại tài liệu: `Tiêu chí chấm phỏng vấn`**.

   > **✅ Kiểm A0a-2** — Tài liệu hiện trong danh sách, cột số tiêu chí đọc ra **2**. Bằng 0 nghĩa là
   > file tải lên được nhưng **không đọc ra tiêu chí nào** — bước 14 sẽ không chấm.

---

#### A0b — Ngân hàng câu hỏi *(chứng minh truy hồi ảnh hưởng tới CÂU HỎI)*

1. Soạn một `.docx` hoặc `.pdf` chứa vài câu hỏi kỹ thuật, **cố ý đặt một câu rất riêng** để bước 12
   nhận ra được, ví dụ: *"Kể một lần bạn phải rollback migration trên production."*
2. Tải lên với **Phạm vi: `Theo tin`** → `[DEV] Kiosk Sandbox`, **Loại: `Ngân hàng câu hỏi`**.

---

#### A0c — Đáp án mong đợi *(chứng minh truy hồi ảnh hưởng tới ĐIỂM SỐ)*

1. Soạn một tài liệu chứa đáp án chuẩn cho đúng câu ở A0b, ví dụ:
   *"Đáp án đạt phải nêu được: khoá bảng khi migrate, nguy cơ mất dữ liệu, và cách kiểm thử trước khi
   chạy trên production."*
2. Tải lên với **Phạm vi: `Theo tin`**, **Loại: `Đáp án mong đợi`**.

> Tài liệu này **không bao giờ được đọc cho ứng viên nghe** — nó chỉ dùng lúc chấm (ADR-025). Trước
> ADR-062 nó được nạp, được truy hồi, được phân loại **rồi không dùng để chấm** — tức HR upload lên
> mà điểm số không hề đổi. Bước 14 sẽ kiểm điều này.

---

#### Kiểm cả ba tài liệu đã vào RAG

```bash
# Tầng 1 — RAG service có nhận việc không (phải thấy 3 dòng POST /ingest)
docker compose logs --tail=50 rag-service | grep -i ingest

# Tầng 2 — chunk đã vào pgvector chưa
docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
  "SELECT count(*) AS so_chunk, count(embedding) AS co_vector FROM document_chunks;"
```

> **✅ Kiểm A0** — `so_chunk > 0` **và** `co_vector = so_chunk`. Có chunk mà `embedding` rỗng nghĩa là
> việc cắt chạy được nhưng bước sinh vector hỏng (thường do `OPENAI_API_KEY` sai) — buổi phỏng vấn sẽ
> không truy hồi được gì.
>
> **Tầng 3 để dành tới bước 12 và 14:** câu hỏi phải bám A0b, còn điểm chấm phải bám A0a + A0c.

### Bước 1 — Gán Hiring Manager vào tin *(chỉ cần cho tin seed)*
**Đăng nhập:** `hr.dev@arisp.local`

> **Vì sao bước này vẫn còn sau ADR-063:** tin dựng từ phiếu (bước R8) đã **tự động** có HM. Tin
> `[DEV] Kiosk Sandbox` thì được seed ghi thẳng vào DB nên không có phiếu, không có HM — phải gán
> tay. Đây cũng là dịp kiểm chứng đường xử lý **dữ liệu có trước ADR-063** vẫn chạy.

1. Vào **Tin tuyển dụng** → mở tin `[DEV] Kiosk Sandbox`.
2. Cuộn xuống cột phải → thẻ **"Đội tuyển dụng"** → bấm **Gán Hiring Manager**.
3. Dropdown → chọn `Trần Hiring Manager — Kỹ thuật` → **Gán vào tin**.

> **✅ Kiểm 1** — Người vừa gán hiện trong danh sách kèm chip **"Người quyết định"** (nền xanh dương).
> Nếu tin có phòng ban trùng, người cùng phòng ban được xếp lên đầu dropdown kèm chữ *(cùng phòng ban)*.

### Bước 2 — Gửi tin đi duyệt
**Vẫn là:** `hr.dev@arisp.local` (hoặc chủ tin)

1. Trên trang chi tiết tin → đổi trạng thái thành **Chờ duyệt** (`draft → pending`).

> **✅ Kiểm 2** — Thẻ Đội tuyển dụng hiện banner vàng: *"Đang chờ Trần Hiring Manager ký duyệt mô tả
> công việc."*
> **✅ Kiểm 2b** — Đăng nhập HM → chuông có thông báo mới.

### Bước 3 — Thử duyệt đăng khi chưa có chữ ký → phải bị chặn
**Đăng nhập:** `hr.dev@arisp.local`

1. Vào **Duyệt tin** → chọn tin → bấm **Duyệt đăng** (`pending → active`).

> **✅ Kiểm 3** — **Bị từ chối**, kèm thông báo yêu cầu lý do vượt cổng. Tin **vẫn ở `pending`**.
> Đây là cổng ADR-061 — qua được là hỏng.
>
> **✅ Kiểm 3b** — Thử luôn bằng tài khoản **chủ tin** (Recruiter): cũng **không tự đăng được**. Nếu
> người viết JD tự bấm đăng được thì cổng ký duyệt tự bỏ qua được (ADR-063).

### Bước 4 — HM ký duyệt
**Đăng nhập:** `hm.dev@arisp.local`

1. Vào **Tin tuyển dụng của tôi** → mở tin.
2. **Trước khi ký:** cột phải có thẻ **"Bản mô tả công việc"** → bấm vào tên file → file JD mở ngay
   trong trình duyệt (PDF xem thẳng, DOCX cũng xem được nhờ `docx-preview`).
3. Thẻ Đội tuyển dụng → khối *"Mô tả công việc này đang chờ bạn ký duyệt"* → bấm **Ký duyệt**.

> **✅ Kiểm 4a — ADR-064, mắt xích quan trọng nhất** — HM phải **xem được chính file JD** ngay trên màn
> ký duyệt, và đó phải là **đúng file đã xuất ở bước J4** (có logo, đúng màu, đúng thứ tự mục).
>
> Trước ADR-064 màn này không có chỗ nào mở file — HM ký duyệt mà không nhìn thấy thứ mình duyệt,
> tức là cổng tồn tại về mặt kỹ thuật nhưng rỗng về mặt nghiệp vụ. Không thấy thẻ này = bản vá đã bị revert.

> **✅ Kiểm 4** — Banner đổi sang xanh lá: *"Trần Hiring Manager đã ký duyệt mô tả công việc."*
>
> **Thử luôn nhánh ngược** (không bắt buộc): bấm **Yêu cầu sửa**, nhập lý do → banner đỏ, chủ tin
> đọc được góp ý. Muốn đi tiếp thì chủ tin gửi duyệt lại (tự đặt lại về chờ ký).

### Bước 5 — Kiểm chứng tin **tự lên `active`** *(ADR-063 — đổi hẳn so với bản trước)*

> Bản trước bước này là *"HR Leader bấm Duyệt đăng lần hai"*. ADR-063 bỏ hẳn bước đó: **chữ ký của
> HM chính là cổng đăng tin**. Không bấm gì thêm cả.

1. Ngay sau bước 4, **không rời trang** — tải lại trang chi tiết tin.

> **✅ Kiểm 5 — QUAN TRỌNG** — Trạng thái tin **đã là `active`**, không ai bấm "Duyệt đăng". Tin cũng
> đã hiện trên job board công khai (`http://localhost:3000`).
>
> **✅ Kiểm 5b** — Vẫn còn ghi nhận **người duyệt** và **thời điểm duyệt** trên tin, và nếu JD là
> PDF/DOCX thì file **đã được đóng dấu duyệt**. Đây là điểm dễ hỏng nhất của thay đổi này: việc đăng
> tin gọi lại chính `UpdateJobStatusCommand` để không đánh rơi bốn việc kèm theo (ghi người duyệt,
> đóng dấu JD, đặt `PublishedAt`, báo cho người tạo tin). Thiếu dấu duyệt = đã có ai đó chép tay
> logic đăng tin sang chỗ khác.
>
> **✅ Kiểm 5c — nhánh hỏng có kiểm soát** — Đặt **hạn nộp hồ sơ về quá khứ** rồi cho HM ký duyệt một
> tin khác: phải báo *"Đã ghi nhận chữ ký duyệt nhưng chưa đăng được tin: …"*. **Chữ ký vẫn còn**
> (không mất cả hai), sửa hạn rồi đăng lại được.

---

## B · Cổng duyệt shortlist

### Bước 6 — Ứng viên nộp hồ sơ
**Đăng nhập:** `practice.dev@arisp.local` tại `http://localhost:3000/jobs/login`

Seed đã tạo sẵn hồ sơ — bỏ qua bước này nếu `applicationId` đã có. Muốn tạo hồ sơ mới thì vào Job
Board → mở tin → **Ứng tuyển** → upload CV → gửi.

### Bước 7 — Gửi hồ sơ cho HM duyệt
**Đăng nhập:** `hr.dev@arisp.local`

1. Vào **Ứng viên** → mở hồ sơ của `practice.dev@arisp.local`.
2. Cột phải → thẻ **"Cổng duyệt Hiring Manager"** → bấm **Gửi Hiring Manager duyệt**.

> **✅ Kiểm 7** — Chip trạng thái hồ sơ đổi thành **"Chờ Hiring Manager duyệt"** (nền xanh da trời),
> kèm nhãn **"Chờ duyệt"** màu vàng trong thẻ cổng.
>
> ⚠️ Nút này **chỉ hiện khi hồ sơ ở `cv_submitted`**. Hồ sơ mới chỉ ở trạng thái "Đã mời" thì không
> có nút — đúng như server yêu cầu.

### Bước 8 — Kiểm cổng có thật sự chặn không
**Vẫn ở màn hồ sơ đó.**

1. Thử xếp lịch qua thẻ **Xếp lịch phỏng vấn** bên dưới.

> **✅ Kiểm 8 — QUAN TRỌNG** — Phải **bị từ chối**: *"Chỉ có thể xếp lịch khi ứng viên đã qua vòng
> duyệt CV và hồ sơ chưa đóng."*
> Xếp được lịch ở đây nghĩa là cổng HM bị vượt mặt — hỏng nặng.

### Bước 9 — HM duyệt hồ sơ
**Đăng nhập:** `hm.dev@arisp.local`

1. Vào **Hồ sơ chờ duyệt** → thấy ứng viên trong danh sách.
2. Bấm **Duyệt hồ sơ**.

> **✅ Kiểm 9** — Hồ sơ biến khỏi danh sách chờ. Về màn HR, thẻ cổng hiện nhãn xanh lá
> **"Hiring Manager đã duyệt"**.
>
> **Thử nhánh vượt cổng** (không bắt buộc, cần hồ sơ thứ hai): đăng nhập HR Admin, ở thẻ cổng bấm
> **Vượt cổng duyệt** → nhập lý do **≥ 10 ký tự** → nhãn hiện **"Đã vượt cổng duyệt"** màu tím
> (nhãn RIÊNG, không gộp vào "đã duyệt"), và **HM nhận được thông báo mình bị vượt**.

### Bước 10 — Duyệt CV + xếp lịch trong một thao tác, qua trình soạn thư
**Đăng nhập:** `hr.dev@arisp.local`

1. Mở lại hồ sơ → bấm **Duyệt** (nút xanh lá).
2. Modal **Xếp lịch** mở ra → chọn một khung giờ vòng 1 → tiếp tục.
3. **Trình soạn thư mở ra.**

> **✅ Kiểm 10 — QUAN TRỌNG NHẤT** — Thân thư **phải hiện sẵn đầy đủ**: lời chào có tên ứng viên,
> **giờ hẹn cụ thể**, địa điểm (nếu Super Admin đã cấu hình), và hai nút *Xác nhận* / *Đổi lịch*.
> **Thân thư rỗng là lỗi** — đó chính là lỗi vừa được vá ở lượt này.
>
> 4. Sửa thử: thêm một câu vào cuối thân thư, ví dụ *"Vui lòng mang theo CMND/CCCD."*
> 5. Bấm **Gửi**.

> **✅ Kiểm 10b** — Hồ sơ sang **"Vòng sơ loại"**, ca đã bị chiếm chỗ.
> **✅ Kiểm 10c** — Mở tab **"Lịch sử email"** ở cuối cột trái → có một dòng mới, chip tím **"Đã sửa"**,
> bung ra thấy **đúng câu bạn vừa thêm**.
>
> > Không cấu hình SMTP cũng test được: thư sẽ ghi `status=failed` nhưng **nội dung vẫn lưu nguyên**
> > trong `email_logs`, và tab này hiện đúng bản đã dựng.

### Bước 11 — Kiểm tính nguyên tử (bấm Huỷ)
Làm với **một hồ sơ khác** đã qua bước 9.

1. Bấm **Duyệt** → chọn ca → trình soạn thư mở ra → bấm **Huỷ**.

> **✅ Kiểm 11** — **Không có gì xảy ra cả**: hồ sơ giữ nguyên trạng thái, ca **không** bị chiếm chỗ,
> `email_logs` **không** có dòng mới. Đây là điều mà ADR-059 sinh ra để bảo đảm.

---

## C · Phỏng vấn thật và chốt kết quả

### Bước 12 — Cấp mã và phỏng vấn tại máy trạm
**Đăng nhập:** `hr.dev@arisp.local`

1. Màn hồ sơ → **Cấp mã phỏng vấn** → ghi lại mã 6 ký tự.
2. Mở `http://localhost:3000/kiosk` → nhập mã → **Bắt đầu phỏng vấn**.
3. Qua bước kiểm tra thiết bị → vào phòng → trả lời vài câu → **Kết thúc phỏng vấn**.

> **✅ Kiểm 12 — RAG trong luồng** — Trong lúc phỏng vấn, terminal `docker compose logs -f rag-service`
> phải nhảy `POST /next-question` cho mỗi câu hỏi và `POST /analyze-answer` cho mỗi câu trả lời.
> **Không có dòng nào = RAG không được gọi**, dù giao diện vẫn chạy bình thường.
>
> **✅ Kiểm 12b — truy hồi có tác dụng** — Ít nhất một câu hỏi phải **bám nội dung playbook nạp ở bước
> A0** (câu "rollback migration" hoặc chủ đề tương tự). Đây là bằng chứng end-to-end mạnh nhất cho
> phần RAG khi bảo vệ.
>
> **✅ Kiểm 12c** — Vào phòng là trình duyệt bật **toàn màn hình**. Bấm `Esc` để thoát → **lớp phủ chặn
> cả phòng**, phải bấm *Quay lại toàn màn hình* mới tiếp tục được, và số lần rời được đếm.
>
> ⚠️ `Alt+Tab` và phím Windows **vẫn thoát được** — đó là giới hạn đã biết của nền web, ghi rõ ở
> ADR-062. Muốn khoá cứng thì làm theo `docs/kiosk-workstation-setup.md`.

### Bước 13 — HR Admin mở màn chốt kết quả → phải thấy cổng
**Đăng nhập:** `hr.dev@arisp.local`

1. Vào **Đánh giá** → mở báo cáo của ứng viên vừa phỏng vấn.

> **✅ Kiểm 13** — Khối *"Quyết định của nhân sự"* hiện **banner vàng**: *"Người chốt kết quả của tin
> này là Trần Hiring Manager. Bạn đang chốt thay…"*, kèm ô **Lý do chốt thay bắt buộc**.
> Bấm chốt khi lý do **dưới 10 ký tự** → **phải bị chặn**.

### Bước 14 — HM chốt kết quả + ghi đề xuất lương
**Đăng nhập:** `hm.dev@arisp.local`

1. Vào **Kết quả phỏng vấn** → mở báo cáo.
2. Bấm **+ Thêm đề xuất cấp bậc & mức lương** → điền:
   - Cấp bậc đề xuất: `Middle`
   - Lương từ: `20000000` · Lương đến: `25000000` · Tiền tệ: `VND`
   - Điểm mạnh / Điểm cần lưu ý: điền tự do
3. Bấm **Xác nhận kết quả AI** (hoặc **Ghi đè** nếu muốn thử — bắt buộc nhập lý do).

> **✅ Kiểm 14 — RAG chấm điểm** — Lúc báo cáo được sinh, log `rag-service` phải có `POST /evaluate`
> và `POST /assess-language`.
>
> **✅ Kiểm 14a — điểm bám ĐÚNG bộ tiêu chí đã khai ở A0a** — Bảng điểm phải hiện đúng hai tiêu chí
> **"Chuyên môn"** và **"Giao tiếp"** với trọng số **60/40** (nhãn tiếng Việt do bạn đặt, không phải
> tên tiếng Anh model tự nghĩ). Điểm tổng phải **đúng bằng trung bình có trọng số** của hai số đó —
> tự nhân tay mà đối chiếu: `technical×0.6 + communication×0.4`.
>
> **✅ Kiểm 14b — đáp án mong đợi ở A0c có ảnh hưởng** — Phần nhận xét cho câu "rollback migration"
> phải đối chiếu với đáp án chuẩn bạn soạn (nhắc tới khoá bảng / mất dữ liệu / kiểm thử trước), chứ
> không phải nhận xét chung chung theo cảm tính của model.
>
> **✅ Kiểm 14c** — Bản đánh giá có **nhận xét ngôn ngữ kèm bậc CEFR**, không phải một điểm tổng trống trơn.
>
> **✅ Kiểm 14b** — **Không có banner vàng và không đòi lý do** (vì đây đúng là người quyết định).
> **✅ Kiểm 14b** — Sau khi chốt, khối kết quả hiện dòng **"Người chốt: Hiring Manager"** và
> **"Đề xuất: Middle · 20.000.000 – 25.000.000 VND"**.
> **✅ Kiểm 14c** — Đây là **vòng cuối**, nên hồ sơ chuyển sang **"Đạt"** (ADR-053). Nếu còn vòng thì
> phải là "Đang phỏng vấn".

---

## D · Thư mời nhận việc

### Bước 15 — Soạn thư mời
**Đăng nhập:** `hr.dev@arisp.local`

1. Mở hồ sơ → cột phải xuất hiện thẻ **"Thư mời nhận việc"** → bấm **Soạn thư mời**.

> **✅ Kiểm 15 — QUAN TRỌNG** — Ô **Mức lương phải điền sẵn `20000000`** và **Cấp bậc `Middle`** —
> lấy từ đề xuất HM ghi ở bước 14. Đây là cả điểm của việc ghi đề xuất lúc chốt.

2. Điền nốt: **Ngày bắt đầu** và **Hạn trả lời** (bắt buộc), *Nơi làm việc*, *Phúc lợi*.
3. Vào ô **Ghi chú nội bộ** gõ: `Còn dư ngân sách, nâng thêm 2 triệu được nếu ứng viên mặc cả.`
4. **Lưu nháp** → rồi **Gửi duyệt**.

> **✅ Kiểm 15b** — Chưa điền đủ lương/ngày bắt đầu/hạn trả lời thì nút **Gửi duyệt bị tắt**.
> **✅ Kiểm 15c** — Hồ sơ **vẫn ở "Đạt"**, chưa sang "Đã gửi thư mời" — bản nháp không phải lời hứa.
>
> **✅ Kiểm 15d (ADR-063)** — HM **soạn và gửi duyệt được** thư mời (đó là "đề xuất mức lương cụ
> thể"), chỉ **không chốt được**. Đăng nhập HM và thử soạn một thư mời: phải làm được tới bước **Gửi
> duyệt**, rồi dừng ở đó.

### Bước 16a — HM thử tự chốt thư mời → phải bị chặn *(ADR-063 — đảo ngược ADR-061)*
**Đăng nhập:** `hm.dev@arisp.local`

1. Vào **Thư mời nhận việc** → mở thư vừa gửi duyệt.

> **✅ Kiểm 16a — QUAN TRỌNG** — **Không có nút "Duyệt thư"** cho HM nữa. Bản trước (ADR-061) HM
> chính là người duyệt; ADR-063 tách vai: **HM đề xuất mức lương, HR Leader chốt**, vì người có nhu
> cầu tuyển không phải người kiểm soát ngân sách lương.
>
> Kiểm tận tầng nghiệp vụ (không chỉ ẩn nút) bằng token của HM:
>
> ```bash
> curl -i -X POST "http://localhost:5000/api/offers/<OFFER_ID>/decide" \
>   -H "Authorization: Bearer <TOKEN_CUA_HM>" \
>   -H "Content-Type: application/json" \
>   -d '{"decision":"approved"}'
> ```
>
> Phải trả **403** kèm câu: *"Hiring Manager đề xuất mức lương nhưng không tự chốt được thư mời — HR
> Leader là người quyết định cuối cùng."* Thư **vẫn ở "Chờ duyệt"**.
>
> Điều kiện này được viết **trong handler**, không chỉ ở policy: nếu ai đó nới policy một dòng thì
> quyền cũ sẽ lặng lẽ sống lại mà không có gì báo.

### Bước 16b — HR Leader chốt thư mời
**Đăng nhập:** `hr.dev@arisp.local`

1. Vào **Thư mời nhận việc** → mở thư → bấm **Duyệt thư**.

> **✅ Kiểm 16b** — Trạng thái sang **"Đã duyệt"**, người duyệt ghi nhận là **HR Leader**.
>
> **Thử nhánh trả về** (không bắt buộc): bấm **Trả về bản nháp** + góp ý → người soạn mở lại thấy
> banner vàng chứa góp ý, sửa rồi gửi duyệt lại được **(không lỗi 500)**.

### Bước 17 — Gửi thư cho ứng viên
**Đăng nhập:** `hr.dev@arisp.local`

1. Mở thư đã duyệt → bấm **Gửi ứng viên** → **trình soạn thư mở ra**.

> **✅ Kiểm 17** — Thân thư hiện sẵn đầy đủ: chức danh, mức lương, ngày bắt đầu, hạn trả lời, nút
> mở Portal. **Tuyệt đối không được thấy câu ghi chú nội bộ ở bước 15.3.**

2. Bấm **Gửi**.

> **✅ Kiểm 17b** — Hồ sơ chuyển sang **"Đã gửi thư mời"** — *đúng lúc GỬI*, không phải lúc tạo nháp.

### Bước 18 — Ứng viên nhận việc
**Đăng nhập:** `practice.dev@arisp.local` tại `http://localhost:3000`

1. Vào **Hồ sơ của tôi** → mở hồ sơ → vào trang **Thư mời nhận việc**.

> **✅ Kiểm 18 — KIỂM RÒ RỈ** — Trang hiện lương, ngày bắt đầu, đếm ngược hạn trả lời.
> **Không được có bất kỳ dấu vết nào của "Ghi chú nội bộ"** ở bước 15.3 — kiểm cả bằng
> `Ctrl+U` (xem mã nguồn trang) và tab Network, không chỉ nhìn bằng mắt.

2. Bấm **Nhận việc**.

> **✅ Kiểm 18b** — Hồ sơ sang **"Đã nhận việc"** (`hired`). Phía nhân sự chip đổi màu xanh ngọc.

---

## E · Kiểm bảo mật (quan trọng nhất khi bảo vệ)

### Bước 19 — Phạm vi dữ liệu do server quyết định
Cần **tài khoản Recruiter thứ hai** (`recruiter.b@arisp.local`) **không** được gán vào tin nào.

Đăng nhập Recruiter B, lấy token từ DevTools → Application → Local Storage, rồi:

```bash
TOKEN="<dán access token của Recruiter B>"

# Không kèm ?mine — trước ADR-061 sẽ thấy dữ liệu cả công ty
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/evaluations
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/applications
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/jobs/admin
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/dashboard/hr

# ADR-063 — bảng mới cũng phải chịu đúng luật đó
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/recruitment-requests
```

> **✅ Kiểm 19** — Cả năm phải trả **danh sách rỗng** (hoặc chỉ dữ liệu tin của chính B).
>
> **✅ Kiểm 19b (ADR-063)** — `/api/recruitment-requests` của Recruiter B **không được** chứa phiếu ở
> Phần R (phiếu đó phân công cho Recruiter khác). Phạm vi do server tính, không có tham số `?mine`
> nào để client tự khai.
>
> **✅ Kiểm 19c** — Đăng nhập **HM** rồi gọi cùng endpoint: chỉ thấy **phiếu do chính HM lập**, không
> thấy phiếu của HM khác.
> Thấy ứng viên hay báo cáo của tin `[DEV] Kiosk Sandbox` là **rò rỉ**.

### Bước 20 — HM chưa được gán tin thì không thấy gì
Tạo HM thứ hai, **không** gán vào tin nào, đăng nhập.

> **✅ Kiểm 20** — Cả 4 màn (Tổng quan · Tin của tôi · Hồ sơ chờ duyệt · Thư mời) đều **rỗng**.
> Đó là hành vi đúng, không phải lỗi.

### Bước 21 — Lọc HTML trong thư
Ở bất kỳ trình soạn thư nào, dán vào thân thư:

```html
<script>alert(1)</script><a href="javascript:alert(2)">bấm thử</a>
```

> **✅ Kiểm 21** — Thư trong tab **Lịch sử email** phải **sạch**: không còn thẻ `<script>`, thuộc tính
> `href="javascript:"` bị gỡ. Lọc chạy **ở server**, nên gọi thẳng API cũng không lách được.

---

## Phụ lục — bảng cổng của ADR-063 (kiểm nhanh khi nghi ngờ bị revert)

| Cổng | Bước | Qua được nghĩa là hỏng ở |
|---|---|---|
| HM không tự duyệt phiếu của mình | R2 | `ApproveRecruitmentRequestCommandHandler` — chặn theo NGƯỜI (`RequestedByUserId == ActorId`), không theo vai trò |
| Trả lại phiếu phải có lý do ≥10 ký tự | R3 | `RejectRecruitmentRequestCommandHandler` |
| Duyệt phiếu luôn kèm phân công Recruiter | R6c | Handler đòi `AssignedRecruiterId` hợp lệ + đúng vai trò `recruiter` |
| Không tạo tin nếu không có phiếu đã duyệt | R7 | `CreateJobCommand` — `RecruitmentRequestId` bắt buộc với tin mới |
| Người lập phiếu tự động thành HM của tin | R8b | `CreateJobCommand` thêm `JobHiringTeamMember` — thiếu là **cổng ký duyệt JD không tồn tại** |
| Một phiếu chỉ sinh một tin | R9 | Unique index có filter `ux_job_postings_recruitment_request_id` |
| Chỉ Recruiter được phân công mới dựng tin | R10 | `CreateJobCommand` — so `AssignedRecruiterId` |
| HM ký duyệt JD là tin lên `active` | 5 | `JobSignOffFeature` gọi lại `UpdateJobStatusCommand` |
| Chủ tin (Recruiter) không tự đăng được | 3b | `UpdateJobStatusCommand` — chỉ admin hoặc HM phụ trách |
| **HM không tự chốt thư mời** | 16a | Policy `OfferApproval` + điều kiện tường minh trong `DecideOfferCommandHandler` |

## Phụ lục — 6 lỗi đã vá ở lượt ADR-061

Nếu gặp lại đúng triệu chứng dưới đây thì bản vá đã bị revert:

| Triệu chứng | Bước | Đã vá ở |
|---|---|---|
| Trình soạn thư mở ra **thân rỗng** | 10, 17 | `EmailComposerModal.tsx` — ghi nội dung mẫu sau khi vùng soạn đã mount |
| HM duyệt xong nhưng **nút Duyệt tắt**, hồ sơ kẹt | 10 | `JobDetailPage.tsx` + `JobPostingDetailPage.tsx` — `CAN_DECIDE_CV` thêm `hm_review` |
| `PATCH /status` báo *"Transition mapping … is not configured"* | — | `ApplicationService.cs` — thêm khoá `hm_review` |
| HM mở tin nhưng **không có nút ký duyệt** | 4 | `JobsController.cs` + `GetJobByIdQuery.cs` — HM là staff, phạm vi theo `JobAccess` |
| **Xếp được lịch** khi đang chờ HM duyệt | 8 | `StaffScheduling.cs` — dùng `IsCvPhase`/`IsTerminal` thay danh sách chuỗi tay |
| Nút "Gửi HM duyệt" hiện ở hồ sơ *Đã mời* rồi báo lỗi | 7 | `ShortlistGatePanel.tsx` — chỉ hiện ở `cv_submitted` |

## Phụ lục — xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| File JD xuất ra **không có logo** | Chưa tải logo ở màn **Mẫu JD**, hoặc file logo không đọc được. Logo là phần DUY NHẤT được phép hỏng lặng lẽ — hệ thống vẫn xuất file, chỉ khuyết ảnh (xem log cảnh báo của API) |
| File JD **thiếu một mục vừa gõ** | Mục đó đang **tắt** trong Mẫu JD, hoặc nội dung để trống. Mục bật mà chưa viết gì thì cố ý không in tiêu đề trống |
| Lương in ra `20,000,000` (dấu phẩy) | Định dạng số đang phụ thuộc culture của máy chủ. `JdLayout` phải khai `NumberFormatInfo` tường minh — container Linux có thể chạy globalization-invariant |
| Bấm **PDF/DOCX** báo *"Chưa có nội dung JD"* | Chưa lưu lần nào. Trình soạn tự lưu trước khi xuất, nên lỗi này nghĩa là bước lưu vừa hỏng — xem thông báo phía trên |
| Nút **Lập phiếu** không hiện | Đang đăng nhập bằng Recruiter — họ thực thi phiếu chứ không phát sinh nhu cầu tuyển (ADR-063) |
| Bấm **Tạo tin** báo *"phải được dựng từ một phiếu…"* | Chưa chọn phiếu ở khối trên cùng của biểu mẫu. Chọn một phiếu, hoặc đi từ màn Phiếu yêu cầu tuyển dụng → chọn phiếu → **Dựng tin** |
| Ô chọn phiếu **rỗng / không có phiếu nào** | Phiếu phải **đã duyệt**, **giao cho chính bạn** (Recruiter), và **chưa dựng tin nào**. Phiếu đã có tin bị loại khỏi danh sách |
| Duyệt phiếu xong nhưng Recruiter **không thấy việc mới** | Payload realtime thiếu `assigned_recruiter_id` — migration `AddRecruitmentRequests` phải `CREATE OR REPLACE` lại `arisp_notify_change()` **và** gọi `SELECT arisp_attach_change_triggers();` |
| HM ký duyệt JD xong tin **vẫn ở `pending`** | `JobSignOffFeature` không gọi được `UpdateJobStatusCommand` — xem thông báo trả về, thường là hạn nộp hồ sơ đã qua hoặc ngân hàng đề trắc nghiệm thiếu câu (ADR-059) |
| Đăng nhập HM xong bị đá về `/admin/login` | Vai trò trong DB không phải đúng `hiring_manager` (migration `NormalizeUserRolesAndAddCheck` ép chữ thường) |
| Thẻ "Đội tuyển dụng" không hiện | Đang xem bằng tài khoản không phải chủ tin / không phải admin |
| Thư báo `status=failed` trong Lịch sử email | Chưa cấu hình `EmailSettings:AppPassword` — **không sao**, nội dung vẫn lưu đủ để kiểm |
| Không thấy thông báo realtime | Bảng mới chưa gắn trigger — chạy `SELECT arisp_attach_change_triggers();` (ADR-057) |
| Mã Kiosk báo hết hạn | TTL 2 giờ, dùng một lần — cấp lại mã mới |
| Bước 12 vào phòng nhưng **AI không hỏi gì**, hoặc bước 14 không sinh được báo cáo | Backend không gọi được `rag-service`. Chạy trên máy mà quên `RAG_SERVICE_URL=http://localhost:8000` thì nó dùng mặc định `http://rag-service:8000` — tên nội bộ mạng Docker, **không phân giải được** |
| **Câu hỏi chẳng liên quan gì tới Playbook, `document_chunks` rỗng** | rag-service đang nối vào **database khác** với API. Máy này dùng Supabase, còn `docker/.env` trỏ container `postgres` — sửa `rag-service/.env`, xem [rag-service-local-setup.md](rag-service-local-setup.md) mục 1 |
| rag-service báo lỗi kết nối lúc khởi động | Thiếu `DATABASE_SSLMODE=require` — Supabase từ chối kết nối không SSL |
| Chạy từ Visual Studio mà thao tác AI báo lỗi kết nối | Đang chọn profile **IIS Express** (không khai `RAG_SERVICE_URL`). Đổi sang `http`/`https` ở dropdown cạnh nút ▶ |
| **Bước 14 không sinh báo cáo, không lỗi trên giao diện** | Tin **chưa khai bộ tiêu chí** (bỏ qua A0a) — ADR-062 không cho chấm bằng điểm model tự nghĩ. Xem log API: *"CHƯA khai bộ tiêu chí chấm phỏng vấn"*. Làm A0a rồi chấm lại bằng `POST /api/dev/regrade-session/{id}` |
| Báo cáo có tiêu chí tên tiếng Anh lạ (`technical_skills`…) | Model tự đặt khoá ngoài rubric — các khoá lạ bị loại khỏi cả tử lẫn mẫu (ADR-060). Kiểm lại mã tiêu chí trong file `.xlsx` |
| Bước 12/14 hỏng khi chạy bằng `docker compose` | Kiểm `docker compose ps rag-service` và `docker compose logs rag-service`; backend khai `depends_on` nhưng chỉ `service_started`, không chờ service sẵn sàng |
