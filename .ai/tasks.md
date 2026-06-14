# Tasks – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

> Cập nhật file này sau mỗi task hoàn thành hoặc khi bắt đầu task mới.
> AI tools phải đọc file này trước khi bắt đầu bất kỳ việc gì để tránh làm trùng hoặc mâu thuẫn.

---

## Trạng thái hiện tại

**Phase:** 0 – Setup & Foundation  
**Last updated:** 2026-06-14

---

## Đang làm (In Progress)

_Chưa có task nào đang thực hiện._

---

## Backlog (Chưa bắt đầu)

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
- [ ] Database schema: thêm `jd_file_url`, `jd_file_name`, `jd_file_format` vào `job_postings`
- [ ] Database schema: tạo bảng mới `cv_jd_analyses` (match_score, summary, skills_matched, skills_gaps, experience_relevance, overall_recommendation, raw_response)
- [ ] Database schema: thêm `cv_jd_analysis_id` vào `applications` (FK → `cv_jd_analyses`, nullable)
- [ ] EF Core migrations
- [ ] `IGeminiProvider` interface + `GeminiProvider` impl (Google Gemini 2.5 Flash API)
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
- [ ] `RagService`: chunk JD/CV, embed, lưu pgvector, retrieve context khi sinh câu hỏi
- [x] `IAIProvider` interface + `OpenAIProvider` impl (GPT-4o streaming)
- [ ] AI question generation với RAG context + adaptive difficulty (interface có nhưng full pipeline chưa hoàn chỉnh)
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
- [ ] `DocumentParserService`: extract text từ PDF/DOCX
- [x] `PlaybookService`: chunk, embed (qua `IEmbeddingProvider`), lưu vào pgvector với scope tag
- [x] `PlaybookService`: track must-ask questions đã hỏi trong session (`MustAskTracking` entity)
- [x] `InterviewService`: nhận signal must-ask chưa xong trước khi kết thúc session
- [ ] `RagService`: cập nhật retrieve logic – merge JD/CV chunks + Playbook chunks theo weighted scope
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
- [ ] Quản lý tài khoản HR nhân viên (chỉ Super Admin thực hiện):
  - [ ] Mời hoặc phân quyền tài khoản HR (Super Admin, HR Leader, Recruiter)
  - [ ] Phân quyền theo department
- [ ] **System Settings UI (Chỉ dành cho Super Admin):**
  - [ ] Cấu hình allowed_email_domains (ví dụ: `fsoft.vn, fpt.com`)
  - [ ] Cấu hình global webhooks (ATS webhook url/secret, Slack/Teams webhook urls)
- [ ] Audit log dashboard: Super Admin xem toàn bộ audit trail hệ thống

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

- [x] 2026-06-14: Frontend boilerplate đầy đủ (React + TypeScript + Vite + TailwindCSS + MUI).
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
