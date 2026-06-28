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
- **Đầy đủ pipeline công nghệ như Real** (STT/RAG/GPT-4o/TTS/Avatar + Hybrid Idle). **Không quay video — chỉ lưu transcript** + Evaluation Report
- Chi phí practice **do doanh nghiệp trả** (mỗi vòng = 1 lượt thử + 1 lượt thật). Tối ưu bằng gating theo phễu, không cắt tech — xem ADR-038

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
| Frontend | React, TypeScript, TailwindCSS |
| Backend | C#, ASP.NET Core .NET 8 |
| API Style | REST API + SignalR |
| Realtime Media | WebRTC |
| Database | PostgreSQL on Supabase (kết nối trực tiếp, không dùng Supabase SDK) |
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

### Backend (C# / ASP.NET Core .NET 8)

**Naming:**
- Namespace: `ARISP.<Layer>.<Module>` (ví dụ: `ARISP.Application.Interview`)
- Class: PascalCase | Interface: prefix `I` | Method: PascalCase + suffix `Async` cho async
- Private field: `_camelCase` | Constant: `UPPER_SNAKE_CASE`

**Project Structure (Clean Architecture):**
```
backend/
├── ARISP.API/            # Controllers, Middleware, Program.cs
├── ARISP.Application/    # Use Cases, DTOs, Interfaces, Validators
├── ARISP.Domain/         # Entities, Value Objects, Domain Events
└── ARISP.Infrastructure/ # EF Core, Repositories, External Services
```

**Patterns bắt buộc:** Repository Pattern, CQRS (MediatR nếu phức tạp), Result Pattern (không throw exception cho business errors), Dependency Injection, Async/Await cho mọi I/O.

**Security:** Không hardcode secrets – luôn dùng `appsettings.json` + env vars. JWT bắt buộc mọi protected endpoint. CORS chặt – chỉ allow frontend domain.

### Frontend (React + TypeScript)

**Naming:** Component: PascalCase | Hook: prefix `use` | Util: camelCase | Type/Interface: PascalCase

**File Structure:**
```
frontend/src/
├── components/  # Reusable UI
├── pages/       # Route-level components
├── hooks/       # Custom hooks
├── services/    # API calls (không fetch trực tiếp trong component)
├── store/       # State management
├── types/       # TypeScript interfaces & types
└── utils/       # Pure utility functions
```

**Patterns:** Không fetch API trong component – qua `services/`. Dùng custom hook cho logic tái sử dụng. Không dùng `any`.

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
3. Không dùng Supabase SDK – kết nối PostgreSQL trực tiếp qua connection string
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
**Cập nhật lần cuối:** 2026-06-14

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
| ADR-002 | DB: PostgreSQL on Supabase, kết nối trực tiếp, không dùng Supabase SDK |
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
| ADR-018 | Language-aware AI: detect từ JD, điều chỉnh system prompt + TTS voice + STT languageCode |
| ADR-023 | Auth nội bộ: Email + Password (chính) + Google OAuth2 optional; pre-provisioning + domain validation |
| ADR-025 | Playbook scope: Company / Job Posting / Round; RAG weighted retrieve |
| ADR-030 | Gemini 2.5 Flash cho CV-JD Analysis; GPT-4o cho phỏng vấn AI và RAG |
| ADR-036 | File storage abstraction `IFileStorageService`: Local (dev) / Cloudflare R2 (prod, presigned URL); DB lưu storageKey |
| ADR-041 | Vòng đời tài khoản staff: yêu cầu tạo (HR→SA duyệt) tách khỏi khóa/mở khóa (`AccountRequest` + `User.LockReason`) |
| ADR-042 | Recruiter workspace cụm Job: `mine` filter, ứng viên theo job, Gemini trích xuất JD auto-fill (mở rộng ADR-030) |

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
| Practice Session | Phỏng vấn thử 1 lượt / vòng (mở sau khi đặt lịch buổi thật của vòng, vào qua Portal không cần code), JD+CV only, không ảnh hưởng verdict |
| Real Session | Phỏng vấn thật On-site, full RAG, kết quả ảnh hưởng tuyển dụng |
| Match Score | Điểm phù hợp CV-JD (0–100) do Gemini chấm – chỉ tham khảo |
| Must-ask | Câu hỏi bắt buộc phải hỏi trước khi kết thúc session (định nghĩa trong Playbook) |
| Adaptive Difficulty | AI tự điều chỉnh độ khó câu hỏi theo chất lượng câu trả lời |
| Hybrid Idle Strategy | HeyGen chỉ bật khi AI nói, phát idle video loop khi im để tiết kiệm cost |
| Pre-provisioning | Super Admin tạo tài khoản HR trước trong DB – không cho phép self-register |
| Single-tenant | Hệ thống chỉ phục vụ 1 doanh nghiệp, không có `organization_id` |

> Đầy đủ: xem [.ai/glossary.md](.ai/glossary.md)
