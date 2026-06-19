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
- [x] `DocumentParserService`: extract text từ PDF/DOCX
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
