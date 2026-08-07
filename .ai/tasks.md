# Tasks – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

> Cập nhật file này sau mỗi task hoàn thành hoặc khi bắt đầu task mới.
> AI tools phải đọc file này trước khi bắt đầu bất kỳ việc gì để tránh làm trùng hoặc mâu thuẫn.

---

## Trạng thái hiện tại

**Phase:** 0 – Setup & Foundation  
**Last updated:** 2026-08-05

---

## Đang làm (In Progress)

_Chưa có task nào đang thực hiện._

---

## Backlog (Chưa bắt đầu)

### FE UI Redesign (mới) – từ mockup `design/mockups/`
- [ ] Áp design system + logo ARISP vào `frontend` thật (token màu brand/ai/ink, font Plus Jakarta Sans/Inter).
- [ ] Dark/light theme toggle toàn FE (lưu localStorage / `preferred_theme`), no-flash init.
- [ ] **i18n UI candidate VI/EN** (react-i18next) — [ADR-033]; cột `candidate_accounts.preferred_locale`.
  - [x] 2026-07-03 **Cập nhật Candidate pages sử dụng i18n** — Translate ProfilePage, ApplicationsPage, SchedulePage, FeedbackPage, SettingsPage, SavedJobsPage sử dụng react-i18next với candidate.json (vi/en).
  - [x] 2026-07-13 **Cập nhật FindJobPage và ApplicationsPage i18n** — Hoàn thiện i18n cho FindJobPage (sort options, salary labels, deadline, posted date) và ApplicationsPage (FILTERS, scheduleInfo, roundTypeLabel, CV tooltips, error messages). Thêm keys mới vào landing.json và candidate.json.
  - [x] 2026-07-13 **Cập nhật JobDetailPage i18n** — Hoàn thiện i18n cho trang chi tiết việc làm (header, tags, CV-JD match section, company info). Thêm keys mới vào landing.json (jobDetail section).
  - [x] 2026-07-16 **Cập nhật i18n cho HR/Recruiter Dashboard và Jobs pages** — Tạo cấu trúc modules/hr và modules/recruiter trong i18n, cập nhật DashboardPage.tsx (HR & Recruiter), JobsPage.tsx, PendingJobsPage.tsx, CandidatesPage.tsx, MyJobsPage.tsx sử dụng react-i18next.
  - [x] 2026-07-16 **Cập nhật i18n cho HR Jobs và Recruiter pages** — Hoàn thiện translation files (dashboard.json, jobs.json, candidates.json) với đầy đủ keys cho HR Dashboard, Jobs list, Candidates list, và Recruiter My Jobs.
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
- [x] Unit test Luồng 1 Configure Job Posting (UC-45/46/47/50) — `CreateJobCommand` (validate, draft của người tạo, sinh RoundConfig + ngôn ngữ vòng kế thừa detect, ingest JD vào RAG, báo realtime), `UpdateJobCommand` (phân quyền chủ tin/admin, chặn archived, tái tạo round chỉ khi đổi, broadcast khi active), `AnalyzeJdCommand` (parse→lưu→Gemini; PDF inline vs DOCX fallback; lỗi parse/lưu→Failure; Gemini lỗi vẫn trả file), `CreateJobSlotsCommand` (slot booked=0, job không tồn tại→NotFound) — **31 test mới, 176/176 pass** ✅ 2026-08-05
- [x] Unit test Luồng 2 Approve Job Posting (UC-48/77/78/79/80) — `UpdateJobStatusCommand` (workflow draft→pending→active/rejected, phân quyền Owner gửi duyệt vs HrAdmin/SuperAdmin duyệt/từ chối, bắt buộc lý do từ chối, đóng dấu duyệt PDF/DOCX best-effort không chặn, thông báo người tạo + nhóm hr_admin, chặn archive khi còn hồ sơ active) + `GetAdminJobsQuery` (mọi trạng thái gồm draft/pending, lọc theo người tạo, đếm ứng viên, gắn tên+vai trò người tạo, sắp mới nhất) — **35 test mới, 211/211 pass** ✅ 2026-08-05
- [x] Unit test Luồng 4 Screen Application (UC-53/54/55/56/57) — danh sách ứng viên `GetApplicationsByJob/ForCreator/AllApplications` (sắp mới nhất trước, không kèm CvText, kèm điểm CV-JD, lọc đúng job/người tạo), làm giàu `MapApplications` (vòng hiện tại từ trạng thái + invite/session, điểm từ Evaluation "real", cờ đã đặt lịch + giờ hẹn từ Booking/Slot), chi tiết `GetApplicationById` (trả CvText, nạp CvJdAnalysis, giờ hẹn + trạng thái xác nhận/lý do báo bận), `GetJobApplicationsQuery` (Recruiter chỉ tin mình vs HrAdmin/SuperAdmin mọi tin, resolve CvFileUrl) + wrapper `GetApplicationById/GetApplications` (lỗi→NotFound, mine=creator vs toàn bộ, resolve URL). Accept/Reject đã phủ ở luồng Application. — **30 test mới, 277/277 pass** ✅ 2026-08-05
- [ ] HR confirm/chỉnh language requirement trước khi publish (UI chưa làm)
- [ ] Candidate invite flow: sinh invite link (signed JWT, 24–72h) → gửi email
- [x] Candidate: nhận invite → submit CV + thông tin cá nhân (Application) (Backend)
- [x] CV upload & parse (PDF → text extraction) (Backend with CV parser stub)
- [x] Unit test luồng Application (`ApplicationService`) — `SubmitApplicationAsync` (chặn tin đóng/hết hạn, tạo cv_submitted, auto-link CV-JD theo hash, đẩy CV vào RAG, báo hr_admin/recruiter + ứng viên tự ứng tuyển), `UpdateApplicationStatusAsync` (bảng chuyển trạng thái hợp lệ, withdrawn điểm cuối, not_pass mở lại, case-insensitive), duyệt/từ chối vòng CV (`Accept`/`Reject`/`SendInterviewInvite` — screening + InterviewInvite + email + xoá invite cũ chưa dùng), `CheckPracticeEligibilityAsync` (1 lượt/vòng) — **36 test mới, 145/145 pass** ✅ 2026-08-05

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
  - [x] Unit test `CvJdAnalysisService` — reuse theo (job + CvHash) không gọi lại Gemini, CV không hợp lệ lưu bản "failed", lỗi AI không persist, enrich reasoning từ RawResponse cache, truy vấn theo id/application, kiểm tra sở hữu, xoá cache — **13 test mới, 93/93 pass** ✅ 2026-08-05
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
- [x] Unit test Luồng 6 Practice Interview (UC-42/43) — tiến hành buổi thử (`InterviewService`, AI/TTS fake): `StartSession` (gating 1 lượt/vòng, cờ 0=không giới hạn, không seed must-ask, ReportLanguage theo UI), `SaveAnswer` (not found/inactive/persist), `GenerateAndSendNextQuestion` (sinh+publish text+audio, cap 12 câu / marker `[END_INTERVIEW]` → đóng phiên), `EndSession`/`GenerateEvaluationReport` (practice **không đổi status hồ sơ + không báo HR** ADR-051, idempotent), `PracticeTimeoutClose` (guard 95% trần 20'). Xem lại (`PortalPracticeFeature`): IDOR, từ chối buổi thật, **ẩn verdict**, ghép nhận xét AI theo sequence, auto-link email, danh sách chỉ "practice" sắp mới nhất + score/turnCount. — **30 test mới, 351/351 pass** ✅ 2026-08-05
- [x] Unit test Luồng 3 Submit Application (UC-16/17/18/27/28) — `GetJobsQuery` (chỉ tin active+public chưa hết hạn; lọc search/category/experience/location/language; phân trang+tổng; sắp lương/urgent-first/độ-phù-hợp-CV), `GetJobByIdQuery` (khách chỉ xem active+public, staff xem draft, Recruiter chỉ tin mình, resolve URL file JD), `GetJobFacetsQuery` (đếm facet trên tin active+public, gộp nhãn intern/fresher), `SubmitApplicationCommand` (hash MD5+parse+lưu file+ủy quyền service, lỗi parse/lưu→Failure, lỗi DB dọn file), `GetCvMatchQuery` (nhánh xác định: unauthorized/none/failed/cache completed+failed) — **36 test mới, 247/247 pass** ✅ 2026-08-05
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
  - [x] Practice Session: Evaluation Report riêng, **chỉ ứng viên xem — HR/Recruiter bị ẩn hoàn toàn** (đảo chiều "HR xem được" theo ADR-051), không ảnh hưởng verdict ✅ 2026-08-05
  - [x] Practice Session: xem lại transcript + nhận xét AI vĩnh viễn — `GET /api/portal/practice/sessions[/{id}]` + trang `/candidate/practice/:sessionId` (ADR-051) ✅ 2026-08-05
  - [ ] Disable nút "Phỏng vấn thử" sau khi đã dùng 1 lần

### Phase 2c – Online Test (Multiple Choice Quiz)
- [x] Database schema: `online_test_questions`, `online_test_submissions` (đã có từ InitialCreate) ✅ 2026-07-24
- [x] EF Core migrations — `AddOnlineTestFlow`: `job_postings.online_test_pass_score`, `online_test_questions.updated_at`, unique index `(application_id, round_number)` ✅ 2026-07-24
- [x] HR Admin / Recruiter: CRUD câu hỏi trắc nghiệm per Job Posting — `OnlineTestController` + trang `JobOnlineTestPage` ✅ 2026-07-24
- [x] Candidate: Thực hiện làm bài trắc nghiệm trên Candidate Portal (Giao diện web trắc nghiệm) — `OnlineTestPage` + `CandidateOnlineTestController` ✅ 2026-07-24
- [x] Backend: Tự động chấm điểm (Auto-scoring) sau khi nộp bài và so khớp đạt/không đạt dựa trên điểm sàn (`OnlineTestPassScore` trên JobPosting) ✅ 2026-07-24
- [x] Auto-progression (mềm): nộp bài → lưu `IsPassed` + realtime `OnlineTestGraded` + notification; kết quả hiện cho HR (`GET /online-test/applications/{id}/result`) để HR cấp Interview Code vòng tiếp — không tự đổi status (ADR-049) ✅ 2026-07-24
- [x] Unit test luồng Online Test — chấm điểm (khớp hoàn toàn, làm tròn 2 số, điểm sàn inclusive, chỉ chấm bộ đề đã bốc), gating (1 lượt/vòng, CV chưa duyệt, đã rút, ngân hàng rỗng, phân quyền), ẩn đáp án + bốc đề deterministic, validate câu hỏi — **33 test mới, 44/44 pass** ✅ 2026-08-05
- [x] Chống gian lận (mức đủ) bài trắc nghiệm: FE bắt `visibilitychange`/`blur` khi làm bài (khử trùng 500ms) → đếm số lần rời tab, banner nhắc + cảnh báo leo thang, gửi kèm khi nộp; BE lưu `OnlineTestSubmission.TabSwitchCount` (migration `AddOnlineTestTabSwitchCount`); HR thấy cột "Rời màn hình" (badge nghi vấn) ở bảng điểm + export `.xlsx` ✅ 2026-08-06
- [x] Loại vòng `online_test` trong form tạo tin: thêm option "Online Test / Trắc nghiệm" vào dropdown Loại vòng (`CreateJobPostingPage`) + hint; sửa nhãn hiển thị 3 nhánh ở `JobDetailPage`/`JobPostingDetailPage` (trước hiện nhầm "Screening") — hoàn tất phần UI còn thiếu của ADR-049 ✅ 2026-08-06

### Phase 3 – Scheduling (Practice) & Interview Code
- [x] Kiosk mode frontend: nhập Interview Code → phiên phỏng vấn thật (token phạm vi phiên), device check, phòng phỏng vấn có avatar, màn kết thúc tự reset (ADR-052) ✅ 2026-08-05
- [x] Ghi hình buổi phỏng vấn thật lên storage + tự xoá sau 7 ngày (`RecordingRetentionHostedService`), HR xem lại trong màn đánh giá (ADR-052) ✅ 2026-08-05
- [x] Database schema: `availability_slots`, `interview_codes` (entities đã có)
- [ ] Database schema: `interview_bookings` (entity đã có nhưng chưa migration)
- [ ] EF Core migrations
- [ ] **Practice (Remote):** Candidate chọn slot → booking → nhận nhắc nhở 24h/1h
- [x] Unit test luồng Scheduling (ADR-048) — HR gán slot (chốt chỗ nguyên tử/chống overbooking, screening→interview, bù trừ khi lưu lỗi, liên kết xếp-lại sau decline), ứng viên confirm/decline (trả chỗ slot, validate lý do), phân loại lịch Upcoming/Past/AwaitingReschedule — **36 test mới, 80/80 pass** ✅ 2026-08-05
- [x] Unit test Luồng 5 Schedule Interview (UC-39/40/41/58/59/60/61/62/63) — quản lý kho khung giờ (StaffScheduling): `GetAvailabilitySlotsQuery` (guard jobId, NotFound/Forbidden theo chủ tin-admin, sắp theo StartTime, lọc theo vòng), `CreateSlotCommand` (validate tương lai/end>start/capacity≥1/round≥1, NotFound job, Forbidden, tạo slot booked=0, mặc định timezone), `DeleteSlotCommand` (NotFound, Forbidden, chặn khi đã có người đặt, xoá khi trống), `UpdateSlotCapacityCommand` (NotFound, Forbidden, ≥1, không nhỏ hơn số đã đặt, cập nhật thành công). Assign/confirm/decline/candidate-schedule đã phủ ở luồng Scheduling. — **24 test mới, 301/301 pass** ✅ 2026-08-05
- [x] Unit test Luồng 7 Conduct Official Interview (UC-44/65/66/67) — `InterviewCodeService`: `GenerateCode` (gate đã đặt lịch buổi thật ADR-015/016, mã 6 ký tự bỏ I/O/0/1, audit `interview_code_generated`, TTL theo round config, screening→interview, báo ứng viên, suy vòng kế từ session completed), `GenerateBatch` (rỗng→Failure, chỉ cấp cho hồ sơ đủ điều kiện), `ValidateCode` tại Kiosk (rỗng/not_found/used/expired, mã hợp lệ → tạo phiên `real` + đánh dấu đã dùng + mint token Kiosk đúng phiên + audit `interview_code_used`, case-insensitive, StartSession lỗi → hoàn tác UsedAt + không mint token), `GetCodesByJob` (Active/Used/Expired + tên ứng viên, chỉ mã của job). — **20 test mới, 321/321 pass** ✅ 2026-08-05
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
  - [x] Unit test `SubmitHrReviewAsync` — Confirm (mọi nhân sự) vs Override (chỉ HR Admin/Super Admin + bắt buộc lý do, Recruiter bị chặn), cập nhật status hồ sơ pass/not_pass, auto-progression sang vòng kế (chỉ real + có RoundConfig → tạo InterviewInvite, status→interview; practice/not_pass không progress), thông báo ứng viên realtime + Notification chống trùng (DedupKey), audit log hr_confirm/hr_override — **16 test mới, 109/109 pass** ✅ 2026-08-05
  - [x] Unit test Luồng 8 Review Interview Result (UC-64/84–90/95) — query HR đọc/giám sát kết quả: `GetEvaluationsQuery` (ẩn buổi thử, lọc theo job + trạng thái pending/completed/verdict tôn trọng HR override, phân trang + total, join tên/vị trí/review), `GetEvaluationDetailQuery` (tra theo EvaluationId hoặc fallback SessionId, ẩn practice, kèm HR review + resolve URL video buổi thật ADR-052), `GetEvaluationsByApplicationQuery` (các vòng của hồ sơ, ẩn practice, trạng thái review), `GetSessionsForHrAsync` (bảng giám sát phiên: ẩn practice, sắp mới nhất, join ứng viên/vị trí/verdict AI mới nhất + cờ có video). Confirm/Override đã phủ ở test `SubmitHrReviewAsync`. — **24 test mới, 375/375 pass** ✅ 2026-08-05
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
- [x] GitHub Actions CI/CD pipeline — `ci.yml` (PR → develop/main) + `deploy.yml` (push main → GHCR → VPS pull) — 2026-07-22
- [x] Docker + docker-compose cho dev và production (`docker-compose.yml`, `docker-compose.prod.yml`)
- [x] Deploy lên Ubuntu VPS (arisp.io.vn, Docker Compose + Nginx + Let's Encrypt)
- [x] Tách config nginx prod ra `nginx/conf.d.prod/` để hết drift giữa repo và VPS — 2026-07-22
- [x] Đóng cổng redis/rag/backend/frontend khỏi internet ở prod (`ports: !reset []`) — 2026-07-22
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

- [x] 2026-08-07: **Đồng bộ màu trang Cài đặt của Recruiter theo HR Leader.** `recruiter/SettingsPage.tsx` trước dùng theme kính tối cứng (`bg-white/[0.03]`, `text-white`, nhấn amber) không có class light-mode → chữ trắng trên nền sáng bị "trôi" mờ. Nay dùng đúng bộ token của `hr/SettingsPage.tsx`: nền `bg-ink-50 dark:bg-ink-950`, thẻ `bg-white dark:bg-white/5` + `shadow-card`, tab active `brand-100/brand-700`, input `border-ink-200 focus:border-brand-400`, nút lưu gradient `from-brand-600 to-ai-600`, toggle `bg-brand-600`. Giữ nguyên namespace i18n `modules/recruiter/settings`, default values Recruiter, và mọi `t()` cũ. StaffSite `tsc --noEmit` xanh. Thuần FE, không đụng BE.

- [x] 2026-08-07: **Thông báo lỗi đăng nhập ứng viên chuyển sang tiếng Việt.** `CandidateLoginCommand` đổi cả 2 chỗ `"Invalid email or password."` → `"Sai email hoặc mật khẩu."` (khớp `StaffLoginCommand`); FE hiển thị thẳng message backend. Chỉ đổi chuỗi literal, không đụng logic/test.

- [x] 2026-08-07: **Email mời phỏng vấn gộp (qua CV + lịch + quy trình vòng) với 2 nút Xác nhận/Từ chối, trạng thái 3 mức, khoá quyết định + auto-reject quá hạn (bổ sung ADR-048).**
  - **Bối cảnh:** ADR-048 đã có HR gán lịch + ứng viên confirm/decline, nhưng nghiệp vụ mới muốn: (1) một email gộp báo qua vòng CV + lịch hẹn + **mô tả quy trình theo từng vòng**, kèm **2 nút Xác nhận / Từ chối**; (2) hiện rõ **3 trạng thái** (đã confirm / chưa confirm / đã từ chối); (3) chưa xác nhận trong hạn → **tự động Reject**; (4) bấm nút trong email **nhảy vào hệ thống** rồi hỏi "bạn chắc chưa?" + cảnh báo **không sửa lại được**. Chốt với user: auto-reject theo **TTL giờ kể từ lúc gửi mail**; Reject = xin đổi lịch (**giữ mô hình reschedule** của ADR-048); nút email = **deep-link cần đăng nhập**.
  - **BE — không cần migration** (tái dùng `confirmation_status`/`decline_reason`/`responded_at`):
    - `SchedulingOptions.ConfirmDeadlineHours` (mặc định 48, bind section `Scheduling`, `<=0` = tắt). `ScheduleConfirmationHostedService` quét 30'/lần: booking `scheduled`+`pending` quá `CreatedAt + TTL` → xử lý như decline (Status=`declined`, lý do `[Hệ thống]…`, trả chỗ slot, thông báo ứng viên bell + realtime, báo nhân sự `ReceiveScheduleResponse {auto:true}`) → vào `awaitingReschedule` để staff gán lại.
    - **Khoá quyết định:** `DeclineScheduleCommand` chặn khi đã `confirmed` ("đã xác nhận… không thể thay đổi"); confirm-sau-decline vốn đã bị chặn bởi guard `Status`. Mỗi lịch phản hồi 1 lần. *(Lưu ý: CHỦ ĐÍCH KHÔNG chặn xác nhận trùng khung giờ — ứng viên được phép xác nhận nhiều lịch trùng giờ; guard trùng giờ đã thử rồi gỡ theo yêu cầu 2026-08-07.)*
    - **Email gộp** trong `AssignSlotCommand` (inject `IConfiguration`): thêm dòng "qua vòng CV" (vòng 1), danh sách **quy trình theo vòng** từ `InterviewRoundConfig` (nhãn VN theo `RoundType`, đánh dấu vòng được mời), **2 nút deep-link** `…/portal/schedule/{appId}?booking={id}&action=confirm|decline`, hộp cảnh báo "mỗi lịch phản hồi 1 lần, không sửa lại + tự huỷ sau {TTL} giờ".
    - **Chỉ 1 email duy nhất** (2026-08-07 bổ sung): bước **duyệt CV KHÔNG gửi email** nữa (`SendInterviewInviteAsync` thêm cờ `sendEmail`, `AcceptApplicationAsync` gọi `sendEmail:false` — chỉ đổi trạng thái + tạo token + chuông "qua CV, chờ xếp lịch"); email gộp ở bước gán slot là email duy nhất chứa cả tin qua CV lẫn lịch. Test `Accept_promotes…` đổi assert sang `Empty(email.Sent)`; toast HR `acceptSuccess` reword (bỏ "đã gửi lời mời").
  - **FE:** `SchedulePage` — **hộp thoại xác nhận** cho cả confirm & decline ("Bạn đã chắc chắn chưa? Sau khi bấm không thể sửa lại"), **tự mở đúng hộp thoại** theo query `?booking=&action=`, **badge 3 trạng thái** (Đã xác nhận / Chưa xác nhận / Đã từ chối), khoá + note `Lock` khi đã xác nhận, state cần đăng nhập có nút về `candidate-login?returnUrl=`. `CandidateLoginPage` nhận `returnUrl` (chỉ path nội bộ) để quay lại sau đăng nhập. `AssignSchedulePanel` (staff) hiện badge tri-state rõ (Đã xác nhận/Chưa xác nhận) + nhãn "đã từ chối (xin đổi lịch)".
  - **Verify:** backend build 0 error; unit test **376 pass** (thêm test khoá decline-sau-confirm); 2 site FE `tsc --noEmit` xanh.

- [x] 2026-08-07: **Bỏ link "Đăng nhập nội bộ" (HR/Recruiter) khỏi màn đăng ký ứng viên.** `CandidateRegisterPage` gỡ đoạn `<p>` chứa `candidateRegister.isRecruiter` + `staffLoginLink` (link `/auth/login`) — trang đăng ký công khai không dẫn vào cổng nội bộ nữa; giữ link "Đăng nhập" ứng viên. Typecheck CandidateSite xanh.

- [x] 2026-08-06: **Thêm loại vòng `online_test` (trắc nghiệm) vào form tạo tin — hoàn tất phần UI còn thiếu của ADR-049.**
  - **Bối cảnh:** BE đã hỗ trợ đầy đủ round type `online_test` (`OnlineTestSupport.ResolveRoundAsync` map vòng trắc nghiệm theo `InterviewRoundConfig.RoundType=="online_test"`; dev seed đã dùng), `RoundType` là chuỗi tự do không ràng buộc enum — nhưng dropdown "Loại vòng" ở `CreateJobPostingPage` chỉ có Screening/Technical (đúng như ghi chú ADR-053).
  - **FE:** thêm `<option value="online_test">` vào dropdown + hint chỉ dẫn cấu hình ngân hàng câu hỏi/điểm sàn ở mục "Bài thi trắc nghiệm" sau khi tạo tin. i18n VI/EN (`form.onlineTest`, `form.onlineTestHint`).
  - **Sửa mislabel:** các nơi hiển thị round type vốn giả định nhị phân (`technical` ? technical : screening) khiến vòng `online_test` bị hiện nhầm "Screening" — nay 3 nhánh ở `JobDetailPage` (recruiter: tab + thẻ vòng) và `JobPostingDetailPage` (HR: tab + subtitle + badge màu emerald). i18n `roundOnlineTest` (recruiter/jobDetail) + `rounds.types.onlineTest` (hr/jobPostingDetail), VI/EN.
  - **Verify:** JSON 6 file hợp lệ; StaffSite `tsc --noEmit` xanh. Không đụng BE (đã sẵn sàng).

- [x] 2026-08-06: **Tab mở từ trước deploy tự phục hồi thay vì chết cứng (stale chunk).**
  - **Triệu chứng:** tab đang mở lúc deploy, bấm sang route lazy-load (vd Đăng nhập) thì đứng im; console đầy `Failed to load module script: Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of "text/html"` + `Failed to fetch dynamically imported module`. F5 thì hết.
  - **Nguyên nhân:** Vite băm hash vào tên chunk, image mới không còn file cũ. `docker/frontend/Dockerfile` chỉ có `location / { try_files $uri $uri/ /index.html; }` nên request `/assets/<chunk-cũ>.js` **rơi vào fallback SPA** → nginx trả `index.html` kèm `Content-Type: text/html` cho một file `.js` → trình duyệt chặn vì strict MIME. Xác minh trực tiếp: `GET /assets/CandidateLoginPage-7eNG9b-W.js` trả **200 + text/html**.
  - **Sửa (2 lớp):** (1) `ARI.Shared/src/utils/staleChunkReload.ts` — nghe `vite:preloadError` + `unhandledrejection`, tự `location.reload()` **đúng 1 lần** (cooldown 15s qua sessionStorage để asset mất thật không gây vòng lặp reload); gắn vào `main.tsx` cả 2 site. (2) Dockerfile: `location /assets/ { try_files $uri =404; }` — 404 thật thay vì HTML giả, kèm `Cache-Control: immutable` cho asset và `no-cache` cho `index.html` (index cache lại thì reload cũng vô nghĩa vì vẫn trỏ chunk đã xoá).
  - **Giới hạn:** không cứu được tab đang mở tại thời điểm deploy bản vá này (tab đó vẫn chạy code cũ) — có tác dụng từ lần deploy kế tiếp.
  - **Bổ sung sau khi quan sát thật trên prod (2026-08-06):** lỗi nổi lên dưới dạng `Uncaught TypeError: Failed to fetch dynamically imported module`, tức đi qua `window.onerror` **chứ không phải** `unhandledrejection` → thêm nhánh nghe `error`, nếu không thì đúng ca hay gặp nhất lại lọt lưới.
  - **Vì sao vá rồi mà người dùng vẫn gặp:** trước bản vá, `index.html` được trả **không kèm `Cache-Control`**, chỉ có `Last-Modified`. Chrome khi đó tự suy thời hạn ≈ 10% khoảng cách từ `Last-Modified` → image build từ 2026-07-22 (~15 ngày) cho ra **~1,5 ngày** cache mà không hỏi lại server. Và **mỗi URL là một entry cache riêng** (`/` khác `/auth/candidate-login` dù cùng trả một file), nên reload route này xong vào route khác vẫn dính bản cũ. Không có cách nào từ server chạm tới các entry đã nằm sẵn trong cache trình duyệt (`Clear-Site-Data`/đổi header đều cần trình duyệt chịu gọi lên server) — chỉ tự hết sau khi hết hạn heuristic. Người dùng mới không dính vì bản mới trả `no-cache`.

- [x] 2026-08-06: **Lỗi xem CV DOCX trên production — bucket R2 thiếu CORS rule.**
  - **Trạng thái prod (đo trực tiếp trong container đang chạy):** `docker exec arisp-backend printenv` → `Storage__Provider=S3`, `ASPNETCORE_ENVIRONMENT=Production`. Prod dùng R2, cấu hình đầy đủ.
  - **Đường đi thật:** màn HR/Recruiter mở CV gọi `GET /api/applications/{id}` → `GetApplicationByIdQueryHandler` **có** resolve `CvFileUrl` qua `_fileStorage.GetUrlAsync` → `S3FileStorageService` trả **presigned URL trên `*.r2.cloudflarestorage.com`** (khác origin).
  - **Vì sao chỉ DOCX chết:** trong `DocumentViewer`, PDF dùng `<iframe src>`, ảnh dùng `<img src>`, "Tải về" dùng `<a href>` — đều không bị CORS chặn. Riêng DOCX phải `fetch()` lấy blob cho `docx-preview` → thiếu CORS là hỏng. Local không tái hiện được vì `appsettings.json` để `Provider=Local`, key `/uploads/<guid>` tình cờ **cũng là đường dẫn phục vụ được** nên trả key thô vẫn chạy.
  - **Sửa:** đặt CORS rule trên bucket R2 (`GET`/`HEAD`, liệt kê đủ 3 origin, không dùng `*`). Xác minh bằng preflight: `staff.arisp.io.vn` và `arisp.io.vn` → 204 kèm đúng `Access-Control-Allow-Origin`; origin lạ → 403 không kèm header. Tài liệu: [docs/r2-storage-cors-setup.md](../docs/r2-storage-cors-setup.md) + ghi chú ADR-036.
  - **Bẫy chẩn đoán đã mắc phải (ghi lại để khỏi lặp):** dùng `GET /api/jobs/{id}` **ẩn danh** để đoán provider và thấy key thô (`cv/<guid>`), kết luận nhầm rằng prod đang chạy `Local`. Thực ra `GetJobByIdQuery` chỉ resolve URL **khi `isStaff`** — request ẩn danh không đi qua `GetUrlAsync` bao giờ. Muốn biết provider thật thì đọc thẳng biến môi trường trong container, đừng suy từ endpoint công khai.
  - **Kết quả:** đã xác nhận trên `staff.arisp.io.vn` — xem được file CV DOCX sau khi đặt CORS rule.
  - **19 hồ sơ key kiểu cũ `/uploads/<guid>`** (từ thời `Provider=Local`, chưa migrate sang R2 → presign vào R2 không có object nên hỏng link): **quyết định chấp nhận mất** (2026-08-06), không migrate. Kiểm tra lại thì thiệt hại giới hạn ở file gốc: **19/19 vẫn còn `cv_text`** đã trích xuất, **14/19 còn kết quả CV-JD analysis** — HR vẫn đọc được nội dung CV, chỉ không mở/tải được file gốc.

- [x] 2026-08-06: **Chống gian lận bài thi trắc nghiệm khi chuyển tab (mức "đủ" — FE bắt + BE lưu vết + HR thấy cờ).**
  - **Bối cảnh:** trước đây bài trắc nghiệm remote KHÔNG có bất kỳ chống gian lận nào (chỉ đếm giờ + tự nộp). Hạ tầng cheat-signal (`useKioskLockdown`/`RecordCheatSignalAsync`/`CheatDetectionSignal`, ADR-054) chỉ gắn cho phiên phỏng vấn Kiosk, không áp cho Online Test.
  - **FE (candidate):** `OnlineTestPage.tsx` thêm effect bắt `visibilitychange`(hidden) + `window.blur` **chỉ khi đang làm bài**, khử trùng 500ms để chuyển tab (thường bắn cả 2 sự kiện) không đếm gấp đôi. Đếm sống ở `useRef` (gửi kèm cả khi tự nộp lúc hết giờ), state để hiện UI: banner nhắc luôn hiển thị + cảnh báo amber leo thang theo số lần. i18n VI/EN (`antiCheatHint`, `tabSwitchWarning`).
  - **BE:** `OnlineTestSubmission.TabSwitchCount` (int, default 0) + migration `AddOnlineTestTabSwitchCount` (`tab_switch_count`, `nullable:false default 0` — an toàn với hàng cũ). `SubmitOnlineTestRequest`/`SubmitOnlineTestCommand` nhận `TabSwitchCount`; handler kẹp `Math.Max(0, …)` khi lưu. `OnlineTestScoreRowDto` + `OnlineTestResultsBuilder` trả `TabSwitchCount`; export `.xlsx` thêm cột "Rời màn hình" (0→"-").
  - **HR:** `JobOnlineTestResultsPage.tsx` thêm cột "Rời màn hình" — badge amber `AlertTriangle` + số lần khi `>0` (tooltip nghi vấn gian lận), gạch mờ khi 0. i18n VI/EN (`table.tabSwitches`, `tabSwitchFlag`, `tabSwitchNone`).
  - **Giới hạn (đã ghi chú):** đếm ở client → có thể bị spoof; đóng hẳn trình duyệt không nộp thì không có bản ghi (nhưng cũng không có kết quả). Bản chất là **răn đe + lưu vết**, không phải khoá cứng (khoá cứng = Kiosk `chrome --kiosk`, ADR-054).
  - **Verify:** `dotnet build` 0 error; `dotnet ef migrations add AddOnlineTestTabSwitchCount`; unit test **375/375 pass**; FE 2 site `tsc --noEmit` xanh.

- [x] 2026-08-06: **Release `develop` → `main` + deploy production (PR #272, tag `696b8a56`).**
  - Kiểm tra trước khi merge: `dotnet build -c Release` 0 error, **304/304 unit test pass**, `npm run build --workspaces` xanh cả CandidateSite lẫn StaffSite; CI trên PR xanh cả 3 job (backend / frontend / rag import).
  - Deploy workflow chạy hết: 4 image build & push GHCR → VPS `pull && up -d` → restart nginx → health check `candidate-api=200 staff=200`.
  - **Xác minh sau deploy (không chỉ dựa vào health check):** cả 6 migration mới (`AddOnlineTestFlow`, `AddOnlineTestScreening`, `AddUserSettings`, `AddBookingConfirmation`, `AddPracticeTranscriptReview`, `AddKioskInterviewRecording`) đã có trong `ef_migrations_history` trên DB production — quan trọng vì `AriDbContextInitialiser` **nuốt lỗi migration** (app vẫn boot khi migrate fail). Route mới trả 401/405 thay vì 404 → đúng là build mới đang chạy.

- [x] 2026-08-06: **Đóng cổng Postgres dev đang hở ra Internet trên VPS production.**
  - `docker-compose.prod.yml` reset `ports` cho **redis / rag / backend / frontend** nhưng **bỏ sót `postgres`** → base compose bind `0.0.0.0:5433` kèm mật khẩu dev mặc định. Docker tự chèn rule iptables nên binding này vượt mặt ufw: quét từ ngoài xác nhận `5433 open=True` (các cổng 5000/3000/3001/8000/6379 đều đóng đúng).
  - Prod dùng DB trên Supabase, **không dịch vụ nào đụng container này** → thêm `postgres: ports: !reset []` + giới hạn RAM 256M, khớp lại nguyên tắc "prod chỉ hở nginx 80/443" ghi ở đầu file.

- [x] 2026-08-06: **Sửa nhãn trường địa chỉ ở form tạo tin tuyển dụng.** `CreateJobPostingPage` đang lấy chính key placeholder (`form.workLocationPlaceholder`) làm `<label>` nên màn hình hiện "VD: Tòa nhà FPT, Quận 9, TP.HCM *". Thêm key `form.workAddress` ("Địa chỉ cụ thể" / "Specific address") cho nhãn, giữ nguyên câu ví dụ ở placeholder trong ô nhập.

- [x] 2026-08-05: **Unit test Luồng 8 — Review Interview Result (UC-64/84–90/95) — 24 test mới, tổng 375/375 pass.**
  - Phủ nốt các query HR đọc/giám sát kết quả phỏng vấn (Confirm/Override đã phủ ở `SubmitHrReviewAsync`). Bất biến chung: **đánh giá/phiên `practice` luôn bị ẩn khỏi nhân sự nội bộ** (ADR-051).
  - **`GetEvaluationsQueryHandlerTests` (8)** — danh sách phân trang: loại `practice`; lọc theo `JobPostingId`; trạng thái `pending`(chưa review)/`completed`(đã review)/`pass`/`not_pass` **tôn trọng HR override** (FinalVerdict thắng AiVerdict); phân trang + `Total`/`TotalPages` + sắp mới nhất trước; join tên ứng viên/vị trí/trạng thái review; rỗng→Total 0.
  - **`GetEvaluationDetailQueryHandlerTests` (7)** — NotFound; tra theo `EvaluationId`; **fallback theo `SessionId`**; **từ chối buổi thử**; app không tồn tại→Failure; kèm `HrReview` (FinalVerdict + IsOverride); **resolve URL video buổi thật** từ `InterviewSession.RecordingUrl` + `RecordingExpiresAt` (ADR-052).
  - **`GetEvaluationsByApplicationQueryHandlerTests` (4)** — app/job NotFound; loại `practice`; kèm trạng thái HR review. **`HrSessionsTests` (5)** — `GetSessionsForHrAsync`: loại `practice`; sắp mới nhất trước; join ứng viên/vị trí + **verdict AI mới nhất theo (hồ sơ, vòng)**; cờ `HasRecording`; rỗng.
  - Hạ tầng: `EvaluationReview/EvaluationData.cs` (factory Job/App/Eval/Review/Session; qualify `ARI.Domain.Entities.HrReview` tránh trùng namespace test `HrReview`). File: `tests/ARI.Application.UnitTests/EvaluationReview/{EvaluationData,GetEvaluationsQueryHandlerTests,GetEvaluationDetailQueryHandlerTests,GetEvaluationsByApplicationQueryHandlerTests,HrSessionsTests}.cs`. `dotnet test`: **375/375 pass**.

- [x] 2026-08-05: **Unit test Luồng 6 — Practice Interview (UC-42/43) — 30 test mới, tổng 351/351 pass.**
  - **`PracticeConductTests` (20)** — tiến hành buổi thử qua `InterviewService` với AI/TTS fake điều khiển được. `StartSessionAsync`: app/job NotFound; tạo phiên `practice` active (InterviewLanguage theo job, ReportLanguage theo UI ứng viên, `PracticeSessionUsed=true`); **gating 1 lượt/vòng** (đã dùng→Failure), `PracticeAttemptsPerRound=0`→không giới hạn, vòng khác không chặn; **không seed must-ask** (chỉ buổi thật). `SaveAnswerAsync`: session not found/inactive/persist transcript+responseTime. `GenerateAndSendNextQuestionAsync`: sinh câu hỏi + publish `ReceiveQuestion`+`ReceiveQuestionAudio` + TTS; **cap 12 câu** → đóng phiên (ClosingText + evaluation, không thêm câu); **marker `[END_INTERVIEW]`** → đóng phiên (lược marker). `EndSessionAsync`/`GenerateEvaluationReport`: `completed` sinh Evaluation `practice` **KHÔNG đổi `Application.Status` + KHÔNG báo `hr_admin`** (ADR-051/053); idempotent khi đã completed; status khác `completed` bỏ qua chấm. `PracticeTimeoutCloseAsync`: guard <95% trần 20'→Failure; quá giờ→đóng phiên + câu kết + evaluation; idempotent.
  - **`PracticeReviewTests` (10)** — xem lại buổi thử (`PortalPracticeFeature`, đọc anon object qua reflection). `GetMyPracticeReviewQuery`: session NotFound; **từ chối phiên `real`** (NotFound); **IDOR Forbidden** cho non-owner; success trả `Turns` + `Evaluation` + **KHÔNG có `AiVerdict`** (ADR-051); `EvaluationPending` khi completed chưa chấm; **ghép nhận xét AI vào đúng lượt theo `SequenceNumber`** (score/analysis/feedback); auto-link hồ sơ cũ theo email. `GetMyPracticeSessionsQuery`: rỗng khi không có hồ sơ; chỉ liệt kê phiên `practice` sắp mới nhất trước (loại `real`); kèm `HasEvaluation`/`OverallScore`/`TurnCount`.
  - Hạ tầng: overload `InterviewServiceFactory.Create(uow,notif,ai,tts,options?)` + `StubAiProvider`/`RecordingTtsService` (điều khiển câu hỏi/đánh giá/TTS), `RecordingNotificationService` thêm `SessionEvents`. File: `tests/ARI.Application.UnitTests/PracticeInterview/{PracticeData,PracticeConductTests,PracticeReviewTests}.cs`. `CheckPracticeEligibility` đã phủ ở luồng Application. `dotnet test`: **351/351 pass**.

- [x] 2026-08-05: **Khoá màn hình Kiosk + ghi nhận mọi lần rời buổi phỏng vấn (ADR-054).**
  - **Khoá màn hình:** `KioskPage` gọi `requestFullscreen()` ngay trong cú click "Bắt đầu phỏng vấn" (bắt buộc phải là thao tác người dùng); `KioskInterviewPage` phủ **lớp chặn toàn bộ giao diện** khi rời toàn màn hình, chỉ tiếp tục khi bấm "Quay lại toàn màn hình"; chặn context menu, phím tắt `F11/F5/Ctrl+P,S,U,F,T,N,W,R`, cảnh báo `beforeunload`; thoát fullscreen khi kết thúc.
  - **Ghi log:** hook mới `ARI.Shared/src/media/useKioskLockdown.ts` đếm + gửi 5 loại tín hiệu (`fullscreen_exit`, `tab_hidden`, `window_blur`, `shortcut_blocked`, `page_unload`) qua `POST /api/interview/session/{id}/signals`; lúc đóng trang dùng `fetch keepalive` (sendBeacon không đặt được Authorization).
  - **Nối hạ tầng chống gian lận đã có nhưng chưa chạy:** `IInterviewService.RecordCheatSignalAsync` lưu `CheatDetectionSignal` thật (chặn payload >2000 ký tự, ép JSON hợp lệ); `SessionHub.ReportCheatSignal` gọi service trước khi broadcast (trước đây chỉ broadcast); `GenerateEvaluationReportAsync` tính `CheatScore` theo trọng số từng loại (trần 100) và ghi `CheatSignals` gộp theo loại — trước đây ghi cứng `"[]"` nên HR không bao giờ thấy chi tiết.
  - **Minh bạch:** màn kết thúc Kiosk hiện "Ghi nhận N lần rời khỏi màn hình phỏng vấn".
  - **Giới hạn đã ghi rõ trong docs:** web không chặn được Alt+Tab/phím Windows → cần `chrome --kiosk` + Windows Assigned Access cho máy thật.
  - **Verify:** `ARI.Application`/`ARI.Infrastructure` build 0 error, `ARI.API` compile 0 error (build đầy đủ vướng khoá file do API đang chạy trong VS); CandidateSite build xanh.

- [x] 2026-08-05: **"Đạt" chỉ khi qua hết vòng + AI không tự đổi trạng thái + chất lượng báo cáo buổi thử (ADR-053).**
  - **Ràng buộc từng bước:** bỏ hẳn ghi `Application.Status` trong `GenerateEvaluationReportAsync` (AI chấm trượt không còn tự đánh rớt hồ sơ trước khi HR xem); `SubmitHrReviewAsync` tính trạng thái một lần theo tổng số vòng (`ResolveTotalRoundsAsync`): `not_pass` / `interview` (còn vòng) / `pass` (vòng cuối). `TriggerAutoProgressionAsync` không còn sửa trạng thái.
  - **Hiển thị tiến độ:** portal (danh sách + chi tiết) trả `TotalRounds`/`PassedRounds`; `ApplicationsPage` hiện badge **"Qua vòng N/M"**, ẩn banner quá hạn khi hồ sơ đã đóng, thêm nhãn `cv_rejected` (trước lọt chuỗi thô).
  - **Điểm từng câu:** `Score` chỉ gán khi `> 0` (còn lại null) + FE ẩn chip điểm/khung trung tính → hết cảnh nhận xét khen mà gắn "0/100 · Cần cải thiện"; rag-service nhận thêm biến thể khoá điểm và kẹp 0–100.
  - **Một màn một ngôn ngữ:** ghim bộ khoá `criterion_scores` trong prompt (rag-service + OpenAIProvider) + alias trong `CriterionBar` → tên tiêu chí dịch được VI/EN; ghi chú khi `reportLanguage` lệch ngôn ngữ UI; transcript giữ nguyên ngôn ngữ buổi phỏng vấn. Đồng bộ cỡ chữ `LanguageMetric` với `CriterionBar`.
  - **Công cụ test:** `POST /api/dev/seed-interview-job` (job `[DEV] Kiosk Sandbox` 3 vòng screening→online_test→technical, 10 câu trắc nghiệm mẫu, lịch vòng 1+3, mã Kiosk vòng 1, tài khoản HR `hr.dev@arisp.local` đăng nhập được); `POST /api/dev/regrade-session/{id}?lang=vi` chấm lại phiên cũ. Hướng dẫn: `docs/kiosk-interview-setup.md`.
  - **Xác nhận vòng trắc nghiệm:** slice Online Test (ADR-049) đã có đủ trên nhánh (commit `b56abf1a`, `86c32804`); còn thiếu duy nhất tuỳ chọn `online_test` trong dropdown tạo job.
  - **Verify:** `dotnet build` 0 error; FE 2 site build xanh. Không có migration mới. Sau khi merge `develop` (bộ test 301 case mới): cập nhật `InterviewServiceFactory` (thêm stub `IFileStorageService`) và chạy lại **304/304 pass**.
  - **Lỗ hổng lộ ra khi merge develop:** test `Pass_practice_does_not_progress_even_with_config` kỳ vọng review một đánh giá **buổi thử** vẫn đẩy hồ sơ sang `pass` — trái ADR-051. Sửa `SubmitHrReviewAsync` chỉ ghi trạng thái khi `SessionType == "real"`, cập nhật test theo hành vi đúng.
  - **Sửa sau khi chạy thật trên Kiosk (cùng ngày):** (1) upload video 500 — `MediaRecorder` gửi `video/webm;codecs=vp9,opus`, dấu phẩy trong tham số MIME làm `MediaTypeHeaderValue.Parse` của AWS SDK ném `FormatException`; nay `S3FileStorageService` + `SaveRecordingAsync` cắt bỏ tham số, và lỗi storage trả `Result.Failure` (400 kèm thông báo) thay vì văng 500. (2) Màn Kiosk co về góc trái — route `/kiosk` bị bọc trong `InterviewLayout` (flex container + thanh tiêu đề thừa); nay tách thành route toàn màn hình độc lập như `PracticeSessionPage`.

- [x] 2026-08-05: **Kiosk phỏng vấn THẬT + ghi hình tự xoá sau 7 ngày (ADR-052) + sửa trạng thái hồ sơ/lịch quá hạn.**
  - **Trạng thái "Đạt" sai:** danh sách hồ sơ (`GetMyApplicationsQueryHandler`) vẫn gom phiên **thử** vào `rounds` + `pendingHrReview` → thẻ hồ sơ hiện "V1 ✓" và "chờ HR xác nhận" chỉ vì một buổi luyện tập. Nay tiến trình vòng chỉ tính phiên **thật** (khớp ADR-051 đã làm cho trang chi tiết).
  - **Lịch quá hạn:** thêm `MissedInterview` (booking `scheduled` đã qua `EndTime` mà vòng đó chưa có phiên thật) ở danh sách + trạng thái vòng `missed` ở chi tiết; FE hiện banner "Đã quá hạn buổi phỏng vấn vòng N" + hướng dẫn liên hệ nhân sự, thay vì kẹt mãi ở "Đã xếp lịch".
  - **Kiosk auth:** `AppRoles.Kiosk_session` + `ITokenService.CreateKioskSessionToken` (claim `session_id`, TTL `Interview:KioskSessionTokenHours`=3h) + policy `InterviewParticipant`; `validate-code` trả `KioskSessionResponse` (sessionId, token, tên ứng viên, vị trí, vòng, ngôn ngữ, `reason` khi mã hỏng). Controller + `SessionHub` kiểm `session_id` khớp phiên. FE: `setInterviewSessionToken` ở `apiClient` (ưu tiên hơn token người dùng) + `accessTokenFactory` SignalR.
  - **Ghi hình:** `POST /interview/session/{id}/recording` (multipart, ≤`MaxRecordingSizeMb`=300) → `IFileStorageService`; entity thêm `recording_size_bytes`/`recording_expires_at`/`recording_deleted_at` (migration `AddKioskInterviewRecording`). `RecordingRetentionHostedService` quét 12h/lần xoá file quá hạn (`RecordingRetentionDays`=7), giữ transcript + đánh giá. Practice bị từ chối ghi hình.
  - **HR xem video:** `GetEvaluationDetailQuery` trả `recordingUrl`/`recordingExpiresAt`/`recordingDeletedAt`; `EvaluationReviewPage` thay placeholder chết (nút Play không onClick) bằng `<video>` thật + dòng nhắc hạn xoá; i18n VI/EN.
  - **FE Kiosk:** `KioskPage` gọi API thật (3 thông báo lỗi theo `reason`, bỏ demo codes, nút "tiếp tục buổi đang dở"), `kioskSession.ts` (sessionStorage + token), `KioskInterviewPage` mới (device check → phòng có avatar/đếm ngược/chỉ báo ghi hình/transcript/nhập kép → màn kết thúc lưu video + tự về màn nhập mã sau 30s). Hook tách thành `useInterviewSession({existingSessionId, sessionType, recordVideo})`, `usePracticeSession` giữ chữ ký cũ. Trần buổi thật `RealMaxDurationMinutes`=45' dùng chung cơ chế hết giờ.
  - **Verify:** `dotnet build` 0 error; `dotnet ef migrations add AddKioskInterviewRecording`; FE 2 site `tsc && vite build` xanh.

- [x] 2026-08-05: **Buổi thử — transcript + nhận xét AI riêng tư cho ứng viên, lưu vĩnh viễn (ADR-051).** Branch `feature/be/practice-transcript-review`.
  - **Ứng viên xem lại:** feature mới `ARI.Application/CandidatePortal/PortalPracticeFeature.cs` — `GetMyPracticeSessionsQuery` (`GET /api/portal/practice/sessions[?applicationId=]`) + `GetMyPracticeReviewQuery` (`GET /api/portal/practice/sessions/{sessionId}`), policy `CandidateOnly` + IDOR. Detail trả meta + `turns[]` (Question order `SequenceNumber` ghép Answer nạp 1 lần, không N+1) + `closingText` + evaluation + `evaluationPending`. Từ chối `NotFound` nếu phiên không phải `practice` (transcript buổi thật vẫn theo cổng `HrReview.ShareTranscript`). **DTO cố ý bỏ `AiVerdict`** — không hiện Pass/Not Pass cho buổi luyện tập.
  - **Ẩn khỏi nhân sự nội bộ:** lọc `SessionType != "practice"` ở `GetSessionsForHrAsync` (cả evaluation join), `GetEvaluationsQuery`, `GetEvaluationsByApplicationQuery`, `GetEvaluationDetailQuery` (cả nhánh fallback theo SessionId → NotFound). Giữ cờ `PracticeSessionUsed`.
  - **Bug sửa kèm:** (1) `GenerateEvaluationReportAsync` trước đó ghi đè `application.Status = "screening"|"not_pass"` + báo `hr_admin` **kể cả phiên thử** → buổi thử điểm thấp đánh rớt hồ sơ thật; nay chỉ chạy khi `SessionType=="real"`. (2) `GetMyApplicationDetailQueryHandler` map vòng bằng `FirstOrDefault(RoundNumber)` → phiên thử che trạng thái phiên thật; nay tách `realSessions`/`practiceSessions`, trả thêm `PracticeSessions[]`. (3) `PracticeSessionPage` hardcode round 1 → đọc `?round=`.
  - **DB:** `InterviewSession.ClosingText` (lưu câu chào kết thúc AI, trước chỉ bắn SignalR rồi mất) + index `ix_questions_session_id`, `ix_answers_session_id`; migration `20260805022000_AddPracticeTranscriptReview`. Không có job xoá — transcript giữ vĩnh viễn (ADR-038 điểm 6 chỉ nói về recording).
  - **Refactor nhỏ:** khối "IDOR Protection + Auto-link" copy 2 chỗ → `PortalSupport.TryEnsureOwnerAsync`, dùng lại ở cả 3 handler.
  - **FE:** trang mới `pages/candidate/PracticeReviewPage.tsx` (route `/candidate/practice/:sessionId`) — banner "chỉ tham khảo", panel nhận xét AI (có trạng thái "AI đang chấm"), transcript hội thoại + nút sao chép. `usePracticeSession` expose `sessionId`; màn kết thúc buổi thử đổi nút chính → "Xem lại & nhận xét AI"; card "Phỏng vấn thử" trong trang chi tiết hồ sơ. Tách `pages/candidate/_reportUi.ts` + `components/CriterionBar.tsx` dùng chung với `ApplicationDetailPage`; StaffSite gỡ nhãn practice/real đã chết (2 `InterviewSessionsPage` + 2 `CandidateDetailPage`). i18n namespace mới `modules/candidate/practiceReview` (VI/EN) + key `practice.ended.viewTranscript`, `applicationDetail.practiceList.*`.
  - **Vòng 2 (cùng ngày, sau khi soi màn thật):** 4 vấn đề user chỉ ra — (1) **trộn Việt–Anh**: thêm `InterviewSession.ReportLanguage` (FE gửi `uiLanguage` lúc start) + `SessionContext.ReportLanguage`, prompt buộc viết toàn bộ text theo 1 ngôn ngữ; (2) **phân tích từng câu trống rỗng**: prompt cũ để `question_analyses: [<optional objects>]` nên model bịa khoá → chốt schema `{sequence_number, score, analysis, feedback}`, thêm `normalize_question_analyses()` ở rag-service + `QuestionAnalysisDto.SequenceNumber`, BE ghép nhận xét vào đúng lượt hỏi–đáp (câu hỏi/trả lời luôn từ DB); (3) **đánh giá ngôn ngữ sai**: prompt chỉ đưa câu trả lời của ứng viên, neo thang 0–10 ↔ CEFR, thêm `cefr_level`/`evidence`/`language_adherence` chạy suốt Python → .NET → FE, `cefr_from_score` bù khi model bỏ trống, và **bỏ hẳn bước chấm khi phiên không có câu trả lời**; (4) **bố cục**: màn xem lại chuyển 2 cột (tổng quan sticky trái, hội thoại + `TurnNote` phải), bỏ mục "phân tích từng câu" tách rời. Migration gộp lại còn 1 file (`AddPracticeTranscriptReview`: `closing_text` + `report_language` + 2 index) — viết bằng SQL `IF NOT EXISTS` + xoá bản ghi lịch sử mồ côi `20260805022000_AddPracticeTranscriptReview`, vì bản trước khi gộp đã kịp chạy trên DB dev nên `ALTER TABLE ... ADD closing_text` báo 42701 (column already exists).
  - **Verify:** `dotnet build ARI.sln` 0 error, 14/14 test xanh; FE 2 site `tsc && vite build` xanh; eslint candidate 0 error, 0 warning mới (20 warning `any` là code cũ); rag-service `py_compile` sạch.
  - **Docs:** ADR-051 + mục "Cập nhật 2026-08-05 — chất lượng báo cáo AI" (`.ai/architecture.md`) + bảng ADR & Glossary trong `CLAUDE.md` + entry này.

- [x] 2026-08-05: **Unit test Luồng 7 — Conduct Official Interview (UC-44/65/66/67) — 20 test mới, tổng 321/321 pass.**
  - Phủ phần lõi của luồng phỏng vấn thật: **`InterviewCodeService`** (cấp/xác thực Interview Code — cổng vào Kiosk).
  - **`GenerateInterviewCodeTests` (10)** — `GenerateCodeAsync`: app/job không tồn tại→Failure; **chặn khi chưa có booking "scheduled"** (ADR-015/016); mã **6 ký tự** trong bảng `A–Z2–9` (bỏ I/O/0/1) + audit `interview_code_generated`; TTL theo `InterviewRoundConfig.InterviewCodeTtlHours`; **screening→interview**; báo ứng viên `ReceiveUserNotification` khi có tài khoản; suy vòng kế = max(session completed)+1. `GenerateBatchAsync`: danh sách rỗng→Failure; **chỉ trả mã cho hồ sơ đủ điều kiện** (bỏ qua hồ sơ chưa đặt lịch).
  - **`ValidateInterviewCodeTests` (7)** — `ValidateCodeAsync` (Kiosk, ADR-052): rỗng→Failure; sai/đã dùng/hết hạn→`Valid=false` kèm reason `not_found`/`used`/`expired`; **mã hợp lệ → tạo phiên `real` + đánh dấu `UsedAt` + mint token Kiosk đúng `SessionId` + audit `interview_code_used`** (trả tên ứng viên/vị trí/ngôn ngữ); **case-insensitive**; **StartSession lỗi (job thiếu) → hoàn tác `UsedAt`, không mint token, không tạo phiên**.
  - **`GetCodesByJobTests` (3)** — gộp mã mọi hồ sơ thuộc job, trạng thái Active/Used/Expired + tên ứng viên; chỉ mã của job đó; rỗng khi chưa có mã.
  - Hạ tầng: `InterviewCodes/InterviewCodeData.cs` (factory + `FakeTokenService` ghi lại tham số mint token) — tái dùng `InterviewServiceFactory` (stub ném lỗi; `StartSessionAsync` nuốt lỗi RAG/avatar nên an toàn) + `RecordingNotificationService`. Namespace số nhiều `InterviewCodes` tránh trùng entity `InterviewCode`. File: `tests/ARI.Application.UnitTests/InterviewCodes/{InterviewCodeData,GenerateInterviewCodeTests,ValidateInterviewCodeTests,GetCodesByJobTests}.cs`. `dotnet test`: **321/321 pass**.

- [x] 2026-08-05: **Unit test Luồng 5 — Schedule Interview (UC-39/40/41/58/59/60/61/62/63) — 24 test mới, tổng 301/301 pass.**
  - Phủ nhánh còn thiếu: **quản lý kho khung giờ của nhân sự** (`StaffScheduling.cs`) — phần assign/confirm/decline/candidate-schedule đã phủ ở batch Scheduling trước.
  - **`GetAvailabilitySlotsQueryHandler` (6)** — jobId rỗng→Failure; job không tồn tại→NotFound; Recruiter không phải chủ tin→Forbidden; chủ tin nhận slot **sắp theo StartTime**; lọc theo vòng; HrAdmin xem mọi tin.
  - **`CreateSlotCommandHandler` (9)** — gauntlet validate trước phân quyền: jobId rỗng, end≤start, start ở quá khứ, capacity<1, round<1; job không tồn tại→NotFound; không phải chủ tin→Forbidden; chủ tin lưu slot **booked=0** (+SaveChanges); timezone trắng→mặc định `Asia/Ho_Chi_Minh`.
  - **`DeleteSlotCommandHandler` (4)** — NotFound; Forbidden (slot vẫn còn); **chặn xoá khi `BookedCount>0`**; xoá slot trống thành công. **`UpdateSlotCapacityCommandHandler` (5)** — NotFound; Forbidden; capacity<1; **không nhỏ hơn số đã đặt**; cập nhật + persist thành công.
  - Hạ tầng: thêm factory `SchedulingData.SlotRequest(...)`. File: `tests/ARI.Application.UnitTests/Scheduling/StaffSlotTests.cs` (+ sửa `SchedulingData.cs`). `dotnet test`: **301/301 pass**.

- [x] 2026-08-05: **Unit test Luồng 4 — Screen Application (UC-53/54/55/56/57) — 30 test mới, tổng 277/277 pass.**
  - **`GetApplicationsListTests` (8)** — `GetApplicationsByJobAsync` (job không tồn tại→Failure; chỉ trả ứng viên của job đó, sắp mới nhất trước, JobTitle override từ job; danh sách bỏ CvText; kèm MatchScore+CvJdSummary), `GetAllApplicationsAsync` (sắp created desc, resolve tiêu đề theo từng job qua batch), `GetApplicationsForCreatorAsync` (rỗng khi không sở hữu job; chỉ gộp ứng viên của tin mình).
  - **`ApplicationListMappingTests` (7)** — làm giàu trong `MapApplications`: cv_submitted→không có vòng, screening→vòng 1, interview→max(invite,session); booking "scheduled"→cờ + giờ hẹn từ slot, booking "cancelled"→không tính; điểm phỏng vấn chỉ lấy Evaluation "real" (bỏ "practice").
  - **`GetApplicationByIdTests` (6)** — NotFound; chi tiết trả CvText + JobTitle; nạp CvJdAnalysis liên kết cho MatchScore; booking scheduled→giờ hẹn + `ScheduleConfirmationStatus`; khi không còn scheduled→đưa `ScheduleDeclineReason`; suy vòng từ invite cho status interview.
  - **`GetJobApplicationsQueryHandlerTests` (5)** — job NotFound; Recruiter không xem được tin người khác (Forbidden); Recruiter xem tin mình; HrAdmin/SuperAdmin xem mọi tin; resolve CvFileUrl qua storage. **`ApplicationQueryHandlerTests` (4)** — `GetApplicationByIdQuery` lỗi→mã NotFound + resolve URL; `GetApplicationsQuery` mine=creator lọc đúng vs không mine→toàn bộ + resolve URL.
  - Hạ tầng test bổ sung: `Screening/ScreeningData.cs` (factory Job/App/Analysis/Slot/Booking/Invite/Session/Eval/Staff). File: `tests/ARI.Application.UnitTests/Screening/{ScreeningData,GetApplicationsListTests,ApplicationListMappingTests,GetApplicationByIdTests,GetJobApplicationsQueryHandlerTests,ApplicationQueryHandlerTests}.cs`. Accept/Reject/UpdateStatus đã phủ ở luồng Application. `dotnet test`: **277/277 pass**.

- [x] 2026-08-05: **Unit test Luồng 3 — Submit Application (UC-16/17/18/27/28) — 36 test mới, tổng 247/247 pass.**
  - **`GetJobsQueryHandler` (13)** — chỉ trả tin active+public chưa hết hạn; lọc search (tiêu đề/kỹ năng), category, experience, location (case-insensitive), language; phân trang giới hạn item + báo tổng, trang kế khác trang trước; sắp `salary_desc` (lương cao trước), urgent-first mặc định, `relevance` theo số kỹ năng trùng CV (rơi về mới nhất khi không có kỹ năng).
  - **`GetJobByIdQueryHandler` (8)** — NotFound; khách chỉ xem active+public (draft/non-public bị ẩn); staff xem draft; **Recruiter chỉ xem tin của chính mình** (tin người khác → coi như khách); staff được resolve URL file JD; kèm tên người tạo + vòng (sắp theo RoundNumber). **`GetJobFacetsQueryHandler` (5)** — chỉ đếm tin active+public, facet category/skill đếm đúng, gộp nhãn `intern`+`fresher`→"Intern / Fresher", board rỗng → total 0.
  - **`SubmitApplicationCommandHandler` (5, wrapper CQRS)** — hash MD5 CV + parse text + lưu file rồi ủy quyền `IApplicationService` (source `job_board`); lỗi parse → Failure (không lưu/không ủy quyền); lỗi lưu → ServerError; **lỗi ghi DB → dọn file đã lưu** (`DeleteAsync`); null byte trong text được loại. **`GetCvMatchQueryHandler` (5, UC-27)** — nhánh xác định không chạy nền: tài khoản không tồn tại→Unauthorized, chưa có CV→none, file không đọc được→failed, cache `completed`→trả phân tích, cache `failed`→failed.
  - Hạ tầng test bổ sung: `JobBoard/{JobBoardData,JobBoardFakes}.cs` (`FakeApplicationService` ghi request+source, `ThrowingScopeFactory`), `RecordingFileStorage` thêm `Deleted` (dọn file). File: `tests/ARI.Application.UnitTests/JobBoard/{GetJobsQueryHandlerTests,GetJobByIdQueryHandlerTests,GetJobFacetsQueryHandlerTests,SubmitApplicationCommandHandlerTests,GetCvMatchQueryHandlerTests}.cs`. `dotnet test`: **247/247 pass**.

- [x] 2026-08-05: **Unit test Luồng 2 — Approve Job Posting (UC-48/77/78/79/80) — 35 test mới, tổng 211/211 pass.**
  - **`UpdateJobStatusCommandHandler` (28)** — cổng chặn (status rỗng/không hợp lệ, cấm về `draft`, NotFound, trùng trạng thái, `archived` bất biến, không chủ tin/không admin → Forbidden); **UC-48** Owner gửi `draft/rejected`→`pending` (báo nhóm hr_admin + `Notification` cho từng hr_admin, xoá lý do từ chối cũ), không phải Owner → Forbidden, `active`→pending bị chặn; **UC-78** HrAdmin/SuperAdmin `pending`→`active` (set `ApprovedByUserId`/`PublishedAt`, báo + `Notification` "approved" cho người tạo, broadcast công khai), Owner tự duyệt → Forbidden, sai trạng thái nguồn/hạn nộp quá khứ bị chặn, `closed`→`active` KHÔNG phê duyệt lại; **UC-79** từ chối bắt buộc lý do + chỉ từ `pending` + chỉ admin (`Notification` "rejected" cho người tạo); **UC-80** đóng dấu duyệt: PDF gọi `StampApprovalAsync`, DOCX gọi `StampApprovalFromTextAsync` (đặt `SignedJdFileUrl`), lỗi đóng dấu KHÔNG chặn duyệt, không có file JD → bỏ qua; đóng tin active→closed, chặn archive khi còn hồ sơ active + soft-delete khi sạch.
  - **`GetAdminJobsQueryHandler` (7)** — trả mọi trạng thái (gồm draft/pending) cho admin, lọc `MineUserId` chỉ tin của người tạo, đếm ứng viên theo tin, gắn tên+vai trò người tạo ("Anna (Recruiter)"), sắp mới nhất, rỗng → list rỗng.
  - Hạ tầng test bổ sung: `JobsFakes.cs` thêm `RecordingJdStampService` (đếm đóng dấu PDF/text + công tắc lỗi), `RecordingFileStorage` thêm `FileBytes` cho `ReadAllBytesAsync`, `JobPostingData` thêm `StatusRequest` + JD-file fields. File: `tests/ARI.Application.UnitTests/JobPostings/{UpdateJobStatusCommandHandlerTests,GetAdminJobsQueryHandlerTests}.cs`. `dotnet test`: **211/211 pass**.

- [x] 2026-08-05: **Unit test Luồng 1 — Configure Job Posting (UC-45/46/47/50) — 31 test mới, tổng 176/176 pass.**
  - **`CreateJobCommandHandler` (10)** — request hợp lệ tạo job `draft` của người tạo + sinh `InterviewRoundConfig` từng vòng (ngôn ngữ vòng bỏ trống kế thừa `DetectedLanguage`, có set thì giữ), người tạo không tồn tại → Unauthorized, đẩy JD vào RAG, báo realtime; validate chặn trước (thiếu title, không có vòng, InterviewMode sai, onsite thiếu location).
  - **`UpdateJobCommandHandler` (10)** — job không tồn tại → NotFound, không phải chủ tin/không admin → Forbidden (admin sửa job người khác OK), chặn khi `archived`, validate như create, **chỉ tái tạo round khi cấu hình đổi** (đổi số lượng → xoá+tạo mới; y hệt → giữ nguyên entity), broadcast `ReceivePublicJobUpdate` khi tin đang `active`, báo realtime người sửa.
  - **`AnalyzeJdCommandHandler` (8, ADR-042)** — parse→lưu file→Gemini trích xuất auto-fill; **PDF gửi inline** (bytes + `application/pdf`) còn **DOCX dùng text fallback** (bytes null); lỗi parse → Failure (không lưu/không gọi AI); lỗi lưu → ServerError; Gemini lỗi vẫn trả file đã lưu (`IsValidJd=false`); JobDescription ưu tiên Gemini, rỗng thì fallback text parse. **`CreateJobSlotsCommandHandler` (3)** — tạo slot `booked_count=0`, job không tồn tại → NotFound, list rỗng vẫn success.
  - Hạ tầng test bổ sung: `TestSupport/RecordingFileStorage.cs` (`RecordingFileStorage` + `StubDocumentParser`), `RecordingNotificationService` thêm `AllEvents` (broadcast toàn hệ thống). File: `tests/ARI.Application.UnitTests/JobPostings/{JobPostingData,JobsFakes,CreateJobCommandHandlerTests,UpdateJobCommandHandlerTests,AnalyzeJdCommandHandlerTests,CreateJobSlotsCommandHandlerTests}.cs`. `dotnet test`: **176/176 pass**.

- [x] 2026-08-05: **Unit test luồng chính Application (Phase 2) — 36 test mới, tổng 145/145 pass.**
  - **`ApplicationService`** với `RecordingEmailService` + `RecordingRagIngestionService` + `ApplicationServiceFactory` (cắm `IServiceScopeFactory` stub — tác vụ phân tích CV nền fire-and-forget bị né bằng input CvFileUrl=null): **`SubmitApplicationAsync` (12)** chặn job không tồn tại/không active/quá hạn, tạo Application `cv_submitted` + Source, **auto-link `CvJdAnalysis` theo `(JobPostingId, CvHash)`** (không khớp → để trống), đẩy CV vào RAG (`IngestAsync("cv", …)`, bỏ qua khi không có CvText), báo nhóm `hr_admin` + recruiter, ứng viên tự ứng tuyển nhận `Notification` `applied:{id}` + realtime, hồ sơ ẩn danh không tạo notification.
  - **`UpdateApplicationStatusAsync` (8)** — bảng chuyển trạng thái hợp lệ (rỗng/không hợp lệ/trùng/bước cấm đều Failure), `withdrawn` là điểm cuối, `not_pass` mở lại được về screening, chuyển hợp lệ case-insensitive + báo realtime ứng viên. **CV decision (12)** — `SendInterviewInviteAsync` (tạo InterviewInvite TTL theo job, nâng cv_submitted/invited→screening, gửi email, xoá invite cũ **chưa dùng** cùng vòng nhưng giữ invite đã đặt lịch), `AcceptApplicationAsync` (chỉ từ cv_submitted/invited → screening + invite + `Notification` `cv_accepted:{id}` + email), `RejectApplicationAsync` (→ `cv_rejected` + thư cảm ơn + `Notification` `cv_rejected:{id}`). **`CheckPracticeEligibilityAsync` (4)** — 1 lượt/vòng, phiên practice vòng khác không chặn.
  - File: `tests/ARI.Application.UnitTests/ApplicationFlow/{ApplicationData,SubmitApplicationTests,UpdateApplicationStatusTests,CvDecisionTests,PracticeEligibilityTests}.cs` + `TestSupport/{RecordingEmailService,ApplicationServiceFactory}.cs`. `dotnet test`: **145/145 pass**.

- [x] 2026-08-05: **Unit test luồng chính HR Review & Confirm/Override (Phase 6) — 16 test mới, tổng 109/109 pass.**
  - **`InterviewService.SubmitHrReviewAsync`** với `InterviewServiceFactory` (dựng service 8 dependency, cắm stub ném lỗi cho 6 dependency media/AI không dùng trong luồng review) + `RecordingNotificationService`: **Confirm** (verdict == AiVerdict) mọi nhân sự làm được kể cả Recruiter; **Override** (đổi verdict) chỉ HR Admin/Super Admin **và bắt buộc `OverrideReason`** — thiếu lý do hoặc Recruiter override → Failure, không persist gì. Cập nhật status hồ sơ pass/not_pass; **auto-progression (ADR-017)**: pass + real + có `InterviewRoundConfig` vòng kế → tạo `InterviewInvite` vòng N+1 + status→interview, còn practice/không có config/not_pass → không progress. Thông báo ứng viên realtime (`ReceiveApplicationStatusUpdate` + `ReceiveUserNotification`) + bản ghi `Notification` chống trùng theo `DedupKey` (`hr_review:{evalId}`), hồ sơ không có tài khoản → bỏ qua realtime nhưng luồng chính vẫn hoàn tất; luôn ghi `AuditLog` `hr_confirm`/`hr_override`. Evaluation/HR user không tồn tại → Failure.
  - File: `tests/ARI.Application.UnitTests/HrReview/{HrReviewData,SubmitHrReviewTests}.cs` + `TestSupport/InterviewServiceFactory.cs`. `dotnet test`: **109/109 pass**.

- [x] 2026-08-05: **Unit test CV-JD Match Analysis (Gemini, ADR-030) — 13 test mới, tổng 93/93 pass.**
  - **`CvJdAnalysisService`** với `FakeGeminiProvider` (đếm số lần gọi AI + nạp sẵn kết quả) + `FakeDocumentParser`: chốt reuse theo `(JobPostingId, CvHash MD5)` — bản "completed" cùng hash trả thẳng KHÔNG gọi Gemini (`AnalyzeCallCount==0`), gọi lần 2 cùng CV → cache hit (AI chỉ chạy 1 lần); CV không hợp lệ (`IsValidCv=false`) lưu bản `failed` (MatchScore 0 + ErrorMessage) rồi trả Failure; lỗi AI → Failure "Lỗi AI" không persist; bản `failed` KHÔNG chặn cache → chạy lại; cache hit enrich `analysis_reasoning`/`seniority_alignment`/`tech_depth_analysis` từ envelope `RawResponse`; `GetById`/`GetByApplication` (link `cv_jd_analysis_id`), `CheckCandidateOwnership` (đúng cả analysis + account), `ClearAllCache`.
  - File: `tests/ARI.Application.UnitTests/CvAnalysis/{CvAnalysisFakes,CvJdAnalysisServiceTests}.cs`. `dotnet test`: **93/93 pass**.

- [x] 2026-08-05: **Unit test luồng chính Scheduling (ADR-048) — 36 test mới, tổng 80/80 pass.**
  - **Mở rộng hạ tầng test dùng chung:** `InMemoryUnitOfWork` thêm hook `OnExecuteSqlRaw` (giả lập SQL thô) + `ThrowOnSaveChanges` (test đường bù trừ). `Scheduling/SchedulingData.cs` factory + `SlotSqlEmulator` — giả lập 2 lệnh SQL nguyên tử chốt/nhả chỗ, giữ `booked_count` ở "DB ảo" tách khỏi entity EF nên guard chống overbooking + DTO trả về khớp production.
  - **`AssignSlotCommandHandler` (19 test)** — chốt chỗ nguyên tử (`booked_count < capacity`) chống overbooking, screening→interview (vòng 2+ giữ interview), tạo booking scheduled/pending, đánh dấu invite ScheduledAt, thông báo ứng viên (bell `Notification` + realtime `ReceiveUserNotification`), liên kết `RescheduledFromId` khi xếp lại sau decline, **bù trừ nhả chỗ khi SaveChanges lỗi**; chặn: CV chưa duyệt (theory 5 status), đã có lịch vòng, sai job/vòng, slot quá khứ, không phải chủ tin→Forbidden (admin OK), app/slot không tồn tại→NotFound.
  - **`ConfirmScheduleCommandHandler` (5) + `DeclineScheduleCommandHandler` (7)** — confirm đặt `confirmed` + báo staff, idempotent khi đã confirmed; decline validate lý do (≥3 ký tự, cắt 500), đặt `declined` + **trả chỗ slot** (booked−1) cho HR xếp lại + báo staff; cả hai chặn booking không còn `scheduled`, không phải chủ hồ sơ→Forbidden, not-found.
  - **`GetCandidateScheduleQueryHandler` (5)** — phân loại Upcoming/Past theo giờ slot, AwaitingReschedule cho booking declined CHƯA xếp lại (vòng đã có lịch mới thì loại khỏi awaiting), không hồ sơ→list rỗng.
  - File: `tests/ARI.Application.UnitTests/Scheduling/{SchedulingData,AssignSlotCommandHandlerTests,CandidateScheduleResponseTests,GetCandidateScheduleQueryHandlerTests}.cs` + sửa `TestSupport/InMemoryUnitOfWork.cs`. `dotnet test`: **80/80 pass**.

- [x] 2026-08-05: **Unit test luồng chính Online Test (Phase 2c) — 33 test mới, tổng 44/44 pass.**
  - **Hạ tầng test tái dùng** (không thêm mocking lib): `tests/ARI.Application.UnitTests/TestSupport/` — `InMemoryUnitOfWork`/`InMemoryRepository<T>` (LINQ-to-objects thay EF, `Seed()` chainable, đếm `SaveChangesCount`) + `RecordingNotificationService` (ghi event realtime, công tắc `ThrowOnPublish` để test best-effort). Dùng chung cho mọi flow test sau này.
  - **`SubmitOnlineTestCommandHandler` (18 test)** — công thức chấm: all-correct=100/pass, 2/3→66.67 làm tròn + trượt, điểm sàn inclusive (=50 → đạt), multiple khớp HOÀN TOÀN (theory: thiếu/dư/rỗng đều sai), câu bỏ trống tính sai, **chỉ chấm bộ đề đã bốc** (perTest=2 trên bank 5 → total=2); cổng chặn: 2 lượt/vòng→Conflict, CV chưa duyệt (cv_submitted/cv_rejected)→chặn, đã rút→chặn, ngân hàng rỗng→fail, không thấy hồ sơ→NotFound, không sở hữu hồ sơ→Forbidden; side-effect: báo ứng viên+recruiter+nhóm hr_admin, lỗi SignalR không hỏng nộp bài.
  - **`CreateOnlineTestQuestionCommandHandler` (10 test)** — validate (theory 6 ca: text rỗng, <2 hoặc >6 phương án, thiếu đáp án đúng, đáp án ngoài danh sách, single mà chọn 2 đúng) chặn trước khi chạm repo; chuẩn hoá đáp án distinct+sort + đồng bộ `CorrectOption` legacy; phân quyền: admin thêm mọi job, recruiter không phải chủ tin→Forbidden, job không tồn tại→NotFound.
  - **`GetCandidateOnlineTestQueryHandler` (5 test)** — CV passed trả câu hỏi (DTO ứng viên không có trường đáp án — ẩn ở compile-time), CV chưa duyệt trả metadata nhưng ẩn câu hỏi, phản ánh trạng thái đã nộp (score/isPassed), **bốc đề deterministic** (2 lần đọc ra cùng bộ + thứ tự), hồ sơ lạ→NotFound.
  - File: `tests/ARI.Application.UnitTests/TestSupport/{InMemoryUnitOfWork,RecordingNotificationService}.cs` + `tests/ARI.Application.UnitTests/OnlineTest/{OnlineTestData,SubmitOnlineTestCommandHandlerTests,CreateOnlineTestQuestionCommandHandlerTests,GetCandidateOnlineTestQueryHandlerTests}.cs`. `dotnet test`: **44/44 pass**, không warning từ file test.

- [x] 2026-07-26: **Dev-only seed endpoint test Phỏng vấn thử + dọn trùng số ADR (practice 048→050).**
  - **Seed:** `POST /api/dev/seed-practice` (`DevController`, gated `IWebHostEnvironment.IsDevelopment()` → prod 404; `AllowAnonymous`) → `SeedPracticeCommand`/handler (MediatR auto-discovered) tạo idempotent `CandidateAccount` (`EmailVerified=true`) + `JobPosting` (`active`, JD vi, **`SalaryCurrency="VND"`**) + `Application` (`Status="interview"` → `PracticeEligible`) + `AvailabilitySlot`/`InterviewBooking` (mirror `StaffScheduling.AssignSlotCommand`); trả `practiceUrl` + tài khoản. `?fresh=true` tạo app mới. Kết hợp `Interview:PracticeAttemptsPerRound=0` để test lặp vô hạn. Không migration.
  - **E2E thật (Supabase):** bắt bug `job_postings.salary_currency` NOT NULL nhưng entity nullable không `HasDefaultValue` → EF gửi NULL → 500; fix đặt `SalaryCurrency="VND"`. Verify: seed OK, gọi lại idempotent (cùng applicationId), `?fresh=true` ra app mới.
  - **Dọn ADR trùng:** develop có ADR-048 TRÙNG (practice PR #72 + lịch #71) và ADR-049 đã bị Online Test chiếm → đổi **practice 048→050** trong code + docs (giữ 048=lịch, 049=online-test). Bảng ADR CLAUDE.md xếp lại thứ tự tăng dần.
  - File: `ari-service/src/ARI.Application/Dev/SeedPractice/SeedPracticeCommand.cs`, `ari-service/src/ARI.API/Controllers/DevController.cs`; renumber các file practice (InterviewService/Options/DTOs/SessionHub/usePracticeSession/PracticeSessionPage/interviewService.ts/.env.example); docs architecture.md (ADR-050) + CLAUDE.md + practice-interview-setup.md. Backend build + 14 test xanh; FE 2 site build xanh.

- [x] 2026-07-25: **Ứng viên xác nhận / báo bận lịch phỏng vấn — staff xếp lại (ADR-048 điểm 6).** Sau khi HR gán lịch, ứng viên **XÁC NHẬN** hoặc **TỪ CHỐI kèm lý do** (bận) để nhân sự sắp khung giờ khác.
  - **Backend:** 3 cột mới trên `InterviewBooking` (`confirmation_status` default `pending` | `decline_reason` | `responded_at`), migration `AddBookingConfirmation` (backfill `pending`). 2 endpoint ứng viên `POST /api/candidate/schedule/{bookingId}/confirm|decline` (`ConfirmScheduleCommand`/`DeclineScheduleCommand`, `CandidateOnly`, guard booking thuộc hồ sơ ứng viên; decline yêu cầu lý do ≥3 ký tự). **Decline** set `Status="declined"` + trả 1 chỗ slot (`GREATEST(booked_count-1,0)`) → gỡ khỏi partial-unique `ux_..._scheduled` nên staff gán lại bình thường qua chính `AssignSlotCommand` (**không cần luồng huỷ riêng** — giải quyết follow-up cũ); assign lần sau set `RescheduledFromId`. `GET /candidate/schedule` đổi shape `{upcoming, past, awaitingReschedule}` (kèm `bookingId`/`confirmationStatus`/`declineReason`). `ApplicationResponse` thêm `scheduleConfirmationStatus`/`scheduleDeclineReason` (detail path). Realtime nhân sự `ReceiveScheduleResponse` (chủ tin + `hr_admin`) + staff bell sync mục `schedule_response:{bookingId}`. `AssignSlotCommand` đổi dedup notification → `schedule_assigned:{booking.Id}` (xếp-lại luôn báo lại) + link ứng viên `/portal/schedule/{app}` + email nhắc xác nhận/báo bận.
  - **Frontend:** `scheduleService` đổi `getMySchedule()` (shape mới) + thêm `confirmSchedule`/`declineSchedule`; candidate `SchedulePage` render nút **Xác nhận tham dự** / **"Tôi bận, đổi lịch"** (nhập lý do) + mục "chờ xếp lại". `AssignSchedulePanel` nhận `confirmationStatus`/`declineReason` → hiện trạng thái xác nhận khi đã xếp + **banner lý do báo bận** khi chờ xếp lại (truyền từ 2 trang chi tiết ứng viên HR + Recruiter). `useAppNotifications`: case `ReceiveScheduleResponse` (chuông nhân sự) + invalidate `my-schedule` khi ứng viên nhận push xếp/xếp-lại. Cả `SchedulePage` + `AssignSchedulePanel` giữ hardcoded VI đúng phong cách sẵn có (không thêm i18n JSON).
  - **Verify:** `dotnet ef migrations add` build 0 error (toàn backend compile), tsc 2 site 0 error, eslint file đổi 0 warning mới (1 warning `payload: any` dòng 71 `useAppNotifications` là code cũ, ngoài diff).
  - **Docs:** ADR-048 điểm 6 (architecture.md) + entry này.

- [x] 2026-07-24: **Nâng cấp Phỏng vấn thử (Practice) — audio-only + trần 20 phút + nhập kép (ADR-050).**
  - **Audio-only (bỏ avatar):** `GetMediaConfigAsync` không mint avatar token khi `SessionType=="practice"` (cờ `Interview:PracticeUseAvatar=false`) → `heyGen=null` → FE phát giọng ElevenLabs qua WebAudio + bot tĩnh, giữ đủ STT/RAG/GPT-4o. Lý do: tránh cạnh tranh concurrency LiveAvatar với buổi thật + đốt credit không dự đoán. Real vẫn có avatar.
  - **Trần 20 phút** (`Interview:PracticeMaxDurationMinutes`): media-config trả `MaxDurationSeconds` → FE đếm ngược (giờ máy). Hết giờ → khoá mic (`stopMic`) → `NotifyTimeout` (SignalR) → AI nói câu kết → đóng phiên. 2 lớp enforce: FE trigger (im lặng) + server `forceClosing` khi `elapsed≥cap` (nói quá giờ). Tách `CloseWithFarewellAsync` (idempotent) từ khối closing inline; `EndSessionAsync` guard `Status=="completed"` chống race double-end.
  - **Nhập kép (voice+keyboard):** transcript Deepgram append vào 1 nguồn `answerText` trong `<textarea>` sửa/gõ tay được; nút Mic on/off (tắt = gõ tự do, gate gửi audio + append theo `micEnabledRef`); `submitAnswer` đọc `answerText`. Đúng "nhập tay" ADR-044 hứa nhưng chưa build.
  - **Bỏ** (user chốt giữa chừng): ghi âm practice → R2 + tự xoá 7 ngày → giữ nguyên ADR-038 điểm 6.
  - **Khuyến nghị gói LiveAvatar** (ghi lại, chưa mua): giữ sandbox/Free giờ; khi real chạy thật → Essential $99 (nếu Hybrid Idle + vòng ≤20') hoặc Business $475. Free/Starter loại vì có watermark.
  - File: BE `InterviewService.cs`, `InterviewOptions.cs`, `InterviewDTOs.cs`, `SessionHub.cs`, `IInterviewService.cs`, `appsettings.json`, `docker/.env.example`; FE `usePracticeSession.ts`, `PracticeSessionPage.tsx`, `interviewService.ts`, i18n `practice.json` (vi/en). Backend build+14 test xanh; FE 2 site build xanh. ADR-050 (ban đầu 048, đổi do trùng) + ADR-038 note + CLAUDE.md + docs/practice-interview-setup.md.

- [x] 2026-07-24: **Online Test — HrAdmin xem điểm & xuất Excel ngang Recruiter (ADR-049 cập nhật).** Backend + route `/hr/jobs/:id/online-test[/results]` + `JobOnlineTestPage`/`JobOnlineTestResultsPage` (nhận `isHr` từ path) đã hỗ trợ sẵn HrAdmin (quyền admin qua `CanManageAsync` cho mọi job, endpoint `InternalStaff`), nhưng `pages/hr/JobPostingDetailPage` **thiếu link điều hướng** (recruiter đã có) → HrAdmin phải gõ URL tay. Thêm nút "Ngân hàng câu hỏi" cạnh nút Chỉnh sửa trên trang HR job detail (→ `/hr/jobs/:id/online-test`; từ đó có link Bảng điểm ứng viên + nút Tải Excel). i18n key `onlineTestBank` cho namespace `modules/hr/jobPostingDetail` (VI/EN). Verify: tsc StaffSite 0 error, i18n HR parity OK (149/149), phần thêm 0 lint warning (1 warning `any` ở dòng 237 là code cũ, không đụng tới).

- [x] 2026-07-24: **Online Test — Import ngân hàng câu hỏi từ Excel (giảm nhập tay, ADR-049 cập nhật).** Thay vì HR gõ từng câu, thêm luồng **upload file `.xlsx`** để thêm hàng loạt: `OnlineTestImportFeature.cs` (`ImportOnlineTestQuestionsCommand` + `GetOnlineTestImportTemplateQuery`), 2 endpoint `POST /online-test/jobs/{id}/questions/import` (multipart `IFormFile`, ≤5MB, chỉ `.xlsx`) + `GET .../questions/template`. Đọc worksheet đầu bằng OpenXML SDK (đã có sẵn) — xử lý SharedString/InlineString, map cell theo cột từ `CellReference`. Layout: A=Câu hỏi · B=Loại (single/multiple, trống→suy từ số đáp án đúng) · C–H=Phương án A–F · I=Đáp án đúng (`A` hoặc `A,C`, nhận cả số). Nén phương án rỗng + **remap chỉ số đáp án đúng**, dùng lại `ValidateQuestion`/`NormalizeType`/`NormalizeCorrect`; mỗi dòng lỗi trả `{row,message}`, chỉ ghi dòng hợp lệ → `OnlineTestImportResultDto(imported, failed, errors[])`. File mẫu OpenXML (header + 2 ví dụ single/multiple). FE: shared type `OnlineTestImportResult` + service `importQuestions`/`downloadTemplate`; `JobOnlineTestPage` thêm card "Nhập từ file Excel" (nút upload + tải mẫu + hiển thị kết quả imported/failed + danh sách lỗi dòng); i18n `bank.import.*` VI/EN. Verify: Application + API compile 0 error (build ra thư mục tạm vì API đang chạy khoá DLL), tsc StaffSite 0 error, eslint file đổi 0 warning, i18n staff parity OK (76/76 key).

- [x] 2026-07-24: **Online Test — Screening Test nâng cấp (ADR-049 cập nhật).** Bổ sung đúng mô tả yêu cầu: (1) **bốc N câu ngẫu nhiên/lượt** (mặc định 20) deterministic theo (câu, hồ sơ, vòng) — cùng ứng viên nhận cùng bộ đề, chấm lại đúng bộ; (2) **câu 1 đáp án & nhiều đáp án** (`QuestionType` + `CorrectOptions` jsonb), chấm all-or-nothing khớp hoàn toàn tập đáp án; (3) **hẹn giờ (mặc định 30')** — FE đếm ngược + tự nộp khi hết giờ; (4) **export bảng điểm .xlsx** (OpenXML SDK, `GET .../results/export`). Thêm cấu hình per-job `OnlineTestQuestionsPerTest`/`DurationMinutes` + `Submission.CorrectCount`/`TotalQuestions`; endpoint `pass-score` → `settings` (3 tham số). Migration `AddOnlineTestScreening`. FE: bank page có type toggle + multi-correct (radio/checkbox) + 3 field cấu hình (mặc định 4 phương án); results page có nút Tải Excel; candidate page radio/checkbox + đồng hồ; i18n VI/EN (staff 68 key, candidate 22 key). Verify: backend build 0 error, tsc 2 site 0 error, eslint file mới 0 warning, key parity OK.

- [x] 2026-07-24: **Online Test (thi trắc nghiệm) — Phase 2c end-to-end (ADR-049).** Trước đó chỉ có 2 entity + 2 bảng chờ suông, 0 dòng logic. Bổ sung: (BE) migration `AddOnlineTestFlow` (`job_postings.online_test_pass_score` mặc định 70, `online_test_questions.updated_at`, unique index `(application_id, round_number)` = 1 lượt/vòng); feature CQRS `ARI.Application/OnlineTest/` — HR CRUD câu hỏi + điểm sàn + xem kết quả (`OnlineTestController`, `InternalStaff`, quyền chủ tin/admin), ứng viên lấy đề (ẩn đáp án) + nộp bài tự chấm `score = correct/total*100`, `isPassed = score >= passScore` (`CandidateOnlineTestController`, `CandidateOnly`); auto-progression mềm: realtime `OnlineTestGraded` + notification idempotent qua `SyncNotificationsAsync` (dedupKey `online_test:{id}`), không tự đổi status. (FE) shared `types/onlineTest` + `fservices/onlineTest`; StaffSite `JobOnlineTestPage` (route recruiter+hr, link từ trang chi tiết job); CandidateSite `OnlineTestPage` + `OnlineTestEntry` (chỉ hiện khi job có đề) trong trang chi tiết hồ sơ. **Bảng tổng hợp điểm theo job cho HR:** `GetOnlineTestResultsByJobQuery` → `GET /online-test/jobs/{id}/results` (summary + từng ứng viên, sort điểm giảm dần) + trang `JobOnlineTestResultsPage` (route `.../online-test/results`, link từ trang ngân hàng câu hỏi). Verify: backend build 0 error, migration tạo OK, tsc 2 site 0 error, eslint file mới 0 warning.

- [x] 2026-07-23: **Đảo chiều luồng đặt lịch phỏng vấn — HR gán cứng 1 giờ cho 1 ứng viên, bỏ ứng viên tự chọn (ADR-048).**
  - **Backend:** thêm `POST /api/schedules/assign` (`AssignSlotCommand` + handler, policy `InternalStaff`) — staff chọn 1 slot trong kho ấn định cho `application_id`+vòng; giữ nguyên side-effects của booking cũ (chốt chỗ nguyên tử chống overbooking, chặn trùng vòng, `screening→interview`, đánh dấu `InterviewInvite.ScheduledAt`) + đẩy realtime `InterviewScheduled` + tạo `Notification` bell + email giờ hẹn (giờ VN). Gỡ `GET /schedule/{id}/slots` + `POST /schedule/{id}/book` (handler `GetOpenSlotsQuery`/`BookSlotCommand`), gỡ helper `AuthorizeCandidateAsync`; `CandidateScheduleController` còn mỗi `GET /candidate/schedule` (read-only). Email "duyệt CV" (`SendInterviewInviteAsync`) bỏ pick-link, đổi thành "nhân sự sẽ xếp lịch".
  - **Frontend:** `scheduleService` thêm `assign()`, gỡ `getOpenSlots`/`book`. Component chung `@ari/shared/ui/AssignSchedulePanel` (chọn slot từ kho, lọc slot tương lai còn chỗ, gán → refetch hồ sơ) chèn vào cột thao tác trang chi tiết ứng viên HR + Recruiter. `SchedulePage` (candidate) chuyển sang read-only, hiển thị giờ đã gán qua `getMySchedule()`, bỏ nút tự chọn.
  - **Docs:** ADR-048 (architecture.md) + bảng ADR CLAUDE.md.
  - **Follow-up:** ~~luồng huỷ/đổi lịch phía staff~~ → đã giải bằng ứng viên báo bận (2026-07-25, xem trên); còn lại: nút staff **tự huỷ** lịch đã xác nhận (không do ứng viên báo bận); dọn invalidate `open-slots` thừa trong `useAppNotifications`.

- [x] 2026-07-22: **Fix `502` staff site sau deploy tự động — nginx cache IP upstream.** Lần chạy `deploy.yml` đầu tiên: 4 image build + push GHCR thành công, VPS pull và up xong, nhưng health-check báo đỏ vì `staff.arisp.io.vn` trả 502 suốt 12 lần thử (candidate 200). Nguyên nhân: nginx resolve hostname upstream một lần lúc khởi động; deploy tạo lại `frontend-staff` (IP mới `172.18.0.5`) nhưng nginx không được tạo lại nên vẫn gọi `172.18.0.7` → `connect() failed (113: Host is unreachable)`. Candidate thoát nạn do trùng IP ngẫu nhiên. Thêm `docker compose restart nginx` sau `up -d` trong `deploy.yml`. Ghi nhận: health-check trong pipeline đã làm đúng việc — bắt lỗi và fail build thay vì báo xanh giả.

- [x] 2026-07-22: **CI/CD GitHub Actions + chuyển production sang nhánh `main` + chấm dứt config drift trên VPS (ADR-047).**
  - **Hotfix `arisp-rag` (đã áp dụng thẳng lên VPS):** container crash-loop **6928 lần**. `docker/.env` chỉ có biến .NET (`ConnectionStrings__DefaultConnection`), trong khi rag-service Python đọc `DATABASE_*` riêng (`app/config.py:19`) → fallback về localhost → `ConnectionRefusedError` khi startup. Thêm `DATABASE_HOST/PORT/NAME/USER/PASSWORD/SSLMODE` (dùng tham số rời thay `DATABASE_URL` vì password chứa `?`, đúng ý `core/db.py:46`). Phát hiện thêm cùng lớp lỗi: thiếu `OPENAI_API_KEY` → service chạy **mock mode sinh câu hỏi giả mà vẫn trả HTTP 200**; thêm `OPENAI_API_KEY` + `APP_ENV=production`. Kết quả: `{"status":"ok","env":"production","mock_mode":false,"db":true}`, RestartCount=0.
  - **Bug chặn deploy 2-SPA:** `docker/frontend/Dockerfile` stage `build` không truyền `VITE_*`, Vite inline lúc compile nên bundle prod nhúng `http://localhost:5000/api` (`ARI.Shared/src/config/constants.ts:1`). Thêm `ARG VITE_API_BASE_URL=/api` — tương đối vì Nginx proxy `/api/` cùng origin; cả 2 SignalR hub đều dẫn xuất từ `API_BASE_URL` nên cũng thành relative → tự dùng `wss://` theo origin. Verify: bundle chứa `st="/api"`, 0 lần `localhost:5000` trong `.js`.
  - **`ports: []` không bao giờ có tác dụng:** Compose merge sequence bằng cách nối, không thay thế — đó là lý do `rag-service` vẫn hở `:8000` ra internet dù prod override đã khai báo `ports: []` từ lâu. Đổi sang `ports: !reset []` / `volumes: !reset []`; verify prod chỉ còn publish 80/443, dev giữ nguyên.
  - **Chấm dứt drift:** 4 file config bị sửa tay trên VPS (`nano`/`sed`) do prod override mount `../nginx/conf.d` vốn chứa config **dev**. Tạo `nginx/conf.d.prod/arisp.conf` (2 origin: `arisp.io.vn` → frontend-candidate:3000, `staff.arisp.io.vn` → frontend-staff:3001; thêm `client_max_body_size 25m` cho CV/JD PDF và `proxy_read_timeout 3600s` cho `/hubs/`), prod compose mount thư mục này. `git reset --hard` trong pipeline giờ an toàn.
  - **`docker/backend/Dockerfile`:** hợp nhất sửa đổi trên VPS — base `aspnet:8.0` (bỏ `-alpine`, thiếu ICU), `ASPNETCORE_HTTP_PORTS=5000`, restore theo `.csproj`. Bỏ `USER root` mà VPS đang dùng: nguyên nhân thật là `Program.cs:40` gọi `Directory.CreateDirectory("uploads")` lúc khởi động trên `/app` thuộc root → tạo sẵn `/app/uploads` + `chown app:app`, chạy bằng user non-root `app` (UID 1654) có sẵn trong image .NET 8.
  - **CI/CD:** `ci.yml` build + test .NET (14/14 pass), build cả 2 workspace FE, import-check rag-service trên mọi PR vào develop/main. `deploy.yml` build 4 image song song trên runner rồi push GHCR, VPS chỉ `pull && up -d` (~30s thay vì ~10 phút build trên máy 3.8GB RAM), health-check 2 origin sau deploy, rollback bằng `workflow_dispatch` với `image_tag` cũ.
  - **`docker/.env.example`:** khôi phục (đã bị xoá khỏi develop), ghi rõ hai hệ tên biến .NET `__` vs Python `UPPER_SNAKE` — chính là gốc của cả 2 sự cố RAG ở trên.

- [x] 2026-07-20: **Refactor Frontend Clean Architecture — `frontend/` → `ari-web/` monorepo, tách ARI.CandidateSite + ARI.StaffSite + ARI.Shared (ADR-046). HOÀN TẤT.**
  - **Wave 0:** `git mv frontend ari-web`; npm workspaces root (`package.json` + `tsconfig.base.json` + scripts `dev:candidate`/`dev:staff`/`build`); app monolith vào `src/ARI.CandidateSite` (`@ari/candidate-site`, port 3000). Kèm sửa 28 lỗi `tsc` có sẵn (JSX thiếu `)}`, type `t` 2-arg, unused vars, `cefrLevel`) để có baseline build xanh.
  - **Wave A:** extract **ARI.Shared** (`@ari/shared`, không build step): `api/apiClient` (+ `configureApiClient`), `fservices` chung (auth/job/application/interview/notification/schedule/profile), `ui` (designSystem + kit + common), `guards`, `document`, `media` (DeviceCheck + practice/room/cheat), `realtime`, `store`, `types`, `config`, `utils`, `authflows`, `i18n` core (`initI18n`+`sharedResources`), `styles`, `tailwind-preset.cjs`. Rewrite ~170 import `@services/@store/@config/@components/@hooks` → `@ari/shared/*` (subpath, không mega-barrel).
  - **Wave B:** carve-out **ARI.StaffSite** (`@ari/staff-site`, port 3001): scaffold đầy đủ (index.html theme-bootstrap, vite process-shim+dedupe, main/App + `StaffHomeRedirect` route `/`); `git mv` pages/{hr,recruiter,super-admin}+auth staff, layouts, `fservices` staff (mirror tên feature backend), i18n staff. Prune candidate về route + namespace candidate.
  - **Wave C:** reshape **ARI.CandidateSite** tách concern: `app/`(App+layouts) + `services/`→**`fservices/`** ("f" prefix) + xóa `StaffRedirect`. 2 site cùng khuôn app/pages/fservices/components/i18n.
  - **Wave D:** infra 2 site — Dockerfile ARG PKG/SITE/PORT (workspaces, layer-cache), compose `frontend-candidate`+`frontend-staff`, **nginx host-based** (`localhost`→candidate, `staff.localhost`→staff). Backend CORS thêm `Frontend:CandidateBaseUrl` (KHÔNG comma-list AdminFrontendUrl), `BuildRedirectUrl` chấp nhận 2 origin, email candidate ưu tiên CandidateBaseUrl. Backend build 0 error.
  - **Wave E:** close-out — xóa dead files (AuthContext, routes/index, CandidateApply, FeedbackPage, Admin/AuthLayout, barrels); **fix candidate refresh** dùng `/auth/candidate/refresh` (sửa lỗi phiên candidate bị đá ra); ADR-046 + tasks.md + CLAUDE.md + `ari-web/README.md`.
  - **Gate:** build cả 3 workspace + backend xanh, lint 0 error; **route freeze** union 2 `App.tsx` == baseline (65=65, không thêm URL); i18n namespace không cheo giữa 2 site; tailwind emit đúng token từ Shared.
  - **Còn lại cho user:** (1) `npm install` ở `ari-web/` rồi `npm run dev:candidate`/`dev:staff` smoke từng portal; (2) `docker compose up --build` verify `localhost` + `staff.localhost` qua nginx + OAuth 2 origin; (3) follow-up tùy chọn (xem ADR-046 "Không làm"): thống nhất `authService`→apiClient + fix bug URL logout, gỡ `@stomp/stompjs`, ESLint import-boundary.

- [x] 2026-07-19: **Refactor Phase 10 (close-out) — tests skeleton + ADR-045 + cập nhật docs. HOÀN TẤT refactor Clean Architecture.**
  - `ari-service/tests/`: `ARI.Domain.UnitTests` (3 tests — chốt cứng AppRoles values + entity defaults) + `ARI.Application.UnitTests` (11 tests — ValidationBehaviour trả Result.Failure không throw, TokenHashing 2 format Base64/Hex với known vectors, StaffLoginCommandValidator message verbatim). `dotnet test`: **14/14 pass**. Đã add vào `ARI.sln` (solution folder `tests`).
  - Docs: ADR-045 đầy đủ trong `.ai/architecture.md` (quyết định + deviations + follow-ups); CLAUDE.md thêm dòng ADR-045; skill `arisp-feature` viết lại bước 3–4 theo pattern CQRS (Command/Handler/Validator + thin controller + ErrorCode mapping).
  - Gate cuối: build 0 lỗi 0 warning (tests), swagger.json diff = RỖNG so baseline Phase 0 (98 paths), inventory `[Authorize]` IDENTICAL (41 attributes: 23 InternalStaff / 8 CandidateOnly / 6 bare / 3 HrManagement / 1 SuperAdminOnly), SignalR negotiate 401 không đổi.
  - Còn lại cho user: (1) chạy practice interview E2E xác nhận SessionHub (đổi `InterviewService` → `IInterviewService`); (2) `docker compose build backend` khi bật Docker Desktop (path Dockerfile đã sửa src/ layout); (3) follow-up tùy chọn — dissolve `ApplicationService`, tách `IEntityTypeConfiguration`, move DTOs/ vào feature folders.

- [x] 2026-07-19: **Refactor Phase 9 (Wave E) — CandidatePortal + Interviews sang CQRS (35 endpoints; CandidatePortalController 1666 → ~430 dòng).**
  - `IInterviewService` + `IInterviewCodeService` mới; **SessionHub đổi sang `IInterviewService`** (chỉ đổi type constructor — hub vẫn gọi TRỰC TIẾP service, KHÔNG qua MediatR pipeline để giữ latency ADR-006). `ARI.Application/Interviews/`: 11 thin delegate handlers (codes + sessions + HR review confirm).
  - `ARI.Application/CandidatePortal/`: CvMatchFeature (background Task.Run + ConcurrentDictionary poll-state move verbatim, đổi sang `ICvJdAnalysisService`), SavedJobs (4), PortalNotifications (5 + SyncNotifications DedupKey verbatim), PortalSettings (4, gồm export file + logout-all), PortalProfile (4 — BCrypt → `IPasswordHasher`, IsStrongPassword dùng chung `AuthSupport`), PortalApplications (GetMyApplications/Detail giữ nguyên shape anonymous qua `Result<object>`, VerifyCvInfo, ApplyToJob với `PortalApplyOutcome` cho case 409 already_applied + applicationId). `PortalSupport` gom các parser JSON/MapProfile/PracticeEligible.
  - Body lỗi có field `code` (bad_format/too_large/no_cv/cv_unreadable/invalid_cv/wrong_current_password/already_applied) tái tạo đúng qua ErrorCode. Verify: build 0 lỗi, swagger diff = rỗng, smoke 20 cases — status/body y hệt (kể cả khác biệt 404-profile vs 401-settings vs 401-cv-match của controller gốc). Hub regression: cần user chạy practice interview E2E xác nhận cuối.

- [x] 2026-07-19: **Refactor Phase 8 (Wave D) — Applications + Scheduling + Dashboard sang CQRS (17 endpoints).**
  - `ARI.Application/Applications/`: 3 queries + 5 commands. `ApplicationService` GIỮ SHARED sau interface `IApplicationService` mới (nhiều consumer: Applications endpoints, CandidatePortal apply, Jobs GetJobApplications) — handlers delegate; deviation có chủ đích so với plan gốc "dissolve" vì logic vốn đã nằm đúng Application layer, dissolve toàn phần để follow-up. `SubmitApplicationCommand` absorb phần xử lý file từ controller (MD5 hash, parse, save, cleanup-on-fail). `HashInviteToken` (SHA256 **hex**) chuyển vào `Common/Security/TokenHashing.Sha256Hex` (khác `Sha256Base64` của refresh token — 2 format cùng tồn tại trong DB), 3 call sites cập nhật, xóa static cũ.
  - `ARI.Application/Scheduling/`: candidate-side (GetOpenSlots, BookSlot — giữ nguyên atomic UPDATE chống overbooking + compensation, GetCandidateSchedule) + staff-side (GetAvailabilitySlots, CreateSlot, DeleteSlot, UpdateSlotCapacity). `SchedulingSupport`: AuthorizeCandidateAsync (invite token hex / ownership) + CanManageAsync (owner/admin) — claims trích ở controller truyền vào.
  - `ARI.Application/Dashboard/`: GetHrDashboardQuery — copy verbatim gồm `RunScopedAsync` (IServiceScopeFactory, 5+2 queries song song) + toàn bộ analytics. Verify: build 0 lỗi, swagger diff = rỗng, smoke 16 cases (dashboard KPI thật, guards/403/404/400 message verbatim, atomic booking flow không đổi).

- [x] 2026-07-19: **Refactor Phase 7 (Wave C) — Jobs + Playbooks + CvAnalysis sang CQRS (18 endpoints; JobsController 1408 → ~260 dòng).**
  - `ARI.Application/Jobs/`: 5 queries (GetJobs với filter/sort/relevance, GetJobFacets, GetJobById, GetAdminJobs, GetJobApplications) + 6 commands (CreateJob + RAG ingest, UpdateJob + re-create rounds, DeleteJob, UpdateJobStatus — toàn bộ approval workflow + đóng dấu JD PDF/DOCX best-effort, CreateJobSlots, AnalyzeJd — Gemini auto-fill ADR-042). `JobsSupport`: ValidateJobRequest dùng chung Create/Update (giữ khác biệt deadline-exemption khi update), facet label maps + BuildFacet, ParseCsv. Khối filter Job Board gộp thành local func `ApplyFilters` dùng chung count + page (trước lặp 2 lần).
  - `ARI.Application/Playbooks/`: GetPlaybooks + UploadPlaybook (absorb + XÓA `PlaybookService` — 1 consumer; parse → save → ingest RAG, lỗi thì xoá file) + DeletePlaybook. `ARI.Application/CvAnalysis/`: 4 thin handlers delegate sang `ICvJdAnalysisService` (interface mới cho `CvJdAnalysisService` — 3 consumer, giữ shared).
  - Controllers giữ guards IFormFile/claims + `MapFailure` cục bộ (map ErrorCode → 400/401/403/404/500 đúng shape cũ). Verify: build 0 lỗi, swagger diff = rỗng, smoke 18 cases dữ liệu thật (list/facets/detail/admin/mine 200, validation + 404 message verbatim).

- [x] 2026-07-19: **Refactor Phase 6 (Wave B) — Admin + AccountRequests sang CQRS (17 endpoints).**
  - `ARI.Application/Admin/`: 6 queries (PendingUsers, Users paged, AdminStats, AuditLogs, SystemSettings, AccountRequests) + 8 commands (ApproveUser, CreateStaffUser, UpdateUserRole, Deactivate/Activate/DeleteUser, UpdateSystemSettings, Approve/RejectAccountRequest). `AdminSupport` (GenerateTemporaryPassword, WriteAuditAsync không SaveChanges, SendStaffWelcomeEmailAsync best-effort) — chuyển verbatim từ private helpers.
  - `ARI.Application/AccountRequests/` (phía HR Leader): GetMyAccountRequests + CreateAccountRequests (batch, validate từng item, chặn trùng, conflict 409). `CommonErrorCodes` (not_found/conflict) cho mapping 404/409. Request classes (CreateStaffUserRequest, UpdateSettingItem...) move sang Application giữ nguyên tên — schema swagger không đổi. Temp-password hash qua `IPasswordHasher`. ActorId từ claims truyền qua command.
  - Verify: build 0 lỗi, swagger diff = rỗng, smoke 14 cases dữ liệu thật (stats/users/audit/settings shape camelCase đúng, 404/400/409 + message verbatim, thứ tự guard giữ nguyên).

- [x] 2026-07-19: **Refactor Phase 5 (Wave A) — Auth: `AuthController` 1107 → ~450 dòng thin controller, 15 commands + 1 query.**
  - `ARI.Application/Auth/`: CandidateLogin, StaffLogin, CompleteExternalStaffSignIn (domain validate + pre-provisioning check), CompleteExternalCandidateSignIn (JIT provisioning), RefreshStaffToken, RefreshCandidateToken, Logout, RegisterCandidate, VerifyCandidateEmail, ResendCandidateVerification, VerifyMagicLink, CandidateForgotPassword, CandidateResetPassword, StaffForgotPassword, StaffResetPassword + GetCurrentUserQuery. `AuthSupport` (NormalizeEmail, IsStrongPassword, refresh-token issuance, verification email) + `AuthErrorCodes` + `Common/Security/TokenHashing` (SHA256).
  - `Result.ErrorCode` (additive) để controller map failure → đúng status cũ (401 invalid_credentials, 403+code email_not_verified, 404 not_found, redirect pending/rejected cho OAuth). BCrypt/JWT inline → `IPasswordHasher`/`ITokenService`. Google OAuth Challenge/Authenticate/SignOut/Redirect + `BuildRedirectUrl` ở lại controller (protocol, không phải business logic). RegisterCandidate KHÔNG dùng validator cho password — giữ đúng thứ tự check gốc (email trùng trước, độ mạnh sau).
  - Verify: build 0 lỗi, swagger diff = rỗng, smoke matrix 16 cases — status + body y hệt (401/400/403/404/503, message tiếng Việt verbatim, /me shape, thứ tự check register đúng).

- [x] 2026-07-19: **Refactor Phase 4 — CQRS plumbing (MediatR + FluentValidation) + pilot StaffNotifications & Evaluations.**
  - Packages: MediatR **pin cứng [12.5.0]** (bản Apache-2.0 cuối — v13+ commercial), FluentValidation.DependencyInjectionExtensions 11.11.0, Microsoft.EntityFrameworkCore 8.0.4 vào ARI.Application (cho LINQ extension trong handler, giống JT); BCrypt.Net-Next + System.IdentityModel.Tokens.Jwt vào Infrastructure. KHÔNG AutoMapper (v15 commercial + projection thủ công là load-bearing).
  - 4 pipeline Behaviours (`Common/Behaviours/`): UnhandledException → Logging (pre-processor) → **Validation trả `Result.Failure` thay vì throw** (deviation JT có chủ đích — giữ Result Pattern + body 400 y hệt) → Performance (warn >500ms).
  - `ITokenService`/`IPasswordHasher` (Application/Interfaces) + `Infrastructure/Identity/{JwtTokenService,BcryptPasswordHasher}` — logic mint JWT copy verbatim từ AuthController (HS256, 7 ngày, map role legacy), sẵn cho Wave A.
  - Pilot: **Evaluations** (4 endpoints → 3 queries, XÓA `EvaluationService`; DTO move vào `Evaluations/`) + **StaffNotifications** (5 endpoints → 1 query + 4 commands, logic sync DedupKey move verbatim). Controllers chỉ còn guard HTTP + `ISender.Send` + mapping Result→ActionResult copy đúng shape cũ.
  - Verify: build 0 lỗi, swagger diff = rỗng, smoke 9 endpoints bằng JWT super_admin tự mint — body/status y hệt (items/unreadCount camelCase, 404 message, validation strings verbatim, danh sách evaluations dữ liệu thật OK).

- [x] 2026-07-19: **Refactor Phase 3 — Hợp nhất raw SQL bootstrap vào EF migration `ReconcileStartupBootstrap`.**
  - `OnModelCreating` thêm 16 `HasIndex(...).HasDatabaseName(...)` khớp đúng tên index bootstrap (13 perf + 2 invite + partial unique `ux_interview_bookings_app_round_scheduled` filter `status='scheduled'`); model giờ chứa `InterviewInvite` + 4 cột approval mà snapshot trước đây thiếu.
  - Migration `Up()/Down()` viết bằng `migrationBuilder.Sql(...)` idempotent (`IF NOT EXISTS`/`IF EXISTS`) thay vì scaffold ops — an toàn trên DB đã bootstrap đầy đủ/dở dang/trống. Bonus: hợp nhất index trùng `"IX_applications_cv_jd_analysis_id"` (EF default từ AddCvJdAnalyses) với bản lowercase của bootstrap — giữ 1 bản.
  - Xoá toàn bộ khối raw SQL khỏi `AriDbContextInitialiser` — schema từ nay 100% do migrations sở hữu. Verify: scaffold thử ra migration RỖNG (model = snapshot), boot dev DB áp đúng 1 migration không warning, boot lần 2 "already up to date", swagger diff = rỗng, GET /api/jobs 200.

- [x] 2026-07-18: **Refactor Phase 2 — DI decomposition theo JT template: `Program.cs` 521 → 62 dòng.**
  - Tạo `ARI.Application/DependencyInjection.cs` (`AddApplication`: InterviewOptions + 6 app services), `ARI.Infrastructure/DependencyInjection.cs` (`AddInfrastructure`: DbContext + Npgsql pooling, UoW, AI provider switch rag/openai, storage switch Local/S3, media real-vs-mock, email queue + hosted service), `ARI.API/DependencyInjection.cs` (`AddWebServices`: swagger, SignalR, JWT + External cookie + Google, 4 authorization policies, CORS, ForwardedHeaders, `INotificationService`).
  - `AriDbContextInitialiser` (Infrastructure/Data): migrate-retry 3 lần + raw SQL bootstrap move verbatim (sẽ xoá ở Phase 3); Program gọi qua `app.InitialiseDatabaseAsync()`. Thêm `ValidateScopes/ValidateOnBuild` (Development) bắt DI miss lúc boot.
  - Thuần relocation — không đổi logic đăng ký. Verify: build 0 lỗi, boot cả 2 nhánh `AI:Provider` (openai/rag), swagger diff = rỗng, negotiate 401, staff login 401 (bad creds), GET /api/jobs 200, log initialiser đủ 5 bước.

- [x] 2026-07-18: **Refactor Phase 1 — Rename `backend/` → `ari-service/` (src/ layout) + `ARISP.*` → `ARI.*` (namespace PascalCase).**
  - Commit A thuần `git mv` (153 file, 100% rename — giữ git history `--follow`); Commit B text sweep 5 token (`ARISP.Domain|Application|Infrastructure|API`→`ARI.*`, `ARISPDbContext`→`AriDbContext`) — KHÔNG blanket replace, giữ nguyên brand values (JWT Issuer `ARISP`, cookie `ARISP.External`, swagger title, email templates).
  - Layout mới theo template Jason Taylor: `ari-service/ARI.sln` + `src/ARI.{Domain,Application,Infrastructure,API}` (tests/ sẽ thêm ở phase sau). Sln paths + Dockerfile (`docker/backend/Dockerfile`: COPY `src/ARI.*`, ENTRYPOINT `ARI.API.dll`) + docker-compose (context/volume `../ari-service`) + hooks `.claude` (arch-guard, tasks-reminder regex `ari-service/`) + skill `arisp-feature` + docs (README, CLAUDE.md, coding-rules, practice-interview-setup, schema.md) cập nhật đồng bộ.
  - Migrations an toàn: chỉ đổi namespace + `[DbContext(typeof(AriDbContext))]`, KHÔNG đụng `[Migration("id")]`/`ef_migrations_history`. Verify: build 0 lỗi (7 warnings pre-existing), 16 migrations list đủ, model fingerprint trước/sau rename identical, swagger.json diff = rỗng (98 paths), SignalR negotiate 401 không đổi. Gate `docker compose build` hoãn (Docker daemon không chạy lúc refactor).

- [x] 2026-07-02: **Practice Interview production-ready: sửa STT chết + giảm trễ AI + avatar full màn + LiveAvatar production (ADR-044 hoàn thiện).**
  - **Root cause STT:** `@deepgram/sdk` v3 chỉ nhận API key (`createClient({ accessToken })` ném "A deepgram API key is required" ngay khi khởi tạo) → STT chưa bao giờ chạy. **Bỏ SDK**, FE mở **WebSocket Deepgram trực tiếp** (`wss://api.deepgram.com/v1/listen`, auth subprotocol `['bearer', <token ngắn hạn>]`), tự parse `Results`/`UtteranceEnd`; auto-reconnect tối đa 3 lần (mint token mới qua media-config). TTL token 60s→300s (`MediaOptions` + appsettings) — trước đây avatar khởi tạo tuần tự làm token hết hạn trước khi STT nối.
  - **Chống echo:** DeviceCheck xin `echoCancellation/noiseSuppression/autoGainControl`; hook bỏ mọi transcript khi AI đang nói (`aiSpeakingRef`) + xả buffer khi AI nói xong (không thì giọng avatar dội vào mic bị submit thành "câu trả lời"); `askedAt` tính từ lúc AI nói xong (responseMs chuẩn).
  - **Giảm trễ (3 đòn bẩy):** ① BE TTS xong **tự đẩy** `ReceiveQuestionAudio` (PCM 24k base64) qua SignalR ngay sau `ReceiveQuestion` — bỏ round-trip FE→BE `/tts` (kèm 2 query xác thực) khỏi critical path (endpoint giữ làm fallback, timeout 6s); ② `SessionHub.SubmitAnswerText` tách `SaveAnswerAsync` (nhanh, không LLM) → sinh + gửi câu hỏi kế **trước** → `AnalyzeAnswerAndAdaptAsync` (adaptive difficulty) chạy **sau** — bỏ 1 vòng LLM khỏi đường trễ; ③ ElevenLabs chuyển `with-timestamps` → endpoint thường `?output_format=pcm_24000&optimize_streaming_latency=3` + fix N+1 chat history (1 query cho toàn bộ answers — Supabase remote mỗi round-trip đắt).
  - **FE fallback audio nâng cấp:** không avatar → phát PCM ElevenLabs qua **WebAudio** (giọng nhất quán), chỉ khi không có audio mới rơi xuống browser TTS. Avatar `keepAlive()` 60s/lần chống idle-timeout; `SESSION_DISCONNECTED` → tự rơi xuống WebAudio. Khởi tạo media **song song** (STT + avatar + hub).
  - **UI phòng thử:** avatar video **phủ kín khung** (`absolute inset-0 object-cover`) thay ô 320px; câu hỏi hiện tại + trạng thái nổi trên video (backdrop-blur); self-view PiP góc phải.
  - **Production:** `Media:HeyGen:IsSandbox` mặc định **false** (đã verify mint token LiveAvatar production OK với key thật). Merge `origin/develop` (notifications staff + SignalR realtime), hợp nhất `SignalRNotificationService`: session events giữ `SendAsync` động (không rơi `questionId`) + app-notification hub strongly-typed của develop.

- [x] 2026-06-30: **Migrate avatar sang HeyGen LiveAvatar (LITE) — Streaming Avatar API cũ đã sunset 410 (ADR-044 cập nhật).**
  - **Nguyên nhân:** `/v1/streaming.create_token` + `@heygen/streaming-avatar` bị HeyGen khai tử (410 endpoint_sunset) → cả endpoint media-config 500.
  - **BE:** `HeyGenAvatarService` mint **LiveAvatar** session token `POST api.liveavatar.com/v1/sessions/token` (`mode=LITE`, `avatar_id`, `is_sandbox`); `MediaOptions.HeyGen` trỏ LiveAvatar + `IsSandbox`. `ElevenLabsTTSService.TextToSpeechBase64PcmAsync` (PCM 24k → `audio_base64`) + endpoint `POST /session/{id}/tts`. **media-config resilient**: provider lỗi → trả null thay vì 500 (không sập phòng).
  - **FE:** thay `@heygen/streaming-avatar` → **`@heygen/liveavatar-web-sdk`**; `usePracticeSession`: `new LiveAvatarSession(token,{voiceChat:false,apiUrl})` → `attach(video)` (SESSION_STREAM_READY) → nhận câu hỏi → fetch TTS (BE) → `repeatAudio(pcm24k)`; AVATAR_SPEAK_STARTED/ENDED → trạng thái nói. LITE = giữ não RAG/GPT-4o, avatar chỉ lip-sync.
  - **Cần đổi cấu hình test:** `Media:HeyGen:DefaultAvatarId` phải là **UUID LiveAvatar** (avatar id Streaming cũ không dùng được); key LiveAvatar (`X-API-KEY`); `IsSandbox=true` để test. BE build 0 lỗi; FE tsc + vite build pass.

- [x] 2026-06-28: **Phỏng vấn thử (Practice) end-to-end với media stack thật — ADR-044 (Deepgram + ElevenLabs + HeyGen).**
  - **BE:** `MediaOptions` (Deepgram/ElevenLabs/HeyGen, section `Media`); `DeepgramTokenService` (mint ephemeral token `/v1/auth/grant`), `ElevenLabsTTSService : ITTSService` (Flash v2.5 stream), `HeyGenAvatarService : IAvatarService` (+ `CreateStreamingTokenAsync`). DI chọn real vs `Mock*` theo việc có API key. `SignalRNotificationService` thật (thay `MockNotificationService`) đẩy `ReceiveQuestion`/`ReceiveSessionStatus` tới `SessionHub`. Endpoint `GET /api/interview/session/{id}/media-config` (CandidateOnly, xác thực sở hữu phiên) + thêm `questionId` vào payload `ReceiveQuestion`.
  - **FE:** thêm `@microsoft/signalr`, `@deepgram/sdk`, `@heygen/streaming-avatar@2.0.16`; hook `usePracticeSession` điều phối startSession→media-config→HeyGen avatar→SignalR(Join/Start)→ReceiveQuestion→avatar.speak→Deepgram live STT→SubmitAnswerText→end. `PracticeSessionPage` rewire: avatar video + transcript realtime + interim + nút "Gửi trả lời" + trạng thái nói/nghe. Fallback mềm khi thiếu key (browser TTS, nhập tay).
  - **RAG CV+JD:** đã tích hợp sẵn — CV ingest lúc apply (`source_type=cv`/applicationId), JD lúc tạo job (`jd`/jobPostingId); rag-service hybrid-retrieve theo đúng scope (practice = JD+CV, không Playbook). Thêm **`EnsureSourcesIngestedAsync`** trong `StartSessionAsync`: idempotent ingest CV+JD nếu THIẾU trước câu hỏi đầu (an toàn khi RAG service từng lỗi / đổi provider). Path `openai` in-process dùng full-text JD+CV.
  - **Chỉ còn cắm 4 API key để test** — xem [docs/practice-interview-setup.md](../docs/practice-interview-setup.md). BE build 0 lỗi; FE `tsc --noEmit` + `vite build` pass.
  - **Chưa làm (sau):** Deepgram/ElevenLabs/HeyGen cho phỏng vấn **thật** on-site (tái dùng services + DeviceCheck); migrate `@heygen/streaming-avatar`→`@heygen/liveavatar-web-sdk`; cheat detection signals trong practice.
- [x] 2026-06-30: **Thông báo (Notifications) cho nhân sự nội bộ (HR Admin / Recruiter) — tách endpoint riêng, end-to-end FE + BE + DB.**
  - **Bối cảnh:** nút chuông trên header HR (`HrLayout`) hiển thị badge "3" + 1 thông báo giả hardcode, nút "Đánh dấu đã đọc"/"Xem tất cả" không hoạt động; header Recruiter/Super Admin (`WorkspaceLayout`) chỉ là dropdown rỗng tĩnh. Trước đó chỉ Candidate có thông báo thật.
  - **Quyết định kiến trúc:** **tách controller riêng** thay vì mở rộng `CandidatePortalController` (policy `CandidateOnly` + logic sync khác hẳn). Dùng **chung bảng `notifications`**, người nhận staff qua cột mới `recipient_user_id`.
  - DB: `Notification` thêm `RecipientUserId` (nullable) + đổi `CandidateAccountId` sang nullable; thêm unique index `(recipient_user_id, dedup_key) WHERE deleted_at IS NULL`. Migration `AddStaffNotifications` (viết tay do bin bị khoá bởi API đang chạy; đã cập nhật model snapshot).
  - BE: `StaffNotificationsController` (`[Authorize(Policy="InternalStaff")]`, route `api/staff/notifications`): `GET` (sync → items + unreadCount), `POST /read-all`, `POST /{id}/read`. `SyncNotificationsAsync` idempotent theo DedupKey, phạm vi theo vai trò (Recruiter chỉ tin mình tạo, HR Admin toàn bộ): ứng viên mới ứng tuyển 30 ngày (`applied`), đánh giá AI chờ HR xác nhận (`pending`), tin chờ duyệt — chỉ HR Admin (`approval`). Link theo prefix `/hr` hoặc `/recruiter`.
  - FE: `staffNotificationService` (list/markAllRead/markRead → `/staff/notifications`). Nối `HrLayout` + `WorkspaceLayout` vào dữ liệu thật: badge unreadCount, danh sách (icon theo loại, thời gian tương đối, dấu chưa đọc), "Đánh dấu đã đọc" hoạt động, click item → mark read + điều hướng link; bỏ toàn bộ dữ liệu hardcode.
  - **Thông báo kết quả duyệt tin về Recruiter (event-driven):** `JobsController.UpdateJobStatus` khi HR Admin **duyệt** (pending→active) hoặc **từ chối** (→rejected) ghi trực tiếp 1 `Notification` cho người tạo tin (`CreatedByUserId`) trong cùng transaction (`AddJobDecisionNotificationAsync`). Type `approved`/`rejected`, link tới chi tiết tin theo workspace người tạo (`/recruiter/my-jobs/{id}` hoặc `/hr/jobs/{id}`), kèm tên người duyệt + lý do từ chối; bỏ qua nếu người duyệt cũng là người tạo; DedupKey có Ticks để mỗi vòng nộp lại là sự kiện riêng. FE thêm icon `approved` (CheckCircle2) / `rejected` (XCircle).
  - **Xóa thông báo (soft delete):** BE thêm `DELETE /api/staff/notifications/{id}` (xóa 1) + `DELETE /api/staff/notifications` (xóa tất cả của user). Dùng `Repository.Delete` → interceptor `SaveChangesAsync` tự chuyển thành soft delete (`DeletedAt`), global query filter loại bản đã xóa. Sửa `SyncNotificationsAsync` build tập `existing` bằng `IgnoreQueryFilters()` (gồm cả bản đã xóa) để thông báo đã xóa KHÔNG bị sync tái sinh ở lần mở sau. FE: `staffNotificationService.remove/clearAll`; nút X hiện khi hover từng item + nút "Xóa tất cả" ở header dropdown (cả `HrLayout` và `WorkspaceLayout`), cập nhật lạc quan.
  - **Chuông nhân sự real-time qua SignalR:** trước đó chuông chỉ tải lại khi đổi route → badge không nhảy khi đang ở yên 1 trang. Nối vào `AppNotificationHub` qua hook `useAppNotifications`: khi nhận push staff-relevant (`ReceiveNewApplication`, `ReceiveJobPostingUpdate` duyệt/từ chối/chờ duyệt, `ReceiveSystemEvent`→`AiEvaluationComplete`) phát sự kiện DOM `STAFF_NOTIF_REFRESH_EVENT`. `HrLayout` + `WorkspaceLayout` refactor load thành `loadNotifs` (`useCallback`), lắng nghe sự kiện → gọi lại `staffNotificationService.list()` tức thời. Cài thiếu `@microsoft/signalr` (khai báo trong package.json nhưng chưa có trong node_modules → SignalR không chạy được trước đó).
  - **Bảo mật SignalR Hub (`[Authorize]`):** trước đó cả 3 hub (`AppNotificationHub`, `SessionHub`, `WebRTCSignalingHub`) đều mở ẩn danh — bất kỳ client nào (kể cả chưa đăng nhập) join được group session theo GUID để nhận `ReceiveQuestion`/`ReceiveCheatAlert`, rò rỉ nội dung phỏng vấn. Thêm `[Authorize]` cho cả 3 hub. FE guard `useAppNotifications` chỉ kết nối khi có token (tránh 401 + reconnect spam trên trang public như Job Board). `SessionHub`/`WebRTCSignalingHub` chưa có client FE (media pipeline Phase 7) nên không phá gì. (Còn lại để siết sau: kiểm tra quyền sở hữu session trong `SessionHub.JoinSession`.)

- [x] 2026-06-27: **Đồng bộ tài liệu `.ai/` + CLAUDE.md về mô hình Scheduling/Practice thật trong code (sửa lệch ADR-020).**
  - Phát hiện docs lỗi thời so với code (Phase B2/B3 đã ship 2026-06-22 nhưng ADR chưa sync): scheduling là cho **phỏng vấn thật per vòng** (`AvailabilitySlot.RoundNumber`, `InterviewBooking`), **đặt lịch xong mở 1 lượt phỏng vấn thử cho vòng đó**; practice **1 lượt/VÒNG** vào qua **Portal** (`/practice/:applicationId`), **không cần Interview Code**; `InterviewCode` **không có** `code_type` (chỉ dùng cho real/Kiosk).
  - **architecture.md:** viết lại ADR-020 (Practice-only → Real per vòng + mở practice), ADR-027 (1 lượt/vòng + Portal), ADR-015 (truy cập practice), ADR-016 (bỏ `code_type`, real-only), ADR-038 (gating + chi phí mỗi vòng=1 thử+1 thật), ADR-013, dòng `ApplicationService` trong Service Boundaries.
  - **context.md:** Remote/On-site interview mode, Availability Slots, flow Phase 2–3, mục Practice Interview. **glossary.md:** mục Practice Interview. **CLAUDE.md:** Interview Modes, Recruitment Flow Phase 1–3, bảng ADR-015/016/038.
  - Làm rõ thêm: **practice giống hệt buổi thật của cùng vòng** — cùng `round_type` + ngôn ngữ (chung `InterviewRoundConfig` theo `RoundNumber`); khác biệt duy nhất là nguồn RAG (JD+CV) + không quay video (ADR-027, context.md, CLAUDE.md).
  - Không đổi code — chỉ tài liệu (code đã đúng từ B3).

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
