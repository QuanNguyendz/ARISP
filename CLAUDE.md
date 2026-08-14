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
| **Super Admin** | Quản trị hệ thống – cấu hình toàn cục (allowed domains, webhooks), quản lý tài khoản HR, theo dõi audit log |
| **HR Leader** | Trưởng nhóm HR – quản lý Job Posting, cấu hình phỏng vấn, upload Playbook, Confirm/Override kết quả AI |
| **Recruiter** | Chuyên viên tuyển dụng – tạo Job Posting nháp, quản lý ứng viên, cấp Interview Code On-site |
| **Candidate** | Ứng viên – tự ứng tuyển qua Job Board, làm phỏng vấn thử (Practice Remote) và phỏng vấn thật (Real On-site) |

---

## Auth Flow

### Cổng đăng nhập (Login Portals)
- **HR / Super Admin / Recruiter:** `/admin/login` → form **Email + Mật khẩu**. Hỗ trợ thêm **Google OAuth2** (Google Sign-In) cho người dùng nội bộ công ty.
- **Candidate (Job Board):** `/jobs/login` → form **Email + Mật khẩu** (tự đăng ký trước đó).
- **Candidate Portal (Xem kết quả/Lên lịch):** Passwordless – nhập email → nhận **Magic Link** → click đăng nhập.
- **Kiosk On-site:** Giao diện khóa (Kiosk Mode) tại văn phòng – chỉ hiển thị form nhập **Interview Code** (6 ký tự).

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
- **Audio-only (ADR-050): KHÔNG avatar** — giữ đủ STT/RAG/GPT-4o + **giọng ElevenLabs** (phát qua WebAudio), bot tĩnh. Bỏ avatar để tránh cạnh tranh concurrency LiveAvatar với buổi thật + đốt credit. **Không quay video — chỉ lưu transcript** + Evaluation Report
- **Trần 20 phút (ADR-050):** đồng hồ đếm ngược; hết giờ → khoá mic → AI nói 1 câu kết thúc → đóng phiên (`Interview:PracticeMaxDurationMinutes`)
- **Nhập kép (ADR-050):** thu âm điền vào ô trả lời, ứng viên **sửa/gõ tay** được (mic on/off) trước khi Gửi — sửa đoạn thu âm nghe sai
- Chi phí practice **do doanh nghiệp trả** (mỗi vòng = 1 lượt thử + 1 lượt thật). Tối ưu bằng gating theo phễu + bỏ avatar practice — xem ADR-038/048

### Real (Phỏng vấn thật – On-site)
- Bắt buộc tại văn phòng công ty, đến đúng khung giờ đã đặt lịch (Availability Slot của vòng)
- Candidate nhập **Interview Code** tại thiết bị Kiosk (code chỉ dùng cho real — không có `code_type`)
- Code: one-time-use, TTL mặc định 2 giờ, 6 ký tự alphanumeric, bind với `application_id` + vòng
- RAG dùng đầy đủ: JD + CV + Playbook nội bộ

---

## Recruitment Flow

### Phase 1 – Enterprise Setup
HR tạo Job Posting với: tên vị trí, JD (text + file PDF/DOCX), cấu hình vòng phỏng vấn (screening / technical / online_test), ngôn ngữ, availability slots (khung giờ phỏng vấn thật per vòng — đặt lịch xong mở phỏng vấn thử), scoring rubric, interview persona, playbook.

### Phase 2 – Candidate Application
1. Candidate xem Job Detail trên Job Board
2. *(Tùy chọn)* Upload CV → Gemini phân tích → hiển thị Match Score + Summary
3. Bấm "Ứng tuyển" → submit CV + thông tin → tạo Application kèm kết quả CV-JD Analysis
4. HR review CV + matchScore → gửi magic link cho ứng viên đã chọn → ứng viên vào Portal **đặt lịch buổi phỏng vấn thật của vòng** → mở 1 lượt phỏng vấn thử cho vòng đó

### Phase 3 – On-site Access
Candidate đến văn phòng **đúng khung giờ đã đặt lịch** → Recruiter cấp Interview Code (6 ký tự, chỉ cho real) → nhập tại Kiosk → vào phỏng vấn thật.

### Phase 4 – Multi-round AI Interview
- **Round 1 (Screening):** Language-aware – AI detect ngôn ngữ từ JD, phỏng vấn bằng ngôn ngữ đó, đánh giá cả nội dung lẫn language proficiency
- **Round 2+ (Technical):** Chỉ kích hoạt khi Pass Round trước. Chuyên sâu kỹ năng kỹ thuật
- **Auto-progression:** Pass Round N → HR cấp Interview Code mới cho Round N+1

### Phase 5 – AI Evaluation
Mỗi round tạo Evaluation Report: Verdict (Pass/Not Pass), Overall Score (0–100), per-criterion scores, language assessment (nếu áp dụng), per-question analysis, recommended next step.

### Phase 6 – HR Review & Confirm
HR Leader xem report + recording → **Confirm** hoặc **Override** (bắt buộc nhập `override_reason`). Sau confirm → gửi thông báo kết quả hoặc invite round tiếp.

### Phase 7 – Candidate Portal
Candidate đăng nhập bằng magic link → xem recording, transcript, Evaluation Report (phần HR cho phép share), feedback.

---

## CV-JD Match Analysis (Gemini AI)

- **Model:** Google Gemini 2.5 Flash
- **Input:** CV file (PDF/DOCX) + JD file gốc (PDF/DOCX) hoặc JD text
- **Output:** `matchScore` (0–100), `summary`, `skillsMatched`, `skillsGaps`, `experienceRelevance`, `overallRecommendation`
- **Reuse:** Kết quả lưu vào `cv_jd_analyses`, link `analysis_id` vào Application – HR nhận y hệt, không chạy lại Gemini
- **Auto-analysis:** Nếu candidate ứng tuyển mà chưa chạy analysis → hệ thống tự chạy 1 lần rồi đính kèm
- Kết quả chỉ mang tính tham khảo – candidate luôn có thể ứng tuyển dù điểm thấp

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React, TypeScript, TailwindCSS — monorepo `ari-web/` (npm workspaces): **ARI.CandidateSite** (public, 3000) + **ARI.StaffSite** (nội bộ, 3001) + **ARI.Shared** (ADR-046) |
| Backend | C#, ASP.NET Core .NET 8 |
| API Style | REST API + SignalR |
| Realtime Media | WebRTC |
| Database | PostgreSQL 17 + pgvector, **tự host trên VPS** (container `pgvector/pgvector:pg17`, bind mount `/var/lib/arisp/pgdata`) — ADR-055. Supabase chỉ còn là môi trường test. Luôn kết nối trực tiếp qua connection string, không dùng SDK |
| ORM | Entity Framework Core |
| Auth | JWT + Role-based + Email/Password + Google OAuth2 (nội bộ, optional) |
| Cache | Redis |
| AI/LLM (phỏng vấn) | OpenAI GPT-4o (Claude là option dành sau — ADR-043) |
| RAG Service | Python + LangChain + LangGraph + FastAPI (`rag-service/`) — Hybrid RAG: dense pgvector + sparse FTS + RRF; sở hữu chunk/embed/retrieve/sinh câu hỏi+đánh giá, .NET gọi qua HTTP/SSE (ADR-039). Vector store: pgvector + text-embedding-3-small |
| CV-JD Analysis | Google Gemini 2.5 Flash |
| STT | Deepgram Nova-3 (streaming) — gộp VAD + endpointing, không cần VAD riêng |
| TTS | ElevenLabs Flash v2.5 (streaming, ~75ms) |
| Avatar | HeyGen Streaming Avatar (Hybrid Idle Strategy) |
| Email | SendGrid / AWS SES |
| File Storage | `IFileStorageService` – Local disk (dev) / Cloudflare R2 S3-compatible (prod) qua AWSSDK.S3 |
| Containers | Docker + Docker Compose |
| Servers | Ubuntu/Linux VPS + Nginx (SSL + routing) |
| CI/CD | GitHub Actions |
| Monitoring | Serilog + Grafana + Health Checks |

> **Streaming-First:** Deepgram STT stream (+VAD) → Hybrid RAG parallel → GPT-4o stream → ElevenLabs Flash v2.5 stream → HeyGen Avatar stream. Mục tiêu latency: **~0.8–1.2 giây** sau khi ứng viên dừng nói (ADR-006/043).

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
16. CV-JD Analysis (Gemini): chạy 1 lần per CV + Job Posting, đính kèm vào Application – không phân tích lại
17. JD File Upload: hỗ trợ PDF/DOCX – Gemini ưu tiên file gốc, fallback sang text JD
18. Gemini AI: dùng cho CV-JD Match Analysis **và** trích xuất JD để auto-fill form tạo tin (ADR-042). Phỏng vấn AI và RAG pipeline vẫn dùng OpenAI GPT-4o
19. **[BẮT BUỘC] Cập nhật `.ai/tasks.md` sau MỌI task hoàn thành:**
    - Đánh dấu task `[x]` + ghi ngày hoàn thành (format `YYYY-MM-DD`)
    - Nếu công việc vừa làm KHÔNG có trong `tasks.md` → tự động bổ sung vào phần Backlog tương ứng rồi đánh dấu `[x]` luôn
    - Thêm entry vào phần `## Completed` với mô tả ngắn về những gì đã làm
    - Commit `tasks.md` CÙNG VỚI code trong cùng 1 commit – không tách riêng
    - **Không được kết thúc task nếu chưa cập nhật `tasks.md`**

---

## Trạng thái Tasks

**Phase hiện tại:** Phase 1–2 (foundation và auth đã xong, đang tiến vào Phase 2a–3)  
**Cập nhật lần cuối:** 2026-08-05

### Completed (tóm tắt)
- Phase 0: GitHub repo, branch strategy, .gitignore, project structure, backend/frontend boilerplate, Docker, Nginx
- Phase 1: Auth hoàn chỉnh (email/password, Google OAuth2, magic link, JWT + refresh token, role-based auth, pre-provisioning)
- Phase 2: Job Posting CRUD backend, multi-round config, language detection, application flow, CV upload
- Phase 3 (backend): Interview Code generation (6-char, one-time-use, TTL 2h)
- Phase 4 (infra): Domain entities (22 entities), IAIProvider + OpenAIProvider, IEmbeddingProvider, DocumentChunk + pgvector, PlaybookService, MustAskTracking, SignalR hubs (SessionHub + WebRTCSignalingHub)
- Phase 6 (backend): EvaluationsController, EvaluationService, HrReview entity, AuditLog entity, Language Assessment DTOs, CheatSignal DTOs
- Frontend: 59 pages theo role, ProtectedRoute, Zustand stores, API services layer

### In Progress
_Chưa có task nào đang thực hiện._

### Backlog (tóm tắt theo Phase)

| Phase | Nội dung chính |
|---|---|
| Phase 0 | GitHub repo, branch strategy, project structure boilerplate, Docker, Nginx |
| Phase 1 | DB schema (users, system_settings), JWT + email/password auth, Google OAuth2 + domain validation, magic link |
| Phase 2 | Job Posting CRUD, Application flow, CV upload, language detection |
| Phase 2a | CV-JD Analysis (Gemini), `cv_jd_analyses` table, API endpoints, Frontend Job Detail |
| Phase 2b | Job Board (self-apply), Practice Interview flow, candidate accounts |
| Phase 2c | Online Test (multiple choice), auto-scoring, auto-progression |
| Phase 3 | Availability slots, Interview Code (6 ký tự) generation, Kiosk mode frontend |
| Phase 4 | AI Interview core (RAG, pgvector, IAIProvider, adaptive difficulty, language-aware) |
| Phase 4b | Interview Playbook (upload, chunk, embed, must-ask enforcement) |
| Phase 5 | Multi-round flow, auto-progression |
| Phase 6 | AI Evaluation, Language Assessment, HR Review dashboard, audit log |
| Phase 7 | Media pipeline (VAD, WebRTC, STT, TTS, HeyGen Avatar, recording) |
| Phase 8 | Cheat Detection (eye tracking, timing, tab switch, speech pattern) |
| Phase 9 | Candidate Portal (magic link auth, view recording/transcript/report) |
| Phase 10 | System Config UI, HR account management (Super Admin) |
| Phase 11 | ATS Webhook, Google OAuth2 provider integration, Slack/Teams notifications |
| Phase 12 | CI/CD, VPS deploy, SSL, Serilog + Grafana monitoring |
| Phase 13 | Redis caching, health checks, Bias Detection, analytics dashboard |

> Chi tiết checkbox từng task: xem [.ai/tasks.md](.ai/tasks.md)

---

## Key Architecture Decisions (Tóm tắt)

| ADR | Quyết định |
|---|---|
| ADR-001 | Backend: ASP.NET Core .NET 8 (không dùng Node.js) |
| ADR-002 | DB: PostgreSQL kết nối trực tiếp, không dùng Supabase SDK. *(Phần "on Supabase" đã bị ADR-055 thay thế)* |
| ADR-003 | SignalR cho session events; WebRTC cho media stream (không trộn lẫn) |
| ADR-004 | AI/LLM: OpenAI GPT-4o + RAG pgvector, abstract qua `IAIProvider` + `IEmbeddingProvider` |
| ADR-005 | STT: **Deepgram Nova-3** (gộp VAD/endpointing, thay Google STT); TTS: ElevenLabs Flash v2.5; Avatar: HeyGen Streaming Avatar + Hybrid Idle Strategy |
| ADR-006 | Streaming-First latency target: **~0.8–1.2s** (Deepgram STT+VAD → Hybrid RAG 0ms → LLM 400–800ms → TTS 75–150ms → Avatar 100–200ms); đòn bẩy: partial-STT→RAG song song, TTS first-sentence, prompt caching, tắt thinking, gọi thẳng OpenAI |
| ADR-011 | HeyGen Hybrid Idle: chỉ bật khi AI nói, phát idle video loop khi im – tiết kiệm ~90% HeyGen cost |
| ADR-012 | Single-tenant: xoá `organizations`, `subscriptions`, không dùng `organization_id` |
| ADR-015 | Practice (Remote) mở qua **Portal** sau khi pass CV + đặt lịch buổi thật của vòng (1 lượt/vòng, không cần code); Real (On-site) qua Interview Code tại Kiosk |
| ADR-016 | Interview Code: **chỉ cho phỏng vấn thật/Kiosk** (đã bỏ `code_type`); 6 ký tự alphanumeric, one-time-use, TTL 2h, bind `application_id` + vòng |
| ADR-038 | Tối ưu chi phí Practice: gating theo phễu (pass CV + đã đặt lịch buổi thật của vòng mới mở thử, 1 lượt/vòng), giữ đủ tech, Hybrid Idle, không quay video practice |
| ADR-039 | RAG microservice Python (`rag-service/`, FastAPI+LangChain+LangGraph) sở hữu **toàn bộ** chunk/embed/hybrid-retrieve/sinh câu hỏi+đánh giá. .NET gọi qua HTTP/SSE (`RagServiceProvider` + `IRagIngestionService`), `OpenAIProvider` là fallback qua cờ `AI:Provider`. pgvector trên Postgres (schema do EF sở hữu). **Giai đoạn 1 Hybrid RAG đã triển khai (2026-06-26)**; CRAG/Agentic còn backlog |
| ADR-040 | Cổng kiểm tra mic + cam bắt buộc trước mọi phỏng vấn (thử & thật) — component `DeviceCheck` |
| ADR-043 | Media stack phỏng vấn chốt: **Cascaded** (~0.8–1.2s, không speech-to-speech) — Deepgram Nova-3 (STT+VAD) + ElevenLabs Flash v2.5 + Hybrid RAG + **GPT-4o (Claude là option dành sau)** + HeyGen. TTFT là yếu tố chính; Haiku 4.5 nhanh nhất nếu cần giảm trễ |
| ADR-044 | Nối media thực tế: **client-SDK + BE mint token** (`/session/{id}/media-config`). Deepgram live (FE) + HeyGen Streaming Avatar (FE `speak`) + ElevenLabs voice route qua HeyGen; `SignalRNotificationService` đẩy `ReceiveQuestion`. Fallback mềm khi thiếu key. BE giữ key, không relay media |
| ADR-018 | Language-aware AI: detect từ JD, điều chỉnh system prompt + TTS voice + STT languageCode |
| ADR-023 | Auth nội bộ: Email + Password (chính) + Google OAuth2 optional; pre-provisioning + domain validation |
| ADR-025 | Playbook scope: Company / Job Posting / Round; RAG weighted retrieve |
| ADR-030 | Gemini 2.5 Flash cho CV-JD Analysis; GPT-4o cho phỏng vấn AI và RAG |
| ADR-036 | File storage abstraction `IFileStorageService`: Local (dev) / Cloudflare R2 (prod, presigned URL); DB lưu storageKey |
| ADR-041 | Vòng đời tài khoản staff: yêu cầu tạo (HR→SA duyệt) tách khỏi khóa/mở khóa (`AccountRequest` + `User.LockReason`) |
| ADR-042 | Recruiter workspace cụm Job: `mine` filter, ứng viên theo job, Gemini trích xuất JD auto-fill (mở rộng ADR-030) |
| ADR-045 | Refactor Clean Architecture chuẩn JT template: `ari-service/` (src/+tests/), namespace `ARI.*`, CQRS + MediatR **pin [12.5.0]** (v13 commercial), FluentValidation, thin controllers, DI per-project, schema 100% migrations. SessionHub gọi thẳng `IInterviewService` (không qua MediatR — latency ADR-006) |
| ADR-046 | Refactor FE mirror ADR-045: `frontend/` → `ari-web/` (npm workspaces), tách **ARI.CandidateSite** (public, 3000) + **ARI.StaffSite** (nội bộ, 3001) + **ARI.Shared**. `services/`→`fservices/` (quy tắc "f"). Tách concern app/pages/fservices/components; import Shared qua `@ari/shared/*`. URL/API/DTO/localStorage/hub freeze. Nginx host-based 2 origin (localhost / staff.localhost); CORS thêm `Frontend:CandidateBaseUrl`. `configureApiClient` refresh riêng mỗi site (candidate = `/auth/candidate/refresh`) |
| ADR-047 | CI/CD GitHub Actions: build 4 image ở runner → GHCR → VPS chỉ `pull && up -d` (không build trên VPS 3.8GB RAM). **`main` = production** (deploy tự động), `develop` = integration. `ci.yml` chặn PR không build được. Config prod hết drift nhờ tách `nginx/conf.d.prod/`; `ports: !reset []` (không phải `ports: []`) mới thực sự đóng cổng. `VITE_API_BASE_URL=/api` tương đối → 1 image dùng mọi domain. 2 origin: `arisp.io.vn` + `staff.arisp.io.vn` |
| ADR-055 | **Production DB bỏ Supabase — Postgres tự host trên VPS** (thay phần hosting của ADR-002; Supabase còn là môi trường test). Chạy **container** `pgvector/pgvector:pg17` chứ không cài lên host (một mô hình vận hành duy nhất, phiên bản ghim trong git). **Bind mount** `/var/lib/arisp/pgdata` thay named volume → `down -v` không xoá được dữ liệu. **`ports: !override ["127.0.0.1:5432:5432"]`** — `ports:` thường sẽ *nối thêm* vào `5433:5432` bind `0.0.0.0` của base (bẫy ADR-047 đ.6), `!reset []` thì SSH tunnel không tới được; prefix `127.0.0.1:` khiến Docker chỉ tạo DNAT loopback nên không ra Internet dù ufw thế nào. Nhóm truy cập DBeaver qua **SSH tunnel** (user `arisp`, mỗi người một public key) — xem `docs/postgres-production-setup.md`. Chuỗi kết nối nội bộ để **`SSL Mode=Disable`** (container không có certificate; giữ `Require` là chết lúc boot). Mật khẩu phải viết **3 chỗ**: `POSTGRES_PASSWORD` + chuỗi .NET + `DATABASE_PASSWORD`. `backend.depends_on` thêm `postgres: service_healthy` (EF migrations chạy lúc boot). Ngân sách RAM 4GB cân lại = 3328M. **Backup nay bắt buộc** (`scripts/backup-db.sh`, cron 03:00, tự `pg_restore --list` kiểm tra bản dump) |
| ADR-048 | **HR gán cứng lịch phỏng vấn cho ứng viên** (đảo phần đặt lịch của ADR-015): staff chọn 1 slot trong kho ấn định cho ứng viên qua `POST /api/schedules/assign`; **bỏ hẳn** ứng viên tự chọn (gỡ `GET /schedule/{id}/slots` + `POST /schedule/{id}/book` + `AuthorizeCandidateAsync`). Giữ nguyên side-effects booking (chốt chỗ nguyên tử, `screening→interview`, gating Interview Code). Ứng viên nhận realtime `InterviewScheduled` + bell + email giờ hẹn. **Xác nhận/báo bận (2026-07-25):** `InterviewBooking` thêm `confirmation_status`/`decline_reason`/`responded_at` (migration `AddBookingConfirmation`); ứng viên `POST /api/candidate/schedule/{bookingId}/confirm|decline` — decline (có lý do) set `Status="declined"` + trả chỗ slot để staff gán lại qua chính `AssignSlotCommand` (không cần luồng huỷ riêng). Nhân sự nhận realtime `ReceiveScheduleResponse` + bell; `AssignSchedulePanel` hiện trạng thái xác nhận + banner lý do báo bận. `SchedulePage` candidate có nút Xác nhận / "Tôi bận, đổi lịch". UI gán: `@ari/shared/ui/AssignSchedulePanel` trên trang chi tiết ứng viên HR/Recruiter |
| ADR-049 | **Online Test (thi trắc nghiệm) — Phase 2c**. Ngân hàng câu hỏi **theo JOB** (`OnlineTestQuestion.JobPostingId`); điểm sàn `OnlineTestPassScore` trên `JobPosting` (mặc định 70). Tự chấm `score = correct/total*100`, `isPassed = score >= passScore`; `CorrectOption` không rời BE (DTO ứng viên ẩn). 1 lượt/vòng qua unique `(application_id, round_number)`. CQRS `ARI.Application/OnlineTest/`: HR `OnlineTestController` (InternalStaff, chủ tin/admin), ứng viên `CandidateOnlineTestController` (CandidateOnly). Auto-progression **mềm**: realtime `OnlineTestGraded` + notification idempotent (`SyncNotificationsAsync`), không tự đổi status; kết quả hiện cho HR qua `GET /online-test/applications/{id}/result` + bảng tổng hợp theo job `GET /online-test/jobs/{id}/results`. FE: `JobOnlineTestPage` + `JobOnlineTestResultsPage` (staff) + `OnlineTestPage`/`OnlineTestEntry` (candidate); i18n VI/EN. **Screening Test (cập nhật):** bốc N câu ngẫu nhiên/lượt (mặc định 20, deterministic theo hồ sơ+vòng), câu 1/nhiều đáp án (`QuestionType`+`CorrectOptions`, chấm khớp hoàn toàn), hẹn giờ (mặc định 30', FE tự nộp khi hết giờ), export `.xlsx` (OpenXML) qua `GET .../results/export`; cấu hình per-job qua `PUT .../settings` (passScore+questionsPerTest+durationMinutes). **Import ngân hàng từ Excel:** upload `.xlsx` `POST .../jobs/{id}/questions/import` (OpenXML đọc, layout A=câu hỏi/B=loại/C–H=phương án/I=đáp án đúng, remap chỉ số + dùng lại `ValidateQuestion`, trả `imported/failed/errors[]`) + tải file mẫu `GET .../questions/template`; FE card "Nhập từ file Excel" trong `JobOnlineTestPage` |
| ADR-054 | **Khoá màn hình Kiosk + ghi log rời phòng**: nhập đúng mã → `requestFullscreen()` ngay trong cú click; rời toàn màn hình → **lớp phủ chặn** cả phòng phỏng vấn cho tới khi bấm quay lại; chặn context menu + phím tắt bắt được + cảnh báo `beforeunload`. Hook chung `useKioskLockdown` ghi nhận `fullscreen_exit`/`tab_hidden`/`window_blur`/`shortcut_blocked`/`page_unload` → `POST /interview/session/{id}/signals` (lúc đóng trang dùng `fetch keepalive`). Nối hạ tầng cũ: `RecordCheatSignalAsync` lưu `CheatDetectionSignal` thật (trước `SessionHub` chỉ broadcast), `CheatScore` tính theo trọng số từng loại + `CheatSignals` gộp theo loại (trước ghi cứng `"[]"`). **Web không chặn được Alt+Tab/phím Windows** — khoá cứng cần `chrome --kiosk` + Windows Assigned Access |
| ADR-053 | **"Đạt" chỉ khi qua HẾT vòng**: AI **không bao giờ** ghi `Application.Status` (bỏ hẳn ở `GenerateEvaluationReportAsync` — trước đây AI chấm trượt là đánh rớt hồ sơ trước khi HR xem); `SubmitHrReviewAsync` tính trạng thái 1 lần theo `ResolveTotalRoundsAsync` = `max(InterviewRoundConfig.RoundNumber)`: không đạt→`not_pass`, đạt & còn vòng→`interview`, **đạt & vòng cuối→`pass`**. Portal trả `TotalRounds`/`PassedRounds` → thẻ hồ sơ hiện **"Qua vòng N/M"**. Sửa kèm: điểm từng câu `null` thay vì 0 (hết "0/100 · Cần cải thiện" sai), **ghim bộ khoá tiêu chí** trong prompt + alias `CriterionBar` (hết tên tiêu chí tiếng Anh giữa màn tiếng Việt), đồng bộ cỡ chữ, ẩn banner quá hạn ở hồ sơ đã đóng. Dev: `POST /api/dev/seed-interview-job` (job 3 vòng + trắc nghiệm + mã Kiosk + tài khoản HR), `POST /api/dev/regrade-session/{id}?lang=` |
| ADR-052 | **Kiosk phỏng vấn THẬT**: `validate-code` trả `sessionId` + **JWT role `Kiosk_session`** (claim `session_id`, TTL 3h) thay cho đăng nhập; policy `InterviewParticipant` (Candidate ∪ Kiosk_session) + kiểm khớp `session_id` ở controller & `SessionHub`. Kiosk **quay video** cam+mic → `POST /interview/session/{id}/recording` → storage, `recording_expires_at = now + Interview:RecordingRetentionDays` (**7 ngày**); `RecordingRetentionHostedService` quét 12h/lần xoá file quá hạn (giữ transcript + đánh giá), ghi `recording_deleted_at`. HR xem video trong `EvaluationReviewPage` (trước là placeholder chết). Trần buổi thật `RealMaxDurationMinutes` (**20'** — bằng trần mỗi phiên của gói LiveAvatar Essential; đổi gói là đổi số). Hook chung `useInterviewSession({existingSessionId, sessionType, recordVideo})`. Kiosk UX: device check → chỉ báo đang ghi hình + đếm ngược → màn kết thúc tự reset 30s, khôi phục phiên khi reload |
| ADR-051 | **Buổi thử = không gian riêng của ứng viên**: transcript + nhận xét AI **lưu vĩnh viễn**, chỉ ứng viên sở hữu xem lại (không giới hạn số lần) qua `GET /api/portal/practice/sessions[/{sessionId}]` (`PortalPracticeFeature`, `CandidateOnly` + IDOR); endpoint từ chối phiên `real` (transcript thật vẫn theo cổng `HrReview.ShareTranscript`). **Ẩn hoàn toàn khỏi HR Lead/Recruiter** (lọc `SessionType != "practice"` ở `GetSessionsForHrAsync` + 3 query Evaluations), chỉ giữ cờ `PracticeSessionUsed`. **Không hiện verdict Pass/Not Pass** (DTO bỏ hẳn `AiVerdict`) — chỉ điểm/tiêu chí/phân tích câu/ngôn ngữ/gợi ý, kèm nhãn tham khảo. Buổi thử **không chạm pipeline thật**: `GenerateEvaluationReportAsync` chỉ đổi `application.Status` + báo `hr_admin` khi `SessionType=="real"` (sửa bug practice đánh rớt hồ sơ); portal detail tách `practiceSessions` khỏi tiến trình vòng. Thêm `InterviewSession.ClosingText` + index `ix_questions_session_id`/`ix_answers_session_id` (migration `AddPracticeTranscriptReview`). FE: `PracticeReviewPage` (`/candidate/practice/:sessionId`, bố cục 2 cột — tổng quan sticky bên trái, hội thoại + nhận xét AI theo từng câu bên phải) + lối vào từ màn kết thúc buổi thử và trang chi tiết hồ sơ. **Báo cáo AI 1 ngôn ngữ** (`InterviewSession.ReportLanguage` lấy từ i18n FE lúc bắt đầu phiên, prompt cấm trộn ngôn ngữ); **phân tích từng câu có schema cứng** `{sequence_number,score,analysis,feedback}` + ghép vào đúng lượt hỏi–đáp từ DB; **đánh giá ngôn ngữ chấm trên chính câu trả lời** (kèm CEFR + dẫn chứng, bỏ qua khi không có câu trả lời) |
| ADR-050 | Practice **audio-only** (bỏ avatar — tránh cạnh tranh concurrency LiveAvatar với buổi thật + đốt credit; giữ đủ STT/RAG/GPT-4o/ElevenLabs qua WebAudio). **Trần 20 phút** (`Interview:PracticeMaxDurationMinutes`) → hết giờ khoá mic + AI câu kết + đóng phiên (2 lớp enforce: FE `NotifyTimeout` + server `forceClosing` theo elapsed; `CloseWithFarewellAsync`/`EndSessionAsync` idempotent). **Nhập kép** voice+keyboard: transcript vào 1 `answerText` sửa/gõ tay được. Real vẫn có avatar. Huỷ ghi âm+xoá 7 ngày (giữ ADR-038 đ.6). SỬA ADR-038 đ.3-4, HIỆN THỰC đ.5. **Dev test:** `POST /api/dev/seed-practice` (chỉ Development) tạo sẵn hồ sơ đủ điều kiện + `Interview:PracticeAttemptsPerRound=0` để lặp lại. *(Ban đầu ADR-048; đổi 050 do trùng số khi merge — 048=lịch, 049=online-test.)* |
| ADR-057 | **Realtime ở TẦNG DATABASE — trigger + `LISTEN/NOTIFY`** thay cho mô hình "command tự nhớ push" (~40 điểm gọi `PublishUserEventAsync` rải rác, quên là hỏng im lặng — đã gặp: `JobReassigned` phát ra mà FE không có `case` nào bắt). Một hàm trigger `arisp_notify_change()` dùng chung cho **toàn bộ 30 bảng** (đọc cột qua `to_jsonb(rec)->>` nên bảng thiếu cột chỉ trả NULL); gắn bằng `arisp_attach_change_triggers()` quét `pg_class` → migration sau chỉ cần gọi lại hàm là bảng mới có realtime. Payload **chỉ chứa khoá** (giới hạn `pg_notify` là 8000 byte + không để rò dữ liệu). `document_chunks` dùng trigger **statement-level** (rag-service xoá/nạp hàng trăm chunk mỗi lần ingest). `DbChangeListenerHostedService` giữ connection **riêng `Pooling=false`+`Multiplexing=false`** (connection từ pool bị trả về là **mất LISTEN im lặng**), hàng đợi `Channel` trần 2000, tự nối lại backoff 1s→30s, mỗi lần (re)connect phát `op="resync"` vì NOTIFY fire-and-forget làm mất sự kiện lúc đứt. `DbChangeRouter` (29 test) là **chốt chặn bảo mật mặc định ĐÓNG**: bảng chưa map thì không gửi cho ai; `Clients.All` chỉ dùng cho `job_postings` với payload rút gọn `{t,op,id}`. **Không** chọn Firebase (nguồn sự thật thứ hai) hay Supabase Realtime (đảo ngược ADR-055 + cần nâng VPS 8GB + phải viết lại phân quyền .NET thành RLS). NOTIFY **transactional** — rollback không sinh event giả. Giữ song song push thủ công cũ, chưa gỡ. Kèm: `docker/.env` local chuyển từ Supabase sang container `pgvector:pg17` (`PGDATA_PATH=./pgdata`) |
| ADR-056 | **Khôi phục khoá ngoại + UNIQUE + index vận hành vào migration EF.** ADR-055 dựng production từ số 0 bằng migration nên **mất im lặng** cả lớp schema vốn được áp TAY bằng SQL lên Supabase: 29→1 khoá ngoại, 44→36 UNIQUE, 28→0 index `idx_*`, mất index vector. Nghiêm trọng nhất là 8 UNIQUE — production đang **cho phép trùng email tài khoản và trùng mã Kiosk 6 ký tự**. Nay khai hết trong `OnModelCreating` (`ConfigureRelationships` + `ConfigureOperationalIndexes`) → migration `RestoreForeignKeysIndexesAndUniqueConstraints`: 38 FK + 43 index, **0 thao tác chạm cột/dữ liệu**, `Down()` đối xứng. Quan hệ khai **không dùng navigation property** (`HasOne<T>().WithMany().HasForeignKey`) nên không dòng query nào phải đổi. 19 `Cascade` / 19 `NoAction` khớp từng cái với Supabase; thêm 10 FK mới cho bảng chưa từng có (`account_requests` để `NoAction` vì là hồ sơ kiểm toán — ADR-041). **ivfflat → HNSW** (ivfflat học phân cụm lúc tạo, DB trắng sẽ ra index rác). **5 cột cố ý không đặt FK**: 3 cột đa hình + `account_requests.batch_id` (không có bảng đích) + `questions.playbook_chunk_id` (rag-service xoá cứng chunk mỗi lần nạp lại → FK sẽ chặn ingest). Kiểm chứng bằng container `pgvector:pg17` trắng + so từng chữ ký với Supabase: **0 FK thiếu, 0 UNIQUE thiếu**; 680/680 test pass. Còn chênh cố ý: 51 cột Supabase là `varchar(n)`, bản EF để `text` (Postgres giống hệt nhau về hiệu năng). |

> Chi tiết đầy đủ từng ADR: xem [.ai/architecture.md](.ai/architecture.md)

---

## Glossary (Thuật ngữ chính)

| Thuật ngữ | Định nghĩa |
|---|---|
| Application | Hồ sơ ứng tuyển của Candidate cho một Job Posting (CV + thông tin cá nhân) |
| Interview Session | Phiên phỏng vấn AI từ đầu đến cuối, gồm nhiều Q&A |
| Verdict | Kết quả đề xuất của AI: `Pass` hoặc `Not Pass` |
| Override | HR Leader thay đổi Verdict của AI – bắt buộc nhập `override_reason` |
| Interview Code | Mã 6 ký tự alphanumeric one-time-use để vào phỏng vấn thật tại Kiosk |
| Magic Link | Link xác thực email không cần mật khẩu, TTL 15 phút, one-time-use |
| Playbook | Tài liệu phỏng vấn nội bộ doanh nghiệp (style, question bank, rubric...) đưa vào RAG |
| RAG | Retrieval-Augmented Generation – retrieve chunks từ JD/CV/Playbook trước khi generate câu hỏi |
| Practice Session | Phỏng vấn thử 1 lượt / vòng (mở sau khi đặt lịch buổi thật của vòng, vào qua Portal không cần code), JD+CV only, không ảnh hưởng verdict. **Audio-only (không avatar), trần 20 phút, nhập kép voice/keyboard** — ADR-050. **Transcript + nhận xét AI lưu vĩnh viễn, chỉ ứng viên xem lại (`/candidate/practice/:sessionId`), ẩn hoàn toàn khỏi HR/Recruiter, không hiện verdict** — ADR-051 |
| Real Session | Phỏng vấn thật On-site, full RAG, kết quả ảnh hưởng tuyển dụng |
| Match Score | Điểm phù hợp CV-JD (0–100) do Gemini chấm – chỉ tham khảo |
| Must-ask | Câu hỏi bắt buộc phải hỏi trước khi kết thúc session (định nghĩa trong Playbook) |
| Adaptive Difficulty | AI tự điều chỉnh độ khó câu hỏi theo chất lượng câu trả lời |
| Hybrid Idle Strategy | HeyGen chỉ bật khi AI nói, phát idle video loop khi im để tiết kiệm cost |
| Pre-provisioning | Super Admin tạo tài khoản HR trước trong DB – không cho phép self-register |
| Single-tenant | Hệ thống chỉ phục vụ 1 doanh nghiệp, không có `organization_id` |

> Đầy đủ: xem [.ai/glossary.md](.ai/glossary.md)
