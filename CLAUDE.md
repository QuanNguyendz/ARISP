# CLAUDE.md – ARISP Project Configuration for Claude Code

> File này là cấu hình độc lập cho **Claude Code**. Nội dung được duy trì gốc trong `.ai/` và sync vào đây khi có thay đổi.  
> Teammates dùng Antigravity đọc từ `AGENTS.md` (bridge → `.ai/`). Không xoá các file `.ai/` hay `AGENTS.md`.

---

## Tổng quan dự án

ARISP là nền tảng tuyển dụng nội bộ doanh nghiệp tích hợp **Job Board IT** và **AI Interview Automation**. Ứng viên tự tìm và ứng tuyển việc làm IT trực tiếp trên nền tảng. AI tự động phỏng vấn ứng viên qua nhiều vòng, đánh giá Pass/Not Pass, HR xác nhận. Không cần nhân sự nội bộ tham gia trực tiếp vào buổi phỏng vấn.

**Mô hình kinh doanh:** Single-tenant – Dành riêng cho 1 doanh nghiệp sử dụng nội bộ. Không hỗ trợ multi-tenant, không dùng `organization_id` trong database.

---

## User Roles

| Role | Mô tả & Phân quyền |
|---|---|
| **Super Admin** | Quản trị hệ thống – cấu hình toàn cục (allowed domains, webhooks), quản lý tài khoản HR, **danh sách đội/bộ phận + gán đội cho từng tài khoản** (ADR-065), theo dõi audit log |
| **HR Leader** (HRM) | Trưởng nhóm HR – sở hữu quy trình + tuân thủ + **ngân sách lương**: **duyệt Recruitment Request kèm phân công Recruiter** (**không lập phiếu** — người lập phiếu trở thành HM của tin), cấu hình phỏng vấn, Playbook **cấp công ty**, **chốt thư mời nhận việc** (quyết định cuối cùng — ADR-063), **gán / chuyển HM chính của tin** (ADR-068). Chốt thay HM khi cần (bắt buộc lý do + audit log) |
| **Hiring Manager** | Trưởng bộ phận cần tuyển – **người khởi phát nhu cầu và quyết định chuyên môn** (ADR-061/063): **lập Recruitment Request** kèm đề xuất dải lương và **bộ tiêu chí chấm CV** (ADR-070), **ký duyệt JD** (ký xong tin lên `active`) hoặc yêu cầu sửa, duyệt shortlist, **chốt Pass/Not Pass kết quả AI**, **soạn thư mời + đề xuất mức lương** rồi gửi HR Leader chốt, **Playbook theo tin** ngay trong màn tin (ADR-069). Tự khai **lịch có mặt được** ở màn tin (tách khỏi lệnh duyệt), và **vào phòng phỏng vấn thật rồi cho ứng viên vào** (ADR-067). **KHÔNG được:** duyệt phiếu của chính mình, tự chốt thư mời. Phạm vi **quyết định** theo đội tuyển dụng của từng tin, không theo phòng ban; riêng đội **ghi trên phiếu** thì lấy cứng từ tài khoản, HM không tự khai (ADR-065) |
| **Recruiter** | Chuyên viên tuyển dụng – vận hành phễu: **dựng JD từ phiếu được phân công**, sàng lọc ứng viên, xếp lịch, cấp Interview Code On-site, soạn thư mời. **Không lập phiếu, không chốt kết quả phỏng vấn, không tự đăng tin** |
| **Candidate** | Ứng viên – tự ứng tuyển qua Job Board, làm phỏng vấn thử (Practice Remote) và phỏng vấn thật (Real On-site) |

---

## Auth Flow

### Cổng đăng nhập (Login Portals)
- **HR / Super Admin / Recruiter / Hiring Manager:** `/admin/login` → form **Email + Mật khẩu**. Hỗ trợ thêm **Google OAuth2** (Google Sign-In) cho người dùng nội bộ công ty.
- **Candidate (Job Board):** `/jobs/login` → form **Email + Mật khẩu** (tự đăng ký trước đó).
- **Candidate Portal (Xem kết quả/Lên lịch):** Passwordless – nhập email → nhận **Magic Link** → click đăng nhập.
- **Kiosk On-site:** Máy trạm tại văn phòng – trình duyệt **toàn màn hình có ràng buộc**, chỉ có form nhập **Interview Code** (6 ký tự). **Không phải** kiosk cấp hệ điều hành (không khoá được Alt+Tab/phím Windows); khoá cứng thật là bước cấu hình máy trạm – ADR-062 + `docs/kiosk-workstation-setup.md`.

### Quy trình đăng ký (Registration Flow)
- **HR / Recruiter / Super Admin:** Không có self-registration. Super Admin tạo tài khoản trước trong DB (pre-provisioning). Nếu email chưa có trong DB → chặn đăng nhập, không tạo tài khoản nháp. Khi dùng Google Sign-In, email bắt buộc thuộc `allowed_email_domains`.
- **Candidate:** Đăng ký tự do bằng bất kỳ email cá nhân nào.

---

## Interview Modes

### Practice (Phỏng vấn thử – Remote)
- Chỉ dành cho luyện tập, không ảnh hưởng verdict tuyển dụng
- **Mở tự động cho từng vòng** sau khi ứng viên **đã pass CV + đặt lịch buổi phỏng vấn thật của vòng đó** (qua Portal). Vào thẳng route Portal (`/practice/:applicationId`) — **KHÔNG cần Interview Code, không cần magic link riêng**. Giới hạn **1 lượt / VÒNG**; cửa sổ dùng = từ lúc đặt lịch đến giờ phỏng vấn thật của vòng — xem ADR-020/027
- **Practice giống hệt buổi thật sắp tới của vòng:** cùng `round_type` (technical → technical, sơ loại/ngôn ngữ → sơ loại/ngôn ngữ) + cùng ngôn ngữ (chung `InterviewRoundConfig` theo `RoundNumber`)
- RAG chỉ dùng JD + CV (không load Playbook nội bộ)
- **Audio-only (ADR-050): KHÔNG avatar** — giữ đủ STT/RAG/GPT-4o + **giọng ElevenLabs** (phát qua WebAudio), bot tĩnh. ADR-067 gỡ avatar khỏi cả buổi thật nên nay hai chế độ giống nhau về media. **Không quay video — chỉ lưu transcript** + Evaluation Report
- **Trần 20 phút (ADR-050):** đồng hồ đếm ngược; hết giờ → khoá mic → AI nói 1 câu kết thúc → đóng phiên (`Interview:PracticeMaxDurationMinutes`)
- **Nhập kép (ADR-050):** thu âm điền vào ô trả lời, ứng viên **sửa/gõ tay** được (mic on/off) trước khi Gửi — sửa đoạn thu âm nghe sai
- Chi phí practice **do doanh nghiệp trả** (mỗi vòng = 1 lượt thử + 1 lượt thật). Tối ưu bằng gating theo phễu — xem ADR-038/048

### Real (Phỏng vấn thật – On-site)
- Bắt buộc tại văn phòng công ty, đến đúng khung giờ đã đặt lịch (Availability Slot của vòng)
- Candidate nhập **Interview Code** tại thiết bị Kiosk (code chỉ dùng cho real — không có `code_type`)
- **Điều kiện bắt đầu (ADR-067):** Hiring Manager phải **đã vào phòng cùng AI**, rồi **cho ứng viên vào**. Trước đó phiên ở trạng thái `waiting` và AI không hỏi câu nào
- **Một ca = một ứng viên**; một người không dự hai buổi trùng giờ, kể cả ở hai tin khác nhau
- Code: one-time-use, TTL mặc định 2 giờ, 6 ký tự alphanumeric, bind với `application_id` + vòng
- RAG dùng đầy đủ: JD + CV + Playbook nội bộ

---

## Recruitment Flow

### Phase 0 – Recruitment Request (ADR-063)
**Điểm bắt đầu BẮT BUỘC của mọi tin — và CHỈ HM lập được.** HM lập phiếu (vị trí, số lượng, **mức độ ưu tiên**, **ngày dự kiến bắt đầu**, lý do, mô tả sơ bộ, yêu cầu ứng viên, **dải lương đề xuất hoặc "thoả thuận"**, **bộ tiêu chí chấm CV (bắt buộc)** — đội xin tuyển do tài khoản quyết định, không gõ tay) → HR Leader **duyệt kèm chọn 1 Recruiter trong cùng một thao tác**, hoặc **trả lại kèm lý do** (thường là chỉnh dải lương) → HM sửa rồi gửi lại. **Không ai duyệt phiếu của chính mình.**

### Phase 1 – Enterprise Setup
**Recruiter được phân công** dựng Job Posting **từ phiếu đã duyệt** (biểu mẫu điền sẵn từ phiếu): tên vị trí, JD (text + file PDF/DOCX), cấu hình vòng phỏng vấn (screening / technical / online_test), ngôn ngữ, availability slots (khung giờ phỏng vấn thật per vòng), bộ tiêu chí chấm CV (chép từ phiếu), interview persona, playbook. **Người lập phiếu tự động thành HM chính của tin** → gửi duyệt → **HM ký duyệt JD là tin lên `active`** (không còn bước duyệt riêng của HR Leader), hoặc **yêu cầu sửa** → tin về `rejected`, Recruiter sửa rồi gửi lại (ADR-068).

### Phase 2 – Candidate Application
1. Candidate xem Job Detail trên Job Board
2. *(Tùy chọn)* Upload CV → Gemini chấm theo bộ tiêu chí của tin → hiển thị Match Score + Summary
3. Bấm "Ứng tuyển" → submit CV + thông tin → tạo Application; hàng đợi nền gắn kết quả CV-JD Analysis (ADR-070)
4. **Recruiter duyệt hồ sơ** → sang **Chờ HM duyệt** (`hm_review`, **không báo ứng viên**) → **HM duyệt** → hồ sơ về **Chờ xếp lịch** (`screening`) của Recruiter → Recruiter xếp ca **nằm trọn trong lịch HM tự khai ở màn tin** (vòng trắc nghiệm không cần HM) → **lúc này** thư báo qua vòng kèm giờ hẹn mới gửi → mở 1 lượt phỏng vấn thử cho vòng đó (ADR-067)
   > Việc chốt giờ cụ thể với ứng viên (SMS, Zalo, gọi điện) **cố ý nằm ngoài hệ thống** — hệ thống chỉ ràng buộc kết quả phải khớp lịch HM.

### Phase 3 – On-site Access
Candidate đến văn phòng **đúng khung giờ đã đặt lịch** → Recruiter cấp Interview Code (6 ký tự, chỉ cho real) → nhập tại Kiosk → **phòng chờ** → Hiring Manager vào phòng và cho vào → bắt đầu phỏng vấn thật. *(Vòng sơ loại: làm từ nhà — Recruiter gửi link Kiosk kèm mã, sau đó cùng đường phòng chờ.)*

### Phase 4 – Multi-round AI Interview
- **Round 1 (Screening):** Language-aware – AI detect ngôn ngữ từ JD, phỏng vấn bằng ngôn ngữ đó, đánh giá cả nội dung lẫn language proficiency
- **Round 2+ (Technical):** Chỉ kích hoạt khi Pass Round trước. Chuyên sâu kỹ năng kỹ thuật
- **Auto-progression:** Pass Round N → HR cấp Interview Code mới cho Round N+1

### Phase 5 – AI Evaluation
Mỗi round tạo Evaluation Report: Verdict (Pass/Not Pass), Overall Score (0–100), per-criterion scores, language assessment (nếu áp dụng), per-question analysis, recommended next step.

### Phase 6 – Hiring Manager chốt kết quả (ADR-061)
**Hiring Manager của tin** xem report + recording → **Confirm** hoặc **Override** (bắt buộc `override_reason`), kèm đề xuất cấp bậc + dải lương để điền sẵn thư mời. Admin chốt thay **luôn** phải nhập `fallback_reason` (≥10 ký tự) → audit log + báo cho HM bị vượt (ADR-068 bỏ ngoại lệ "tin chưa gán HM"). Recruiter không chốt được ở bất kỳ trường hợp nào.

### Phase 6b – Offer → Hired (ADR-061)
Nháp (HM chính/Recruiter **đề xuất** mức lương) → gửi duyệt (báo HR Leader) → **HR Leader chốt** mức lương/điều kiện (ADR-063 — HM không tự chốt đề xuất của chính mình) → gửi ứng viên (qua trình soạn thư) → ứng viên nhận (`hired`) / từ chối (`offer_declined`) / quá hạn (hosted service tự đóng). Hồ sơ sang `offer` lúc **GỬI**, không phải lúc tạo nháp. Một hồ sơ chỉ có **một thư còn hiệu lực** (unique index có filter). Ghi chú đàm phán nội bộ không bao giờ ra Portal.

### Phase 6c – Thư gửi ứng viên sửa được trước khi gửi (ADR-061)
**Có người bấm nút thì có trình soạn; máy tự gửi thì không.** Bấm gửi → mở trình soạn đã điền đầy đủ (không còn placeholder) → sửa → gửi. Việc gửi nằm trong **chính lệnh nghiệp vụ**, nên bấm Huỷ = không chốt chỗ, không đổi trạng thái, không thư nào đi. Lọc HTML ở server; `email_logs` lưu đúng bản đã gửi → tab "Lịch sử email".

### Phase 7 – Candidate Portal
Candidate đăng nhập bằng magic link → xem recording, transcript, Evaluation Report (phần HR cho phép share), feedback.

---

## CV-JD Match Analysis (Gemini AI — ADR-070)

- **Chỉ chấm khi tin có bộ tiêu chí chấm CV** (HM khai trên phiếu, sửa ở màn tin). Không có → không gọi AI
- **Input:** CV (PDF/DOCX) + file JD gốc + nội dung tin + bộ tiêu chí kèm mức neo. Dự phòng GPT-4o-mini (rag-service) cũng nhận file
- **AI chọn dải + trả lời có/không từng ý kiểm** (trích CV); **backend tính điểm trong dải** rồi cộng có trọng số ra `matchScore` (ADR-071)
- **Reuse:** `cv_jd_analyses` theo khoá (tin, MD5 CV, phiên bản bộ tiêu chí). Lưu bộ mới → hàng đợi tự chấm lại, điểm cũ giữ làm lịch sử
- HM/Recruiter/HR xem **phép tính + bằng chứng** từng tiêu chí ở màn hồ sơ. Điểm chỉ để tham khảo — ứng viên luôn ứng tuyển được
---

## Tech Stack

> Danh sách phiên bản và thư viện: đọc thẳng manifest — `ari-web/package.json`,
> `ari-service/src/ARI.API/ARI.API.csproj`, `rag-service/pyproject.toml`, `docker/docker-compose.yml`.
> Bảng chép tay ở đây đã bị bỏ vì manifest luôn đúng hơn.
>
> **Các lựa chọn stack là quyết định, không phải dữ kiện** — chúng nằm ở `## Quy tắc bắt buộc`
> (không dùng Supabase SDK, không dùng Node.js cho backend, Streaming-First) và ở bảng ADR bên dưới:
> ADR-001 (backend .NET 8) · ADR-004 (GPT-4o + RAG) · ADR-005/043 (Deepgram · ElevenLabs)
> · ADR-006 (trần độ trễ 0.8–1.2s) · ADR-039 (rag-service) · ADR-046 (monorepo `ari-web/`)
> · ADR-055 (Postgres tự host).

---


## Coding Rules

> **Backend** (C# / ASP.NET Core .NET 8): quy ước chi tiết ở [ari-service/CLAUDE.md](ari-service/CLAUDE.md) — tự nạp khi làm trong `ari-service/`.
> **Frontend** (React + TypeScript, npm workspaces): quy ước chi tiết ở [ari-web/CLAUDE.md](ari-web/CLAUDE.md) — tự nạp khi làm trong `ari-web/`.

### Database (PostgreSQL + EF Core)

- Table: `snake_case` số nhiều | Column: `snake_case`
- Mọi thay đổi schema qua EF Core Migration – không sửa DB trực tiếp
- Luôn có `created_at`, `updated_at` trên mọi entity chính
- Soft delete (`deleted_at` nullable) thay vì hard delete
- **Single-tenant:** Không cần `organization_id` ở bất kỳ đâu

### Git

**Branch naming:**

| Prefix | Mục đích |
|---|---|
| `main` | Production |
| `develop` | Integration branch |
| `setup/<scope>` | Khởi tạo boilerplate giai đoạn đầu |
| `feature/<scope>/<tên>` | Tính năng mới |
| `fix/<scope>/<mô-tả>` | Bug fix |
| `chore/<scope>/<mô-tả>` | Config, CI, dependency |
| `docs/<mô-tả>` | Tài liệu thuần |

> **Scope:** `be` | `fe` | `docker` | `infra` | `db` | `ai`

**Commit format:** `<type>(<scope>): <mô tả ngắn>`  
Type: `feat` | `fix` | `refactor` | `docs` | `test` | `chore` | `setup`

---

## Quy tắc bắt buộc

1. Không tự ý thay đổi tech stack khi chưa được user xác nhận
2. Không hardcode secrets – luôn dùng environment variables
3. Không dùng Supabase SDK – kết nối PostgreSQL trực tiếp qua connection string. **Production dùng Postgres tự host** (ADR-055); Supabase chỉ dành cho môi trường test. Chuỗi kết nối tới container nội bộ phải để `SSL Mode=Disable` (container không phục vụ certificate)
4. Không đề xuất Node.js cho backend
5. Mọi thay đổi kiến trúc → cập nhật `.ai/architecture.md`
6. Trước khi bắt đầu task mới → kiểm tra `.ai/tasks.md`
7. Khi có quyết định mới → ghi lại ngay vào file `.ai/` tương ứng
8. Business logic không gọi trực tiếp OpenAI SDK – qua `IAIProvider` + `IEmbeddingProvider`
9. WebRTC chỉ dùng cho media stream. Session events dùng SignalR
10. Streaming-First – không chấp nhận batch nếu có alternative streaming khả thi
11. Single-tenant – không dùng `organization_id`, không thiết kế multi-tenant
12. Interview Code: one-time-use, TTL ngắn (mặc định 2 giờ), 6 ký tự alphanumeric, vô hiệu hóa ngay sau khi dùng
13. Language detection: AI detect từ JD – không hardcode mapping ngôn ngữ
14. Connection Drop Recovery: session duy trì active khi mất kết nối, resume khi nhập lại code (track qua `must_ask_tracking`)
15. OAuth2 Email Domain validation: bắt buộc xác thực domain thuộc `allowed_email_domains` khi dùng Google Sign-In
16. CV-JD Analysis (Gemini): chạy 1 lần per CV + Job Posting + phiên bản bộ tiêu chí, đính kèm vào Application – không phân tích lại
17. JD File Upload: hỗ trợ PDF/DOCX – AI chấm CV nhận file JD gốc (PDF nguyên, DOCX → text) kèm nội dung tin
18. Gemini AI: dùng cho CV-JD Match Analysis, trích xuất JD để auto-fill form tạo tin (ADR-042) **và** gợi ý bộ tiêu chí chấm CV (ADR-070). Phỏng vấn AI và RAG pipeline vẫn dùng OpenAI GPT-4o
19. Phạm vi dữ liệu của nhân sự do **SERVER** quyết định (`JobAccess.ScopedJobIdsAsync`), không bao giờ do tham số client khai (`?mine`) – ADR-061
20. Người chốt **kết quả phỏng vấn** là **Hiring Manager của tin**; admin chốt thay phải ghi lý do + audit log + báo cho HM. Recruiter không bao giờ chốt được. Nhưng người chốt **thư mời nhận việc** là **HR Leader** — HM chỉ đề xuất (ADR-063, policy `OfferApproval` tách khỏi `HiringDecision`)
21. Mọi thư **gửi ứng viên do người bấm nút** đều phải qua trình soạn thảo (điền sẵn, sửa được) và gửi **kèm chính lệnh nghiệp vụ** – không tách thành "lưu nháp rồi gửi"
22. Mọi tin tuyển dụng phải bắt nguồn từ một **Recruitment Request đã duyệt** (`job_postings.recruitment_request_id`); người dựng tin phải đúng Recruiter được phân công; **một phiếu chỉ sinh một tin** (unique index có filter)
23. **Không ai duyệt phiếu do chính mình lập** – chặn theo NGƯỜI, không theo vai trò (kể cả Super Admin). Từ chối phiếu bắt buộc kèm lý do ≥10 ký tự
24. Bảng mới phải được phân loại trong `DbChangeRouter` (định tuyến hoặc `IntentionallyUnrouted`) **và** migration phải gọi `SELECT arisp_attach_change_triggers();` – EF không tự sinh
25. **[BẮT BUỘC] Cập nhật `.ai/tasks.md` sau MỌI task hoàn thành:**
    - Đánh dấu task `[x]` + ghi ngày hoàn thành (format `YYYY-MM-DD`)
    - Nếu công việc vừa làm KHÔNG có trong `tasks.md` → tự động bổ sung vào phần Backlog tương ứng rồi đánh dấu `[x]` luôn
    - Thêm entry vào phần `## Completed` với mô tả ngắn về những gì đã làm
    - Commit `tasks.md` CÙNG VỚI code trong cùng 1 commit – không tách riêng
    - **Không được kết thúc task nếu chưa cập nhật `tasks.md`**
26. **Đội/bộ phận là dữ liệu TỔ CHỨC, không phải trục phân quyền** (ADR-065 — ADR-061 giữ nguyên): `JobAccess` không bao giờ đọc `DepartmentId`. Đội trên phiếu lấy **cứng từ tài khoản** người lập, bỏ qua giá trị client gửi lên; chỉ Super Admin gán được đội cho tài khoản — **không thêm department vào lệnh sửa hồ sơ cá nhân**
27. **Mọi tin luôn có đúng một HM chính; thiếu HM (hoặc HM bị khoá) thì cổng ĐÓNG, không bao giờ mở** (ADR-068) — kiểm qua `JobAccess.RequireActiveHiringManagerAsync`, không viết nhánh "không HM" mới. Chỉ HR Leader/SA gán/chuyển HM chính (lý do ≥10); không gỡ được HM chính; khoá tài khoản HM không bị chặn mà báo HR chuyển HM
28. **Không chấm CV khi tin chưa có bộ tiêu chí chấm CV** (ADR-070) — không có nhánh "AI tự cho điểm"; điểm cuối luôn do backend cộng có trọng số. Mọi cửa ghi bộ tiêu chí đi qua `CvRubricService.SaveForJobAsync`; tin thiếu bộ tiêu chí không sang `pending`/`active` (kể cả admin vượt cổng)
29. **Buổi phỏng vấn chỉ ra báo cáo khi vòng của nó có bộ tiêu chí chấm phỏng vấn của TIN** (ADR-073) — vòng → tin, không lùi về bộ công ty; mọi cửa ghi đi qua `InterviewRubricService.SaveAsync`; tin thiếu bộ không lên `active`. Báo cáo chỉ sinh ở hàng đợi nền (`InterviewEvaluator`), mọi kết cục ghi vào `interview_sessions.evaluation_status` — không được gọi AI chấm trong lệnh đóng phiên

---

## Trạng thái Tasks

> **Nguồn duy nhất: [.ai/tasks.md](.ai/tasks.md)** — phase hiện tại, việc đang làm, backlog và mục Completed.
>
> **Không chép trạng thái vào file này.** Bản chép tay trước đây đứng yên ở *"Phase 1–2, đang tiến vào
> Phase 2a–3"* với ngày cập nhật 2026-08-05, trong khi dự án đã chạy tới ADR-061 — bảng backlog còn
> liệt kê Kiosk, media pipeline, Portal và đánh giá AI là "chưa bắt đầu" dù cả bốn đã xong. Một bản đồ
> sai tệ hơn không có bản đồ.

---


## Key Architecture Decisions (Tóm tắt)

| ADR | Quyết định |
|---|---|
| ADR-001 | Backend: ASP.NET Core .NET 8 (không dùng Node.js) |
| ADR-002 | DB: PostgreSQL kết nối trực tiếp, không dùng Supabase SDK. *(Phần "on Supabase" đã bị ADR-055 thay thế)* |
| ADR-003 | SignalR cho session events; WebRTC cho media stream (không trộn lẫn) |
| ADR-004 | AI/LLM: OpenAI GPT-4o + RAG pgvector, abstract qua `IAIProvider` + `IEmbeddingProvider` |
| ADR-005 | STT: **Deepgram Nova-3** (gộp VAD/endpointing, thay Google STT); TTS: ElevenLabs Flash v2.5. *(Phần avatar HeyGen đã bị ADR-067 gỡ khỏi dự án.)* |
| ADR-006 | Streaming-First latency target: **~0.8–1.2s** (Deepgram STT+VAD → Hybrid RAG 0ms → LLM 400–800ms → TTS 75–150ms); đòn bẩy: partial-STT→RAG song song, TTS first-sentence, prompt caching, tắt thinking, gọi thẳng OpenAI |
| ADR-011 | *(HẾT HIỆU LỰC — ADR-067 gỡ avatar khỏi dự án.)* |
| ADR-012 | Single-tenant: xoá `organizations`, `subscriptions`, không dùng `organization_id` |
| ADR-015 | Practice (Remote) mở qua **Portal** sau khi pass CV + đặt lịch buổi thật của vòng (1 lượt/vòng, không cần code); Real (On-site) qua Interview Code tại Kiosk |
| ADR-016 | Interview Code: **chỉ cho phỏng vấn thật/Kiosk** (đã bỏ `code_type`); 6 ký tự alphanumeric, one-time-use, TTL 2h, bind `application_id` + vòng. **Một mã sống/vòng**; cấp ngay trên danh sách ứng viên — chuyên môn đưa tận tay tại văn phòng (không hiện Portal), sơ loại gửi link Kiosk kèm mã (làm từ nhà) |
| ADR-038 | Tối ưu chi phí Practice: gating theo phễu (pass CV + đã đặt lịch buổi thật của vòng mới mở thử, 1 lượt/vòng), giữ đủ tech, không quay video practice |
| ADR-039 | RAG microservice Python (`rag-service/`, FastAPI+LangChain+LangGraph) sở hữu **toàn bộ** chunk/embed/hybrid-retrieve/sinh câu hỏi+đánh giá. .NET gọi qua HTTP/SSE (`RagServiceProvider` + `IRagIngestionService`). **Không còn fallback in-process** — `OpenAIProvider` và cờ `AI:Provider` đã bị gỡ hẳn ở ADR-062. pgvector trên Postgres (schema do EF sở hữu). **Giai đoạn 1 Hybrid RAG đã triển khai**; CRAG/Agentic còn backlog |
| ADR-040 | Cổng kiểm tra mic + cam bắt buộc trước mọi phỏng vấn (thử & thật) — component `DeviceCheck` |
| ADR-043 | Media stack phỏng vấn chốt: **Cascaded** (~0.8–1.2s, không speech-to-speech) — Deepgram Nova-3 (STT+VAD) + ElevenLabs Flash v2.5 + Hybrid RAG + **GPT-4o (Claude là option dành sau)**. TTFT là yếu tố chính; Haiku 4.5 nhanh nhất nếu cần giảm trễ |
| ADR-044 | Nối media thực tế: **client-SDK + BE mint token** (`/session/{id}/media-config`). Deepgram live (FE) + ElevenLabs TTS phát qua WebAudio (avatar đã gỡ — ADR-067). Fallback mềm khi thiếu key. BE giữ key, không relay media |
| ADR-018 | Language-aware AI: detect từ JD, điều chỉnh system prompt + TTS voice + STT languageCode |
| ADR-023 | Auth nội bộ: Email + Password (chính) + Google OAuth2 optional; pre-provisioning + domain validation |
| ADR-025 | **Playbook**: 3 scope `org`/`job_posting`/`round`, 10 loại tài liệu. **Phạm vi đọc từ bảng `playbook_documents`, không phải metadata chunk** — `org` áp mọi tin, `job_posting`/`round` phải khớp tin (và vòng); `deleted_at` loại ngay. **Xoá playbook = gỡ chunk trước, soft-delete sau** (gỡ lỗi thì không xoá mềm) — dùng lại `IngestAsync(text:"")`. **Loại tài liệu quyết định CÁCH dùng**: `compliance` là ràng buộc CẤM hỏi, `expected_answer` cấm đọc cho ứng viên nghe, `must_ask` chặn kết thúc phiên. Trọng số hybrid chỉ còn việc xếp hạng (org 0.6, job/round 1.0) |
| ADR-030 | Gemini 2.5 Flash cho CV-JD Analysis; GPT-4o cho phỏng vấn AI và RAG |
| ADR-036 | File storage abstraction `IFileStorageService`: Local (dev) / Cloudflare R2 (prod, presigned URL); DB lưu storageKey |
| ADR-041 | Vòng đời tài khoản staff: yêu cầu tạo (HR→SA duyệt) tách khỏi khóa/mở khóa (`AccountRequest` + `User.LockReason`) |
| ADR-042 | Recruiter workspace cụm Job: `mine` filter, ứng viên theo job, Gemini trích xuất JD auto-fill (mở rộng ADR-030) |
| ADR-045 | Refactor Clean Architecture chuẩn JT template: `ari-service/` (src/+tests/), namespace `ARI.*`, CQRS + MediatR **pin [12.5.0]** (v13 commercial), FluentValidation, thin controllers, DI per-project, schema 100% migrations. SessionHub gọi thẳng `IInterviewService` (latency ADR-006) |
| ADR-046 | Refactor FE mirror ADR-045: `frontend/` → `ari-web/` (npm workspaces), tách **ARI.CandidateSite** (public, 3000) + **ARI.StaffSite** (nội bộ, 3001) + **ARI.Shared**. `services/`→`fservices/` (quy tắc "f"). Import Shared qua `@ari/shared/*`. Nginx host-based 2 origin; `configureApiClient` refresh riêng mỗi site (candidate = `/auth/candidate/refresh`) |
| ADR-047 | CI/CD GitHub Actions: build 4 image ở runner → GHCR → VPS chỉ `pull && up -d` (không build trên VPS 3.8GB RAM). **`main` = production** (deploy tự động), `develop` = integration. `ci.yml` chặn PR không build được. `ports: !reset []` (không phải `ports: []`) mới thực sự đóng cổng. `VITE_API_BASE_URL=/api` tương đối → 1 image dùng mọi domain |
| ADR-055 | **Production DB bỏ Supabase — Postgres tự host trên VPS** (Supabase còn là môi trường test). **Container** `pgvector/pgvector:pg17`, **bind mount** `/var/lib/arisp/pgdata` (`down -v` không xoá được dữ liệu). **`ports: !override ["127.0.0.1:5432:5432"]`** — `ports:` thường *nối thêm* vào bind `0.0.0.0` của base, `!reset []` thì SSH tunnel không tới. Truy cập qua **SSH tunnel** (`docs/postgres-production-setup.md`). Chuỗi nội bộ **`SSL Mode=Disable`**. Mật khẩu viết **3 chỗ** (`POSTGRES_PASSWORD` + chuỗi .NET + `DATABASE_PASSWORD`). **Backup bắt buộc** (`scripts/backup-db.sh`, cron 03:00) |
| ADR-048 | **HR gán cứng lịch phỏng vấn cho ứng viên** (đảo phần đặt lịch của ADR-015): staff chọn 1 slot trong kho ấn định cho ứng viên qua `POST /api/schedules/assign`; **bỏ hẳn** ứng viên tự chọn (gỡ `GET /schedule/{id}/slots` + `POST /schedule/{id}/book` + `AuthorizeCandidateAsync`). Giữ nguyên side-effects booking (chốt chỗ nguyên tử, `screening→interview`, gating Interview Code). Ứng viên nhận realtime `InterviewScheduled` + bell + email giờ hẹn. **Xác nhận/báo bận (2026-07-25):** `InterviewBooking` thêm `confirmation_status`/`decline_reason`/`responded_at` (migration `AddBookingConfirmation`); ứng viên `POST /api/candidate/schedule/{bookingId}/confirm|decline` — decline (có lý do) set `Status="declined"` + trả chỗ slot để staff gán lại qua chính `AssignSlotCommand` (không cần luồng huỷ riêng). Nhân sự nhận realtime `ReceiveScheduleResponse` + bell. UI gán: `@ari/shared/ui/AssignSchedulePanel` |
| ADR-049 | **Online Test (thi trắc nghiệm)**. Ngân hàng câu hỏi **theo JOB** (`OnlineTestQuestion.JobPostingId`); điểm sàn `OnlineTestPassScore` trên `JobPosting` (mặc định 70). Tự chấm `score = correct/total*100`; `CorrectOption` **không rời BE**. 1 lượt/vòng qua unique `(application_id, round_number)`. Auto-progression **mềm**: realtime + notification idempotent, không tự đổi status. Bốc N câu ngẫu nhiên/lượt (deterministic theo hồ sơ+vòng), import/export Excel. Khung giờ thi: ADR-072 |
| ADR-054 | **Khoá màn hình Kiosk + ghi log rời phòng**: nhập đúng mã → `requestFullscreen()` ngay trong cú click; rời toàn màn hình → **lớp phủ chặn** cả phòng cho tới khi bấm quay lại. Hook chung `useKioskLockdown` ghi nhận `fullscreen_exit`/`tab_hidden`/`window_blur`/`shortcut_blocked`/`page_unload` → `POST /interview/session/{id}/signals` (lúc đóng trang dùng `fetch keepalive`). `RecordCheatSignalAsync` lưu `CheatDetectionSignal` thật; `CheatScore` tính theo trọng số từng loại. **Web không chặn được Alt+Tab/phím Windows** — khoá cứng cần `chrome --kiosk` + Windows Assigned Access |
| ADR-053 | **"Đạt" chỉ khi qua HẾT vòng**: AI **không bao giờ** ghi `Application.Status` (bỏ hẳn ở `GenerateEvaluationReportAsync` — trước đây AI chấm trượt là đánh rớt hồ sơ trước khi HR xem); `SubmitHrReviewAsync` tính trạng thái 1 lần theo `ResolveTotalRoundsAsync` = `max(InterviewRoundConfig.RoundNumber)`: không đạt→`not_pass`, đạt & còn vòng→`interview`, **đạt & vòng cuối→`pass`**. Portal trả `TotalRounds`/`PassedRounds` → thẻ hồ sơ hiện **"Qua vòng N/M"**. Sửa kèm: điểm từng câu `null` thay vì 0 (hết "0/100 · Cần cải thiện" sai), **ghim bộ khoá tiêu chí** trong prompt. Dev: `POST /api/dev/seed-interview-job` (job 3 vòng + trắc nghiệm + mã Kiosk + tài khoản HR), `POST /api/dev/regrade-session/{id}?lang=` |
| ADR-052 | **Kiosk phỏng vấn THẬT**: `validate-code` trả `sessionId` + **JWT role `Kiosk_session`** (claim `session_id`, TTL 3h) thay cho đăng nhập; policy `InterviewParticipant` + kiểm khớp `session_id` ở controller & `SessionHub`. Kiosk **quay video** cam+mic → storage, hạn `Interview:RecordingRetentionDays` (**7 ngày**); hosted service quét 12h/lần xoá file quá hạn, giữ transcript + đánh giá. Trần buổi thật `RealMaxDurationMinutes` (**20'**). Hook chung `useInterviewSession`. UX: device check → đếm ngược → màn kết thúc tự reset 30s, khôi phục phiên khi reload |
| ADR-051 | **Buổi thử = không gian riêng của ứng viên**: transcript + nhận xét AI lưu vĩnh viễn, chỉ chủ nhân xem lại; **Ẩn hoàn toàn khỏi HR/Recruiter** (lọc `SessionType != "practice"`), chỉ giữ cờ `PracticeSessionUsed`. **Không hiện verdict Pass/Not Pass** — chỉ điểm/tiêu chí/phân tích câu, kèm nhãn tham khảo. Buổi thử **không chạm pipeline thật** (sửa bug practice đánh rớt hồ sơ). **Báo cáo AI 1 ngôn ngữ** (`ReportLanguage`); **phân tích từng câu có schema cứng** + ghép vào đúng lượt hỏi–đáp từ DB; **đánh giá ngôn ngữ chấm trên chính câu trả lời** (kèm CEFR + dẫn chứng) |
| ADR-050 | Practice **audio-only** (bỏ avatar; giữ đủ STT/RAG/GPT-4o/ElevenLabs qua WebAudio — ADR-067 sau đó mở rộng cho cả buổi thật). **Trần 20 phút** (`Interview:PracticeMaxDurationMinutes`) → hết giờ khoá mic + AI câu kết + đóng phiên (**2 lớp enforce** FE + server, idempotent). **Nhập kép** voice+keyboard: transcript vào 1 `answerText` sửa/gõ tay được. Huỷ ghi âm+xoá 7 ngày (giữ ADR-038 đ.6). SỬA ADR-038 đ.3-4, HIỆN THỰC đ.5. **Dev test:** `POST /api/dev/seed-practice` (chỉ Development) tạo sẵn hồ sơ đủ điều kiện + `Interview:PracticeAttemptsPerRound=0` để lặp lại. *(Ban đầu ADR-048; đổi 050 do trùng số khi merge — 048=lịch, 049=online-test.)* |
| ADR-057 | **Realtime ở TẦNG DATABASE — trigger + `LISTEN/NOTIFY`** thay cho mô hình "command tự nhớ push" (~40 điểm gọi rải rác, quên là hỏng im lặng). Một hàm `arisp_notify_change()` dùng chung; gắn bằng `arisp_attach_change_triggers()` quét `pg_class` → **bảng mới chỉ cần gọi lại hàm** (quy tắc 24). Payload **chỉ chứa khoá**. `DbChangeListenerHostedService` giữ connection **riêng `Pooling=false`+`Multiplexing=false`** (connection từ pool bị trả về là **mất LISTEN im lặng**), tự nối lại, mỗi lần (re)connect phát `op="resync"`. `DbChangeRouter` là **chốt chặn bảo mật mặc định ĐÓNG**: bảng chưa map thì không gửi cho ai. NOTIFY **transactional** — rollback không sinh event giả |
| ADR-058 | **`booked_count` = số booking `status='scheduled'`** — vị từ chiếm chỗ DUY NHẤT, giống nhau ở mọi nơi đọc. Số hiển thị **suy từ dòng booking** (không thể trôi), cột giữ vai trò khoá tương tranh; lệch thì `LogWarning`, KHÔNG tự chữa. Cột mới `interview_bookings.declined_by` để phân biệt "báo bận" với "hệ thống huỷ". **Dời lịch**: bắt buộc cùng tin (trước chỉ so vòng, mà vòng 1 có ở MỌI tin), chiếm chỗ nguyên tử, được-ăn-cả-ngã-về-không. Phủ `CanManageAsync` cho cả 5 endpoint `management/*` |
| ADR-059 | **Thư mời chỉ rời hệ thống khi đã có giờ hẹn** — trước đó duyệt xong chưa gán ca thì không thư nào đi, ứng viên chỉ thấy chuông trong Portal. *(Cách hợp nhất "duyệt CV kèm `slotId`" đã bị **ADR-067** thay: duyệt đi qua Hiring Manager trước, xếp lịch là bước riêng — nhưng bất biến "thư đi cùng giờ hẹn" giữ nguyên.)* Thư mời gom về `InterviewInviteEmail`; **địa điểm là `SystemSetting`**, chưa cấu hình thì bỏ hẳn dòng. **KHÔNG mở phỏng vấn thử** cho vòng trắc nghiệm (lộ đề) và vòng **đã lỡ buổi thật** — chặn 3 tầng. Tin có vòng trắc nghiệm không rời được nháp khi ngân hàng đề thiếu câu. **Gỡ hẳn đường "mời không kèm lịch"**. **Phớt lờ thư mời ≠ báo bận**: bỏ auto-huỷ 48h; dời lịch chỉ cho người báo bận; người im lặng bị nhắc 24h/3h (`In-Reply-To`) rồi **quá giờ + ân hạn 2h không có phiên thật → tự đánh trượt** (`no_show`); vòng trắc nghiệm thì hết hạn = **hệ thống tự nộp bài trống** |
| ADR-056 | **Khôi phục khoá ngoại + UNIQUE + index vận hành vào migration EF.** ADR-055 dựng production từ số 0 bằng migration nên **mất im lặng** cả lớp schema vốn được áp TAY bằng SQL lên Supabase (29→1 FK, 44→36 UNIQUE, 28→0 index, mất index vector). Nghiêm trọng nhất là 8 UNIQUE — production đang **cho phép trùng email tài khoản và trùng mã Kiosk 6 ký tự**. Nay khai hết trong `OnModelCreating` (`ConfigureRelationships` + `ConfigureOperationalIndexes`) → migration `RestoreForeignKeysIndexesAndUniqueConstraints`: 38 FK + 43 index, **0 thao tác chạm cột/dữ liệu**, `Down()` đối xứng. Quan hệ khai **không dùng navigation property** (`HasOne<T>().WithMany().HasForeignKey`) nên không dòng query nào phải đổi. **ivfflat → HNSW** (ivfflat học phân cụm lúc tạo, DB trắng sẽ ra index rác). **5 cột cố ý không đặt FK**: 3 cột đa hình + `account_requests.batch_id` (không có bảng đích) + `questions.playbook_chunk_id` (rag-service xoá cứng chunk mỗi lần nạp lại → FK sẽ chặn ingest) |
| ADR-060 | **Chấm điểm theo bộ tiêu chí doanh nghiệp**: AI chỉ chấm TẮNG tiêu chí, **backend cộng trung bình có trọng số** rồi so `JobPosting.InterviewPassScore` (mặc định 70) ra verdict — trước đây prompt hỏi luôn `score` tổng, con số đó không phải trung bình có trọng số của gì cả. Rubric khai bằng **file Excel theo mẫu**, là 2 loại tài liệu playbook `cv_rubric`/`interview_rubric`; chọn theo **vòng → tin → công ty**. **Tổng trọng số ≠ 100 chặn ngay tại cổng upload**. Điểm lưu dạng **ảnh chụp** chứ không trỏ rubric sống. **Tiêu chí AI quên chấm bị loại khỏi CẢ tử lẫn mẫu** (tính 0 là đánh trượt oan vì lỗi model). **Chưa khai rubric → giữ nguyên hành vi cũ** *(riêng CV: ADR-070 bỏ nhánh này)* |

| ADR-061 | **Vai trò Hiring Manager theo đội tuyển dụng của từng tin; ba cổng quyết định; thư gửi ứng viên sửa được; phễu khép kín tới `hired`.** Phạm vi theo `job_hiring_team_members` **không phải phòng ban**. `JobAccess` là helper tầng Application **không phải policy**, trả **mức** `None<TeamMember<Owner<Admin`. Duyệt của HM là **CỘT** (`hm_decision`, `hm_sign_off_status`) không phải status mới. **DÙNG LẠI `HrReview`** cho verdict của HM; `reviewer_role` là **ảnh chụp**. Cổng **mềm**: admin vượt được nhưng lý do ≥10 ký tự + audit log + **luôn báo HM bị vượt**. Thư sửa được = **`emailOverride` truyền vào command sẵn có**; **sanitize ở server** (Ganss.Xss). **Một offer sống/hồ sơ** chặn ở DB; Portal dùng **DTO riêng** `CandidateOfferDto`. Danh tính/phạm vi do SERVER quyết định (bỏ `?mine`, header client). Test chốt chặn "mọi entity phải được phân loại định tuyến" (bẫy trigger ADR-057) |

| ADR-063 | **Phiếu yêu cầu tuyển dụng của HM; tách cổng đề xuất khỏi cổng chốt lương.** Bảng `recruitment_requests`: HM lập + đề xuất dải lương → HR Leader **duyệt kèm phân công Recruiter trong một thao tác** hoặc trả lại kèm lý do (`rejected` là một vòng sửa–gửi lại). **Mọi tin từ phiếu đã duyệt**; **người lập phiếu tự thành HM của tin**; **chữ ký JD của HM là cổng đăng tin** (gọi lại `UpdateJobStatusCommand`). Policy **`OfferApproval`** (HR Leader + SA) chốt thư mời. **Không ai duyệt phiếu của chính mình** (chặn theo NGƯỜI). Liên kết phiếu↔tin **một chiều**; payload realtime thêm `assigned_recruiter_id` |

| ADR-064 | **Trình soạn JD theo mẫu công ty; HM duyệt bằng chính file JD.** Thêm bước **soạn JD** giữa phiếu và tin. Mẫu JD là **cấu hình có cấu trúc** (logo, thông tin công ty, màu/phông, danh sách mục) do HR Leader sở hữu; **`key` của mục bất biến**. **Một bố cục `JdLayout`, hai bộ xuất** DOCX/PDF (OpenXML + PdfSharpCore, không thêm thư viện). **Lưu nội dung JD** để bị trả về không phải soạn lại; file tự đính kèm sang màn tạo tin. Logo là phần duy nhất được hỏng lặng lẽ; định dạng số khai tường minh (không tra `CultureInfo`) |
| ADR-065 | **Định danh đội/bộ phận của nhân sự; hoàn thiện phiếu.** Bảng `departments`; `User.Department` chuỗi **bỏ hẳn** (nhân viên từng tự sửa được). **KHÔNG lật ADR-061**: `JobAccess` không đọc `DepartmentId` — đây là toàn vẹn dữ liệu, không phải phân quyền. Đội trên phiếu lấy **cứng từ tài khoản**; chỉ Super Admin gán đội; đội giải thể thì **tắt, không xoá**. "Thoả thuận" **suy ra** từ hai ô lương trống. Mức độ ưu tiên `high→medium→low` sắp ở SQL. **Không backfill** text cũ (migration `RAISE NOTICE`) |
| ADR-066 | **Thu hồi phê duyệt phiếu — mở lại để sửa, hoặc đóng phiếu.** Hai thao tác một bản chất (huỷ hiệu lực chữ ký): dùng chung hàm kiểm + cờ `CanRevoke`; **không thêm status** (`cancelled` = phiếu hết hiệu lực). **Chặn cứng khi phiếu đã dựng thành tin** (TIN là nguồn sự thật, suy bằng LEFT JOIN). Mở lại thì gỡ phân công; đóng thì giữ dấu vết. Lý do ≥10 ký tự vào **cột riêng** `RevokedReason`/`RevokedByUserId` (không ghi đè `Review*`). Chủ phiếu hoặc admin; Recruiter không. **Chốt người nhận báo TRƯỚC khi sửa phiếu** |
| ADR-067 | **Phễu duyệt hồ sơ khép kín quanh Hiring Manager; một ca một ứng viên; gỡ Live Avatar.** Recruiter *Duyệt hồ sơ* → `hm_review` (**không báo ứng viên**) → HM duyệt → `screening` = "Chờ xếp lịch" → xếp ca **nằm trọn** trong lịch HM rảnh → **lúc này** thư "qua vòng" mới gửi. Lịch rảnh **khai riêng ở màn tin** (bảng `hiring_manager_availabilities`, tin × vòng). **Ba luật xếp lịch** trong MỘT hàm `ValidateAssignmentAsync`: một ca một ứng viên (theo **dòng booking**; vòng trắc nghiệm miễn, `capacity NULL` = không giới hạn) · ứng viên không dự hai buổi **chồng giờ** · ca **trọn** trong khung HM. **Phòng chờ**: phiên `real` sinh ở **`waiting`**, HM *vào phòng* rồi *cho vào* mới `active` (chốt ở trạng thái phiên); `StartedAt` = lúc cho vào. Admin mở cửa thay có audit `interview_admitted_without_hm`. Chốt giờ với ứng viên (SMS/Zalo) **cố ý ngoài hệ thống**. **Gỡ hẳn HeyGen LiveAvatar** |
| ADR-068 | **Mọi tin luôn có đúng một HM chính — thiếu HM (hoặc HM bị khoá) thì cổng ĐÓNG** (code cũ mở mọi cổng: gỡ HM là bỏ qua cả ba cổng). Chỉ HR Leader/SA gán/chuyển HM (`PUT /jobs/{id}/hiring-manager`, lý do ≥10); khoá tài khoản HM không bị chặn mà báo HR. **HM yêu cầu sửa JD → tin về `rejected`**; **ký + đăng nguyên tử**; vượt cổng ghi `bypassed`. **HM chính soạn + gửi duyệt thư mời**, gửi duyệt báo HR Leader. Link thông báo theo vai người nhận (`StaffLinks`). Migration `BackfillPrimaryHiringManagers` |
| ADR-069 | **Đường vào báo cáo AI sau buổi phỏng vấn thật:** danh sách **theo vòng** suy từ dữ liệu (ca · `evaluating` khi AI còn chấm · báo cáo · video · transcript) ở màn tin + màn hồ sơ của cả ba vai và mục "Buổi vừa kết thúc" của HM; màn đánh giá có **transcript đầy đủ** + ca. **Playbook theo tin do HM chính quản lý ngay trong màn tin** (`api/jobs/{id}/playbooks`), HR giữ playbook công ty; luật trong `PlaybookAccess` |
| ADR-070 | **Bộ tiêu chí chấm CV bắt buộc; điểm CV giải thích được.** HM khai trên **phiếu** (trình soạn web + AI gợi ý + mẫu công ty + Excel, mức neo 4 dải), chép sang tin khi dựng tin, sửa ở màn tin → **chấm lại mọi hồ sơ** (hàng đợi tự lành, điểm cũ giữ làm lịch sử). **Không bộ tiêu chí → không gọi AI, không sang `pending`/`active`**; tin cũ vẫn nhận hồ sơ, hoãn chấm, nhắc HM. Khoá dùng lại `(tin, MD5 CV, phiên bản bộ tiêu chí)` UNIQUE; `failed`→`invalid_cv`. AI nhận file JD gốc, chấm từng tiêu chí kèm bằng chứng; dự phòng GPT-4o-mini đọc được PDF. Màn hồ sơ HM/Recruiter/HR hiện **phép tính + bằng chứng**. Gỡ `CvAnalysisController` (endpoint ẩn danh, thiếu phạm vi) |
| ADR-071 | **Điểm TRONG dải do ý kiểm quyết định.** Mỗi tiêu chí có ≤ 8 ý kiểm có/không; AI chọn dải + trả lời từng ý kèm trích dẫn, backend tính `đáy + ý đạt/ý trả lời × độ rộng` (90–100, 2/4 → 95). Ý đạt không trích dẫn → không tính; ý bỏ sót → loại khỏi mẫu. Tiêu chí chưa có ý kiểm: AI ước lượng, kẹp vào dải. Màn hồ sơ in phép tính + ✓/✗ từng ý |
| ADR-072 | **Bài trắc nghiệm là đợt thi giờ cố định:** mở lúc giờ hẹn, **đóng lúc giờ hẹn + thời lượng**; vào muộn thì còn ít giờ (bỏ "cửa vào 1 tiếng"). Luật duy nhất `OnlineTestWindow`. **Thời lượng một nguồn** = `MaxDurationMinutes` của vòng `online_test` (bỏ `job_postings.online_test_duration_minutes`). Ca thi: giờ kết thúc = giờ đóng, server tự tính. Đồng hồ FE theo `ServerNow`. Không đổi thời lượng khi còn ca thi chưa đóng |
| ADR-073 | **Báo cáo phỏng vấn không bao giờ mất im lặng.** Bộ tiêu chí chấm phỏng vấn do **HM khai theo tin** (bộ chung + bộ riêng theo vòng, trình soạn CV dùng lại ở `mode="interview"`, không ý kiểm), **không lùi về bộ công ty** (bộ công ty = mẫu); tin thiếu bộ không lên `active`. Đóng phiên chỉ ghi `evaluation_status=pending` → hàng đợi + lượt quét 2' (`EvaluationHostedService`) chấm, thử lại tối đa 3 lượt, `blocked_no_rubric` tự chấm khi HM lưu bộ tiêu chí; buổi thật không có câu trả lời → báo cáo hệ thống cho HM chốt. Giao diện nói đúng lý do (`needs_rubric`, `evaluation_failed` + nút "Chấm lại") |

> Chi tiết đầy đủ từng ADR: xem [.ai/architecture.md](.ai/architecture.md)

---

## Glossary (Thuật ngữ chính)

> **Nguồn duy nhất: [.ai/glossary.md](.ai/glossary.md)** — định nghĩa đầy đủ mọi thuật ngữ nghiệp vụ
> — vai trò, cổng duyệt (Shortlist Approval · JD Sign-off · HR Bypass / HR Fallback), phễu
> (Offer · Hired · Email Composer), buổi phỏng vấn (Kiosk · Chế độ khoá màn hình · Cheat Signal ·
> CheatScore · Interview Code · Practice / Real Interview), và hạ tầng (Playbook · RAG · Magic Link).
>
> Bảng chép tay ở đây đã bỏ để file này không phải đồng bộ tay hai nơi. Các thuật ngữ mang **ràng buộc
> bắt buộc** (mã Kiosk 6 ký tự one-time-use, single-tenant không `organization_id`, một hồ sơ chỉ một
> thư mời còn hiệu lực…) vẫn được phát biểu thành luật ở `## Quy tắc bắt buộc` và trong bảng ADR.
