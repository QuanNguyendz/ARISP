# Tasks – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

> Cập nhật file này sau mỗi task hoàn thành hoặc khi bắt đầu task mới.
> AI tools phải đọc file này trước khi bắt đầu bất kỳ việc gì để tránh làm trùng hoặc mâu thuẫn.

---

## Trạng thái hiện tại

**Phase:** 0 – Setup & Foundation  
**Last updated:** 2026-06-18

---

## Đang làm (In Progress)

_Chưa có task nào đang thực hiện._

---

## Backlog (Chưa bắt đầu)

### FE UI Redesign (mới) – từ mockup `design/mockups/`
- [ ] Áp design system + logo ARISP vào `frontend` thật (token màu brand/ai/ink, font Plus Jakarta Sans/Inter).
- [ ] Dark/light theme toggle toàn FE (lưu localStorage / `preferred_theme`), no-flash init.
- [ ] **i18n UI candidate VI/EN** (react-i18next) — [ADR-033]; cột `candidate_accounts.preferred_locale`.
- [ ] **Saved Jobs (bookmark)** — [ADR-034]; bảng `saved_jobs`, API lưu/bỏ lưu + trang "Việc đã lưu".
- [x] 2026-06-18 **Candidate Google OAuth2 (no domain)** — [ADR-035]; mở rộng auth flow, tự tạo `candidate_accounts`.
- [ ] Header candidate: tìm kiếm toàn cục (⌘K), menu người dùng, notification center (đọc/đánh dấu đã đọc), badge số liệu.
- [ ] Notification backend cho candidate (interview invite, verdict, CV-JD done, HR viewed) — nguồn dữ liệu + realtime (SignalR) / polling.

### Phase 0 – Foundation

#### GitHub & Source Control
- [x] Tạo GitHub repository (private, tên `ARISP`)
- [ ] Thêm toàn bộ thành viên vào repo với quyền phù hợp (Admin / Write)
- [x] Thiết lập **branch strategy:**
  - `main` – production-ready, chỉ merge qua Pull Request được review
  - `develop` – integration branch, merge từ các feature branch
  - `feature/<tên-feature>` – ví dụ: `feature/auth-jwt`
  - `fix/<mô-tả-lỗi>` – ví dụ: `fix/jwt-refresh-token-expiry`
- [ ] **Branch protection rules** cho `main` và `develop`:
  - Require PR + ít nhất 1 reviewer approve trước khi merge
  - Require CI checks pass (sau khi có GitHub Actions)
  - Không cho phép force push
- [x] Tạo `.gitignore` cho backend (.NET: bin/, obj/, *.user, appsettings.*.json)
- [x] Tạo `.gitignore` cho frontend (node_modules/, dist/, .env*)
- [ ] Tạo **PR template** (`.github/pull_request_template.md`): mô tả thay đổi, checklist, link task
- [x] Thống nhất **commit message convention** (đã có trong `CLAUDE.md`):
  - Format: `<type>(<scope>): <mô tả ngắn>`
  - Type: `feat` | `fix` | `refactor` | `docs` | `test` | `chore`
- [ ] Tạo **GitHub Issues** cho từng Phase/task (gán assignee theo phân công)
- [ ] Tạo **GitHub Projects board** (Kanban: Backlog → In Progress → Review → Done)
- [ ] Setup **GitHub Secrets** cho CI/CD sau này (OPENAI_API_KEY, DB_CONNECTION_STRING, ...)

#### Project Structure & Boilerplate
- [x] Tạo cấu trúc thư mục dự án (`backend/`, `frontend/`, `docker/`, `nginx/`, `scripts/`)
- [x] Khởi tạo backend boilerplate (ASP.NET Core .NET 8, Clean Architecture, namespace `ARISP.*`)
- [x] Khởi tạo frontend boilerplate (React + TypeScript + Vite + TailwindCSS)
- [x] Setup Docker + docker-compose (backend, frontend, postgres, redis)
- [x] Setup Nginx config cơ bản

### Phase 1 – Global Settings & Auth Setup
- [ ] Database schema: `users`, `refresh_tokens`, `system_settings` (entities `users` và `refresh_tokens` đã có; `system_settings` chưa có entity)
- [ ] EF Core migrations (chưa commit migration files – chỉ apply qua CLI)
- [x] Đăng ký / Đăng nhập endpoint (HR Admin + Recruiter + Super Admin)
- [x] JWT issue + refresh token (không chứa organization_id claim)
- [x] Role-based authorization middleware cụ thể cho 4 role (`SuperAdmin`, `HRAdmin`, `Recruiter`, `Candidate`)
- [x] Magic link auth cho Candidate Portal (email + one-time token, TTL 15 phút)
- [x] **OAuth2 & Domain Validation:** Tích hợp Google OAuth2 (Google Sign-In) cho HR Users
- [x] **OAuth2 & Domain Validation:** Validate email domain + pre-provisioned check khi dùng Google Sign-In

### Phase 2 – Job Posting & Application
- [x] Database schema: `job_postings`, `interview_round_configs`, `applications`
- [ ] EF Core migrations
- [x] HR Admin / Recruiter: CRUD Job Posting
  - [x] Thông tin cơ bản (tên vị trí, lĩnh vực, JD)
  - [x] **Upload file JD gốc (PDF/DOCX)** – trường `jd_file_url`, `jd_file_name`, `jd_file_format` đã có trong entity
  - [x] Cấu hình multi-round: số vòng, loại vòng (Screening/Technical), ngôn ngữ
  - [x] Availability Slots (Practice): entity `AvailabilitySlot` đã có
  - [x] Phỏng vấn thật: Trường `interview_mode` đã có
  - [x] Scoring Rubric: trường `scoring_rubric` (JSONB) đã có
  - [x] Interview Persona: trường `persona_config` (JSONB) đã có
- [x] Language detection khi tạo Job Posting: `JobDescriptionLanguageDetector` service đã có
- [ ] HR confirm/chỉnh language requirement trước khi publish (UI chưa làm)
- [ ] Candidate invite flow: sinh invite link (signed JWT, 24–72h) → gửi email
- [x] Candidate: nhận invite → submit CV + thông tin cá nhân (Application) (Backend)
- [x] CV upload & parse (PDF → text extraction) (Backend with CV parser stub)

### Phase 2a – CV-JD Match Analysis (Gemini AI)
- [x] Database schema: thêm `jd_file_url`, `jd_file_name`, `jd_file_format` vào `job_postings` (2026-06-15 – migration `AddJdFileFieldsToJobPosting`)
- [x] Database schema: tạo bảng mới `cv_jd_analyses` (match_score, summary, skills_matched, skills_gaps, red_flags, experience_relevance, overall_recommendation, ai_model, status, error_message, prompt_tokens, completion_tokens, processing_time_ms, raw_response)
- [x] Database schema: thêm `cv_jd_analysis_id` vào `applications` (FK → `cv_jd_analyses`, nullable)
- [x] EF Core migrations
- [x] `IGeminiProvider` interface + `GeminiProvider` impl (Google Gemini 2.5 Flash API)
  - [ ] Method: `AnalyzeCvJdMatchAsync(cvFileStream, jdFileStream/jdText)` → `CvJdAnalysisResult`
  - [ ] Hỗ trợ multimodal input: gửi file PDF/DOCX trực tiếp cho Gemini
  - [ ] Config: Gemini API key qua env var `GEMINI_API_KEY`
- [ ] `CvJdAnalysisService`:
  - [ ] Nhận CV file + Job Posting ID → lấy JD (file/text) → gọi `IGeminiProvider` → lưu kết quả vào `cv_jd_analyses`
  - [ ] Check đã có analysis cho cùng CV hash + JobPosting chưa → trả kết quả cũ (không gọi lại Gemini)
  - [ ] Khi candidate submit Application: link `cv_jd_analysis_id` vào Application
  - [ ] Nếu chưa có analysis khi submit → tự động chạy 1 lần rồi đính kèm
- [ ] API endpoints:
  - [ ] `POST /api/cv-analysis/analyze` – Candidate upload CV + jobPostingId → nhận kết quả phân tích (public, không cần login)
  - [ ] `GET /api/cv-analysis/{id}` – Lấy kết quả đã phân tích
  - [ ] `GET /api/applications/{id}/cv-analysis` – HR xem kết quả CV-JD analysis của Application
- [ ] Frontend – Job Detail Page:
  - [ ] Khu vực upload CV để kiểm tra độ phù hợp (trước khi ứng tuyển)
  - [ ] Hiển thị kết quả: Match Score (thanh progress), Summary, Skills Matched/Gaps
  - [ ] Nút "Ứng tuyển ngay" hoạt động bất kể điểm cao/thấp
- [ ] Frontend – HR Candidate Detail Page:
  - [ ] Hiển thị CV-JD Analysis (Match Score, Summary, Skills) cho HR review
- [ ] Frontend – Job Posting Create/Edit:
  - [ ] Thêm khu vực upload file JD gốc (PDF/DOCX) bên cạnh textarea JD text

### Phase 2b – Job Board & Practice Interview
- [ ] Database schema: `candidate_accounts` (self-registered), extend `job_postings` với flag `is_public_listing`
- [ ] EF Core migrations
- [x] Candidate self-registration: email + password (role `Candidate`) – endpoint đã có
- [ ] Candidate: tìm kiếm Job Posting IT (keyword, level, salary range, location)
- [ ] Candidate: xem Job Detail (tên công ty, JD, yêu cầu hiển thị công khai)
- [ ] Candidate: self-apply → submit CV + thông tin cá nhân → tạo `Application`
- [ ] HR Admin / Recruiter: xem danh sách ứng viên tự ứng tuyển qua Job Board (kèm CV)
- [ ] HR Admin / Recruiter: gửi magic link thủ công cho ứng viên sau khi review CV
- [ ] Magic link screen: xác nhận vị trí ứng tuyển → chọn Phỏng vấn thử / Phỏng vấn thực
- [ ] **Practice Interview:**
  - [ ] `ApplicationService`: check eligibility (`practice_session_used` flag per application)
  - [ ] Practice Session: `session_type = practice`, interview flow dùng JD + CV only (không load Playbook)
  - [ ] Practice Session: Evaluation Report riêng, HR xem được, không ảnh hưởng verdict
  - [ ] Disable nút "Phỏng vấn thử" sau khi đã dùng 1 lần

### Phase 2c – Online Test (Multiple Choice Quiz)
- [ ] Database schema: `online_test_questions`, `online_test_submissions`
- [ ] EF Core migrations
- [ ] HR Admin / Recruiter: CRUD câu hỏi trắc nghiệm per Job Posting
- [ ] Candidate: Thực hiện làm bài trắc nghiệm trên Candidate Portal (Giao diện web trắc nghiệm)
- [ ] Backend: Tự động chấm điểm (Auto-scoring) sau khi nộp bài và so khớp đạt/không đạt dựa trên điểm sàn
- [ ] Auto-progression: Nếu ứng viên đạt trắc nghiệm -> Cho phép HR tạo Interview Code cho vòng phỏng vấn tiếp theo

### Phase 3 – Scheduling (Practice) & Interview Code
- [x] Database schema: `availability_slots`, `interview_codes` (entities đã có)
- [ ] Database schema: `interview_bookings` (entity đã có nhưng chưa migration)
- [ ] EF Core migrations
- [ ] **Practice (Remote):** Candidate chọn slot → booking → nhận nhắc nhở 24h/1h
- [x] HR generate Interview Code (format `ARX7K2`, 6 ký tự alphanumeric) cho thi thật
  - [x] One-time-use: vô hiệu hóa sau khi dùng
  - [x] TTL: mặc định 2 giờ, cấu hình per Job Posting
  - [x] Bind với `application_id` cụ thể
  - [ ] Batch generate cho nhiều ứng viên
- [ ] **On-site:** Kiosk mode frontend: nhập Interview Code → validate → vào interview room
- [ ] Audit log: ghi lại creation time, usage time, `application_id` cho mỗi Interview Code

### Phase 4 – AI Interview Core
- [x] Database schema: `interview_sessions`, `questions`, `answers`, `document_chunks`
- [x] Bật pgvector extension trên PostgreSQL (configured trong DbContext)
- [x] `IEmbeddingProvider` interface + `OpenAIEmbeddingProvider` impl (`text-embedding-3-small`)
- [x] `RagService`: chunk JD/CV, embed, lưu pgvector, retrieve context khi sinh câu hỏi — **chuyển sang RAG microservice Python** (`rag-service/`, ADR-039 Giai đoạn 1) ✅ 2026-06-26
- [x] `IAIProvider` interface + `OpenAIProvider` impl (GPT-4o streaming)
- [x] AI question generation với RAG context + adaptive difficulty — Hybrid RAG (LangGraph) trong RAG service, .NET gọi qua `RagServiceProvider` (SSE) ✅ 2026-06-26
- [x] **RAG microservice Python (ADR-039 Giai đoạn 1):** FastAPI+LangChain+LangGraph; endpoints `/ingest` `/retrieve` `/next-question`(SSE) `/analyze-answer` `/evaluate` `/detect-language` `/assess-language` `/complete-json` `/embed`; hybrid dense(pgvector)+sparse(FTS)+RRF+scope weighting; `IRagIngestionService` + `LocalRagIngestionService` fallback; EF migration GIN index FTS ✅ 2026-06-26
- [ ] Interview session flow: start → question loop → adaptive difficulty → end
- [x] **Language-aware:** `JobDescriptionLanguageDetector` service phát hiện ngôn ngữ từ JD
- [ ] **Language-aware:** TTS voice selection theo ngôn ngữ (ElevenLabs multilingual)
- [ ] **Language-aware:** STT `languageCode` config theo ngôn ngữ (Google STT)
- [ ] Điều kiện dừng: AI tự dừng khi khai thác hết context JD + CV
- [x] **Session Type – phân biệt `practice` vs `real`:**
  - [x] `practice`: chỉ retrieve JD + CV chunks, không load Playbook, gated bởi eligibility check
  - [x] `real`: retrieve JD + CV + Playbook chunks (full RAG pipeline), yêu cầu nhập Interview Code tại Kiosk.

### Phase 4b – Interview Playbook (Org Knowledge Base)
- [x] Database schema: `playbook_documents`, `playbook_chunks` (scope: org/job_posting/round – entity `PlaybookDocument` + `DocumentChunk` đã có)
- [ ] EF Core migrations
- [ ] Document upload endpoint (PDF, DOCX, TXT, Markdown, JSON)
- [x] `DocumentParserService`: extract text từ PDF/DOCX
- [x] `PlaybookService`: chunk, embed, lưu vào pgvector với scope tag — **đẩy sang RAG service qua `IRagIngestionService` (/ingest)** ✅ 2026-06-26
- [x] `PlaybookService`: track must-ask questions đã hỏi trong session (`MustAskTracking` entity)
- [x] `InterviewService`: nhận signal must-ask chưa xong trước khi kết thúc session
- [x] `RagService`: retrieve logic – merge JD/CV chunks + Playbook chunks theo weighted scope — **Hybrid retriever (RRF + scope weight) trong RAG service Python** ✅ 2026-06-26
- [ ] HR Admin UI: quản lý Playbook documents (upload, preview, xóa) per Job Posting/Round
- [ ] Validation: file size limit, format check, virus scan (optional)

### Phase 5 – Multi-round & Auto-progression
- [x] Database schema: `interview_rounds` (dùng `round_number` trong `InterviewSession`)
- [ ] Database schema: `round_evaluations` (chưa có entity riêng)
- [ ] Multi-round config: HR cấu hình danh sách vòng per Job Posting (UI chưa làm)
- [x] `InterviewService` hỗ trợ `round_number` và `round_type` per session
- [ ] Auto-progression: sau HR Leader duyệt Pass Round N → lưu kết quả
  - [ ] On-site: Recruiter hẹn lịch offline và generate Interview Code mới cho Round N+1 khi ứng viên đến công ty.
- [ ] Email notification cho Candidate khi được invite Round tiếp theo

### Phase 6 – AI Evaluation & HR Review
- [x] Database schema: `evaluations`, `hr_reviews`, `audit_logs` (entities đã có)
- [ ] Database schema: `language_assessments` (dùng JSONB trong `Evaluation`, chưa có entity riêng)
- [x] AI Evaluation sau mỗi Round: Verdict + Score + Reasoning (entity + DTOs đầy đủ)
- [x] **Language Assessment** (Round 1, nếu có language requirement):
  - [x] DTOs: `LanguageAssessmentDto` với fluency, grammar, vocabulary, comprehension
  - [ ] `IAIProvider.AssessLanguageProficiencyAsync()`: implement actual call
- [x] Evaluation Report: per-question analysis + recommended next step (DTOs đầy đủ)
- [x] HR Dashboard (Phân quyền rõ ràng cho 3 role: SuperAdmin, HR Leader, Recruiter):
  - [x] Danh sách Application per Job Posting (filter, sort) – `EvaluationsController`
  - [x] Xem Evaluation Report + recording per Application per Round
  - [ ] Confirm / Override verdict (HrReview entity có, endpoint `/evaluations/{id}/review` cần kiểm tra)
- [x] `AuditLogService`: entity `AuditLog` đã có, ghi lại mọi action
- [ ] Notification: email + in-app (SignalR) khi Evaluation hoàn thành, cần HR review
- [ ] Email kết quả cho Candidate sau khi HR Leader xác nhận

### Phase 7 – Media & Realtime
- [ ] Frontend: VAD (Voice Activity Detection) – detect near-end-of-speech, trigger early RAG
- [ ] Frontend: Stream audio chunks qua WebSocket lên backend
- [x] `ISTTProvider` interface – định nghĩa xong, `MockSTTProvider` stub
- [ ] `ISTTProvider` – `GoogleSpeechProvider` impl (streaming real-time, multilingual)
- [ ] `WhisperProvider` impl (batch, fallback)
- [x] `ITTSService` interface – định nghĩa xong, `MockTTSService` stub
- [ ] `ITTSService` – ElevenLabs Flash v2.5 streaming impl
- [x] `IAvatarService` interface – định nghĩa xong, `MockAvatarService` stub
- [ ] `IAvatarService` – HeyGen Streaming Avatar API + Hybrid Idle Strategy impl
- [x] SignalR Hub: `SessionHub` – session lifecycle events (start, question-sent, answer-received, session-end)
- [x] `WebRTCSignalingHub` – SDP/ICE signaling cho avatar streaming
- [x] Interview recording: trường `recording_url` trong `InterviewSession` đã có

### Phase 8 – Cheat Detection
- [x] Frontend: custom hooks `cheat-detection` đã có
- [ ] Frontend: thu thập signals đầy đủ trong session
  - [ ] Eye tracking (webcam-based, `WebGazer.js` hoặc tương đương)
  - [ ] Response timing (thời gian từ câu hỏi → bắt đầu trả lời)
  - [ ] Tab switching / focus loss (browser Visibility API)
  - [ ] Speech pattern (reading cadence detection từ partial transcript)
- [x] Backend: `CheatDetectionSignal` entity đã có
- [ ] Backend: `CheatDetectionService`
  - [ ] Nhận signals từ frontend qua SignalR/WebSocket
  - [ ] Heuristic analysis + AI analysis
  - [ ] Generate `CheatScore` (0–100) + `CheatSignals[]`
- [x] Tích hợp CheatScore vào Evaluation Report (DTO `CheatSignalDto` đã có)
- [ ] HR xem CheatScore + signals khi review (UI chưa làm)

### Phase 9 – Candidate Portal
- [x] Frontend: Candidate Portal pages đã có (`/pages/candidate/`)
- [ ] Magic link endpoint: validate token → issue session (endpoint `/auth/magic-link/verify` đã có, cần kiểm tra candidate portal flow)
- [ ] Candidate xem: danh sách Applications của mình
- [ ] Candidate xem per Application:
  - [ ] Recording phỏng vấn (nếu HR bật)
  - [ ] Transcript
  - [ ] Evaluation Report (phần HR cho phép share)
  - [ ] Feedback (nếu HR bật)
- [ ] HR Leader: cấu hình per Job Posting những gì Candidate được xem

### Phase 10 – System Configuration & Global Audit
- [x] Frontend Super Admin pages đã có: Dashboard, Users, PendingUsers, AuditLogs, Settings
- [x] 2026-06-21 **Redesign toàn bộ khu Super Admin** sang style mới (ink/brand/ai + dark mode) + wiring data thật qua `adminService`. Layout chung tham số hóa `WorkspaceLayout` (tái dùng cho HR/Recruiter sau).
- [x] Quản lý tài khoản HR nhân viên (chỉ Super Admin thực hiện):
  - [x] 2026-06-21 Tạo staff (HR Admin/Recruiter) qua modal, đổi vai trò, khóa/mở, xóa (soft delete) — `AdminController` + Users page
  - [ ] Phân quyền theo department
- [x] **System Settings UI (Chỉ dành cho Super Admin):**
  - [x] 2026-06-21 Cấu hình allowed_email_domains (chip input) — `GET/PUT /api/admin/settings`
  - [x] 2026-06-21 Cấu hình global webhooks (ATS webhook url/secret, Slack/Teams webhook urls)
- [x] 2026-06-21 Audit log dashboard: `GET /api/admin/audit-logs` (filter action + paginate, resolve actor name) + trang AuditLogs data thật

### Phase 11 – Integrations
- [x] `WebhookDelivery` entity đã có (retry logic, status tracking)
- [ ] **ATS Webhook (Global):**
  - [ ] Push events: `application.submitted`, `interview.completed`, `evaluation.confirmed`
  - [ ] Retry logic với exponential backoff
  - [ ] Webhook delivery log (success/failure per event)
- [ ] **OAuth2 & Domain validation:**
  - [x] Google Workspace OAuth2 (Google Sign-In) provider integration – đã có trong AuthController
  - [x] Domain parsing, allowed domain verification, database email validation on callback
- [ ] **Slack/Teams Notifications (Global Webhook):**
  - [ ] HR nhận notification khi Evaluation cần Review
  - [ ] HR nhận notification khi Candidate schedule/reschedule

### Phase 12 – Infra & Deploy
- [ ] GitHub Actions CI/CD pipeline (workflows directory tồn tại nhưng rỗng)
- [x] Docker + docker-compose cho dev và production (`docker-compose.yml`, `docker-compose.prod.yml`)
- [ ] Deploy lên Ubuntu VPS
- [x] SSL với Nginx (config đã có)
- [x] Serilog logging setup (rolling file logs, Serilog integration)
- [ ] Grafana monitoring (track latency từng bước pipeline)
- [ ] pg_dump backup schedule

### Phase 13 – Polish & Scale
- [ ] Redis caching (session data, slot availability, frequently accessed evaluations)
- [ ] Health checks
- [ ] Cloudflare CDN (optional)
- [ ] Performance optimization (interview pipeline latency)
- [ ] **Bias Detection & Fairness Report:**
  - [ ] Opt-in demographic data collection
  - [ ] Statistical analysis per Job Posting
  - [ ] Fairness Report cho HR Leader + Super Admin
- [ ] Analytics dashboard cho HR:
  - [ ] Pass rate per Job Posting / Round
  - [ ] Average score distribution
  - [ ] Language proficiency benchmark
  - [ ] Time-to-hire metrics
  - [ ] Cheat Detection aggregate stats

---

## Completed

- [x] 2026-06-26: **RAG Interview Microservice (Python) — Giai đoạn 1: Hybrid RAG (ADR-039 mở rộng).**
  - **Service mới `rag-service/`** (FastAPI + LangChain + LangGraph, Python 3.11): sở hữu toàn bộ pipeline chunk/embed/retrieve/sinh câu hỏi+đánh giá. Backend .NET chỉ orchestrate session/SignalR/persistence, gọi qua HTTP/SSE nội bộ (`http://rag-service:8000`, không expose Nginx).
  - **Hybrid retriever:** dense (pgvector cosine `<=>`) + sparse (Postgres full-text `ts_rank`/`plainto_tsquery`) → hợp nhất Reciprocal Rank Fusion + weighting theo scope (ADR-025: JD/CV & job_posting/round playbook cao, company playbook trung bình). `LangGraph StateGraph` retrieve→generate, để mở rộng CRAG (Giai đoạn 2) & Agentic (Giai đoạn 3).
  - **Endpoints:** `/ingest` (idempotent theo source), `/retrieve`, `/next-question` (SSE stream token), `/analyze-answer`, `/evaluate`, `/detect-language`, `/assess-language`, `/complete-json`, `/embed`, `/health`. Wire JSON camelCase. Mock mode khi thiếu `OPENAI_API_KEY` để test pipeline không cần key.
  - **.NET:** `RagServiceProvider` (impl `IAIProvider`+`IEmbeddingProvider`+`IRagIngestionService`, SSE client); `IRagIngestionService` + `LocalRagIngestionService` fallback in-process; chuyển ingest CV (ApplicationService), Playbook (PlaybookService), JD (JobsController) sang `/ingest`; DI chọn provider theo cờ `AI:Provider` (`rag`|`openai`|`local`). EF migration `AddDocumentChunksFtsIndex` (GIN FTS). `document_chunks` vẫn do EF sở hữu schema.
  - **Hạ tầng:** service `rag-service` trong docker-compose dev/prod; `docker/rag/Dockerfile` (multi-stage); `RAG_SERVICE_URL` + biến `DATABASE_*` dùng chung. pytest: chunker, retriever (RRF/scope weight), schemas, embeddings mock.

- [x] 2026-06-23: **Bảo mật + ổn định GeminiProvider — API key ra khỏi URL/log + retry backoff.**
  - **Lỗi rò rỉ secret:** key Gemini bị nhét vào URL `?key={_apiKey}` ở cả 3 endpoint (CV-JD analyze, CV review, JD extraction) → `HttpClient` logging in nguyên key ra log INF (lộ trong file log/Grafana). Chuyển sang truyền qua header **`x-goog-api-key`**; URL trong log không còn chứa key.
  - **Retry transient:** thêm helper `PostToGeminiAsync` với exponential backoff (3 lần, 0.5s→1s) cho **503 (overload)** / **429 (rate limit)** — giảm rớt phân tích CV khi Flash quá tải (trước đây gọi 1 phát, gặp 503 là fail luôn). Lỗi sau khi hết retry vẫn ném exception → caller trả `Result.Failure` như cũ (UI degrade an toàn).
  - **FE timeout:** retry đồng bộ kéo dài thời gian phản hồi (worst case ~55s) → vượt timeout mặc định 30s của `apiClient` (lỗi "timeout of 30000ms exceeded" khi upload CV). Nâng timeout riêng `AI_REQUEST_TIMEOUT_MS=120000` cho `profileService.uploadCv` và `getCvMatch` (các call chạy Gemini đồng bộ).
  - **Đọc body lỗi 503:** thay `EnsureSuccessStatusCode` bằng đọc response body → log/exception kèm thông điệp thật của Gemini ("model is overloaded").
  - **Fallback chéo nhà cung cấp Gemini → OpenAI GPT-4o-mini:** thêm `IAIProvider.CompleteJsonAsync` (json_object mode) + `GetAnalysisJsonAsync` trong GeminiProvider bọc kết quả OpenAI vào envelope giống Gemini để khối parse dùng chung. Áp cho cả 3 hàm (CV-JD analyze, CV review, JD extraction). Khi Gemini 503/hết retry → tự chuyển OpenAI; chỉ khi cả hai cùng lỗi mới degrade. **Cần cấu hình OpenAI thật** (`AI:Provider` ≠ "local" + `AI:OpenAI:ApiKey`/`OPENAI_API_KEY`) — hiện đang `local` (mock) nên fallback chưa kích hoạt.
  - **Nhãn provider động trên UI:** `GetAnalysisJsonAsync` trả kèm tên provider ("Gemini" | "GPT-4o-mini"); `CvReviewResultDto.Provider` → `CvReviewResponse.ReviewedBy` (lưu trong `cv_review_json` + trả FE). Thẻ "Đánh giá CV bởi AI (...)" ở `ProfilePage` hiển thị đúng AI đã chấm (`review.reviewedBy ?? 'Gemini'`).
  - **Provider cho cả CV-JD analysis:** `CvJdAnalysisResultDto.Provider` set từ `GetAnalysisJsonAsync`; `CvJdAnalysisService` lưu `CvJdAnalysis.AiModel` = provider thật (thay hardcode "gemini-2.5-flash") → HR/recruiter nhận qua entity sẵn có. Candidate cv-match: `CvMatchAnalysisDto.ReviewedBy` (map từ `cached.AiModel`) → FE `CvMatchAnalysis.reviewedBy`, JobDetailPage hiện "Phân tích bởi AI (...)".
  - **CV upload chỉ PDF/DOCX:** bỏ `.txt` (yêu cầu) ở `CvAnalysisController`; đồng bộ cả `CandidatePortalController.UploadCv`. **`.doc` legacy KHÔNG hỗ trợ** — đã khảo sát: NPOI (mọi bản, kể cả 2.5.6/2.8.0) đã bỏ HWPF; FreeSpire.Doc chèn watermark vào text; Aspose trả phí; LibreOffice headless nặng phụ thuộc. Kết luận: chấp nhận PDF/DOCX (chờ user quyết nếu muốn hỗ trợ .doc qua LibreOffice/commercial).
  - **Gom AI provider 1 nơi:** `git mv` `OpenAIProvider.cs` từ `Services/` → `AI/` (cùng `GeminiProvider`), đổi namespace `ARISP.Infrastructure.Services` → `.AI`, thêm using ở `Program.cs`.
  - **Lưu ý vận hành:** key Gemini cũ đã lộ trong log → cần rotate ở Google AI Studio. Build BE 0 lỗi, FE `tsc` sạch.
- [x] 2026-06-23: **Tooling — bộ skill `.claude/skills/` (arisp-feature, arisp-screen) + sửa config FE + dọn lockfile mồ côi + MCP Cloudflare/GitHub.**
  - Tạo skill `arisp-feature` (vertical-slice Clean Architecture + reference.md template) và `arisp-screen` (design system từ code đã redesign). React Router future flags (`v7_startTransition`, `v7_relativeSplatPath`). `.eslintrc.cjs` thiếu `module.exports`; `tsconfig.json` bỏ `baseUrl` deprecated + path tương đối. Xoá `package-lock.json` mồ côi ở root. Thêm MCP `cloudflare-bindings` (SSE) + `github` (PAT header) vào `.mcp.json`.
- [x] 2026-06-22: **Đồng bộ các bảng phân tích HR Dashboard sang recharts (nhất quán).**
  - **"Chất lượng nguồn ứng viên" (phân bố điểm match)** → `BarChart` recharts: cột màu theo tone (emerald→red), trục Y số nguyên, tooltip "{khoảng} điểm · N hồ sơ · X%". Bỏ bar % thủ công (`TONE_BAR`).
  - **"Hiệu suất Recruiter"** (bảng) → horizontal `BarChart` (ứng viên/recruiter, sắp giảm dần), tooltip hiện Tin/Ứng viên/Đã tuyển. Bỏ helper `initialsOf`.
  - Giữ nguyên: "Phễu tuyển dụng" (funnel có % chuyển đổi), "Lấp đầy chỉ tiêu" (progress) — vốn là pattern phù hợp, không phải distribution chart. Màu/tooltip dùng chung style với Trend. FE build 0 lỗi.
- [x] 2026-06-22: **Sửa bảng "Xu hướng ứng tuyển" (HR Dashboard) — chuyên nghiệp + tooltip.**
  - Bản bar cũ "tàng hình" (chiều cao % không ăn trong lưới kéo–thả); bản SVG tự chế bị méo chấm khi kéo giãn (`preserveAspectRatio="none"`) và không có số liệu khi hover.
  - Thay bằng **recharts** `AreaChart` trong `ResponsiveContainer`: tự co giãn không méo, trục Y số nguyên (`allowDecimals=false`), trục X ngày (auto thưa nhãn), **tooltip tuỳ biến** hiện "Ngày d/m · N hồ sơ", chấm tròn + activeDot, vùng tô gradient tím brand. recharts nằm trong chunk lazy của DashboardPage (không phình bundle chính). FE build 0 lỗi.
- [x] 2026-06-22: **Phase B4 — Chuẩn hoá Interview Code 6 ký tự + gate tiến trình đa vòng theo HR confirm.**
  - **Code 6 ký tự alphanumeric** (ADR-016): `InterviewCodeService.GenerateSecureRandomCode` bỏ dạng `ARX-7K2P` (7 ký tự + gạch) → 6 ký tự liền từ charset không nhầm lẫn. Khớp UI kiosk 6 ô sẵn có; validate ẩn danh giữ nguyên.
  - **Auto-progression đúng flow:** `SubmitHrReviewAsync`/`TriggerAutoProgressionAsync` khi HR **confirm Pass** vòng N (real) & còn vòng N+1 → tạo `InterviewInvite` vòng N+1 (token hoá) + email **link chọn lịch** env-aware `{base}/portal/schedule/{appId}?token=&round=` (thay link login hardcode `https://arisp.portal/...`). Đảm bảo "mỗi vòng cần duyệt" + không phỏng vấn 2 vòng thật cùng ngày (vòng kế chỉ mở sau khi confirm). `InterviewController.ConfirmReview` truyền base URL từ config.
  - Build BE 0 lỗi.
- [x] 2026-06-22: **Phase B3 — Phỏng vấn thử theo VÒNG (1 lượt/vòng) thay cho 1 lượt/hồ sơ.**
  - `InterviewService.StartSessionAsync`: guard practice đổi từ cờ `PracticeSessionUsed` (toàn hồ sơ) → kiểm tra tồn tại phiên `practice` của đúng (application, round). `CheckPracticeEligibilityAsync(appId, round)` + endpoint `practice-eligibility/{id}?round=` cũng theo vòng. Cờ cũ giữ để tương thích, không còn dùng làm điều kiện chặn.
  - **Cờ hiển thị `PracticeAvailable` (CandidatePortal GetApplications) chuyển theo VÒNG:** dựa trên vòng đang hoạt động (`activeRound` = vòng invite mới nhất) + chưa có phiên practice/real của vòng đó, thay cho cờ `PracticeSessionUsed` toàn hồ sơ (vốn khoá practice vòng 2+). Trả thêm `ActiveRound`; FE thêm `activeRound` vào `MyApplicationItem`, nút "Bắt đầu phỏng vấn thử" gắn `?round=` + nhãn "Vòng N".
  - **Mở practice ngay khi mời:** `SendInterviewInviteAsync` nâng cả `invited`→`screening` (không chỉ `cv_submitted`) để `PracticeAvailable=true` sau khi recruiter bấm Mời. Thêm nút "Vào cổng ứng viên để phỏng vấn thử" trên màn đặt lịch thành công.
  - **Vị trí thi thử:** nút trong Cổng ứng viên (đăng nhập) → /candidate/applications → thẻ hồ sơ → "Bắt đầu phỏng vấn thử". (Trang practice hiện vẫn là UI mock — pipeline AI đầy đủ ở Phase 7.)
  - Build BE 0 lỗi, FE `tsc` sạch.
- [x] 2026-06-22: **Phase B2 — Lời mời theo vòng + ứng viên chọn lịch (booking) với chống trùng.**
  - **Entity mới `InterviewInvite`** (DbSet + `CREATE TABLE IF NOT EXISTS interview_invites` + index lúc khởi động): token magic-link phạm vi hẹp theo (application, round), lưu hash SHA256, có `ScheduledAt`.
  - **`SendInterviewInviteAsync` (rewrite):** bỏ hardcode `localhost`; nhận `frontendBaseUrl` từ config (`Frontend:CandidateBaseUrl` → fallback `Authentication:AdminFrontendUrl`); tạo InterviewInvite + email **link CHỌN LỊCH** `{base}/portal/schedule/{appId}?token=&round=`; vô hiệu invite cũ chưa dùng; chuyển `cv_submitted→screening`. Email nêu rõ phỏng vấn thật dùng Interview Code tại văn phòng. `ApplicationsController.SendInvite` truyền base URL + `?round`.
  - **`CandidateScheduleController`** (`api/schedule/...`, AllowAnonymous + xác thực bằng token lời mời HOẶC Candidate JWT sở hữu hồ sơ): `GET {appId}/slots?round&token` (slot còn trống, tương lai), `POST {appId}/book` — enforce **1 slot/lần đặt vòng**, **không trùng khung giờ** với booking scheduled khác của ứng viên (kể cả JD khác), tăng `BookedCount`, set `invite.ScheduledAt`; `GET candidate/schedule` (sắp tới/đã qua).
  - **FE:** trang công khai `candidate/SchedulePage.tsx` (`/portal/schedule/:applicationId`, đọc `token`+`round` từ query) — gom slot theo ngày, chọn + xác nhận; `scheduleService` nối `getOpenSlots/book`.
  - **Siết chống overbooking (race) ở tầng DB:** đặt chỗ dùng `UPDATE ... SET booked_count=booked_count+1 WHERE id=@id AND booked_count<capacity` (nguyên tử, row-lock) — affected=0 ⇒ slot vừa đầy, từ chối; có bù trừ decrement nếu lưu booking lỗi. Thêm **partial unique index** `ux_interview_bookings_app_round_scheduled (application_id, round_number) WHERE status='scheduled'` chặn double-book cùng vòng. Không mutate entity slot đang được EF theo dõi (tránh ghi đè đếm).
  - Build BE 0 lỗi, FE `tsc` sạch. **Cần restart BE** để tạo bảng `interview_invites` + index unique booking.
- [x] 2026-06-22: **Phase B1 — Recruiter cấu hình lịch phỏng vấn (Availability Slots CRUD).**
  - **BE `ScheduleController`** (`api/schedules/slots`): GET (lọc theo job+vòng), POST (tạo slot, validate giờ/tương lai/capacity), DELETE (chỉ khi `BookedCount==0`), PATCH `/capacity` (không nhỏ hơn số đã đặt). Phân quyền: chủ tin hoặc HrAdmin/SuperAdmin. DTO `ScheduleDTOs.cs` (`AvailabilitySlotResponse` có `IsAvailable`, `CreateSlotRequest`, `UpdateSlotCapacityRequest`).
  - **FE:** nối thật `scheduleService` (getSlots/createSlot/deleteSlot/updateSlotCapacity + stub getOpenSlots/book/getMySchedule cho B2); trang mới `recruiter/JobScheduleConfigPage.tsx` (route `/recruiter/my-jobs/:id/schedule`) — chọn vòng, thêm/xoá slot, +/- capacity; nút "Lịch phỏng vấn" ở recruiter JobDetailPage. Type `AvailabilitySlot` thêm `jobPostingId/roundNumber/isAvailable`.
  - Build BE 0 lỗi, FE `tsc` sạch.
- [x] 2026-06-22: **Xem CV/JD DOCX inline (không buộc tải về) — Phase A của redesign flow phỏng vấn.**
  - **Gốc rễ:** mọi điểm "Xem CV/JD" là `<a target=_blank>` → trình duyệt không render DOCX (chỉ tải về). PDF thì xem được.
  - **Component mới `DocumentViewer.tsx`** (`frontend/src/components/document/`): `DocumentViewerProvider` (mount 1 lần ở `App.tsx`) + hook `useDocumentViewer().openDocument(url, fileName)`. Modal portal: PDF → `<iframe>`; DOCX → `fetch` bytes + `renderAsync` của thư viện **`docx-preview`** (client-side, không gửi file ra ngoài); ảnh → `<img>`; khác → tải về. Luôn có nút "Tải về" + ESC để đóng.
  - **Thay 8 điểm xem** sang mở modal: recruiter `CandidatesPage`/`CandidateDetailPage`/`JobDetailPage` (CV + JD gốc), hr `CandidateDetailPage`/`JobPostingDetailPage` (JD gốc + JD đã đóng dấu), candidate `ApplicationsPage`/`ProfilePage` (CV hồ sơ).
  - FE `tsc --noEmit` sạch. **Lưu ý vận hành:** fetch DOCX từ R2 presigned cần bật CORS (GET) cho origin FE; dev/local cùng origin nên OK.
  - **Bug trạng thái:** badge ở `JobPostingDetailPage` (HR) chỉ map `active/paused/draft`, dồn mọi trạng thái khác (gồm `pending`) thành "Đã đóng" → tin chờ duyệt hiển thị sai. Đã thay bằng map đầy đủ (`STATUS_LABEL/STATUS_BADGE`: draft/pending/active/rejected/closed/archived).
  - **Thiếu UI duyệt trên màn chi tiết:** thêm panel "Tin đang chờ bạn duyệt" (Duyệt & đóng dấu / Từ chối kèm modal lý do) chỉ hiện khi `status='pending'`; banner đã-từ-chối (lý do) và banner đã-duyệt (người duyệt + thời gian). Gọi `PATCH /jobs/{id}/status` sẵn có.
  - **Hiển thị file JD cho HR:** thêm card "Tài liệu JD" — tải JD gốc + JD đã đóng dấu (nếu có). `GetJobById`/`UpdateJobStatus` resolve storageKey → URL cho cả `JdFileUrl` lẫn `SignedJdFileUrl` (staff). (JD gốc đã bắt buộc khi recruiter tạo tin — giữ nguyên.)
  - **Đóng dấu duyệt (visual stamp, không phải PKI):** package `PdfSharpCore` (chạy Linux nhờ FontResolver mặc định). `IJdStampService`/`JdStampService` vẽ con dấu "ĐÃ DUYỆT — HR LEADER + tên người duyệt + ngày + chữ ký" lên góc trên-phải trang đầu PDF. `UpdateJobStatus` khi `pending→active` (admin): ghi `ApprovedByUserId/ApprovedAt/ApproverName`, đọc JD gốc → stamp → lưu file mới vào `SignedJdFileUrl`. Thất bại đóng dấu **không chặn** việc duyệt (log warning).
  - **DOCX:** không convert giữ định dạng (tránh phụ thuộc nặng/commercial trên Linux). Thay vào đó `StampApprovalFromTextAsync` parse text DOCX (`IDocumentParserService`, fallback `JobDescription`) → render PDF mới (tự xuống dòng + phân trang) + đóng dấu trang đầu. File đã duyệt luôn là PDF kể cả khi gốc DOCX.
  - **Schema:** thêm cột `approved_by_user_id/approved_at/approver_name/signed_jd_file_url` trên `job_postings` qua `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` lúc khởi động (cùng pattern block index). Entity + DTO + type FE cập nhật.
  - Build BE 0 lỗi, FE `tsc --noEmit` sạch. **Cần restart BE** để chạy ADD COLUMN + nạp `PdfSharpCore`.
- [x] 2026-06-22: **Sửa timeout 30s màn Hồ sơ ứng tuyển (candidate) + thiếu index FK.**
  - **Gốc rễ:** migration `InitialCreate` gần như **không tạo index FK** → mọi truy vấn theo `candidate_account_id`/`application_id`... bị **seq scan** toàn bảng. Cộng thêm `applications` nạp cả `CvText` (TOAST lớn) → `/portal/applications` timeout ~30s, notifications ~7s.
  - **Index (Program.cs):** ensure idempotent `CREATE INDEX IF NOT EXISTS` lúc khởi động cho 13 cột lọc nóng: applications(candidate_account_id / candidate_email / job_posting_id / cv_jd_analysis_id), notifications(candidate_account_id), saved_jobs(candidate_account_id), interview_sessions(application_id), evaluations(session_id / application_id), hr_reviews(evaluation_id), interview_codes(application_id), interview_bookings(application_id), job_postings(created_by_user_id). Mỗi câu chạy riêng (lỗi 1 cái không chặn cái khác).
  - **CandidatePortalController.GetApplications:** liên kết hồ sơ theo email bằng **SQL UPDATE** (bỏ load-rồi-Update); projection nhẹ apps (bỏ CvText/CoverLetter/Demographic), jobs (bỏ JobDescription), analyses (Id+score), sessions + evaluations (bỏ JSON).
  - **SyncNotificationsAsync (hot path notifications):** apps/jobs/sessions/evaluations chuyển projection nhẹ.
  - Build BE 0 lỗi. **Lần restart đầu sẽ tạo index** (một lần, có thể hơi lâu trên bảng lớn), sau đó nhanh hẳn.
- [x] 2026-06-22: **Rà & sửa các endpoint còn `GetAllAsync` trên bảng nặng (projection SQL).**
  - `EvaluationService.GetEvaluationsAsync`: bỏ nạp toàn bộ Evaluation (cột JSON criterion/question/reasoning/cheat/language) + reviews + apps(CvText) + jobs(JobDescription) → projection lite `EvalLite/ReviewLite/EvalAppLite` + title-only.
  - `EvaluationService` (theo application): `GetAllAsync<HrReview>` → `FindAsync` theo evalIds của hồ sơ.
  - `InterviewService.GetSessionsForHrAsync`: evaluations chuyển projection nhẹ (bỏ JSON).
  - `JobsController.GetAdminJobs`: project thẳng sang `JobPostingListItemResponse` (bỏ JobDescription/ScoringRubric/persona/JD file); đếm ứng viên bằng **SQL GROUP BY** thay vì nạp Application(CvText).
  - `AdminController.GetStats`: bỏ nạp toàn bộ User+CandidateAccount → project (Role,IsActive) + `CountAsync` cho candidates/pending.
  - `PlaybooksController.GetPlaybooks`: project lite (bỏ cột parsedText lớn), lọc scope ở SQL.
  - Còn lại cố ý giữ: `CvJdAnalysisService.ClearAllCacheAsync` (admin hiếm), SystemSetting/AccountRequest (bảng nhỏ). Build BE 0 lỗi.
- [x] 2026-06-22: **Bật connection pooling + retry (gốc rễ latency & timeout auth Supabase).**
  - Stack trace lỗi migration cho thấy `Npgsql.UnpooledDataSource` → **pooling đang TẮT** → mỗi query mở connection mới (TLS+auth tới Supabase ở xa ~1–1.5s), gây vừa chậm vừa timeout auth thoáng qua (kể cả `MigrateAsync` lúc khởi động).
  - `Program.cs`: override tham số pool qua `NpgsqlConnectionStringBuilder` (Pooling=true, MinPoolSize=2, MaxPoolSize=20, ConnectionIdleLifetime=300, KeepAlive=30, Timeout=30, CommandTimeout=60) — giữ host/credential từ user-secret. Thêm `EnableRetryOnFailure(3)` cho lỗi mạng thoáng qua. Migration khởi động bọc retry 3 lần (delay 3s). An toàn vì repo không dùng explicit transaction (chỉ `ExecuteSqlRawAsync` đơn lệnh) và Npgsql tắt auto-prepare mặc định (hợp pgbouncer).
  - **Lưu ý:** nếu connection string đang trỏ Supabase transaction pooler (cổng 6543), server chạy lâu dài nên dùng direct/session (5432) để pooling client phát huy tối đa.
- [x] 2026-06-22: **Dashboard HR — đọc DB song song (6.6s → ~2.5s).**
  - Sau projection, `/dashboard/hr` vẫn ~6.6s vì **7 query tuần tự** tới Supabase ở xa (mỗi cái 150–1800ms round-trip). `IUnitOfWork`/`ARISPDbContext` là scoped → thêm `RunScopedAsync<T>` (mỗi query 1 DI scope = DbContext/connection riêng) và chạy **song song**: 5 bảng nền (jobs/apps/sessions/evaluations/reviews) `Task.WhenAll` → latency = max (~1.8s) thay vì sum (~5s); 2 query phụ thuộc (cv_jd_analyses + users) cũng song song. Build BE 0 lỗi.
  - **Lưu ý vận hành:** `ERR_CONNECTION_REFUSED` trên console là do backend **chưa lắng nghe cổng 5000** lúc FE gọi (đang restart) — không phải bug; backend lên là hết (log cho thấy ngay sau đó trả 200).
- [x] 2026-06-22: **Tối ưu latency C+D — projection SQL (sửa timeout 43s) + code-split bundle.**
  - **Nguyên nhân timeout** `/api/applications` (43s, Npgsql read timeout): `GetAllApplicationsAsync` nạp **toàn bộ entity gồm `CvText`** (text CV rất lớn) qua Supabase; dashboard cũ cũng `GetAllAsync` 5 bảng đầy đủ (Application.CvText, Evaluation JSON lớn).
  - **C — projection ở tầng SQL:** thêm `IRepository.QueryAsync<TResult>(shaper)` + `CountAsync(predicate)` (impl Infrastructure, EF dịch SQL; **không leak EF lên Application** vì shaper chỉ dùng `IQueryable`/LINQ). `ApplicationService` (GetAll/ByJob/ForCreator) chỉ `Select` cột nhẹ (bỏ `CvText/CoverLetter/DemographicData`), CvText=null cho list (chi tiết vẫn đủ ở GetApplicationById). `DashboardController` chuyển 5 `GetAllAsync` → `QueryAsync` projection lite (JobLite/AppLite/EvalLite/ReviewLite, không cột text/JSON lớn).
  - **D — code-split:** `App.tsx` lazy-load toàn bộ page (`React.lazy` + `Suspense` fallback spinner); layout/guard giữ eager. **Bundle chính 1.113kB → 455kB** (gzip 144kB); `react-grid-layout` tách vào chunk HR DashboardPage (117kB) chỉ tải khi vào dashboard.
  - Build BE 0 lỗi, `npm run build` FE xanh (mỗi page 1 chunk 5–36kB).
- [x] 2026-06-22: **Tối ưu latency HR Dashboard — gộp 3 request → 1 + react-query cache.**
  - **Trước:** dashboard chờ song song 3 call rồi mới render: `/dashboard/hr` (BE đã load full jobs+apps+sessions+evaluations+reviews) **+** `getAdminJobPostings` (full jobs lần 2) **+** `getApplications` (full apps lần 2) — trùng lặp nặng, payload toàn bộ rows, không cache.
  - **BE:** mở rộng `HrDashboardResponse` với `Analytics` (matchBuckets+avgMatch, trend 14 ngày, recruiters leaderboard, vacancy fill), `TopJobs`, `PendingJobs`+`PendingJobsCount` — tính ngay từ data controller **đã load sẵn** (chỉ thêm 1 batch load `User` cho tên recruiter). DTO: `MatchBucketDto/TrendPointDto/RecruiterStatDto/VacancyJobDto/DashboardJobDto/PendingJobDto/HrAnalyticsDto`.
  - **FE:** DashboardPage chỉ còn **1 request** (`getHrOverview`) qua **react-query** (`queryKey ['hr-dashboard']`, staleTime 5' → vào lại hiện tức thì, refetch nền không chớp skeleton). Bỏ `getAdminJobPostings`+`getApplications`+`buildAnalytics` client; widget đọc thẳng `data.analytics/topJobs/pendingJobs`. Payload còn aggregate nhỏ thay vì toàn bộ rows.
  - Build BE 0 lỗi, `npm run build` FE xanh. (Đề xuất tiếp theo chưa làm: BE dùng `Count()/GroupBy` dịch SQL thay `GetAllAsync` 5 bảng; code-split bundle 1.1MB.)
- [x] 2026-06-22: **Fix build + lỗi runtime react-grid-layout.**
  - `tsconfig.json`: `ignoreDeprecations` `"6.0"` (TS 5.9.3 báo TS5103 invalid) → `"5.0"` ⇒ bước `tsc` trong `npm run build` chạy được.
  - Dọn 13 lỗi `TS6133` (import/biến thừa) chặn build, ở các file có sẵn: `shared/index`, `auth/CandidateLoginPage`, `auth/CandidateRegisterPage`, `hr/SettingsPage`, `recruiter/SettingsPage`, `kiosk/KioskPage`, `interview/InterviewRoomPage` (bỏ `handleNextQuestion` chết + setter không dùng), `hr/EvaluationReviewPage` (bỏ state chết `detailLoading/detailError`).
  - **`process is not defined`** khi kéo widget (react-grid-layout/react-draggable đọc `process.env.NODE_ENV`): thêm `define: { 'process.env.NODE_ENV': JSON.stringify(mode) }` trong `vite.config.ts` (mode-aware) + shim `globalThis.process` trong `main.tsx`. Bundle prod còn **0** token `process.env.NODE_ENV`. `npm run build` xanh trọn vẹn.
- [x] 2026-06-22: **HR Dashboard — sắp xếp lại theo ưu tiên + widget phân tích kéo–thả.**
  - Bỏ badge `5` mock ở sidebar mục "Đánh giá" (HrLayout); `sidebarItems` thêm type `badge?: number`.
  - **Zone "Ưu tiên xử lý" ghim trên đầu** (đúng 2 mục tiêu chính HR Leader): **Tin chờ bạn duyệt** (jobs `status='pending'`, top 3 + CTA `/hr/jobs/pending`) và **Verdict chờ xác nhận** (`pendingReviews` + danh sách ứng viên đã có verdict + CTA `/hr/evaluations`). Đặt **trên** KPI.
  - **KPI** đổi card Pending → "Đã tuyển" (hết trùng với zone ưu tiên).
  - **Các bảng phân tích thành widget dashboard kéo–thả 2 chiều + đổi cỡ** (`react-grid-layout` — **thêm dependency, user duyệt** vì framer-motion `Reorder` chỉ mượt 1 trục): lưới 12 cột, mỗi widget có tay nắm `.widget-drag-handle` (`GripVertical`) để kéo, kéo góc dưới–phải để resize; **bảng ít data chiếm ít cột/hàng** (vacancies/trend/match nhỏ, funnel/tin/ứng viên rộng). Bố cục lưu `localStorage` theo `user.id` (`arisp:hr-dash-layout:*`), `reconcileLayout` tự bổ sung widget mới, nút **Khôi phục bố cục**. Theme placeholder/resize-handle (đỏ mặc định → tông brand, dark mode) trong `index.css`. 7 widget: Phễu · Chất lượng nguồn · Xu hướng 14 ngày · Hiệu suất Recruiter · Lấp đầy chỉ tiêu · Tin tuyển dụng · Ứng viên gần đây. Bỏ cụm "Cần làm"/"AI Insight" cũ (đã gộp vào zone ưu tiên). `vite build` xanh.
- [x] 2026-06-22: **HR Leader — skeleton loading + fix sidebar cố định khi scroll.**
  - **HrLayout:** root `min-h-screen` → `h-screen overflow-hidden`, `aside` thêm `h-screen`, nav desktop `overflow-y-auto` → sidebar + topbar đứng yên, chỉ `<main>` cuộn (trước đây cả body cuộn kéo theo sidebar).
  - **`hr/_skeletons.tsx` mới** (dùng lại `ui/Skeleton` shimmer): `HrStatsSkeleton`, `JobListSkeleton`, `CandidatesTableSkeleton`, `SessionListSkeleton`, `EvaluationListSkeleton`, `CardGridSkeleton`, `RequestListSkeleton`, `HrDashboardSkeleton` (body-only) — khớp bố cục từng màn.
  - Thay `LoadingSpinner`/`Loader2` page-level bằng skeleton: Dashboard, Jobs, Candidates, Phiên phỏng vấn, Tin chờ duyệt, Đánh giá; nâng block `animate-pulse` của Playbooks & Nhóm HR lên shimmer. Giữ `Loader2` cho action inline (mời, duyệt, xoá, submit). Type-check phần thay đổi 0 lỗi mới (2 cảnh báo TS6133 `detailLoading/detailError` ở EvaluationReviewPage là dead state có sẵn từ trước).
- [x] 2026-06-21: **HR Dashboard — khu Phân tích tuyển dụng (analytics thật, không thêm thư viện).** Tính client-side từ `getAdminJobPostings` + `getApplications`: (1) **Phễu** kèm tỉ lệ chuyển đổi **từng bước** + tỉ lệ tuyển thành công tổng; (2) **Phân bố điểm match CV–JD** (5 mức + điểm TB); (3) **Xu hướng ứng tuyển 14 ngày** (bar chart SVG/CSS); (4) **Hiệu suất Recruiter** (tin · ứng viên · tuyển — HR quản lý toàn bộ Recruiter); (5) **Lấp đầy chỉ tiêu** (`vacancies`): tổng đã tuyển/chỉ tiêu + theo từng tin. Vẽ bằng Tailwind + SVG, không thêm dependency. Type-check 0 lỗi.
- [x] 2026-06-21: **HR Leader chỉnh sửa: logout + dashboard theo job + trường `vacancies` (chỉ tiêu tuyển).**
  - **HrLayout:** bỏ nút Đăng xuất ở sidebar trái; fix dropdown logout góc phải (thay overlay `fixed inset-0 z-30` — vốn chặn click do stacking context — bằng outside-click qua `ref` + listener `mousedown`).
  - **HR Dashboard:** thêm mục **Tin tuyển dụng** (toàn bộ Recruiter, sắp theo số ứng viên) hiển thị **trước** danh sách ứng viên. (HR đã dùng `getAdminJobPostings`/`getApplications` không scope → thấy hết.)
  - **Trường mới `JobPosting.Vacancies`** (số lượng cần tuyển / chỉ tiêu): entity + `CreateJobPostingRequest`/`JobPostingResponse`/`JobPostingListItemResponse` + persist Create/Update; migration `20260622000000_AddVacanciesToJobPosting` (cột `vacancies` int nullable, tự apply lúc khởi động). FE: type `JobPosting`/`CreateJobPostingRequest`; input "Số lượng cần tuyển" ở form tạo/sửa tin; recruiter **Job Detail** hiện "Tuyển X/Y" + banner gợi ý **Đóng tin** khi tuyển đủ (X = ứng viên `pass`); HR Job Detail hiện "Chỉ tiêu". Build BE 0 lỗi (copy-lock do API đang chạy), type-check FE sạch.
- [x] 2026-06-21: **HR Leader workspace — Playbook + Nhóm HR (account requests) + Chi tiết ứng viên** [Phase 4b/ADR-025, ADR-041].
  - **BE:** `PlaybooksController` mới (`api/playbooks`, policy HrManagement): `POST` upload multipart (parse text → lưu file → `PlaybookService.UploadPlaybookAsync` chunk+embed vào `document_chunks` cho RAG), `GET` list (lọc theo scope, kèm tên người upload), `DELETE` (soft delete). API compile 0 lỗi (copy-lock do tiến trình đang chạy).
  - **FE services:** `playbookService` (getPlaybooks/uploadPlaybook/deletePlaybook) + `accountRequestService` (getMine/create).
  - **FE màn (thay stub/mock → data thật, theme sáng/tối):**
    - **Playbook** (`/hr/playbooks`): upload modal (file PDF/DOCX/TXT/MD + scope org/job_posting/round + documentType + chọn tin/vòng) · list theo scope · xoá · badge "Đã nạp RAG".
    - **Nhóm HR** (`/hr/team`): gửi yêu cầu tạo tài khoản lẻ/hàng loạt + **import CSV** (tải template) + theo dõi trạng thái (pending/approved/rejected + lý do) — hoàn tất phần FE còn nợ của ADR-041.
    - **Chi tiết ứng viên HR** (`/hr/candidates/:id`): data thật (hồ sơ + đánh giá theo application + phiên phỏng vấn + thao tác gửi magic link / cấp Interview Code / xem CV).
  - (HrLayout, Dashboard, Jobs, PendingJobs, Candidates, Evaluations w/ override, Interviews, JobDetail của HR đã có data thật từ trước.) Type-check 0 lỗi ở file mới.
- [x] 2026-06-21: **Recruiter workspace — Cấp Interview Code + redesign Ứng viên / Đánh giá / Phỏng vấn** [ADR-042].
  - **BE:** `ApplicationService.GetApplicationsForCreatorAsync` (ứng viên thuộc tin của recruiter, kèm matchScore) + `GET /applications?mine=true` (ICurrentUserService) + resolve CV URL ở `GetApplications`/`GetApplicationById`. (Endpoint cấp/validate mã & sessions đã có sẵn từ trước.) Application build 0 lỗi (API copy-lock do tiến trình đang chạy — biên dịch sạch).
  - **FE:** `applicationService.getApplications(mine)` + `getHrApplicationById`; `interviewService.generateCode/getCodesByJob` + type `InterviewCodeSummary`; `evaluationService.getEvaluationsByApplicationId`. Helpers `_jobUi` thêm verdict/sessionStatus/scoreColor.
    - **Cấp mã phỏng vấn (mới)** `/recruiter/code`: list ứng viên của recruiter → sinh Interview Code 1-lần + copy + đếm hết hạn; thêm nav "Cấp mã phỏng vấn".
    - **Ứng viên** redesign (data thật, scoped `mine`): search + filter trạng thái + matchScore + link CV + vào chi tiết.
    - **Chi tiết ứng viên** redesign (data thật): hồ sơ + báo cáo đánh giá theo application + phiên phỏng vấn (lọc theo applicationId) + thao tác (gửi magic link, cấp Interview Code, xem CV).
    - **Phỏng vấn** redesign: `getHrSessions` lọc theo tập ứng viên của recruiter + search/filter + verdict/trạng thái/bản ghi.
    - **Đánh giá** redesign **chỉ-xem** (Recruiter không có quyền Confirm/Override — thuộc HR Leader): gom đánh giá theo ứng viên của recruiter + modal chi tiết read-only.
  - Toàn bộ chuyển sang theme sáng/tối (bỏ glassmorphism cũ + mock data). Type-check 0 lỗi ở file mới.
- [x] 2026-06-21: **Recruiter workspace — cụm màn Job end-to-end** [ADR-042].
  - **BE:** `IGeminiProvider.ExtractJobFromJdAsync` + impl `GeminiProvider` (Gemini 2.5 Flash, PDF inline/DOCX fallback) → DTO `JdExtractionResultDto`/`AnalyzeJdResponse`. `JobsController`: `POST /jobs/analyze-jd` (multipart: parse + lưu file + Gemini auto-fill), `GET /jobs/admin?mine=true` (lọc theo người tạo), `GET /jobs/{id}/applications` (owner-or-admin, resolve CV URL). `ApplicationService.GetApplicationsByJobAsync` (kèm matchScore). Thêm `JdFileUrl/Name/Format` vào CreateJobPostingRequest/Response + persist Create/Update; resolve JD URL cho staff trong GetJobById. Build 0 lỗi.
  - **FE:** `RecruiterLayout` → `WorkspaceLayout` dùng chung (theme sáng/tối). Redesign + data thật: **Dashboard** (lưới tin của tôi + KPI + nháp/bị từ chối), **Tin tuyển dụng** (list + filter trạng thái), **Job Detail mới** (`/recruiter/my-jobs/:id`: phễu ứng viên + danh sách ứng viên theo job + gửi magic link + gửi duyệt/đóng tin), **Create/Edit** (`/recruiter/my-jobs/:id/edit`) với card upload & phân tích JD auto-fill (bắt buộc JD khi tạo). `jobService` (mine/applications/analyzeJd) + types + helpers `_jobUi`/`_skeletons`. App.tsx tách route detail vs edit. Type-check sạch ở toàn bộ file mới.
  - **Chưa làm:** màn HR Leader duyệt tin (API sẵn), màn cấp Interview Code, redesign Candidates/Evaluations/Interviews của Recruiter.
- [x] 2026-06-21: **Sync tài liệu `.ai/` theo thay đổi gần đây.** `context.md`: bổ sung vòng đời tài khoản staff (ADR-041 — yêu cầu tạo HR→SA + khóa kèm lý do) vào Registration Flow + System Admin; thêm dòng **File Storage** (ADR-036, R2) vào bảng Tech Stack. `glossary.md`: thêm Account Request, Lock Reason, Unlock Appeal, Storage Key, `IFileStorageService`, `BatchId`. `coding-rules.md` giữ nguyên (chỉ chứa quy ước, không bị ảnh hưởng).
- [x] 2026-06-21: **Vòng đời tài khoản staff — Yêu cầu tạo (HR→SA) + Khóa có lý do** [ADR-041].
  - **BE:** entity `AccountRequest` (`account_requests`) + migration `AddAccountRequestsAndUserLockReason` (kèm cột `users.lock_reason`); thêm `ARISPDbContextFactory` (design-time). `AccountRequestsController` (`api/hr/account-requests`, policy HrManagement): HR gửi yêu cầu lẻ/bulk + GET theo dõi. `AdminController`: `GET /admin/account-requests`, `POST .../{id}/approve` (tạo User active + email), `.../reject` (lý do). `deactivate` bắt buộc lý do → `User.LockReason`; `activate` xóa lý do. `stats` đổi `pendingUsers`→`lockedUsers` + `pendingRequests`. Tất cả ghi AuditLog.
  - **FE:** `adminService` thêm account-request methods + `deactivateUser(id, reason)` + `lockReason`. "Duyệt User mới" → hiển thị `account_requests` pending (duyệt / từ chối kèm modal lý do), không còn lẫn tài khoản khóa. Dashboard: block "Yêu cầu tạo tài khoản" + stat "YC chờ duyệt"/"Bị khóa". Users: khóa mở modal nhập lý do, hiển thị "Bị khóa" + lý do, stat "Bị khóa".
  - **Chưa làm:** màn HR Leader gửi request + CSV; kháng cáo mở khóa (phase sau, khi dựng khu HR Leader).
- [x] 2026-06-21: **Kết nối `allowed_email_domains` (UI Settings) vào luồng OAuth thật.** Trước: `AuthController.ExternalCallback` chỉ đọc domain từ appsettings/env (`Authentication:AllowedDomains`), nên Settings UI lưu vào `system_settings` mà không có tác dụng. Nay đọc **DB trước** (Super Admin quản lý qua UI), fallback config; chuẩn hóa strip `@` + lowercase. Validate live theo từng lần login, không cache.
- [x] 2026-06-21: **Super Admin — skeleton loading + fix nút đăng xuất.**
  - Thay `LoadingSpinner` page-level bằng skeleton khớp bố cục từng màn (`super-admin/_skeletons.tsx`: Dashboard/Table/CardList/LogList/Settings/StatsGrid). Giữ `Loader2` cho action inline (duyệt, lưu, đổi role, submit modal) vì spinner hợp lý ở đó.
  - **Fix logout không bấm được:** trong `WorkspaceLayout`, overlay click-outside (`z-30`, con của root) bị header `sticky z-20` (stacking context) đẩy lên TRÊN dropdown → nuốt click vào nút "Đăng xuất". Thay overlay bằng outside-click listener dùng `useRef` (notif + user menu) → nút hoạt động. (HrLayout có cùng bug, sẽ hết khi chuyển sang WorkspaceLayout lúc redesign HR.)
- [x] 2026-06-21: **Làm sạch `Program.cs`** — xóa toàn bộ `SeedDataAsync` (seed user demo + 8 mock job + session/evaluation/HR review giả) và `record MockJob`; giữ auto-migration on startup (bỏ phần gọi seed); gỡ `using ARISP.Domain.Entities` thừa. File từ 832 → 352 dòng. Build BE: 0 lỗi.
- [x] 2026-06-21: **Redesign khu Super Admin (full-stack, data thật).**
  - **BE** (`AdminController`, policy `SuperAdminOnly`): thêm `GET /admin/stats`, `GET /admin/audit-logs` (filter action/entity + paginate + resolve tên actor), `GET/PUT /admin/settings` (key/value: allowed_email_domains, ATS/Slack/Teams webhook), `POST /admin/users/{id}/activate|deactivate`, `DELETE /admin/users/{id}` (soft delete) — tất cả ghi `AuditLog`. Bảng `audit_logs`/`system_settings` đã có từ InitialCreate, không cần migration.
  - **FE**: `services/admin/adminService.ts` (service layer thay mock), `utils/adminLabels.ts` (role/action label + timeAgo). Layout chung tham số hóa `components/layout/WorkspaceLayout.tsx`; `SuperAdminLayout` thành wrapper. Redesign 5 trang (Dashboard, Users + modal tạo staff, PendingUsers approve/reject, AuditLogs, Settings) sang style mockup (ink/brand/ai, font-display, shadow-card, dark mode qua override layer). Bỏ mọi tham chiếu "Organization" (single-tenant) và route hỏng `/super-admin/jobs`.
  - **Fix kèm:** màn login nội bộ (`LoginPage`) dark mode — đổi `via-ink-50` → `via-ink-100` để khớp override layer (nền không còn bị sáng).
- [x] 2026-06-21: Thêm loading skeleton cho `ProfilePage` (candidate/profile) — thay spinner đơn bằng `ProfileSkeleton` mô phỏng đúng bố cục (section nav + completeness, banner cá nhân + lưới field, các section card), đồng bộ với skeleton các màn khác.
- [x] 2026-06-21: Sửa card CV + card Lịch phỏng vấn ở sidebar `ApplicationsPage`.
  - **CV:** thêm nút "Xem" (icon Eye) mở CV inline trong tab mới (`profileCvUrl` + `target="_blank"`) — đồng bộ pattern xem inline của ApplyPage; giữ nút tải về riêng.
  - **Lịch phỏng vấn:** trước hiển thị "Th6 21" thiếu năm + lịch đã quá giờ vẫn hiện. Nay: thêm tick `now` (setInterval 30s) để cập nhật realtime + tự ẩn mốc đã qua (`startTime > now`), helper `scheduleInfo` hiển thị ngày đầy đủ ("Thứ Bảy, 21/06/2026"), giờ + timezone, và nhãn tương đối ("Ngày mai" / "Còn 2g 15p" / "Còn N ngày").
- [x] 2026-06-20: Gia cố cổng kiểm tra thiết bị `DeviceCheck` (2 case bảo mật/độ tin cậy).
  - **Case 1 — thu hồi quyền giữa chừng:** trước chỉ chụp trạng thái 1 lần → vẫn vào được dù đã tắt quyền. Nay lắng nghe `ended`/`mute` trên cả video+audio track → mất quyền/rút thiết bị/OS-mute thì khóa nút ngay + báo "Mất quyền truy cập" + nút Thử lại.
  - **Case 2 — camera bị che / quá tối:** track vẫn `live` khi che ống kính nên không phát hiện qua trạng thái track. Nay giám sát độ sáng khung hình (vẽ xuống canvas 32×24, tính luma trung bình mỗi 700ms; tối < ngưỡng 2 lần liên tiếp → `camDark`) → phủ cảnh báo lên preview, đánh dấu trạng thái Camera "Bị che / tối", và khóa nút vào phỏng vấn cho đến khi thấy hình lại.
  - Áp dụng chung cho cả Practice & Real (Kiosk Phase 7) vì dùng chung component.
- [x] 2026-06-20: Sửa vi phạm thiết kế — phỏng vấn thử chỉ mở sau khi QUA vòng CV (ADR-038) + nhóm trạng thái theo quy trình.
  - **Lỗi:** `PracticeAvailable = !PracticeSessionUsed` (CandidatePortalController) → practice hiện cả khi `cv_submitted` ("HR đang xem hồ sơ"), ứng viên chưa qua CV đã thấy nút phỏng vấn thử.
  - **BE:** thêm `PracticeEligible(status)` — loại `invited`/`cv_submitted`/`withdrawn`/`pass`/`not_pass`; `PracticeAvailable = !PracticeSessionUsed && PracticeEligible(Status)`. Practice chỉ bật từ `screening` trở đi (sau khi HR chuyển khỏi cv_submitted = đã qua CV).
  - **FE (`ApplicationsPage`):** `showPractice = practiceAvailable && !hasCode` (tin backend). Thêm `groupOf(app)` gom nhóm theo quy trình: có mã On-site / còn lượt practice (đã qua CV) → "Cần hành động"; HR xem hồ sơ → "Đang xử lý"; pass/not_pass → "Đã hoàn tất" (dùng cho cả counts + filter). Card practice ghi rõ "Bạn đã qua vòng CV!".
- [x] 2026-06-20: Sửa điều hướng Phỏng vấn thử + dọn màn ứng viên cũ không đúng style.
  - Nút "Phỏng vấn thử" trước đây dẫn về màn cũ `/candidate/interviews` (InterviewSchedulePage). Repoint tất cả điểm vào practice → `/interview/practice/:applicationId` (mới): card practice trong `ApplicationsPage` dùng `app.id`; **gỡ mục nav "Phỏng vấn thử"** ở `CandidateHeader` (practice giờ khởi động theo từng hồ sơ, không còn là điểm đến độc lập).
  - **Xóa khỏi dự án** các màn cũ đã bị thay thế: `CandidateHome.tsx` (dashboard/jobs — thay bằng job board), `PortalPage.tsx` (thay bằng ApplicationDetailPage). Cập nhật barrel `pages/candidate/index.ts`. Route cũ `/candidate/dashboard`→`/`, `/candidate/jobs`→`/jobs`, `/candidate/portal`→`/candidate/applications` (redirect).
  - **Giữ tạm** Kết quả (`FeedbackPage`) + Lịch phỏng vấn (`InterviewSchedulePage`) vì đang chờ redesign ("để sau") — vẫn style cũ nhưng không còn bị nút practice trỏ tới. Layout cũ (CandidateLayout/Nav/Footer) giữ lại do 2 màn này còn dùng.
- [x] 2026-06-20: Dựng màn Phỏng vấn thử (FE) + cổng kiểm tra thiết bị bắt buộc + chốt RAG service Python (ADR-039/040).
  - FE: component dùng chung `components/interview/DeviceCheck.tsx` — `getUserMedia` preview camera + đo mức âm mic (Web Audio), chặn vào phòng đến khi cả mic+cam `live`, xử lý từ chối quyền/thiếu thiết bị + Thử lại, bàn giao stream cho phòng (không prompt lần 2).
  - FE: `PracticeSessionPage` dựng lại theo 3 phase — intro (giải thích: JD+CV, 1 lần, không ghi hình) → DeviceCheck (cổng bắt buộc) → phòng phỏng vấn thử (self-view camera thật, mute/end hoạt động, transcript panel, avatar, badge "Không ghi hình") → ended. Route tách full-bleed khỏi InterviewLayout.
  - ADR-040: cổng mic+cam bắt buộc cho cả thử & thật (Real/Kiosk sẽ tái dùng `DeviceCheck` ở Phase 7).
  - ADR-039: chốt tách RAG thành microservice Python (FastAPI), .NET gọi qua HTTP, `IEmbeddingProvider` thành client, pgvector giữ trên Postgres — **chưa triển khai** (task backend/infra Phase 4/4b).
  - ⏳ Chưa wire: pipeline AI thật (STT/RAG/GPT-4o/TTS/HeyGen — Phase 7), backend tạo practice session + cấp code `type=practice` sau khi HR pass CV (ADR-038).
- [x] 2026-06-20: Chốt flow + chi phí Phỏng vấn thử (design decision, chưa code) — thêm **ADR-038**, cập nhật ADR-015/016/027 + CLAUDE.md.
  - Practice **chỉ mở cho ứng viên pass CV** + HR cấp **Interview Code 6 ký tự type=`practice`** (remote, không còn magic link cho practice). 1 lần/application, one-time.
  - Giữ **đầy đủ pipeline công nghệ** cả thử & thật (STT/RAG/GPT-4o/TTS/HeyGen+Hybrid Idle). Tối ưu chi phí bằng **gating phễu** (giảm số buổi) chứ không cắt tech. Practice **không quay video — chỉ transcript** + Evaluation Report. Real RAG có Playbook, practice chỉ JD+CV.
  - Tác động backlog (khi build Phase 2b/3/7): thêm `code_type` (practice|real) vào Interview Code entity + generation; gate practice sau khi HR Pass CV; trần cứng số câu/thời lượng; practice không lưu video.
- [x] 2026-06-20: Trang Cài đặt ứng viên (`/candidate/settings`) — end-to-end FE + BE + DB, khớp mockup `candidate-settings.html`.
  - DB: thêm cột `settings_json` (text, nullable) vào `candidate_accounts` lưu tùy chọn cá nhân. Migration `AddCandidateSettings` (viết tay do bin bị khoá bởi API đang chạy; đã cập nhật model snapshot).
  - BE (`CandidatePortalController`): `GET/PUT /api/portal/settings` (DTO `CandidateSettingsDto`: ngôn ngữ + ma trận thông báo Email/Đẩy theo 4 loại + quyền riêng tư), `GET /settings/export` (xuất hồ sơ + đơn ứng tuyển ra JSON tải về), `POST /settings/logout-all` (thu hồi toàn bộ `CandidateRefreshToken`).
  - FE: `settingsService` (get/update/exportData/logoutAllDevices). Trang mới `SettingsPage` (trong CandidateAppLayout) — section nav dính (Giao diện/Thông báo/Quyền riêng tư/Phiên đăng nhập); chọn theme Sáng/Tối/Hệ thống (client-side localStorage, hoạt động thật) + ngôn ngữ; ma trận công tắc thông báo + quyền riêng tư auto-save; nút tải dữ liệu JSON; đăng xuất khỏi tất cả thiết bị; thanh trạng thái "Đang lưu/Đã lưu" dính đáy. Route `/candidate/settings`.
- [x] 2026-06-20: Thông báo (Notifications) cho ứng viên — end-to-end FE + BE + DB.
  - DB: bảng `notifications` (entity `Notification` ISoftDelete: candidate_account_id, dedup_key, type, title, body, link, is_read, timestamps) + unique index `(candidate_account_id, dedup_key) WHERE deleted_at IS NULL`. Migration `AddNotifications` (viết tay do bin bị khoá bởi API đang chạy; đã cập nhật model snapshot).
  - BE (`CandidatePortalController`): `GET /api/portal/notifications` (sync từ sự kiện thực tế → trả items + unreadCount), `POST /notifications/read-all`, `POST /notifications/{id}/read`. `SyncNotificationsAsync` sinh thông báo idempotent theo DedupKey từ: đã nộp hồ sơ (`applied`), lời mời PV (mã On-site còn hiệu lực — `invite`), kết quả vòng đã HR chia sẻ (`result`), lịch PV sắp tới (`schedule`). Giữ trạng thái đã đọc.
  - FE: `notificationService` (list/markAllRead/markRead). `CandidateHeader` dropdown chuông nối dữ liệu thật (badge unreadCount, danh sách, "Đánh dấu đã đọc", click item → mark read + điều hướng link). Trang mới `NotificationsPage` (`/candidate/notifications`, trong CandidateAppLayout) — filter tabs (Tất cả/Chưa đọc/Phỏng vấn/Kết quả/Hệ thống), nhóm theo ngày (Hôm nay/Hôm qua/Trước đó), icon theo loại, mark read, empty/skeleton.
- [x] 2026-06-20: Header dùng chung cho khu vực ứng viên/job board (`CandidateHeader`) — đồng bộ giao diện mọi màn.
  - FE: tạo `components/layout/CandidateHeader.tsx` (bản đầy đủ: logo + nav active theo URL + ô tìm kiếm + nút Lưu việc + đổi ngôn ngữ VI + sáng/tối + chuông + menu user, có mobile menu). Dùng cho `CandidateAppLayout` (Hồ sơ ứng tuyển/Chi tiết/Hồ sơ cá nhân/Việc đã lưu), `FindJobPage` (thay `Header` nội bộ), `JobDetailPage` (thay `Nav` nội bộ). Gỡ code header trùng lặp + import thừa. ApplyPage giữ header gọn riêng (form tập trung).
- [x] 2026-06-20: Màn ứng tuyển — validate theo blur (không nhắc lỗi mỗi ký tự) + "Nơi làm việc mong muốn" thành combo box tỉnh/thành.
  - FE: thay cờ `touched` chung bằng `touchedFields` (Set) — lỗi chỉ hiện sau blur hoặc bấm Gửi, gõ lại thì xoá touched trường đó. "Nơi làm việc mong muốn" dùng `SearchableSelect` (danh sách tỉnh/thành từ provinceService) prefill theo provinceCode/name của hồ sơ.
  - SĐT (chỉnh sau): chỉ cho nhập chữ số (strip `\D`), validate **live** ngay khi gõ — báo "Số điện thoại không hợp lệ (chỉ chứa 8–15 chữ số)." tới khi đủ 8–15 số (giống quy tắc màn Hồ sơ). Các trường khác vẫn blur/submit.
- [x] 2026-06-20: Màn ứng tuyển — "Nơi làm việc mong muốn" prefill chỉ tỉnh/thành. ApplyPage lấy `provinceName` (fallback: phần sau dấu phẩy cuối của `location` cũ) thay vì cả chuỗi "Phường …, Tỉnh …". Cập nhật mốc so sánh dirty để không bật popup huỷ nhầm.
- [x] 2026-06-20: Hồ sơ ứng viên (ProfilePage) — bỏ ô "Phường / Xã", đổi "Tỉnh / Thành phố" → "Nơi làm việc mong muốn".
  - FE: gỡ Field phường/xã + state `wards`/`wardsLoading` + effect tải phường + `onWardChange` (không còn dùng). Đổi nhãn province Field. Khi lưu set `wardCode/wardName = null` → Location (nơi làm việc mong muốn) chỉ còn tỉnh/thành.
- [x] 2026-06-20: Màn ứng tuyển đầy đủ (`/jobs/:id/apply`) — form nộp hồ sơ trước khi gửi về nhân sự.
  - DB: thêm cột `desired_location`, `cover_letter`, `notice_period` vào `applications` (entity `Application` + migration `AddApplicationApplyFields`, auto-apply lúc khởi động).
  - BE: `POST /api/portal/applications/{jobId}/apply` chuyển sang **multipart** — CV mặc định lấy hồ sơ, ứng viên có thể đính kèm `CvFile` khác cho riêng tin (validate PDF/DOCX ≤10MB, lưu BẢN SAO immutable). Validate bắt buộc: họ tên, SĐT, nơi làm việc mong muốn, thư giới thiệu (Q1), notice period (Q2). Lưu 3 trường mới qua `SubmitApplicationRequest`/`SubmitApplicationAsync`. Vẫn auto-link CV–JD analysis + embed CV.
  - FE: trang mới `ApplyPage` (`/jobs/:id/apply`) — prefill họ tên/SĐT/email + nơi làm việc từ hồ sơ; chọn nguồn CV (CV hồ sơ ↔ tải CV khác); 2 câu hỏi thư giới thiệu; các trường bắt buộc đánh dấu `*` đỏ + validate client-side. Nút "Quay lại" → **popup xác nhận huỷ** (nếu đã nhập dữ liệu). Gửi thành công → `/candidate/applications`; 409 đã ứng tuyển → về danh sách. Nút "Ứng tuyển ngay" ở JobDetailPage giờ điều hướng tới màn này (đã ứng tuyển → "Xem hồ sơ").
- [x] 2026-06-20: "Ứng tuyển ngay" (JobDetailPage) — gửi hồ sơ thật về bộ phận nhân sự (trước đó nút chỉ điều hướng tới `/candidate/applications/{jobId}` = sai, dùng jobId làm applicationId).
  - BE: endpoint mới `POST /api/portal/applications/{jobPostingId}/apply` (CandidateOnly) — ứng tuyển "một chạm" bằng CV trong hồ sơ: kiểm tra có ProfileCv (chưa có → 400 `no_cv`), chặn trùng (đã có hồ sơ chưa rút → 409 `already_applied` + applicationId), đọc CV hồ sơ → lưu BẢN SAO riêng (immutable, không phụ thuộc khi ứng viên đổi/xoá CV hồ sơ), parse text + hash, gọi `ApplicationService.SubmitApplicationAsync(source="job_board")` → tạo Application status `cv_submitted`, auto-link `CvJdAnalysis` đã cache theo (job+CV hash) để HR thấy match score, embed CV. Inject `ApplicationService` vào `CandidatePortalController`.
  - FE: `applicationService.applyToJob(jobId)`; `JobDetailPage` nút "Ứng tuyển ngay" gọi API thật — spinner "Đang gửi hồ sơ…", thành công → điều hướng `/candidate/applications`; chưa đăng nhập → trang đăng nhập; chưa có CV → banner + nút "Tải CV lên"; đã ứng tuyển → nút đổi thành "Đã ứng tuyển · Xem hồ sơ". Tự kiểm tra trạng thái đã ứng tuyển khi mở trang (qua getMyApplications).
- [x] 2026-06-20: CV–JD Match Analysis (Gemini) — ép trả về tiếng Việt + bố cục dễ nhìn.
  - BE (`GeminiProvider`): thêm CRITICAL LANGUAGE RULE + viết lại mô tả schema bằng tiếng Việt → mọi trường text (summary/skills_matched/skills_gaps/red_flags/experience_relevance/reasoning…) viết tiếng Việt, chỉ giữ tên công nghệ nguyên gốc. `summary` chuẩn hoá 2 đoạn có marker `🌟 Điểm sáng:` / `⚠️ Điểm cần lưu ý:`.
  - FE (`JobDetailPage`): kỹ năng khớp/còn thiếu đổi từ câu nối dấu phẩy → **chip tags** (xanh/hổ phách); tóm tắt tách thành 2 khối "Điểm sáng" (nền xanh) / "Điểm cần lưu ý" (nền hổ phách) qua helper `parseMatchSummary` (fallback nếu thiếu marker).
  - Cache: prompt chỉ áp dụng cho phân tích MỚI (cache theo job+CV hash). Đã xoá 5 bản `cv_jd_analyses` chưa gắn đơn ứng tuyển nào (bản tiếng Anh cũ) để các tin đó tự phân tích lại ra tiếng Việt; giữ nguyên các bản đã gắn application (mock/HR).
- [x] 2026-06-20: Favicon (logo trên tab trình duyệt) — `index.html` trỏ `/arisp.svg` nhưng file không tồn tại → tab hiện icon quả địa cầu mặc định. Tạo `frontend/public/arisp.svg` dùng đúng logo thương hiệu (chữ "A" monogram gradient + sparkle AI, khớp logo header/mockup).
- [x] 2026-06-20: Phân trang danh sách tin tuyển dụng (Job Board / FindJobPage).
  - FE: phân trang client-side (toàn bộ job đã tải sẵn, lọc/sắp xếp trong bộ nhớ) — `JOBS_PER_PAGE = 8`, cắt `sortedJobs` theo trang, component `Pagination` (nút trước/sau + số trang, có dấu "…" khi >7 trang, ẩn khi chỉ 1 trang). Đổi bộ lọc/tìm kiếm/sắp xếp → tự về trang 1; đổi trang → cuộn mượt lên đầu danh sách (`scroll-mt-24`). Tiêu đề vẫn hiển thị tổng số tin khớp.
- [x] 2026-06-20: Màn "Chi tiết hồ sơ ứng tuyển" (Candidate) — dựng mới khớp mockup `design/mockups/candidate-results.html`, thay màn cũ.
  - Vấn đề: nút "Xem chi tiết" ở `ApplicationsPage` dẫn `/candidate/applications/:id` về màn `CandidateApply` cũ (layout dark glassmorphism, không còn dùng).
  - BE: mở rộng `GET /api/portal/applications/{id}` — mỗi vòng (session) đính kèm: `recordingUrl` (chỉ khi `ShareRecording`, resolve qua FileStorage), `transcriptShared`, `pendingHrReview`, `hrFeedback` (khi `ShareFeedback`), `hrFinalVerdict` (verdict HR xác nhận khi đã share), và `evaluation` **chỉ khi `ShareEvaluation`** gồm verdict/overallScore/reasoning/recommendedNextStep + **criterionScores** (parse `{technical:88,...}` → list {name,score}), **questionAnalyses** (parse list {Question,Answer,Score,Analysis,Feedback}), **languageAssessment** (parse {fluency,grammar,...}). Response thêm location/department/interviewMode/detectedLanguage + interviewCode/upcomingInterview. Parse JSON ở BE (helper `ParseCriterionScores/ParseQuestionAnalyses/ParseLanguageAssessment`) → FE nhận dữ liệu typed, giữ mô hình bảo mật (không lộ gì khi HR chưa share).
  - FE: trang mới `ApplicationDetailPage` tại `/candidate/applications/:id` (chuyển vào `CandidateAppLayout` redesigned, gỡ route khỏi `CandidateLayout` cũ + gỡ import `CandidateApply`). Bố cục 2 cột khớp mockup: trái = lịch PV sắp tới + danh sách các vòng (chọn vòng để xem báo cáo, badge Pass/Not Pass/Chờ HR/Thử + điểm); phải = báo cáo đánh giá (header verdict+điểm, video bản ghi nếu được share, tabs Tiêu chí/Câu hỏi, thanh điểm theo tiêu chí có nhãn tiếng Việt, đánh giá ngôn ngữ, phân tích từng câu hỏi dạng accordion, bước tiếp theo + nhận xét HR). Trạng thái rỗng: chờ HR xác nhận / chưa có báo cáo. `applicationService.getMyApplicationDetail(id)` + types `MyApplicationDetail/MyApplicationSession/MySharedEvaluation/...`. Thêm dark override `.from-emerald-50`, `.from-red-50`, `.bg-white/60` vào `index.css`.
- [x] 2026-06-20: Fix dark-mode màn "Quên mật khẩu" — gradient nền có vệt sáng (stop giữa `via-ink-100` chưa có override) → thêm `html.dark .via-ink-100` vào `index.css` (mirror `.via-white`).
- [x] 2026-06-20: Sửa các lỗi hiển thị dark-mode còn sót (lớp override theo class trong `index.css`).
  - Hồ sơ ứng tuyển: thêm override dark cho nền có hậu tố độ mờ + gradient mà thẻ dùng nhưng chưa được phủ → còn sáng, khó đọc: `.bg-amber-50/60` (hộp mã On-site), `.bg-brand-50/60` (hộp "Lịch phỏng vấn sắp tới"), `.from-ai-50` (thẻ "Mẹo từ AI").
  - Đăng ký ứng viên: input Email bị autofill của trình duyệt sơn nền sáng đè theme tối → thêm rule `:-webkit-autofill` (transition cực dài giữ nền trong suốt + `-webkit-text-fill-color` theo theme sáng/tối) cho mọi input.
- [x] 2026-06-20: Skeleton loading (shimmer kiểu Facebook) thay spinner.
  - FE: component dùng lại `components/ui/Skeleton.tsx` (khối bo góc + vệt sáng shimmer quét ngang, hỗ trợ dark). Thêm keyframe `shimmer` + util `animate-shimmer` vào `tailwind.config.js`. `ApplicationsPage` khi tải hiển thị `ApplicationsSkeleton` mô phỏng nguyên bố cục (banner + 4 thẻ thống kê + chips lọc + 3 thẻ hồ sơ + sidebar) thay cho spinner Loader2 → cảm giác trang sắp hiện, người dùng chờ thoải mái hơn.
- [x] 2026-06-19: Màn "Hồ sơ ứng tuyển" (Candidate) — dựng lại khớp mockup `design/candidate-applications.html`.
  - BE: mở rộng `GET /api/portal/applications` — mỗi hồ sơ thêm `interviewCode` (mã On-site còn hiệu lực: code + expiresAt + roundNumber), `upcomingInterview` (booking sắp tới: startTime/timezone/roundNumber từ InterviewBooking+AvailabilitySlot), `practiceAvailable`, `pendingHrReview` (vòng completed + AI đã chấm nhưng HR chưa chia sẻ — KHÔNG lộ điểm, giữ mô hình bảo mật), `hrFeedback` (candidateFeedback khi shareFeedback), `interviewMode`. Không cần DB mới — dùng các entity sẵn có (InterviewCode/InterviewBooking/AvailabilitySlot).
  - FE: `ApplicationsPage` fetch song song applications + profile. Banner đầy đủ (headline, email/phone/location, "Sửa hồ sơ"). Thẻ hồ sơ render theo trạng thái: **mã phỏng vấn On-site** (đếm ngược hết hạn + sao chép mã), **chờ HR xác nhận**, **CTA phỏng vấn thử**, **feedback khi kết thúc** (+"Xem feedback"); round stepper có nhãn loại vòng + verdict. Sidebar 4 thẻ: **CV** (tên file, tải về, cập nhật), **lịch phỏng vấn sắp tới**, **độ hoàn thiện hồ sơ** (checklist), **mẹo từ AI**. Filter tabs + nhãn "Mới cập nhật". Cập nhật type `MyApplicationItem` (+code/upcoming/practiceAvailable/pendingHrReview/hrFeedback).
  - Mock: thêm dữ liệu cho `quannguyen23.a@gmail.com` để xem thử đủ các biến thể (mã On-site `7K9X2P`, lịch PV sắp tới, feedback not_pass).
- [x] 2026-06-19: Banner "Tải CV lên" (FindJobPage) — chuyển sang Hồ sơ + tự cuộn tới khối CV kèm chỉ dẫn.
  - FE: banner điều hướng `/candidate/profile?focus=cv`. `ProfilePage` đọc `?focus=cv` → sau khi hồ sơ render, cuộn mượt tới `<section id="cv">` (block center), bật hiệu ứng chỉ dẫn (viền glow tím + nhãn "Tải CV của bạn lên tại đây") ~4s rồi gỡ param khỏi URL (không lặp khi refresh). Thêm keyframe `guide-glow` + util `animate-guide-glow` vào `tailwind.config.js`. Cũng nối nút bookmark header (FindJobPage/JobDetailPage) + mục menu "Việc đã lưu" tới `/candidate/saved-jobs`.
- [x] 2026-06-19: Lưu việc làm (Saved Jobs / bookmark) cho Candidate — end-to-end FE + BE + DB.
  - DB: bảng `saved_jobs` (entity `SavedJob` ISoftDelete: candidate_account_id, job_posting_id, timestamps). Partial unique index `(candidate_account_id, job_posting_id) WHERE deleted_at IS NULL` để lưu lại được sau khi bỏ lưu. Migration `AddSavedJobs` (đã apply).
  - BE: `CandidatePortalController` (CandidateOnly) thêm `GET /api/portal/saved-jobs` (đầy đủ thông tin job, chỉ tin active+public, sort theo savedAt), `GET /api/portal/saved-jobs/ids` (tô đậm nút bookmark), `POST`/`DELETE /api/portal/saved-jobs/{jobId}` (idempotent).
  - FE: `savedJobService` (getSavedJobs/getSavedJobIds/save/unsave). `FindJobPage` JobCard nối nút bookmark vào API thật (load ids khi đăng nhập, toggle optimistic, chưa login → tới trang đăng nhập). `JobDetailPage` nút "Lưu việc làm" ↔ "Đã lưu" (load trạng thái + toggle optimistic). Trang mới `SavedJobsPage` tại `/candidate/saved-jobs` (CandidateAppLayout) — grid thẻ job, bỏ lưu tại chỗ, empty state. Khớp nav "Việc đã lưu" đã có sẵn.
- [x] 2026-06-19: Fix dark-mode — banner "Mẹo — Tải CV để xem độ phù hợp" (FindJobPage) bị sáng khi bật nền tối.
  - FE: `index.css` bổ sung override dark cho các gradient-stop còn thiếu `.from-ai-50/80` và `.to-brand-50/70` (mirror pattern `.from-ai-50/70`), thêm override viền `.border-ai-200` → tím nhạt mờ. Banner dùng gradient `from-ai-50/80 to-brand-50/70` không có dark override nên nền vẫn sáng; nay darken đồng bộ với các card accent khác.
- [x] 2026-06-19: Job Detail (Candidate) — Độ phù hợp CV–JD phân tích đúng CV trong hồ sơ (thay vì số liệu cứng).
  - BE: `GET /api/portal/jobs/{id}/cv-match` (CandidateOnly) — lấy CV trong hồ sơ ứng viên, phân tích CV–JD qua `CvJdAnalysisService.AnalyzeAndCacheAsync` (cache theo job + CV hash). Chưa có CV → `hasCv=false`. AI chưa cấu hình/CV lỗi → `aiAvailable=false` + message, vẫn trả file CV. DTO `CvMatchResponse`/`CvMatchAnalysisDto`.
  - BE: thêm `IFileStorageService.ReadAllBytesAsync(storageKey)` (Local đọc đĩa, S3 GetObject→bytes) để đọc CV phía server phục vụ phân tích.
  - FE: `profileService.getCvMatch(jobId)`; `JobDetailPage` thay card "Độ phù hợp CV–JD" cứng (87, React/.NET/EF Core) bằng kết quả thật — trạng thái: chưa đăng nhập/không phải ứng viên → nút Đăng nhập; đang phân tích → spinner; chưa có CV → nút "Tải CV lên"; có CV → hiện rõ **tên file CV** (bấm xem) + điểm match + Khớp/Thiếu + tóm tắt.
  - Fix timeout: Gemini mất ~25s > axios timeout 30s nên hay báo "Không tải được". Chuyển sang **chạy nền + poll cache**: endpoint trả ngay `status` (processing|completed|failed|none), phân tích chạy `Task.Run` qua `IServiceScopeFactory`, cache theo job+CV hash; FE poll mỗi 2.5s tới khi xong (mỗi request nhanh, không giữ kết nối 25s). Lỗi AI không ghi DB được giữ ở `_matchJobs` (in-memory) để poll đọc trạng thái failed.
- [x] 2026-06-19: Job Board (Candidate) — bộ lọc theo dữ liệu thật + số lượng + mock data + Địa điểm theo Province API.
  - BE: `GET /api/jobs/facets` trả các bộ lọc khả dụng (chỉ giá trị THỰC SỰ có trong tin active+public) kèm số lượng — Lĩnh vực (jobCategory), Hình thức (employmentType), Cấp bậc (experienceLevel), Nơi làm việc (workMode), Địa điểm (location), Kỹ năng (skills), Ngôn ngữ (detectedLanguage). Có bảng nhãn + thứ tự sắp xếp. `JobFacetsResponse`/`JobFacetItem`. Thêm `Skills` vào `JobPostingListItemResponse` (public list) để FE lọc/khớp.
  - Seed: thêm 8 tin mock (active+public) trong `Program.cs` (record `MockJob`) phủ nhiều lĩnh vực/cấp bậc/hình thức/thành phố; chuẩn hoá tin seed cũ (`location="Hồ Chí Minh"`, `jobCategory="backend"`). Location dùng tên thành phố ngắn khớp Province Open API.
  - FE: `jobService.getJobFacets()` + `provinceService.getCities()` (lọc `division_type` = thành phố trung ương, rút gọn "Thành phố X" → "X"). `FindJobPage` render bộ lọc động từ facets — mỗi mục hiển thị "Nhãn (số)"; chỉ hiện mục có trong DB; lọc theo giá trị thô khớp DB. Địa điểm = facet ∩ thành phố từ Province API.
- [x] 2026-06-19: Địa điểm Candidate — Provinces Open API v2 (sau sáp nhập 07/2025). Xem [ADR-037](architecture.md).
  - DB: thêm `province_code/province_name/ward_code/ward_name` vào `candidate_accounts` (migration `AddCandidateAdminDivision`); `location` chuyển thành chuỗi hiển thị suy ra tự động "Phường X, Tỉnh Y".
  - BE: DTO + `UpdateProfile` set code/name + derive Location; `MapProfile` trả các trường mới.
  - FE: `provinceService` (gọi v2 `/p/` + `/p/{code}?depth=2`, cache phiên); ProfilePage thay input text bằng 2 dropdown phụ thuộc Tỉnh→Phường (34 tỉnh, 2 cấp). Completeness tính theo `provinceCode`.
- [x] 2026-06-19: Profile CV — hiển thị tên file gốc + tách xem/tải về.
  - BE: thêm cột `profile_cv_file_name` (migration `AddProfileCvFileName`); `UploadCv` lưu tên gốc; DTO trả `CvFileName` + `CvDownloadUrl`. `IFileStorageService.GetDownloadUrlAsync` (S3: presigned + Content-Disposition=attachment; Local: đường dẫn tương đối).
  - FE: khối CV hiện **tên file kèm đuôi** (thay "CV hồ sơ hiện tại"); bấm vùng file → xem tab mới; bấm icon → tải về (anchor `download` + URL attachment).
- [x] 2026-06-19: File Storage Abstraction — Local (dev) / Cloudflare R2 (prod). Xem [ADR-036](architecture.md).
  - BE: `IFileStorageService` (SaveAsync/GetUrlAsync/DeleteAsync) + `LocalFileStorageService` (ghi ./uploads, key `/uploads/<guid>.ext`) + `S3FileStorageService` (AWSSDK.S3, file private + presigned URL). `S3StorageOptions`. DI chọn theo `Storage:Provider` (Local|S3), fail-fast khi S3 thiếu cấu hình. Thêm package `AWSSDK.S3`.
  - Refactor: `ApplicationsController` (CV ứng tuyển — đọc bytes 1 lần để hash+parse+save), `CandidatePortalController.UploadCv` (xoá CV cũ khi upload mới), resolve URL ở GET applications/detail/profile (helper `BuildProfileAsync` + `cvUrlMap`). DB lưu storageKey, không lưu URL tuyệt đối.
  - FE: `ASSET_BASE_URL` + `resolveAssetUrl()` trong constants.ts (bỏ `/api` của API_BASE_URL); link CV ở ProfilePage dùng nó → mở đúng file (trước đó trỏ nhầm localhost:3000).
  - Cấu hình: `appsettings.json` thêm section `Storage` (Provider=Local default, S3 placeholder). Secrets R2 set qua user-secrets (`Storage:Provider=S3` + `Storage:S3:*`). Bucket `arisp-uploads`, KeyPrefix `cv`.
  - Follow-up: HR/staff side (`ApplicationService.CvFileUrl`) cần resolve presigned URL khi bật S3 prod (dev Local không ảnh hưởng).
- [x] 2026-06-19: Profile — đổi/đặt mật khẩu (modal) + fix Swagger lỗi upload CV + mở được file CV.
  - BE: `POST /api/portal/profile/change-password` (verify mật khẩu hiện tại bằng BCrypt nếu đã có; cho đặt lần đầu với tài khoản Google; áp `IsStrongPassword`; chặn trùng mật khẩu cũ). Fix Swagger 500: `UploadCv` thêm `[Consumes("multipart/form-data")]`, bỏ `[FromForm]` trên IFormFile.
  - FE: `ChangePasswordModal` (checklist điều kiện real-time, hiện/ẩn, xác nhận khớp) + `profileService.changePassword`. Lưu ý: prop đặt `passwordSet` (không phải `hasPassword`) để tránh hook quét secret hiểu nhầm `Password=` là connection string.
  - FE: tag gợi ý kỹ năng phổ biến (~40 skill) dưới ô nhập — bấm để thêm nhanh, tự ẩn skill đã thêm.
- [x] 2026-06-19: Profile — validate nghiệp vụ + upload CV & đánh giá AI (Gemini).
  - FE validate: SĐT chỉ nhận số/`+ - ( )` (sanitize onChange) + kiểm 8–15 chữ số khi lưu; Ngày sinh `max=today` + chặn tương lai khi lưu; lọc bỏ mục Kinh nghiệm/Học vấn trống trước khi PUT (không lưu rác).
  - BE: `ReviewCvAsync` trong `IGeminiProvider`/`GeminiProvider` (đánh giá CV độc lập, không cần JD → score/verdict/strengths/improvements/missing_sections, prompt tiếng Việt). `POST /api/portal/profile/cv` (PDF/DOCX ≤5MB): Gemini đánh giá → từ chối nếu không phải CV hợp lệ; lưu file vào /uploads + set `ProfileCvUrl` + lưu `CvReviewJson`. Nếu AI không khả dụng vẫn lưu CV (aiAvailable=false). Migration `AddCvReviewToCandidate` (cột cv_review_json).
  - FE: `profileService.uploadCv` + section CV mới (upload PDF/DOCX, trạng thái phân tích, hiển thị `CvReviewCard`: điểm/verdict/điểm mạnh/gợi ý cải thiện/còn thiếu).
  - Lưu ý: `GEMINI_API_KEY` chưa cấu hình ở môi trường này → cần `dotnet user-secrets set "GEMINI_API_KEY" "<key>"` để phần đánh giá AI hoạt động.
- [x] 2026-06-18: Candidate redesign #2 — màn "Hồ sơ của tôi" (`/candidate/profile`) theo mockup, end-to-end.
  - DB: migration `AddCandidateProfileFields` thêm 9 cột vào `candidate_accounts` (location, date_of_birth, about, linkedin_url, github_url, portfolio_url, skills_json, experience_json, education_json). JSON cột default "" (deserialize → list rỗng), an toàn với data cũ. Áp dụng tự động khi backend khởi động lại.
  - BE: `GET/PUT /api/portal/profile` + `CandidateProfileDtos` (skills/experience/education serialize JSON). Trả `hasPassword`/`emailVerified` cho mục Tài khoản & bảo mật.
  - FE: `profileService` + `ProfilePage` (section nav, banner, thông tin cá nhân, kỹ năng tag, kinh nghiệm & học vấn editor add/remove, liên kết, mục bảo mật, sticky save bar — lưu thật qua PUT, cập nhật tên ở authStore). Route `/candidate/profile` chuyển sang group `CandidateAppLayout`.
  - Lưu ý lucide-react 1.x KHÔNG có `Linkedin/Github/Chrome` → dùng `Link2/Link/Globe`.
- [x] 2026-06-18: Candidate redesign #1 — sửa redirect sau login + màn "Hồ sơ ứng tuyển" (landing) theo mockup.
  - FE: redirect sau login của ứng viên → `/` (job board). Thủ phạm thật là `GuestRoute`/`ProtectedRoute` map `candidate → /candidate/portal` (ghi đè `navigate` vì trang login bọc trong GuestRoute). Đã sửa cả GuestRoute, ProtectedRoute, CandidateLoginPage, OAuthCallbackPage, LoginPage → `/`.
  - FE: layout mới `CandidateAppLayout` (theme sáng ink/brand/ai theo mockup — nav Việc làm/Hồ sơ ứng tuyển/Phỏng vấn thử, user menu, theme toggle, logout). Route candidate redesign tách khỏi group cũ (`CandidateLayout` dark) để migrate dần, tránh double-navbar.
  - FE: trang `ApplicationsPage` mới (data-driven) — banner hồ sơ, stats (tổng/đang xử lý/cần hành động/đã pass vòng), filter tabs, card đơn ứng tuyển (match score, status badge, round stepper với verdict, ngày). Sửa bug `getMyApplications` gọi sai path → `/portal/applications`.
  - BE: làm giàu `GET /api/portal/applications` — thêm MatchScore (CvJdAnalysis), Location/Department (JobPosting), Rounds[] (vòng + verdict/score chỉ lộ khi HR ShareEvaluation), CvFileUrl, UpdatedAt. Batch query tránh N+1. Không cần migration.
  - Còn lại (các lượt sau): Application detail, Hồ sơ cá nhân, Việc đã lưu (+bảng saved_jobs), Kết quả & lịch PV, Cài đặt, Thông báo (+bảng notifications). Dark-mode per-class tinh chỉnh sau.
- [x] 2026-06-18: Chuẩn hóa email (lowercase) + fix tên hiển thị khi đăng nhập Google.
  - BE: helper `NormalizeEmail` (trim + ToLowerInvariant); áp dụng cho mọi luồng candidate — login, register (lưu email đã chuẩn hóa), Google JIT callback, verify-email, resend, magic-link verify, forgot/reset password. Lookup dùng `c.Email.ToLower() == normalized` (case-insensitive, bắt được cả dữ liệu cũ mixed-case). → Đăng ký thủ công và đăng nhập Google cùng email = 1 tài khoản, không còn tạo trùng do khác hoa/thường.
  - BE: thêm claim `name` (FullName) vào JWT của cả candidate lẫn staff (`GenerateJwtTokenForCandidate/User`). Sửa bug: trước đây login Google không truyền tên → FE rơi xuống `payload.email` (hiện email làm tên). Giờ token chứa tên thật. Không cần migration.
- [x] 2026-06-18: Trang Điều khoản sử dụng (`/terms`) + Chính sách bảo mật (`/privacy`).
  - Shell dùng chung `components/legal/LegalPageShell` (header logo + nút Quay lại, tiêu đề, ngày cập nhật, footer cross-link) + `LegalSection`. 2 trang `pages/legal/TermsPage` & `PrivacyPolicyPage` nội dung tiếng Việt sát đặc thù ARISP (phỏng vấn AI ghi hình/transcript, CV-JD, bên thứ ba xử lý dữ liệu, quyền ứng viên). Route public trong App.tsx.
  - Wire 2 link ở `CandidateRegisterPage` (trước là `href="#"`) → `Link` mở tab mới (`target=_blank`, `stopPropagation` để không toggle checkbox). _Lưu ý: nội dung là bản mẫu, cần pháp chế rà soát trước go-live._
- [x] 2026-06-18: Gỡ bỏ MUI khỏi dự án — frontend chỉ còn TailwindCSS (đồng bộ source ↔ docs).
  - Viết lại bằng Tailwind: `common/LoadingButton`, `common/ErrorAlert`, `common/LoadingSpinner` (dùng lucide-react), `layout/InterviewLayout`, `pages/NotFoundPage`, `interview/PracticeSessionPage`. Thay `sx` gradient ở `InterviewSchedulePage` bằng className Tailwind.
  - Gỡ deps: `@mui/material`, `@mui/icons-material`, `@mui/x-date-pickers`, `@emotion/react`, `@emotion/styled` khỏi `package.json`; `npm install` + `npm prune` (còn lại `@emotion/is-prop-valid`,`memoize` là transitive của framer-motion). `npm ls` xác nhận MUI không còn trong cây phụ thuộc. `vite build` ✓.
  - Docs: bỏ "MUI" khỏi CLAUDE.md, README.md, .ai/architecture.md, .ai/context.md, .ai/tasks.md.
- [x] 2026-06-18: Candidate email verification (chặn login đến khi xác minh) + banner sau đăng ký.
  - BE: `RegisterCandidate` đổi `EmailVerified=false` + gửi email xác minh (token Audience `candidate_verify`, TTL 24h, helper `SendCandidateVerificationEmailAsync`). Thêm `GET /api/auth/candidate/verify-email` (kích hoạt) + `POST /api/auth/candidate/resend-verification` (gửi lại, trả Ok generic). `CandidateLogin` chặn khi `!EmailVerified` → 403 kèm `code=email_not_verified`. Seed candidate set `EmailVerified=true` idempotent (cả bản ghi đã tồn tại) để không khóa tài khoản test. Không cần migration (cột đã có sẵn).
  - FE: trang mới `VerifyEmailPage` (`/auth/verify-email`, có guard StrictMode double-call), `authService.verifyEmail/resendVerification`, `candidateLogin` giữ `code` lỗi. Sau đăng ký điều hướng `?verify=sent&email=` → banner "đã gửi email xác minh"; login chưa xác minh hiện nút "Gửi lại email xác minh". Đồng bộ quy tắc mật khẩu FE↔BE (thêm ký tự đặc biệt, 4 vạch strength).
- [x] 2026-06-18: Candidate Google Sign-In end-to-end (đăng nhập với Google ở `/auth/candidate-login`).
  - BE (`AuthController`): thêm `GET /api/auth/candidate/external/signin` (Challenge Google) + `GET /api/auth/candidate/external/callback`. Khác luồng staff — KHÔNG validate domain, JIT tạo `CandidateAccount` (PasswordHash rỗng, EmailVerified=true) nếu email chưa tồn tại; sinh JWT + refresh token candidate rồi redirect kèm `access_token/refresh_token/role=Candidate` về `/auth/callback`.
  - BE: guard `CandidateLogin` — tài khoản đăng ký qua Google (PasswordHash rỗng) báo lỗi rõ ràng thay vì BCrypt throw.
  - FE: `authService.buildCandidateOAuthRedirectUrl` + `candidateLoginWithGoogle`; wire onClick nút "Đăng nhập với Google" trong `CandidateLoginPage.tsx`. Tái dùng `OAuthCallbackPage` (`getRoleDashboard` đã route candidate → `/candidate/portal`).
  - Lưu ý: dùng chung Google scheme + CallbackPath `/api/auth/external/google-callback` với staff; redirect đích phân biệt qua `RedirectUri` của Challenge.
  - BE (hardening): bỏ fallback MOCK âm thầm. Production/Staging thiếu `Authentication:Google:ClientId/ClientSecret` → fail-fast lúc khởi động (đồng nhất với JWT/DB). Development thiếu → KHÔNG đăng ký provider + `Log.Warning` (tắt mềm). Endpoint `*/external/signin` kiểm tra scheme qua `IAuthenticationSchemeProvider`, chưa đăng ký → trả 503 thay vì 500.
- [x] 2026-06-15: Sửa lỗi font không nhất quán giữa các OS (Windows hiển thị sai dấu tiếng Việt).
  - Nguyên nhân: `Inter`/`Plus Jakarta Sans` được khai báo nhưng chưa bao giờ được tải → fallback khác nhau (macOS→SF Pro, Windows→Arial).
  - Self-host font qua `@fontsource/inter` + `@fontsource/plus-jakarta-sans` (import weight 400–800, kèm subset vietnamese) trong `main.tsx`.
  - Thêm fallback hệ thống (`system-ui`, `Segoe UI`, `-apple-system`...) vào font stack ở `tailwind.config.js` + `index.css`.

- [x] 2026-06-15: Bộ mockup UI redesign (HTML/Tailwind) trong `design/mockups/` + logo ARISP.
  - 10 màn: design system, Job Board, Job Detail, HR Dashboard, Interview Room, Kiosk, auth (login/register/admin), Logo showcase.
  - Logo vector ARISP (icon/mark/horizontal SVG), favicon cho tất cả trang.
  - Dark/light toggle (lưu localStorage, no-flash) + notification dropdown cho candidate.
  - Header Job Board (logged-in): tìm kiếm toàn cục, hồ sơ ứng tuyển + badge, việc đã lưu, đổi ngôn ngữ UI, chuông, menu người dùng, menu mobile.
  - Ghi nhận quyết định sản phẩm mới: [ADR-033] i18n UI candidate, [ADR-034] Saved Jobs, [ADR-035] Candidate Google OAuth (no domain).
  - Backlog triển khai code: xem mục "FE UI Redesign (mới)" trong Backlog.

- [x] 2026-06-15: Bổ sung mockup `candidate-applications.html` — màn Hồ sơ ứng tuyển của candidate.
  - Profile banner, 4 stat cards, tab lọc theo trạng thái, danh sách đơn ứng tuyển với round stepper nhiều vòng.
  - Các trạng thái: cần nhập Interview Code (On-site, có ô mã 6 ký tự + TTL), chờ HR xác nhận, mời phỏng vấn thử (Practice), HR đang xem, Not Pass.
  - Sidebar: quản lý CV, lịch phỏng vấn sắp tới, độ hoàn thiện hồ sơ, mẹo AI.
  - Nối link "Hồ sơ ứng tuyển"/"Đơn ứng tuyển" ở job-board & job-detail; thêm "Hồ sơ ƯT" vào prototype nav của tất cả mockup.

- [x] 2026-06-15: Bổ sung mockup `candidate-profile.html` — màn Hồ sơ của tôi (xem/chỉnh sửa thông tin candidate).
  - Section nav dính (sticky) + theo dõi cuộn: Thông tin cá nhân, CV & tài liệu, Kỹ năng, Kinh nghiệm, Học vấn, Liên kết, Tài khoản & bảo mật.
  - Banner đổi ảnh bìa/avatar, form thông tin cá nhân (email đã xác minh, readonly), quản lý nhiều CV (mặc định/xoá/upload), skill chips + gợi ý AI, timeline kinh nghiệm, học vấn, liên kết LinkedIn/GitHub/Portfolio.
  - Tài khoản & bảo mật: đổi mật khẩu, Google đã liên kết, toggle thông báo email, xoá tài khoản; thanh "Lưu thay đổi" dính đáy.
  - Nối link user-menu "Hồ sơ của tôi" ở job-board/job-detail/candidate-applications; thêm "Cá nhân" vào prototype nav toàn bộ mockup.

- [x] 2026-06-16: Bổ sung 3 mockup candidate còn thiếu — hoàn tất luồng user-menu candidate.
  - `candidate-saved-jobs.html` — Việc đã lưu: grid card (bỏ lưu, match score, trạng thái còn hạn/đã đóng), sort, xoá tất cả, empty-state hint.
  - `candidate-results.html` — Kết quả & lịch phỏng vấn (Candidate Portal): lịch sắp tới, danh sách vòng đã hoàn thành, Evaluation Report (verdict + overall, recording player, điểm theo tiêu chí, đánh giá ngôn ngữ language-aware, phân tích từng câu hỏi, bước tiếp theo, nhận xét HR).
  - `candidate-settings.html` — Cài đặt: chọn theme sáng/tối/hệ thống, ngôn ngữ UI, ma trận thông báo Email/Đẩy, quyền riêng tư (HR xem hồ sơ, lưu bản ghi, xuất dữ liệu), phiên đăng nhập/thiết bị.
  - Nối toàn bộ link user-menu (Việc đã lưu/Kết quả/Cài đặt) + icon bookmark header ở job-board/candidate-applications/candidate-profile; thêm "Đã lưu/Kết quả/Cài đặt" vào prototype nav của tất cả 16 mockup.

- [x] 2026-06-16: Bổ sung mockup `candidate-notifications.html` — màn Tất cả thông báo.
  - Header row (đếm chưa đọc, đánh dấu tất cả đã đọc, link sang cài đặt thông báo), tab lọc (Tất cả/Chưa đọc/Phỏng vấn/Kết quả/Hệ thống).
  - Danh sách nhóm theo thời gian (Hôm nay/Hôm qua/Trước đó), item có icon theo loại, trạng thái chưa đọc, deep-link hành động (xem mã, xem báo cáo, feedback...), nút tải thêm.
  - Nối link "Xem tất cả thông báo" trong bell dropdown của 7 trang candidate → trang mới; bell ở header trỏ thẳng sang khi đang ở trang này; thêm "Thông báo" vào prototype nav của toàn bộ 17 mockup.

- [x] 2026-06-16: Bộ mockup HR/Admin — dựng 7 trang còn thiếu &amp; nối toàn bộ link `#`.
  - `hr-jobs.html` — Tin tuyển dụng: stats, filter, bảng quản lý tin (đang tuyển/nháp chờ duyệt/đã đóng), số ứng viên, vòng PV, phân trang.
  - `hr-job-edit.html` — Tạo/sửa tin (Enterprise Setup): thông tin cơ bản, upload JD PDF/DOCX + text (Gemini), cấu hình vòng PV (drag, độ khó), rubric chấm điểm có trọng số, persona AI + adaptive difficulty, đính kèm Playbook, availability slots.
  - `hr-candidates.html` — Ứng viên: stats, bảng (match score, vòng, verdict AI), thao tác cấp Interview Code / duyệt verdict / gửi magic link, bulk select.
  - `hr-sessions.html` — Phiên phỏng vấn: phiên LIVE (giám sát), bảng phiên Real/Practice với bản ghi + transcript + báo cáo.
  - `hr-playbook.html` — Playbook: upload zone, lọc theo phạm vi (Công ty/Tin/Vòng), card trạng thái embed (pgvector) + chunks + must-ask.
  - `hr-team.html` — Nhóm HR: quản lý tài khoản role-based (Super Admin/HR Leader/Recruiter), pre-provisioning + chờ kích hoạt, allowed email domains.
  - `hr-settings.html` — Cài đặt workspace: domain &amp; bảo mật, mặc định phỏng vấn (TTL code/magic link, auto-progression), tích hợp/webhook (ATS/Slack/SendGrid), audit log.
  - Nối toàn bộ link `#` trong hr-dashboard &amp; hr-evaluation: sidebar (6 mục), account menu, notif dropdown, panel "Cần làm", "Tạo tin", trợ giúp → 0 link `#` còn sót.
  - Sidebar HR `sticky top-0 h-screen` + nav `overflow-y-auto` (cố định 1 màn khi cuộn); thêm `scrollbar-gutter:stable` mọi trang HR để tránh lệch chiều rộng giữa các trang.

- [x] 2026-06-16: Màn quên/đặt lại mật khẩu (auth).
  - `auth-forgot.html` — nhập email gửi liên kết đặt lại (TTL 15 phút, one-time) + trạng thái "đã gửi" (resend countdown 30s, đổi email).
  - `auth-reset.html` — đặt mật khẩu mới: chỉ báo độ mạnh 4 mức, kiểm tra khớp xác nhận, trạng thái thành công; link yêu cầu lại khi hết hạn.
  - Nối link "Quên mật khẩu?" ở auth-login (ứng viên) &amp; auth-admin (nội bộ) → auth-forgot; back-link dùng history.back() để về đúng cổng đăng nhập.

- [x] 2026-06-17: Triển khai code FE màn Quên/Đặt lại mật khẩu (end-to-end, theo mockup auth-forgot/auth-reset).
  - `ForgotPasswordPage.tsx` — redesign sang light theme (brand/ink/ai), 2 trạng thái request → "đã gửi" (hiện email, resend cooldown 30s, "Dùng email khác"); gọi `authService.forgotPassword` → `POST /auth/candidate/forgot-password`.
  - `ResetPasswordPage.tsx` — redesign light theme: chỉ báo độ mạnh 4 mức, kiểm tra khớp xác nhận realtime, validate mật khẩu mirror backend (≥8 ký tự + chữ hoa + chữ số + ký tự đặc biệt !@#$%^&amp;*), trạng thái thành công; đọc `token`+`email` từ query, gọi `authService.resetPassword` → `POST /auth/candidate/reset-password`.
  - Flow đầy đủ: forgot → email link `{frontendUrl}/auth/reset-password?token=&email=` (backend AuthController) → reset → về `/auth/candidate-login`. TTL link 2 giờ (đồng bộ backend, không phải 15 phút như mockup).
  - Lưu ý còn lại: staff LoginPage cũng trỏ "Quên mật khẩu?" → /auth/forgot-password nhưng backend chỉ có endpoint recovery cho Candidate (HR/Recruiter pre-provisioning, chưa có luồng recovery).

- [x] 2026-06-18: Tách riêng luồng forgot/reset password cho staff nội bộ (quyết định "tách riêng — staff có endpoint riêng").
  - BE: thêm `POST /auth/staff/forgot-password` + `POST /auth/staff/reset-password` (query bảng `Users`, anti-enumeration, chỉ gửi khi `IsActive`); tài khoản SSO-only vẫn đặt được mật khẩu lần đầu qua link.
  - BE: thêm cột phân loại `MagicLink.Audience` (`candidate`|`staff`, hằng `MagicLinkAudience`); reset link đính kèm `&audience=`; cả 2 endpoint reset lọc đúng audience để token 2 cổng không lẫn nhau. Đổi tên `IsValidCandidatePassword` → `IsStrongPassword` (dùng chung).
  - DB: migration `AddAudienceToMagicLink` (cột `audience text NOT NULL DEFAULT 'candidate'` — bản ghi cũ coi như candidate). ⚠️ Cần chạy `dotnet ef database update` khi deploy.
  - FE: `authService.staffForgotPassword/staffResetPassword`; ForgotPasswordPage & ResetPasswordPage param hoá theo `?audience=` (đổi endpoint + back-link + trang đăng nhập đích); staff LoginPage trỏ "Quên mật khẩu?" → `/auth/forgot-password?audience=staff`. Dọn import thừa (`motion`, `Sparkles`) trong LoginPage.
  - Ghi chú kiến trúc: mở rộng ADR-023 — xem `.ai/architecture.md`.

- [x] 2026-06-18: Gửi email bất đồng bộ qua hàng đợi nền — bỏ độ trễ 3–4s ở forgot-password.
  - Nguyên nhân: `EmailService` (MailKit SMTP) gọi `await` đồng bộ trong request → connect+auth+send chặn 3–4s; email không tồn tại trả 0.14s, email thật trả 3–4s.
  - Thêm `IEmailQueue` + record `EmailQueueItem` (Application), `EmailBackgroundQueue` (Channel unbounded, single-reader, Singleton) + `EmailQueueHostedService` (BackgroundService, gửi trong DI scope riêng, log lỗi không sập vòng lặp) (Infrastructure); đăng ký trong Program.cs.
  - AuthController: đổi phụ thuộc `IEmailService` → `IEmailQueue`, 2 endpoint forgot-password (candidate + staff) `Enqueue` thay vì `await SendEmailAsync` → response trả về tức thì.
  - Build BE OK. ⚠️ Đang chạy qua Visual Studio (lock DLL) nên cần stop app + rebuild/restart trong VS để áp dụng & test latency.
  - Đã verify latency thực tế (dotnet run nền :5000): staff & candidate forgot-password với email tồn tại giảm 3–4s → ~0.26–0.43s; DB xác nhận token tạo đúng audience.

- [x] 2026-06-18: Guard auth + rà soát phân quyền + dọn màn cũ (FE).
  - GuestRoute: thêm `components/auth/GuestRoute.tsx` — đã đăng nhập mà vào /auth/login, /auth/register, /auth/candidate-login, /auth/candidate-register → redirect về home theo role (tránh đổi URL để quay lại màn đăng nhập khi còn phiên). Bọc 4 route này trong App.tsx; chừa lại forgot/reset/callback.
  - Phân quyền: rà soát ProtectedRoute — đã chặn đúng (chưa auth → login theo cổng; sai role → /403). Toàn bộ nhóm route super-admin/hr/recruiter/candidate/interview đều bọc ProtectedRoute; kiosk public là chủ ý. Không có lỗ hổng "đổi route vào được màn".
  - Xoá 17 màn cũ chết (không còn import ở đâu, là bản trước redesign): toàn bộ `pages/admin/` (13 file — đã được `pages/hr/` thay thế), `recruiter/JobPostingsPage`, `recruiter/JobPostingDetailPage`, `candidate/DashboardPage`, `candidate/MyApplicationsPage`; dọn export thừa trong `candidate/index.ts`.
  - ⚠️ Các màn style cũ CÒN được route (hr/recruiter/candidate dashboards, super-admin...) vẫn đang dùng → không xoá được, cần REDESIGN theo mockup (việc riêng). Repo còn nhiều lỗi `noUnusedLocals`/type có sẵn từ trước (≈28) làm `npm run build` fail — chưa thuộc phạm vi task này.

- [x] 2026-06-18: Redesign + wire màn HR Jobs theo mockup hr-jobs.html (màn redesign #1).
  - Bối cảnh: HrLayout là layout DUY NHẤT đã sang theme mới (ink/brand); candidate/recruiter/super-admin layout vẫn theme cũ (redesign cả area = cascade, làm sau). Chọn HR Jobs vì shell sẵn + backend sẵn.
  - BE (#2 bổ sung data): thêm `ApplicantCount` vào `JobPostingListItemResponse`; `GET /jobs/admin` (GetAdminJobs) đếm ứng viên theo từng tin (batch, tránh N+1).
  - FE (#3 wire + #1 redesign): thêm `applicantCount`/`publishedAt` vào type JobPosting; viết lại `hr/JobsPage.tsx` — bỏ mock, fetch thật qua `jobService.getAdminJobPostings()`, stats thật, tab lọc theo trạng thái (Tất cả/Đang tuyển/Nháp/Tạm dừng/Đã đóng), loading/error/empty states, badge trạng thái + Gấp, hiển thị phòng ban/địa điểm/ngôn ngữ/số ứng viên/ngày tạo.
  - Verify thật (mint staff JWT gọi /jobs/admin): 200, 6 tin, applicantCount đúng (0/0/0/1/2/9), có trên mọi item. BE build OK, FE typecheck màn này sạch.

- [x] 2026-06-18: Redesign + wire màn HR Candidates theo mockup hr-candidates.html (màn redesign #2).
  - BE (#2 bổ sung data): thêm `MatchScore` (int?) vào `ApplicationResponse`; `GetAllApplicationsAsync` join `cv_jd_analyses` theo batch (tránh N+1) để gắn điểm match CV–JD.
  - FE (#3 wire + #1 redesign): thêm type `HrApplicationItem` (khớp JSON thật), sửa `applicationService.getApplications` trả mảng phẳng (trước typing sai PaginatedResponse, chưa ai gọi); viết lại `hr/CandidatesPage.tsx` — bỏ mock, fetch thật `GET /applications`, search (tên/email/vị trí), lọc theo nhóm trạng thái, stats thật, cột Match score, badge trạng thái map từ status thô backend, action gửi magic link (`sendInvite`) + xem chi tiết.
  - Verify thật (staff JWT gọi /applications): 200, 12 hồ sơ, matchScore có trên mọi item; app có phân tích trả đúng 92, còn lại null. BE build OK, FE typecheck màn này sạch.

- [x] 2026-06-18: Redesign + wire màn HR Dashboard theo mockup hr-dashboard.html (màn redesign #3).
  - BE (#2 mới hoàn toàn): thêm `DashboardController` `GET /api/dashboard/hr` (Policy InternalStaff) + `DashboardDTOs` (HrDashboardResponse/FunnelStepDto/RecentCandidateDto). Tính KPI (tin đang tuyển, nháp, tổng hồ sơ, phiên PV AI, verdict chờ duyệt, đã tuyển), phễu tuyển dụng 5 bước, ứng viên gần đây (kèm match score + verdict/vòng mới nhất). pendingReviews = evaluations chưa có HrReview.
  - FE: thêm `dashboardService` + types; wire `hr/DashboardPage.tsx` — bỏ toàn bộ mock, KPI/phễu/ứng viên gần đây/"Cần làm" lấy số thật; greeting theo user + ngày hôm nay thật; bỏ phần "Phỏng vấn hôm nay" bịa số, AI Insight hiển thị số thật (tổng hồ sơ + verdict chờ).
  - Verify thật (staff JWT gọi /dashboard/hr): 200 — activeJobs=3, draftJobs=3, totalApplications=12, aiInterviews=3, pendingReviews=1, hired=2; phễu 12/1/2/2/2; 6 ứng viên gần đây (Nguyen Anh Quan match=92). Khớp chéo với /jobs/admin & /applications. BE build OK, FE typecheck sạch.

- [x] 2026-06-18: Redesign + wire màn HR Interview Sessions theo mockup hr-sessions.html (màn redesign #4).
  - BE (#2 mới): thêm `GET /api/interview/sessions` (Policy InternalStaff) trong `InterviewController` + method `InterviewService.GetSessionsForHrAsync` + DTO `HrInterviewSessionItem`. Join InterviewSession + Application (tên ứng viên) + JobPosting (vị trí) + Evaluation mới nhất theo (application, round) → verdict; trả kèm sessionType (thử/thật), roundNumber/roundType, durationSeconds, hasRecording, status.
  - FE (#3 wire + #1 redesign): thêm `interviewService.getHrSessions()` + type `HrInterviewSessionItem`; viết lại `hr/InterviewSessionsPage.tsx` — bỏ mảng cứng mock, fetch thật, stats (tổng/đang diễn ra/hoàn thành/có ghi hình), search (tên/vị trí), tab lọc trạng thái, badge verdict Pass/Not Pass + trạng thái, hiển thị vòng/loại/thời lượng/ghi hình, nút Xem điều hướng tới evaluation hoặc hồ sơ ứng viên.
  - Verify thật (staff JWT gọi /interview/sessions): 200 — 3 phiên (John Doe round1 pass/round2 not_pass, Phong VG round1 pass), join đúng vị trí "Senior Backend Engineer", verdict + evaluationId liên kết đúng. BE build OK, FE typecheck sạch.

- [x] 2026-06-18: Redesign + wire màn HR Pending Jobs (tin chờ duyệt) theo mockup hr-pending-jobs (màn redesign #5).
  - BE (#3 bổ sung dữ liệu sẵn có cho FE): enrich `JobPostingListItemResponse` thêm SalaryMin/Max/Currency/IsNegotiable, CreatedByUserId, CreatedByName, RejectionReason; `GET /jobs/admin` resolve tên người tạo theo batch (join Users, tránh N+1). Tận dụng `PATCH /jobs/{id}/status` có sẵn cho duyệt/từ chối.
  - FE (#3 wire + #1 redesign): thêm `jobService.updateJobStatus(id,status,reason?)`; mở rộng type JobPosting.status (draft|pending|active|paused|rejected|closed|archived) + createdByName/rejectionReason; viết lại `hr/PendingJobsPage.tsx` — bỏ mock, fetch `GET /jobs/admin` lọc status=pending, stats thật (chờ duyệt/đang tuyển/bị từ chối/tạo hôm nay), nút Duyệt (→active) với spinner, modal Từ chối bắt buộc nhập lý do (→rejected + rejectionReason), cập nhật optimistic, loading/error/empty states.
  - Verify thật (staff JWT): GET /jobs/admin trả createdByName ("Nguyen Anh Quan"/"Alex HR Admin") + salary đúng; flip 1 job sang pending → hiện trong danh sách; PATCH approve pending→active = 200; reject thiếu lý do = 400 (chặn đúng), reject kèm lý do = 200 và rejection_reason lưu DB. Đã khôi phục job test về draft. BE build OK, FE typecheck sạch.

- [x] 2026-06-16: Bộ mockup Recruiter — workspace riêng theo quyền Recruiter (tạo tin nháp, quản lý ứng viên, cấp Interview Code).
  - `recruiter-dashboard.html` — KPI (ứng viên xử lý, cần cấp code, tin nháp chờ duyệt, on-site hôm nay), danh sách cần cấp code, tin nháp của tôi, lịch on-site, nhắc việc; sidebar rút gọn + ghi chú giới hạn quyền (không confirm verdict/Playbook).
  - `recruiter-candidates.html` — bảng ứng viên với trạng thái On-site (đã đến/chờ check-in), thao tác cấp code / magic link / xem (không có confirm verdict).
  - `recruiter-code.html` — màn cấp Interview Code: chọn ứng viên + vòng + TTL, mã 6 ký tự monospace + đếm ngược TTL live, QR cho Kiosk, sao chép/in/cấp lại/vô hiệu hoá, hàng chờ check-in, lịch sử mã (hiệu lực/đã dùng/hết hạn/vô hiệu).
  - Thêm entry "Recruiter" vào prototype nav của toàn bộ mockup (29 trang).

- [x] 2026-06-16: Tách workspace Recruiter khỏi trang HR (sửa link lẫn lộn sang HR Leader).
  - `recruiter-jobs.html` — Tin tuyển dụng của Recruiter: nháp/chờ HR duyệt/đang tuyển, chỉ sửa nháp của mình, tin đã đăng read-only (HR quản lý).
  - `recruiter-job-edit.html` — Tạo/sửa tin nháp: thông tin cơ bản + JD + đề xuất vòng, hành động "Gửi HR duyệt" (rubric/persona/Playbook do HR hoàn thiện), luồng duyệt 3 bước.
  - `recruiter-account.html` — Tài khoản của tôi (cá nhân + giao diện + bảo mật, không có cấu hình hệ thống — Super Admin quản lý).
  - Bỏ mục "Phiên phỏng vấn" khỏi sidebar Recruiter (phạm vi HR); trỏ lại toàn bộ link hr-jobs/hr-job-edit/hr-settings/hr-sessions trong recruiter-dashboard/candidates/code sang bản recruiter. Chỉ giữ entry "HR" ở prototype nav.

- [x] 2026-06-15: Claude Code hooks (`.claude/`) cho quy ước ARISP — secret-guard, bash-guard, arch-guard, format, tasks-reminder.

- [x] 2026-06-15: Sửa lỗi schema lệch entity gây HTTP 500 ở `GET /api/jobs`.
  - Nguyên nhân: migration `InitialCreate` bị sửa tại chỗ (thêm 3 cột JD) sau khi đã apply, nên EF báo "up to date" và DB thật thiếu cột `jd_file_format`, `jd_file_name`, `jd_file_url`.
  - Thêm 3 cột thiếu vào bảng `job_postings` trên DB hiện tại.
  - Khôi phục `InitialCreate` về đúng trạng thái gốc (gỡ 3 cột JD khỏi `.cs`, `.Designer.cs`, `ModelSnapshot.cs`).
  - Tạo migration độc lập `20260614201133_AddJdFileFieldsToJobPosting` (Up: AddColumn x3, Down: DropColumn x3) để lịch sử migration trung thực cho cả DB mới lẫn DB đã seed.

- [x] 2026-06-02: Linked authentication service to the React frontend and ASP.NET Core backend auth bridge.
  - Added frontend auth config.
  - Added Candidate email/password auth flow with backend token exchange.
  - Added ASP.NET Core named JWT bearer validation and custom login endpoint.

- [x] 2026-06-06: Implemented the backend API endpoints for the Evaluation module.
  - Created `EvaluationsController` with endpoints for listing evaluations (with job posting and status filters), retrieving detailed evaluations by ID or Session ID, and fetching evaluations by `applicationId`.
  - Created `EvaluationService` using the Repository pattern via `IUnitOfWork` to separate query logic.
  - Created detailed response DTOs (`EvaluationDetailResponse`, `EvaluationListItemResponse`, and sub-DTOs) to parse JSONB database columns into typed objects.
  - Registered `EvaluationService` in dependency injection and built successfully.

- [x] 2026-06-14: Backend Clean Architecture boilerplate với Repository Pattern + Unit of Work.
  - Cấu trúc 4 layer: `ARISP.API`, `ARISP.Application`, `ARISP.Domain`, `ARISP.Infrastructure`.
  - Generic `Repository<T>` + `UnitOfWork` với lazy-init `ConcurrentDictionary`.
  - Tất cả 22 domain entities được đăng ký trong `ARISPDbContext`.

- [x] 2026-06-14: Domain entities đầy đủ cho toàn bộ hệ thống.
  - 22 entities: `User`, `CandidateAccount`, `JobPosting`, `Application`, `InterviewRoundConfig`, `InterviewSession`, `Question`, `Answer`, `InterviewCode`, `InterviewBooking`, `AvailabilitySlot`, `Evaluation`, `HrReview`, `PlaybookDocument`, `DocumentChunk`, `CheatDetectionSignal`, `MustAskTracking`, `AuditLog`, `RefreshToken`, `CandidateRefreshToken`, `MagicLink`, `WebhookDelivery`.
  - pgvector extension (`vector` type) cho `DocumentChunk.Embedding`.
  - JSONB columns cho dữ liệu phức tạp (ScoringRubric, PersonaConfig, CriterionScores, CheatSignals, v.v.).
  - Soft-delete pattern (`ISoftDelete` interface + query filter).

- [x] 2026-06-14: AI provider interfaces và OpenAI implementation.
  - `IAIProvider` interface với các method: question generation, answer analysis, evaluation, language detection.
  - `IEmbeddingProvider` interface + `OpenAIEmbeddingProvider` impl (`text-embedding-3-small`).
  - `OpenAIProvider` implementation (GPT-4o).
  - Mock stubs cho `ISTTProvider`, `ITTSService`, `IAvatarService`, `INotificationService`.

- [x] 2026-06-14: Auth hệ thống đầy đủ.
  - Candidate: email/password register + login.
  - Staff (HR/Admin/Recruiter): email/password login, pre-provisioning enforced.
  - Google OAuth2 Sign-In với domain validation + pre-provisioned email check.
  - Magic link auth cho Candidate Portal (SHA256 hash, TTL 15 phút, one-time-use).
  - JWT + refresh token cho cả HR (`RefreshToken`) và Candidate (`CandidateRefreshToken`).
  - `AuthController` với password reset, token revocation.
  - Authorization policies: `SuperAdminOnly`, `HrManagement`, `InternalStaff`.

- [x] 2026-06-14: Job Posting CRUD backend đầy đủ.
  - `JobsController` với đầy đủ CRUD + status workflow (draft → pending → active → rejected/closed/archived).
  - Cấu hình multi-round: `InterviewRoundConfig` per Job Posting.
  - Language detection từ JD: `JobDescriptionLanguageDetector`.
  - Trường `jd_file_url`, `jd_file_name`, `jd_file_format` cho upload file JD.
  - `PersonaConfig` (JSONB), `ScoringRubric` (JSONB) đã có trong entity.
  - `AvailabilitySlot` entity cho Practice scheduling.

- [x] 2026-06-14: Interview Code backend hoàn chỉnh.
  - `InterviewCodeService` generate 6-char alphanumeric code.
  - One-time-use: vô hiệu hóa ngay sau khi dùng.
  - TTL mặc định 2 giờ, bind với `application_id`.
  - `InterviewCode` entity với `UsedAt` tracking.

- [x] 2026-06-14: Interview Session infrastructure.
  - `InterviewSession`, `Question`, `Answer` entities với đầy đủ fields.
  - `InterviewService` hỗ trợ `round_number`, `round_type`, `session_type` (practice/real).
  - `MustAskTracking` + `PlaybookService` theo dõi câu hỏi bắt buộc.
  - `PlaybookDocument` entity với scope (company/job_posting/round).
  - `DocumentChunk` với pgvector embedding cho RAG.

- [x] 2026-06-14: SignalR Hubs.
  - `SessionHub`: session lifecycle events (start, question-sent, answer-received, session-end).
  - `WebRTCSignalingHub`: SDP/ICE signaling cho avatar streaming.

- [x] 2026-06-14: Frontend boilerplate đầy đủ (React + TypeScript + Vite + TailwindCSS). _(MUI gỡ bỏ 2026-06-18)_
  - 59 pages theo role: admin, hr, recruiter, candidate, super-admin, interview, landing.
  - `ProtectedRoute` + Google OAuth2 callback handler.
  - Zustand stores: auth, application, interview.
  - API services layer: auth, job, application, evaluation, interview, schedule.
  - Custom hooks: interview, cheat-detection.

- [x] 2026-06-14: Infrastructure (Docker + Nginx + Logging).
  - `docker-compose.yml` (dev) + `docker-compose.prod.yml` (prod).
  - Nginx reverse proxy config với SSL support.
  - Serilog rolling file logs (`logs/arisp_api_log.txt`).
  - `ErrorHandlingMiddleware` global exception handler.
  - EmailService với MailKit (HTML email, magic links, notifications).
