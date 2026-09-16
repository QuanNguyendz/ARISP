# Kịch bản test tay một lượt — toàn bộ luồng tuyển dụng trên localhost

> Đi đúng thứ tự sơ đồ **"ARISP — Use-case overview (as implemented in source code · 2026-09-15 · incl. ADR-068)"**:
> từ lúc Hiring Manager lập phiếu tới lúc ứng viên nhận việc, qua đủ 5 làn (HM · HR Leader · Recruiter · System · Candidate).
>
> - Mỗi bước ghi: **ai đăng nhập · bấm gì · phải thấy gì**. Tick `[x]` khi xong.
> - Dòng **✅** là điểm kiểm. Sai ở đó thì **dừng lại**, vì các bước sau dựa vào nó.
> - Tin dùng để test có **3 vòng**: 1 · Trắc nghiệm → 2 · Sơ loại (làm từ nhà) → 3 · Chuyên môn (tại văn phòng).
>   Đây cũng là bộ vòng mặc định của phiếu, nên bạn không phải sửa gì ở phần này.
> - Thời gian: khoảng **1,5–2 giờ** (hai buổi phỏng vấn AI + một bài thi phải chờ tới giờ mở).
> - Các nhánh phụ (từ chối, vượt cổng, quá hạn…) gom ở **Phần H**, chạy sau khi xong luồng chính.
>
> Tài liệu cũ `e2e-test-hiring-manager.md` viết trước ADR-067/068, nên một số bước ở đó không còn đúng.
> Khi hai file khác nhau, làm theo file này.

---

## Phần 0 · Chạy project trên localhost

### 0.1 Chuẩn bị (chỉ làm lần đầu)

| Cần có | Kiểm bằng |
|---|---|
| .NET SDK 8 trở lên | `dotnet --version` |
| Node.js ≥ 20 | `node --version` |
| Python venv của rag-service đã cài sẵn | có file `rag-service\.venv\Scripts\python.exe` |
| Thư viện frontend | chạy `npm install` một lần trong `ari-web` |

**Cấu hình**. Không commit các file dưới đây lên git.

| File / nơi cấu hình | Khoá cần có | Dùng cho |
|---|---|---|
| `ari-service/src/ARI.API/appsettings.Development.json` hoặc `dotnet user-secrets` | `ConnectionStrings:DefaultConnection`, `JWT:Secret` | API (bắt buộc) |
| như trên | `GEMINI_API_KEY` | Chấm độ phù hợp CV–JD, gợi ý kỹ năng khi dựng tin |
| như trên | `Media:Deepgram:ApiKey`, `Media:ElevenLabs:ApiKey` | Nhận giọng nói / đọc câu hỏi. Thiếu khoá thì vẫn **gõ tay** câu trả lời được |
| như trên | `EmailSettings:SenderEmail`, `EmailSettings:AppPassword` | Gửi thư thật. Thiếu thì thư ghi `failed` nhưng **nội dung vẫn lưu** ở tab *Lịch sử email* |
| `rag-service/.env` | `DATABASE_*` (**cùng database với API**), `DATABASE_SSLMODE=require`, `OPENAI_API_KEY` | Sinh câu hỏi, chấm điểm, nạp playbook |

> ⚠️ **API và rag-service phải dùng CHUNG một database.** Nếu lệch nhau thì RAG vẫn chạy nhưng truy
> hồi rỗng, và **không báo lỗi gì**. Cách cấu hình đúng xem [rag-service-local-setup.md](rag-service-local-setup.md), mục 1–2.

### 0.2 Khởi động — mở 4 cửa sổ PowerShell tại thư mục gốc `ARISP`

```powershell
# Cửa sổ 1 — RAG service (cổng 8000). Để cửa sổ này hiện suốt buổi test để theo dõi log.
cd rag-service
.\.venv\Scripts\python.exe -m uvicorn app.main:app --port 8000 --reload
```

```powershell
# Cửa sổ 2 — API (cổng 5000)
$env:RAG_SERVICE_URL = "http://localhost:8000"
dotnet run --project ari-service/src/ARI.API --launch-profile http
```

> Chạy API bằng **Visual Studio** cũng được: chọn profile **`http`** hoặc **`https`** rồi bấm ▶.
> **Không chọn `IIS Express`**: profile đó không khai `RAG_SERVICE_URL`, nên mọi thao tác AI sẽ lỗi kết nối.

```powershell
# Cửa sổ 3 — Site nhân sự (cổng 3001)
cd ari-web
npm run dev:staff
```

```powershell
# Cửa sổ 4 — Site ứng viên + Kiosk (cổng 3000)
cd ari-web
npm run dev:candidate
```

| Dịch vụ | Địa chỉ |
|---|---|
| Site nhân sự (HR Leader · Recruiter · HM · Super Admin) | http://localhost:3001/auth/login |
| Site ứng viên | http://localhost:3000 — đăng nhập tại `/auth/candidate-login` |
| Kiosk phỏng vấn thật | http://localhost:3000/kiosk |
| API (Swagger) | http://localhost:5000/swagger |
| RAG service (Swagger) | http://localhost:8000/docs |

### 0.3 Kiểm hệ thống đã sống

> ⚠️ Trong Windows PowerShell 5.1, `curl` là tên gọi tắt của `Invoke-WebRequest`, không phải curl thật.
> Hãy gõ **`curl.exe`**.

```powershell
curl.exe http://localhost:8000/health          # phải trả 200
curl.exe -I http://localhost:5000/swagger/index.html
```

- [ ] ✅ **0.3** — Cả hai lệnh trả 200, và hai site mở được trên trình duyệt.

---

## Phần 1 · Dữ liệu và tài khoản test

### 1.1 Tạo tài khoản dev có sẵn mật khẩu

```powershell
curl.exe -X POST "http://localhost:5000/api/dev/seed-interview-job"
```

Lệnh này tạo `hr.dev`, `hm.dev` và `practice.dev` (bảng bên dưới). Nó cũng tạo tin
`[DEV] Kiosk Sandbox`; kịch bản này **không dùng** tin đó, bạn cứ để yên.

### 1.2 Tạo Super Admin và Recruiter

Không có SMTP thì mật khẩu tạm của tài khoản tạo từ giao diện sẽ không tới tay bạn. Cách nhanh nhất là
chạy SQL dưới đây trên **database local/test** (ví dụ Supabase SQL Editor). Câu lệnh chép mã băm mật
khẩu của `hr.dev`, nên hai tài khoản mới dùng cùng mật khẩu `Hr123456!`.

```sql
-- CHỈ chạy trên database dev/test, không bao giờ chạy trên production.
INSERT INTO users (id, email, password_hash, role, full_name, is_active, created_at, updated_at)
SELECT gen_random_uuid(), v.email, u.password_hash, v.role, v.full_name, true, now(), now()
FROM users u
CROSS JOIN (VALUES
  ('sa.dev@arisp.local',        'super_admin', 'SA Dev'),
  ('recruiter.dev@arisp.local', 'recruiter',   'Recruiter Dev')
) AS v(email, role, full_name)
WHERE u.email = 'hr.dev@arisp.local'
ON CONFLICT (email) DO NOTHING;
```

> Database đã có sẵn Super Admin thì dùng tài khoản đó. Nếu có SMTP, bạn có thể tạo Recruiter bằng
> giao diện (Super Admin → **Tất cả người dùng** → **Thêm staff**) để kiểm luôn luồng tạo tài khoản.

### 1.3 Gán đội cho Hiring Manager (bắt buộc — HM chưa có đội thì không lập được phiếu)

**Đăng nhập:** `sa.dev@arisp.local`

1. Thanh bên → **Đội / Bộ phận** → **Thêm đội**: tên `Backend Team`, mã `BE`.
2. Thanh bên → **Quản lý Users → Tất cả người dùng** → dòng `hm.dev@arisp.local` → cột **Đội / Bộ phận** → chọn `Backend Team`.

- [ ] ✅ **1.3** — Ô đội là **danh sách chọn**, không phải ô gõ chữ.
- [ ] ✅ **1.3b** — Thêm đội tên `backend team` (chữ thường) → **bị chặn vì trùng tên**.

### Bảng tài khoản

| Vai trò | Email | Mật khẩu | Đăng nhập tại |
|---|---|---|---|
| Super Admin | `sa.dev@arisp.local` | `Hr123456!` | :3001 |
| HR Leader | `hr.dev@arisp.local` | `Hr123456!` | :3001 |
| Recruiter | `recruiter.dev@arisp.local` | `Hr123456!` | :3001 |
| Hiring Manager | `hm.dev@arisp.local` | `Hm123456!` | :3001 |
| Ứng viên | `practice.dev@arisp.local` | `Practice123!` | :3000 |

> **Mẹo:** mở **mỗi vai một profile trình duyệt** (Chrome profile khác nhau, hoặc Chrome + Edge + cửa sổ
> ẩn danh). Như vậy bạn không phải đăng xuất/đăng nhập liên tục, và thấy chuông thông báo nhảy theo
> thời gian thực.
>
> Chuẩn bị thêm **một file CV** (PDF hoặc DOCX) cho bước C1.

---

## Phần A · Phiếu yêu cầu tuyển dụng — làn HM ↔ HR Leader

### A1 · HM lập phiếu — `CreateRecruitmentRequestCommand`
**Đăng nhập:** `hm.dev` → **Phiếu yêu cầu tuyển dụng** → **Lập phiếu**

Điền:
- Vị trí `Senior Backend Engineer` · Số lượng `2` · Mức độ ưu tiên `Ưu tiên cao`.
- Ngày dự kiến bắt đầu: một ngày trong tương lai.
- Thời gian `Toàn thời gian` · Cấp bậc `Senior` · Hình thức `On-site` · Nơi làm việc `Hà Nội`.
- Lương tối thiểu `60000000`, tối đa `90000000`. Mức này cố ý cao để HR Leader trả phiếu ở A2.
- Đơn vị `VND`.
- Lý do và Mô tả sơ bộ: vài dòng.
- Yêu cầu ứng viên: **mỗi dòng một ý**, gõ 3 dòng.
- Vòng phỏng vấn: **giữ nguyên** `Trắc nghiệm → Sơ loại → Chuyên môn`.

Bấm **Gửi phiếu**.

- [ ] ✅ **A1** — Phiếu hiện chip **Chờ duyệt**. Ô *Hiring Manager* và *Đội / Bộ phận* điền sẵn (`Backend Team`) và **không sửa được**.
- [ ] ✅ **A1b** — Ô **Đơn vị** là danh sách chọn, chỉ có **VND** và **USD**.
- [ ] ✅ **A1c** — Bỏ tích *Thoả thuận* và để trống cả hai ô lương → **bị chặn**.
- [ ] ✅ **A1d** — Đăng nhập `hr.dev`: **chuông có thông báo** phiếu mới.
- [ ] ✅ **A1e** — Mở lại phiếu bằng `hm.dev`: **không có** nút *Duyệt & phân công* hay *Trả lại*, vì không ai tự duyệt phiếu của mình.

### A2 · HR Leader trả phiếu về — `RejectRecruitmentRequestCommand`
**Đăng nhập:** `hr.dev` → **Phiếu yêu cầu tuyển dụng** → mở phiếu → **Trả lại**

1. Gõ lý do dưới 10 ký tự → nút **Xác nhận trả lại** vẫn mờ.
2. Gõ `Dải lương vượt khung cấp bậc, đề nghị 45–60 triệu` → **Xác nhận trả lại**.

- [ ] ✅ **A2** — Phiếu sang **Bị trả lại** và hiện nguyên văn lý do. `hm.dev` nhận chuông.
- [ ] ✅ **A2b** — HR Leader **không có nút Lập phiếu**, vì chỉ HM lập được phiếu.

### A3 · HM sửa rồi gửi lại — `UpdateRecruitmentRequestCommand` → `ResubmitRecruitmentRequestCommand`
**Đăng nhập:** `hm.dev` → mở phiếu

1. **Sửa phiếu** → lương `45000000` – `60000000` → **Gửi phiếu**.
2. Bấm **Gửi lại**.

- [ ] ✅ **A3** — Phiếu về **Chờ duyệt**, có nhãn **Lần gửi thứ 2**, và lý do bị trả lần trước **vẫn hiện**.

### A4 · HR Leader duyệt kèm phân công Recruiter — `ApproveRecruitmentRequestCommand`
**Đăng nhập:** `hr.dev` → mở phiếu → khối **Phân công Recruiter** → chọn `Recruiter Dev` → **Duyệt & phân công**

- [ ] ✅ **A4** — Nút duyệt **mờ khi chưa chọn Recruiter**. Duyệt xong, phiếu sang **Đã duyệt** và hiện *Phụ trách: Recruiter Dev*.
- [ ] ✅ **A4b** — `recruiter.dev` nhận chuông *"Bạn được phân công một yêu cầu tuyển dụng"*.

---

## Phần B · Soạn JD và đăng tin — làn Recruiter ↔ HM ↔ System

### B0 · (Tuỳ chọn) HR Leader cấu hình mẫu JD
**Đăng nhập:** `hr.dev` → **Mẫu JD**: tải logo, điền tên công ty, chọn màu nhấn → **Lưu mẫu**.
Bỏ qua bước này thì trình soạn dùng mẫu mặc định.

### B1 · Recruiter soạn JD theo mẫu — `SaveJdDocumentCommand` · `GenerateJdFileCommand`
**Đăng nhập:** `recruiter.dev` → **Phiếu yêu cầu tuyển dụng** → chọn phiếu → **Soạn JD theo mẫu công ty**

1. Viết thêm vài dòng vào mỗi mục.
2. **Lưu nháp**, rồi tải lại trang.
3. Bấm **Xem trước**.

- [ ] ✅ **B1** — Trình soạn **đã điền sẵn** từ phiếu: vị trí, số lượng, lương 45–60 triệu. *Mô tả sơ bộ* và *Yêu cầu ứng viên* nằm đúng vào hai mục tương ứng.
- [ ] ✅ **B1b** — Ô **Đơn vị** là danh sách chọn VND/USD.
- [ ] ✅ **B1c** — Sau khi tải lại trang, nội dung vừa gõ **vẫn còn**. File xem trước in lương dạng `45.000.000 – 60.000.000 VND` (dấu chấm).

### B2 · Recruiter dựng tin nháp — `CreateJobCommand`
Trong trình soạn → **Dựng tin từ JD này** → kiểm biểu mẫu → **Tạo tin**

- [ ] ✅ **B2** — Biểu mẫu mở ra **đã gắn sẵn file JD**, đã điền tiêu đề, lương, số lượng. Phần cấu hình vòng có đủ **3 vòng** đúng thứ tự trên phiếu.
- [ ] ✅ **B2b (System)** — Mở tin vừa tạo: thẻ **Đội tuyển dụng** đã có `HM Dev` kèm chip **Người quyết định**, dù **không ai gán tay**.
- [ ] ✅ **B2c (System)** — Cửa sổ uvicorn hiện dòng `POST /ingest` (JD được nạp vào RAG).
- [ ] ✅ **B2d** — Quay lại phiếu: nút dựng tin **biến mất**, thay bằng *"Phiếu này đã có tin tuyển dụng."*

### B3 · Gửi HM ký khi ngân hàng đề còn trống → phải bị chặn
**Vẫn là** `recruiter.dev` → **Tin tuyển dụng** → mở tin → **Gửi HM ký duyệt**

- [ ] ✅ **B3** — Bị chặn với thông báo *"…có vòng trắc nghiệm nhưng ngân hàng đề đang trống"*. Tin **vẫn là nháp**.

### B4 · Soạn ngân hàng đề trắc nghiệm — `CreateOnlineTestQuestionCommand`
**Đăng nhập:** `hm.dev` → **Tin tuyển dụng của tôi** → mở tin → **Mở ngân hàng đề**.
Recruiter chủ tin cũng làm được, qua nút **Ngân hàng câu hỏi**.

1. Khối **Cấu hình bài thi**: Điểm đạt `70` · Số câu mỗi bài `3` · Thời lượng `10` → **Lưu cấu hình**.
   (Mặc định mỗi bài 20 câu, nên phải hạ xuống 3 để không phải soạn nhiều câu.)
2. Thêm **3 câu hỏi**, mỗi câu chọn một đáp án đúng. **Ghi nhớ đáp án đúng** để làm bài ở D4.

- [ ] ✅ **B4** — Danh sách hiện **3 câu**.

### B5 · Recruiter gửi HM ký duyệt — `UpdateJobStatusCommand → pending`
**Đăng nhập:** `recruiter.dev` → mở tin → **Gửi HM ký duyệt**

- [ ] ✅ **B5** — Tin sang **Chờ duyệt**, có banner *"Đang chờ HM Dev ký duyệt mô tả công việc."* `hm.dev` nhận chuông.
- [ ] ✅ **B5b** — Recruiter **không có nút nào để tự đăng tin**.

### B6 · HM yêu cầu sửa JD — `JobHmSignOffCommand → rejected`
**Đăng nhập:** `hm.dev` → mở tin

1. Thẻ **Bản mô tả công việc** → bấm tên file. File JD mở ngay trong trình duyệt.
2. **Yêu cầu sửa** → nhập `Bổ sung yêu cầu kinh nghiệm hệ thống phân tán` → **Gửi yêu cầu sửa**.

- [ ] ✅ **B6** — HM **xem được chính file JD** trước khi quyết định.
- [ ] ✅ **B6b** — Tin quay về **Bị từ chối**. Recruiter thấy banner *"HM Dev yêu cầu sửa"* kèm góp ý và nút **Sửa JD bằng trình soạn**.

### B7 · Recruiter sửa rồi gửi lại
**Đăng nhập:** `recruiter.dev` → **Sửa JD bằng trình soạn** → sửa → **Lưu nháp** → quay lại tin → **Gửi HM ký duyệt**

- [ ] ✅ **B7** — Tin lại về **Chờ duyệt**. File JD gắn với tin là **bản vừa sửa**.

### B8 · HM ký duyệt → tin tự đăng — `JobHmSignOffCommand` + System *Publish*
**Đăng nhập:** `hm.dev` → mở tin → **Ký duyệt**

- [ ] ✅ **B8** — Tin sang **Đang tuyển** (`active`) mà **không ai bấm "Duyệt đăng"**.
- [ ] ✅ **B8b** — Tin hiện trên job board http://localhost:3000.

### B9 · HM nạp playbook của tin — **bắt buộc có bộ tiêu chí chấm**
**Vẫn là** `hm.dev`, tại màn tin → thẻ **Playbook của tin** → **Thêm**

> Thiếu **Tiêu chí chấm phỏng vấn** (ở cấp tin hoặc cấp công ty) thì sau buổi phỏng vấn **không sinh
> được báo cáo đánh giá**, và giao diện **không báo lỗi gì**.

1. Loại tài liệu `Tiêu chí chấm phỏng vấn` → **Tải file mẫu**. Sửa thành 2 tiêu chí: `technical` 60 và `communication` 40.
2. Thử sai trước: đổi 40 thành `50` (tổng 110) → **Tải lên** → **bị từ chối**.
3. Sửa lại cho tổng = 100 → Áp cho `Cả tin (mọi vòng)` → **Tải lên**.
4. (Tuỳ chọn) Thêm một file `Ngân hàng câu hỏi` có một câu rất riêng, ví dụ *"Kể một lần bạn phải rollback migration trên production"*. Ở phần E/F, bạn sẽ nhận ra câu này nếu AI dùng tới.

- [ ] ✅ **B9** — Tổng trọng số ≠ 100 thì bị chặn ngay khi tải lên. File đúng thì hiện **"2 tiêu chí"**.
- [ ] ✅ **B9b** — Cửa sổ uvicorn hiện `POST /ingest` cho mỗi file.

### B10 · HM khai lịch có mặt — `SetHmAvailabilityCommand`
**Vẫn là** `hm.dev`, tại màn tin → thẻ **Lịch tôi có mặt được**

- **Vòng 2**: ngày mai `08:00` → `12:00` → **Lưu lịch**.
- **Vòng 3**: ngày mai `13:00` → `17:00` → **Lưu lịch**.

- [ ] ✅ **B10** — Vòng 1 **không có ô khai lịch** và hiện ghi chú *"…là bài trắc nghiệm trực tuyến — không cần bạn có mặt…"*.

> Buổi phỏng vấn thật **không bị khoá theo giờ ca**: bạn vẫn làm được ngay hôm nay với ca xếp cho ngày
> mai. Ca chỉ cần nằm **trong tương lai** và **trọn trong khung** HM đã khai.

---

## Phần C · Ứng tuyển và cổng duyệt hồ sơ — làn Candidate ↔ Recruiter ↔ HM

### C1 · Ứng viên nộp hồ sơ — `SubmitApplicationCommand`
**Đăng nhập (:3000):** `practice.dev` → mở tin `Senior Backend Engineer`

1. (Tuỳ chọn) Tải CV lên để xem **điểm phù hợp CV–JD** (cần `GEMINI_API_KEY`).
2. **Ứng tuyển** → tải CV lên → **Gửi hồ sơ ứng tuyển**.

- [ ] ✅ **C1** — Hồ sơ hiện trong **Việc đã ứng tuyển**.
- [ ] ✅ **C1b (System)** — Ở màn tin của Recruiter, hồ sơ nằm cột **Mới ứng tuyển** và có **Điểm CV**. Nếu chưa có `GEMINI_API_KEY` thì cột này trống; ghi nhận rồi đi tiếp.

### C2 · Recruiter sàng CV, gửi HM duyệt — `RequestHmApprovalCommand`
**Đăng nhập:** `recruiter.dev` → mở tin → cột **Mới ứng tuyển** → chọn ứng viên → **Duyệt hồ sơ**

- [ ] ✅ **C2** — Hồ sơ sang **Chờ HM duyệt**. Có thông báo *"Ứng viên chưa được báo — thư qua vòng chỉ gửi khi đã xếp được lịch"*.
- [ ] ✅ **C2b** — Phía ứng viên **không có** thông báo hay thư nào.
- [ ] ✅ **C2c** — Ở bước này **không có nút Xếp lịch**, vì cổng HM đang đóng.

### C3 · HM duyệt hồ sơ — `HmDecideApplicationCommand`
**Đăng nhập:** `hm.dev` → mở tin → **Ứng viên của tin** → chọn ứng viên → **Duyệt** → hộp xác nhận → **Duyệt**

- [ ] ✅ **C3** — Hộp xác nhận ghi *"Vòng 1 là bài trắc nghiệm trực tuyến — không cần bạn có mặt…"*.
- [ ] ✅ **C3b (System)** — Hồ sơ sang **Chờ xếp lịch**, và `recruiter.dev` nhận chuông.

---

## Phần D · Vòng 1 — Trắc nghiệm

### D1 · Recruiter tạo ca thi — `CreateSlotCommand`
**Đăng nhập:** `recruiter.dev` → mở tin → **Lịch phỏng vấn** → **Vòng 1 · Trắc nghiệm** → **Thêm khung giờ**

- Ngày **hôm nay**, bắt đầu = **giờ hiện tại + 10 phút**, kết thúc = bắt đầu + 1 giờ.
- Để trống ô giới hạn số ứng viên.

> Bài thi **chỉ mở từ giờ bắt đầu ca tới +1 giờ**, và ca phải nằm **trong tương lai** lúc tạo. Vì vậy
> hãy đặt ca gần giờ hiện tại để chỉ phải chờ vài phút.

- [ ] ✅ **D1** — Ca được tạo. Vòng trắc nghiệm **không đòi** ca nằm trong lịch HM.

### D2 · Recruiter xếp lịch và gửi thư mời — `AssignSlotCommand`
Màn tin → cột **Chờ xếp lịch** → chọn ứng viên → **Mời phỏng vấn** → chọn ca vừa tạo → trình soạn thư mở ra

1. Thêm một câu vào cuối thư, ví dụ *"Vui lòng chuẩn bị máy tính có kết nối ổn định."*
2. **Gửi**.

- [ ] ✅ **D2** — Thư **đã điền sẵn** tên ứng viên và **giờ thi cụ thể**. Không còn placeholder nào.
- [ ] ✅ **D2b (System)** — Hồ sơ sang **Chờ xác nhận lịch**. Khối *Lịch sử email* của hồ sơ có dòng mới gắn chip **Đã sửa**, bung ra thấy đúng câu vừa thêm.
- [ ] ✅ **D2c** — Ứng viên nhận chuông. Vòng trắc nghiệm **không mở phỏng vấn thử**.

### D3 · (Tuỳ chọn) Ứng viên xác nhận lịch — `ConfirmScheduleCommand`
**Đăng nhập (:3000):** `practice.dev` → **Lịch phỏng vấn** → **Xác nhận tham dự** → **Tôi chắc chắn — Xác nhận**

- [ ] ✅ **D3** — Recruiter thấy trạng thái **Đã xác nhận lịch**.

### D4 · Ứng viên làm bài — `SubmitOnlineTestCommand`
**Vẫn là** `practice.dev` → **Việc đã ứng tuyển** → mở hồ sơ → **Mở bài thi trắc nghiệm**

1. Mở **trước giờ bắt đầu ca** → bài thi **chưa mở**.
2. Tới giờ → **Tôi đã hiểu — Bắt đầu làm bài** → làm cả 3 câu → **Nộp bài**.

> ⚠️ Trong lúc làm bài, **chuyển tab hoặc thu nhỏ cửa sổ là bài bị nộp ngay**.

- [ ] ✅ **D4** — Trước giờ thì không làm được. Nộp xong hiện **Đã nộp bài**.
- [ ] ✅ **D4b (System)** — Máy tự chấm, nhưng **trạng thái hồ sơ không đổi**.

### D5 · Recruiter xem điểm và mời vòng 2 — `GetOnlineTestAnswerSheetQuery`
**Đăng nhập:** `recruiter.dev` → màn tin → cột vòng 1 → chọn ứng viên → **Xem bài làm**

- [ ] ✅ **D5** — Bài làm hiện **Đúng x/3 câu**, điểm, và nhãn **Đạt / Chưa đạt điểm sàn**.

Việc bấm **Mời vào vòng 2** làm ở bước E2, sau khi đã tạo ca vòng 2.

---

## Phần E · Vòng 2 — Sơ loại (ứng viên làm từ nhà)

### E1 · Recruiter tạo ca trong lịch của HM — `CreateSlotCommand`
**Đăng nhập:** `recruiter.dev` → **Lịch phỏng vấn** → **Vòng 2 · Sơ loại**

1. Cố ý tạo một ca **ngoài** khung HM: **ngày mai 12:30–13:00** → có cảnh báo *"…nằm ngoài mọi khung giờ rảnh của Hiring Manager…"* → vẫn **Thêm khung giờ**. Ca này dùng để kiểm ở E2.
2. Khối **Lịch rảnh của Hiring Manager** → **Dùng khung này** → chỉnh thành **ngày mai 09:00–09:30** → **Thêm khung giờ**.

> Không đặt ca thử ở 14:00: ca các vòng trong cùng một tin không được chồng giờ nhau, nên nó sẽ chặn ca vòng 3 ở F1.

- [ ] ✅ **E1** — Ca vòng hội thoại **cố định 1 ứng viên/ca**. Ca ngoài lịch HM vẫn tạo được nhưng bị gắn nhãn **Ngoài lịch HM**.

### E2 · Mời vào vòng 2 — `AssignSlotCommand`
Màn tin → chọn ứng viên → **Mời vào vòng 2**

1. Chọn ca **12:30** → **bị từ chối**, vì ca nằm ngoài lịch HM.
2. Chọn lại ca **09:00** → trình soạn thư → **Gửi**.

- [ ] ✅ **E2** — Server **không cho gán** ca ngoài lịch HM.
- [ ] ✅ **E2b (System)** — Hồ sơ ở vòng 2, trạng thái **Chờ xác nhận lịch**, và ứng viên nhận chuông.

### E3 · (Tuỳ chọn) Ứng viên phỏng vấn thử — `StartSessionAsync(practice)`
**Đăng nhập (:3000):** `practice.dev` → mở hồ sơ → **Bắt đầu phỏng vấn thử** → kiểm tra mic/cam → trả lời 1–2 câu → kết thúc

- [ ] ✅ **E3** — Có **đồng hồ 20 phút**, **không có avatar**. Ô trả lời **sửa hoặc gõ tay được** trước khi bấm Gửi.
- [ ] ✅ **E3b** — Kết quả buổi thử **không có Đạt/Không đạt**, và **nhân sự không thấy** buổi thử này.

### E4 · Recruiter cấp mã làm từ nhà — `IssueApplicationInterviewCodeCommand`
**Đăng nhập:** `recruiter.dev` → màn tin → chọn ứng viên → thẻ **Mã vào phòng phỏng vấn · Vòng 2** (nhãn **Làm từ nhà**) → **Cấp mã phỏng vấn** → **Sao chép link vào phòng**

- [ ] ✅ **E4** — Mã có **6 ký tự**, kèm giờ hết hạn và dòng *"chỉ dùng một lần"*. Tải lại trang thì thẻ **vẫn hiện đúng mã đó**. Muốn mã khác phải bấm **Cấp mã mới**, có hộp xác nhận báo mã cũ sẽ hết hiệu lực. **Đừng bấm** ở lượt này.
- [ ] ✅ **E4b** — Portal ứng viên có nút **Vào phòng phỏng vấn**.

### E5 · Ứng viên vào phòng chờ — `ValidateInterviewCodeCommand` → `StartSessionAsync(real)`
**Trình duyệt của ứng viên:** dán link vừa sao chép, hoặc Portal → **Vào phòng phỏng vấn**

Ô mã đã điền sẵn → **Bắt đầu phỏng vấn** → màn hình chuyển toàn màn hình → kiểm tra thiết bị → **phòng chờ**

- [ ] ✅ **E5** — Mã được **điền sẵn**, rồi tự biến khỏi thanh địa chỉ.
- [ ] ✅ **E5b (System)** — Ứng viên ngồi ở **phòng chờ**, và **AI chưa hỏi câu nào**.

### E6 · HM vào phòng và cho ứng viên vào — `JoinInterviewRoomCommand` · `AdmitCandidateCommand`
**Đăng nhập:** `hm.dev` → **Phòng phỏng vấn**

1. Ứng viên hiện trạng thái **Đang chờ vào phòng**.
2. Nút **Cho ứng viên vào** đang **mờ**. Bấm **Vào phòng** trước, rồi mới bấm **Cho ứng viên vào**.

- [ ] ✅ **E6** — Chưa **Vào phòng** thì không cho ứng viên vào được.
- [ ] ✅ **E6b** — Ứng viên được vào và AI **bắt đầu hỏi**. Trạng thái đổi sang **Đang phỏng vấn**.

### E7 · Ứng viên phỏng vấn với AI — `SessionHub` · Deepgram · RAG · ElevenLabs
Trả lời 2–3 câu (nói hoặc gõ) → **Kết thúc phỏng vấn**

- [ ] ✅ **E7** — Cửa sổ uvicorn hiện `POST /next-question` mỗi câu hỏi và `POST /analyze-answer` mỗi câu trả lời.
- [ ] ✅ **E7b** — Bấm `Esc` → **lớp phủ chặn** cả phòng, phải bấm quay lại toàn màn hình mới làm tiếp. (Alt+Tab vẫn thoát được; đây là giới hạn đã biết của nền web.)
- [ ] ✅ **E7c (System)** — Sau khi kết thúc, uvicorn hiện `POST /evaluate`, và `hm.dev` nhận chuông *"Có kết quả phỏng vấn chờ bạn chốt"*.

### E8 · HR Leader thử chốt thay → phải đòi lý do
**Đăng nhập:** `hr.dev` → **Đánh giá** → mở báo cáo vừa sinh

- [ ] ✅ **E8** — Có banner vàng *"Người chốt kết quả của tin này là HM Dev. Bạn đang chốt thay…"* kèm ô **Lý do chốt thay**. Bấm chốt khi lý do dưới 10 ký tự → **bị chặn**.

**Đừng chốt ở đây** — để HM chốt ở E9.

### E9 · HM chốt Đạt vòng 2 — `ConfirmHrReviewCommand`
**Đăng nhập:** `hm.dev` → **Kết quả phỏng vấn** → mở báo cáo

1. Xem ca, transcript đầy đủ, và bảng điểm theo 2 tiêu chí.
2. Nếu AI chấm **Đạt** → **Xác nhận AI**. Nếu AI chấm **Không đạt** → **Override** → chọn Đạt → nhập lý do → xác nhận.

- [ ] ✅ **E9** — Bảng điểm hiện đúng **Chuyên môn / Giao tiếp** với trọng số **60/40**. Điểm tổng = `technical×0.6 + communication×0.4`.
- [ ] ✅ **E9b** — HM **không phải nhập lý do chốt thay**, vì HM chính là người quyết định.
- [ ] ✅ **E9c (System)** — Vẫn còn vòng 3, nên hồ sơ **chưa "Đạt"**: vẫn ở trạng thái đang phỏng vấn, và vòng 3 được mở (có thư/chuông cho ứng viên).

---

## Phần F · Vòng 3 — Chuyên môn (tại văn phòng)

### F1 · Recruiter tạo ca và mời vòng 3
**Đăng nhập:** `recruiter.dev`

1. **Lịch phỏng vấn** → **Vòng 3 · Chuyên môn** → **Dùng khung này** → chỉnh thành **ngày mai 14:00–14:30** → **Thêm khung giờ**.
2. Màn tin → chọn ứng viên → **Mời vào vòng 3** → chọn ca → **Gửi**.

- [ ] ✅ **F1** — Gán được ca 14:00 của vòng 3. Thư mời ghi đúng giờ ca vòng 3.

### F2 · Cấp mã tại văn phòng và phỏng vấn ở Kiosk
1. `recruiter.dev` → thẻ **Mã vào phòng phỏng vấn · Vòng 3** (nhãn **Tại văn phòng**) → **Mở trang Kiosk** (mở http://localhost:3000/kiosk) → **Cấp mã phỏng vấn**.
2. Trên trang Kiosk: nhập mã → **Bắt đầu phỏng vấn** → phòng chờ.
3. `hm.dev` → **Phòng phỏng vấn** → **Vào phòng** → **Cho ứng viên vào**.
4. Trả lời 2–3 câu → **Kết thúc phỏng vấn**.

- [ ] ✅ **F2** — Thẻ mã vòng tại văn phòng có nút **Mở trang Kiosk**, không có nút *Sao chép link*. Portal ứng viên **không hiện** mã của vòng này.
- [ ] ✅ **F2b** — Nhập lại **cùng mã** sau khi đã dùng → **bị từ chối**, vì mã chỉ dùng một lần.

### F3 · HM chốt Đạt vòng cuối và đề xuất lương
**Đăng nhập:** `hm.dev` → **Kết quả phỏng vấn** → mở báo cáo vòng 3

1. **+ Thêm đề xuất cấp bậc & mức lương**:
   - Cấp bậc `Senior`.
   - Lương từ `45000000`, lương đến `55000000`.
   - Tiền tệ `VND` (danh sách chọn).
2. **Xác nhận AI**, hoặc **Override** thành Đạt nếu cần.

- [ ] ✅ **F3** — Ô **Tiền tệ** là danh sách chọn VND/USD.
- [ ] ✅ **F3b (System)** — Đây là vòng cuối, nên hồ sơ sang **Đạt**. Ứng viên nhận thư chúc mừng. Recruiter (chủ tin) và HM nhận chuông nhắc **soạn thư mời**.

---

## Phần G · Thư mời nhận việc — làn HM ↔ HR Leader ↔ Recruiter ↔ Candidate

### G1 · HM soạn và gửi duyệt — `CreateOfferCommand` · `SubmitOfferCommand`
**Đăng nhập:** `hm.dev` → mở tin → **Ứng viên của tin** → chọn ứng viên → thẻ **Thư mời nhận việc** → **Soạn thư mời**

1. Kiểm phần điền sẵn: **Mức lương 55000000**, **Tiền tệ VND**. Đây là mức "đến" trong đề xuất ở F3.
2. Điền **Ngày bắt đầu** và **Hạn trả lời**.
3. Ô **Ghi chú nội bộ**: `Còn dư ngân sách, nâng thêm 2 triệu được nếu ứng viên mặc cả.`
4. **Lưu nháp** → mở lại → **Gửi duyệt**.

- [ ] ✅ **G1** — Lương và đơn vị **điền sẵn từ đề xuất của HM**. Ô Tiền tệ là danh sách chọn.
- [ ] ✅ **G1b** — Nút **Gửi duyệt** mờ cho tới khi có đủ lương, ngày bắt đầu và hạn trả lời.
- [ ] ✅ **G1c** — HM **không có** nút **Duyệt thư** và **không có** nút **Gửi ứng viên**.
- [ ] ✅ **G1d** — `hr.dev` nhận chuông. Hồ sơ **vẫn ở Đạt**, vì bản nháp chưa phải lời hứa.

### G2 · HR Leader trả về, người soạn sửa, HR Leader duyệt — `DecideOfferCommand`
1. `hr.dev` → **Thư mời nhận việc** → **Mở thư** → **Trả về bản nháp** → nhập góp ý → **Gửi lại cho người soạn**.
2. `hm.dev` mở thư: có banner vàng chứa góp ý → sửa lương thành `52000000` → **Lưu nháp** → **Gửi duyệt**.
3. `hr.dev` → mở thư → **Duyệt thư**.

- [ ] ✅ **G2** — Vòng trả về rồi gửi lại **không lỗi**. Duyệt xong, thư sang **Đã duyệt**.

### G3 · Recruiter gửi thư cho ứng viên — `SendOfferCommand`
**Đăng nhập:** `recruiter.dev` → **Thư mời nhận việc** → **Mở thư** → **Gửi ứng viên** → trình soạn thư → **Gửi**

- [ ] ✅ **G3** — Thân thư có đủ chức danh, lương `52.000.000 VND`, ngày bắt đầu, hạn trả lời. **Không có câu ghi chú nội bộ.**
- [ ] ✅ **G3b** — Hồ sơ sang **Đã gửi thư mời** đúng lúc bấm **Gửi**, không phải lúc tạo nháp.

### G4 · Ứng viên nhận việc — `RespondToOfferCommand (accept)`
**Đăng nhập (:3000):** `practice.dev` → **Việc đã ứng tuyển** → mở hồ sơ → trang **Thư mời nhận việc**

1. Kiểm rò rỉ: nhấn `Ctrl+U`, mở tab **Network** của DevTools, tìm chuỗi `ngân sách` → **không được thấy**.
2. **Nhận việc**.

- [ ] ✅ **G4** — Trang có lương, ngày bắt đầu và đếm ngược hạn trả lời. **Không có ghi chú nội bộ** ở bất kỳ đâu.
- [ ] ✅ **G4b (System)** — Hồ sơ sang **Đã nhận việc** (`hired`). Phía nhân sự cũng đổi chip tương ứng.

🎉 **Hết luồng chính.**

---

## Phần H · Nhánh phụ — chạy thêm khi còn thời gian

Mỗi dòng dưới đây cần **dữ liệu mới** (phiếu, tin hoặc hồ sơ khác) để không làm hỏng luồng chính.
Hồ sơ mới: tạo thêm một tài khoản ứng viên, hoặc gọi lại `seed-interview-job?fresh=true` (chỉ áp cho tin sandbox).

| # | Nhánh trên sơ đồ | Cách làm | Phải thấy |
|---|---|---|---|
| H1 | HM **Mở lại / Đóng** phiếu đã duyệt | Phiếu **đã duyệt nhưng chưa dựng tin** → khối *Nhu cầu tuyển đã thay đổi?* → **Mở lại để sửa** hoặc **Đóng phiếu** (lý do ≥ 10 ký tự) | Mở lại: phiếu về **Chờ duyệt**, **mất Recruiter phụ trách**, và Recruiter nhận chuông. Đóng: phiếu sang **Đã rút** nhưng giữ tên người duyệt. Phiếu **đã có tin** thì khối này không hiện |
| H2 | HR Leader **chuyển HM chính** | `hr.dev` → màn tin → **Đội tuyển dụng** → **Chuyển Hiring Manager** (lý do ≥ 10) | Cả hai HM đều nhận chuông. Việc đang chờ chuyển sang người mới. HM cũ không còn thấy nút ký/chốt |
| H3 | HM bị khoá → cổng **ĐÓNG** | `sa.dev` khoá `hm.dev` → mở một tin của HM đó | Banner *"…không còn hoạt động… mọi cổng duyệt đang ĐÓNG"*. HR Leader nhận chuông yêu cầu chuyển HM. **Nhớ mở khoá lại** |
| H4 | HR **Đăng vượt cổng HM** | Tin đang **Chờ duyệt** → `hr.dev` → **Đăng vượt cổng HM** (lý do ≥ 10) | Tin **Đang tuyển**, trạng thái ký = *vượt cổng*, và HM nhận chuông |
| H5 | HR **từ chối tin** | Tin đang **Chờ duyệt** → `hr.dev` từ chối kèm lý do | Tin **Bị từ chối**, Recruiter thấy banner *HR từ chối* |
| H6 | Recruiter **loại CV** | Cột **Mới ứng tuyển** → **Loại** | Hồ sơ `cv_rejected`, có thư cảm ơn trong *Lịch sử email* |
| H7 | HM **từ chối hồ sơ** | Hồ sơ **Chờ HM duyệt** → HM **Từ chối** (lý do) | Hồ sơ `cv_rejected`, có thư cảm ơn |
| H8 | HR **vượt cổng duyệt hồ sơ** | Hồ sơ **Chờ HM duyệt** → `hr.dev` → **Vượt cổng duyệt** (lý do ≥ 10) | Hồ sơ **Chờ xếp lịch**, nhãn *Đã vượt cổng duyệt*, và HM nhận chuông |
| H9 | Ứng viên **báo bận** | Portal → **Từ chối tham dự** + lý do | Ca được trả lại, Recruiter nhận chuông, hồ sơ **Báo bận — cần xếp lại**. Recruiter xếp ca khác được |
| H10 | Bài thi **hết hạn** | Xếp ca thi nhưng không vào làm. Chờ tới *giờ bắt đầu + 1 giờ + thời lượng bài + 5 phút* | Hệ thống **tự nộp bài trống**. Cột điểm ghi *Hết hạn · hệ thống tự nộp* |
| H11 | **Không đến phỏng vấn** (no-show) | Xếp ca vòng hội thoại nhưng không vào phòng. Chờ tới *hết ca + 2 giờ* | Hồ sơ `not_pass`, trạng thái *Không dự buổi đã hẹn*, và không còn lượt thử cho vòng đó |
| H12 | HR/SA **cho vào thay HM** | Màn nhân sự không có nút này, nên gọi API bằng token của `hr.dev` (lấy trong DevTools). Khi ứng viên đang ở phòng chờ: `curl.exe -H "Authorization: Bearer <TOKEN>" http://localhost:5000/api/interview/waiting-rooms` để lấy `sessionId`, rồi `curl.exe -X POST -H "Authorization: Bearer <TOKEN>" http://localhost:5000/api/interview/session/<sessionId>/admit` | Phiên chạy, và nhật ký kiểm toán (Super Admin → **Audit Logs**) có `interview_admitted_without_hm` |
| H13 | HR **chốt thay HM** | E8 nhưng nhập lý do ≥ 10 rồi chốt | Người chốt ghi *HR Admin (chốt thay)*, và HM nhận chuông |
| H14 | HM chốt **Không đạt** | Báo cáo → chốt Không đạt | Hồ sơ `not_pass`, có thư cảm ơn |
| H15 | Ứng viên **từ chối thư mời** / thư **quá hạn** | G4 → **Từ chối**, hoặc để quá hạn trả lời | Hồ sơ `offer_declined`. Thư quá hạn thì trạng thái thư là *Hết hạn* |
| H16 | Chặn giá trị đơn vị tiền lạ | Gọi API tạo phiếu/tin với `"salaryCurrency":"EUR"` | Trả lỗi *"Đơn vị tiền chỉ được chọn VND hoặc USD."* |

---

## Phần I · Sự cố thường gặp

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| `curl` trả về đối tượng lạ hoặc báo lỗi tham số | Trong PowerShell 5.1, `curl` là tên tắt của `Invoke-WebRequest`. Gõ **`curl.exe`** |
| Mọi thao tác AI báo lỗi kết nối | API không tìm được rag-service. Thiếu `RAG_SERVICE_URL=http://localhost:8000`, hoặc đang chạy profile **IIS Express** |
| AI hỏi chung chung, không bám playbook | rag-service đang dùng **database khác** với API. Xem [rag-service-local-setup.md](rag-service-local-setup.md) |
| Phỏng vấn xong nhưng **không có báo cáo** | Tin chưa có **Tiêu chí chấm phỏng vấn** (bước B9). Log API ghi *"CHƯA khai bộ tiêu chí chấm phỏng vấn"*. Nạp rubric rồi chấm lại bằng `POST /api/dev/regrade-session/{sessionId}` |
| HM không lập được phiếu | Tài khoản chưa có đội. Làm lại bước 1.3 |
| **Gửi HM ký duyệt** bị chặn | Ngân hàng đề ít câu hơn *Số câu mỗi bài* (bước B4) |
| Không chọn được ca khi xếp lịch vòng 2/3 | Ca nằm ngoài **Lịch tôi có mặt được** của HM, hoặc trùng giờ với buổi khác của ứng viên |
| Không tạo được ca | Giờ bắt đầu đã ở quá khứ, vì ca phải nằm trong tương lai |
| Bài thi báo chưa mở | Chưa tới giờ bắt đầu ca. Bài chỉ mở trong 1 giờ kể từ giờ bắt đầu |
| **Cho ứng viên vào** bị mờ | HM chưa bấm **Vào phòng** |
| Tài khoản tạo từ giao diện không đăng nhập được | Không có SMTP nên mật khẩu tạm không gửi đi. Dùng SQL ở bước 1.2 |
| Thư ghi **Gửi lỗi** trong *Lịch sử email* | Chưa cấu hình `EmailSettings`. Không ảnh hưởng luồng, vì nội dung thư vẫn được lưu |
| Không thấy chuông realtime | Tải lại trang. Nếu vẫn không có, kiểm log API phần `DbChangeListener` |
| Không nói được, chỉ gõ được | Thiếu `Media:Deepgram:ApiKey` / `Media:ElevenLabs:ApiKey`. Hệ thống tự chuyển sang gõ tay, luồng vẫn chạy |
