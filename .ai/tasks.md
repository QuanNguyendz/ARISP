# Tasks – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

> Cập nhật file này sau mỗi task hoàn thành hoặc khi bắt đầu task mới.
> AI tools phải đọc file này trước khi bắt đầu bất kỳ việc gì để tránh làm trùng hoặc mâu thuẫn.

---

## Trạng thái hiện tại

**Phase:** 0 – Setup & Foundation  
**Last updated:** 2026-06-02

---

## Đang làm (In Progress)

_Chưa có task nào đang thực hiện._

---

## Backlog (Chưa bắt đầu)

### Phase 0 – Foundation

#### GitHub & Source Control
- [ ] Tạo GitHub repository (private, tên `ARISP`)
- [ ] Thêm toàn bộ thành viên vào repo với quyền phù hợp (Admin / Write)
- [ ] Thiết lập **branch strategy:**
  - `main` – production-ready, chỉ merge qua Pull Request được review
  - `develop` – integration branch, merge từ các feature branch
  - `feature/<tên-feature>` – ví dụ: `feature/auth-jwt`
  - `fix/<mô-tả-lỗi>` – ví dụ: `fix/jwt-refresh-token-expiry`
- [ ] **Branch protection rules** cho `main` và `develop`:
  - Require PR + ít nhất 1 reviewer approve trước khi merge
  - Require CI checks pass (sau khi có GitHub Actions)
  - Không cho phép force push
- [ ] Tạo `.gitignore` cho backend (.NET: bin/, obj/, *.user, appsettings.*.json)
- [ ] Tạo `.gitignore` cho frontend (node_modules/, dist/, .env*)
- [ ] Tạo **PR template** (`.github/pull_request_template.md`): mô tả thay đổi, checklist, link task
- [ ] Thống nhất **commit message convention** (đã có trong `coding-rules.md`):
  - Format: `<type>(<scope>): <mô tả ngắn>`
  - Type: `feat` | `fix` | `refactor` | `docs` | `test` | `chore`
- [ ] Tạo **GitHub Issues** cho từng Phase/task (gán assignee theo phân công)
- [ ] Tạo **GitHub Projects board** (Kanban: Backlog → In Progress → Review → Done)
- [ ] Setup **GitHub Secrets** cho CI/CD sau này (OPENAI_API_KEY, DB_CONNECTION_STRING, ...)

#### Project Structure & Boilerplate
- [ ] Tạo cấu trúc thư mục dự án (`backend/`, `frontend/`, `docker/`, `nginx/`, `scripts/`)
- [ ] Khởi tạo backend boilerplate (ASP.NET Core .NET 8, Clean Architecture, namespace `ARISP.*`)
- [ ] Khởi tạo frontend boilerplate (React + TypeScript + Vite + TailwindCSS)
- [ ] Setup Docker + docker-compose (backend, frontend, postgres, redis)
- [ ] Setup Nginx config cơ bản

### Phase 1 – Global Settings & Auth Setup
- [ ] Database schema: `users`, `refresh_tokens`, `system_settings` (Không dùng `organizations` và `subscriptions`)
- [ ] EF Core migrations (loại bỏ IMultiTenant và các cột organization_id)
- [ ] Đăng ký / Đăng nhập endpoint (HR Admin + Recruiter + Super Admin)
- [ ] JWT issue + refresh token (không chứa organization_id claim)
- [ ] Role-based authorization middleware cụ thể cho 4 role (`SuperAdmin`, `HRAdmin`, `Recruiter`, `Candidate`)
- [ ] Magic link auth cho Candidate Portal (email + one-time token, TTL 15 phút)
- [ ] **OAuth2 & Domain Validation:** Tích hợp Google OAuth2 / Microsoft Entra ID cho HR Users
- [ ] **OAuth2 & Domain Validation:** Viết middleware validate email domain đăng nhập từ Google/Microsoft thuộc danh sách `allowed_email_domains` lưu trong bảng `system_settings`

### Phase 2 – Job Posting & Application
- [ ] Database schema: `job_postings`, `interview_round_configs`, `applications`
- [ ] EF Core migrations
- [ ] HR Admin / Recruiter: CRUD Job Posting
  - [ ] Thông tin cơ bản (tên vị trí, lĩnh vực, JD)
  - [ ] Cấu hình multi-round: số vòng, loại vòng (Screening/Technical), ngôn ngữ
  - [ ] Availability Slots (Practice): danh sách khung giờ, capacity cho phỏng vấn thử
  - [ ] Phỏng vấn thật: Bắt buộc On-site (Tại công ty) - Trường `interview_mode` mặc định là `'onsite'`
  - [ ] Scoring Rubric (optional): custom tiêu chí đánh giá
  - [ ] Interview Persona (optional): tên, giọng avatar AI
- [ ] Language detection khi tạo Job Posting: `LanguageDetectionService` gọi AI phân tích JD
- [ ] HR confirm/chỉnh language requirement trước khi publish
- [ ] Candidate invite flow: sinh invite link (signed JWT, 24–72h) → gửi email
- [ ] Candidate: nhận invite → submit CV + thông tin cá nhân (Application)
- [ ] CV upload & parse (PDF → text extraction)

### Phase 2b – Job Board & Practice Interview
- [ ] Database schema: `candidate_accounts` (self-registered), extend `job_postings` với flag `is_public_listing`
- [ ] EF Core migrations
- [ ] Candidate self-registration: email + password (role `Candidate`)
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
- [ ] Database schema: `availability_slots`, `interview_bookings`, `interview_codes`
- [ ] EF Core migrations
- [ ] **Practice (Remote):** Candidate chọn slot → booking → nhận nhắc nhở 24h/1h
- [ ] HR generate Interview Code (format `ARX-7K2P`, 6–8 ký tự alphanumeric) cho thi thật
  - [ ] One-time-use: vô hiệu hóa sau khi dùng
  - [ ] TTL: mặc định 2 giờ, cấu hình per Job Posting
  - [ ] Bind với `application_id` cụ thể
  - [ ] Batch generate cho nhiều ứng viên
- [ ] **On-site:** Kiosk mode frontend: nhập Interview Code → validate → vào interview room
- [ ] Audit log: ghi lại creation time, usage time, `application_id` cho mỗi Interview Code

### Phase 4 – AI Interview Core
- [ ] Database schema: `interview_sessions`, `questions`, `answers`, `document_chunks`
- [ ] Bật pgvector extension trên PostgreSQL
- [ ] `IEmbeddingProvider` interface + `OpenAIEmbeddingProvider` impl (`text-embedding-3-small` - gỡ bỏ organization_id)
- [ ] `RagService`: chunk JD/CV, embed, lưu pgvector, retrieve context khi sinh câu hỏi (không lọc theo organization_id)
- [ ] `IAIProvider` interface + `OpenAIProvider` impl (GPT-4o streaming)
- [ ] AI question generation với RAG context + adaptive difficulty
- [ ] Interview session flow: start → question loop → adaptive difficulty → end
- [ ] **Language-aware:** System prompt tự động điều chỉnh theo ngôn ngữ detect từ JD
- [ ] **Language-aware:** TTS voice selection theo ngôn ngữ (ElevenLabs multilingual)
- [ ] **Language-aware:** STT `languageCode` config theo ngôn ngữ (Google STT)
- [ ] Điều kiện dừng: AI tự dừng khi khai thác hết context JD + CV
- [ ] **Session Type – phân biệt `practice` vs `real`:**
  - [ ] `practice`: chỉ retrieve JD + CV chunks, không load Playbook, gated bởi eligibility check
  - [ ] `real`: retrieve JD + CV + Playbook chunks (full RAG pipeline), yêu cầu nhập Interview Code tại Kiosk.

### Phase 4b – Interview Playbook (Org Knowledge Base)
- [ ] Database schema: `playbook_documents`, `playbook_chunks` (scope: org/job_posting/round, type: style/competency/question_bank/... - không dùng organization_id)
- [ ] EF Core migrations
- [ ] Document upload endpoint (PDF, DOCX, TXT, Markdown, JSON)
- [ ] `DocumentParserService`: extract text từ PDF/DOCX
- [ ] `PlaybookService`: chunk, embed (qua `IEmbeddingProvider`), lưu vào pgvector với scope tag (không lây lẫn giữa các job/round)
- [ ] `PlaybookService`: track must-ask questions đã hỏi trong session
- [ ] `InterviewService`: nhận signal must-ask chưa xong trước khi kết thúc session
- [ ] `RagService`: cập nhật retrieve logic – merge JD/CV chunks + Playbook chunks theo weighted scope
- [ ] HR Admin UI: quản lý Playbook documents (upload, preview, xóa) per Job Posting/Round
- [ ] Validation: file size limit, format check, virus scan (optional)

### Phase 5 – Multi-round & Auto-progression
- [ ] Database schema: `interview_rounds`, `round_evaluations`
- [ ] Multi-round config: HR cấu hình danh sách vòng per Job Posting
- [ ] `InterviewService` hỗ trợ `round_number` và `round_type` per session
- [ ] Auto-progression: sau HR Leader duyệt Pass Round N → lưu kết quả
  - [ ] On-site: Recruiter hẹn lịch offline và generate Interview Code mới cho Round N+1 khi ứng viên đến công ty.
- [ ] Email notification cho Candidate khi được invite Round tiếp theo

### Phase 6 – AI Evaluation & HR Review
- [ ] Database schema: `evaluations`, `language_assessments`, `hr_reviews`, `audit_logs` (loại bỏ organization_id)
- [ ] AI Evaluation sau mỗi Round: Verdict + Score + Reasoning
- [ ] **Language Assessment** (Round 1, nếu có language requirement):
  - [ ] `IAIProvider.AssessLanguageProficiencyAsync()`: fluency, grammar, vocabulary, comprehension
  - [ ] Đưa vào Evaluation Report như criterion riêng
- [ ] Evaluation Report: per-question analysis + recommended next step
- [ ] HR Dashboard (Phân quyền rõ ràng cho 3 role: SuperAdmin, HR Leader, Recruiter):
  - [ ] Danh sách Application per Job Posting (filter, sort)
  - [ ] Xem Evaluation Report + recording per Application per Round
  - [ ] Confirm / Override verdict (HR Leader có quyền duyệt, Recruiter chỉ có quyền xem)
- [ ] `AuditLogService`: ghi lại mọi Confirm/Override với timestamp + HR user + reason
- [ ] Notification: email + in-app (SignalR) khi Evaluation hoàn thành, cần HR review
- [ ] Email kết quả cho Candidate sau khi HR Leader xác nhận

### Phase 7 – Media & Realtime
- [ ] Frontend: VAD (Voice Activity Detection) – detect near-end-of-speech, trigger early RAG
- [ ] Frontend: Stream audio chunks qua WebSocket lên backend
- [ ] `ISTTProvider` interface + `GoogleSpeechProvider` impl (streaming real-time, multilingual)
- [ ] `WhisperProvider` impl (batch, fallback)
- [ ] ElevenLabs TTS streaming (Flash v2.5, multilingual voice)
- [ ] HeyGen Avatar integration (Streaming Avatar API + Hybrid Idle Strategy)
- [ ] SignalR Hub: session lifecycle events (start, question-sent, answer-received, session-end)
- [ ] Interview recording: lưu trữ audio/video per session (cho HR review)

### Phase 8 – Cheat Detection
- [ ] Frontend: thu thập signals trong session
  - [ ] Eye tracking (webcam-based, `WebGazer.js` hoặc tương đương)
  - [ ] Response timing (thời gian từ câu hỏi → bắt đầu trả lời)
  - [ ] Tab switching / focus loss (browser Visibility API)
  - [ ] Speech pattern (reading cadence detection từ partial transcript)
- [ ] Backend: `CheatDetectionService`
  - [ ] Nhận signals từ frontend qua SignalR/WebSocket
  - [ ] Heuristic analysis + AI analysis
  - [ ] Generate `CheatScore` (0–100) + `CheatSignals[]`
- [ ] Tích hợp CheatScore vào Evaluation Report (section riêng)
- [ ] HR xem CheatScore + signals khi review (không auto-fail)

### Phase 9 – Candidate Portal
- [ ] Frontend: Candidate Portal (route riêng, auth bằng magic link)
- [ ] Magic link endpoint: validate token → issue session
- [ ] Candidate xem: danh sách Applications của mình
- [ ] Candidate xem per Application:
  - [ ] Recording phỏng vấn (nếu HR bật)
  - [ ] Transcript
  - [ ] Evaluation Report (phần HR cho phép share)
  - [ ] Feedback (nếu HR bật)
- [ ] HR Leader: cấu hình per Job Posting những gì Candidate được xem

### Phase 10 – System Configuration & Global Audit
- [ ] Quản lý tài khoản HR nhân viên (chỉ Super Admin thực hiện):
  - [ ] Mời hoặc phân quyền tài khoản HR (Super Admin, HR Leader, Recruiter)
  - [ ] Phân quyền theo department
- [ ] **System Settings UI (Chỉ dành cho Super Admin):**
  - [ ] Cấu hình allowed_email_domains (ví dụ: `fsoft.vn, fpt.com`)
  - [ ] Cấu hình global webhooks (ATS webhook url/secret, Slack/Teams webhook urls)
- [ ] Audit log dashboard: Super Admin xem toàn bộ audit trail hệ thống

### Phase 11 – Integrations
- [ ] **ATS Webhook (Global):**
  - [ ] Push events: `application.submitted`, `interview.completed`, `evaluation.confirmed`
  - [ ] Retry logic với exponential backoff
  - [ ] Webhook delivery log (success/failure per event)
- [ ] **OAuth2 & Domain validation:**
  - [ ] Google Workspace / Microsoft Entra ID OAuth2 provider integration
  - [ ] Domain parsing and allowed domain verification on callback
- [ ] **Slack/Teams Notifications (Global Webhook):**
  - [ ] HR nhận notification khi Evaluation cần Review
  - [ ] HR nhận notification khi Candidate schedule/reschedule

### Phase 12 – Infra & Deploy
- [ ] GitHub Actions CI/CD pipeline
- [ ] Deploy lên Ubuntu VPS
- [ ] SSL với Nginx
- [ ] Serilog + Grafana monitoring (track latency từng bước pipeline)
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

- [x] 2026-06-02: Linked Firebase project `arisp-auth-service` to the React frontend and ASP.NET Core backend auth bridge.
  - Added frontend Firebase SDK config via `VITE_FIREBASE_*`.
  - Added Candidate Firebase email/password auth flow with backend token exchange.
  - Added ASP.NET Core named Firebase JWT bearer validation and `POST /api/auth/firebase/candidate/login`.
