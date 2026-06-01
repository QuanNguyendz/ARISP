# ARISP – Database Schema Documentation

> **Source file:** `docs/database/schema.sql`
> **DB:** PostgreSQL (Supabase hosted) + pgvector extension
> **ORM:** EF Core – mọi thay đổi schema đi qua migration, không sửa DB trực tiếp
> **Convention:** `snake_case` tables/columns · UUID PKs · `created_at` / `updated_at` trên mọi bảng chính · soft delete qua `deleted_at`
> **Last updated:** 2026-06-01

---

## Sơ đồ quan hệ tổng quan (Strict Single-tenant)

```
system_settings (Global configs: Allowed domains, OAuth2, Webhooks, etc.)

users (role: super_admin | hr_admin | recruiter)
job_postings (interview_mode: onsite)
  ├── interview_round_configs
  ├── availability_slots (Phỏng vấn thử)
  └── playbook_documents (scope: org | job_posting | round)
applications
  ├── [candidate_accounts] ← nếu ứng viên ứng tuyển từ 'job_board'
  ├── interview_codes (Mã On-site phục vụ Kiosk)
  ├── interview_bookings (Practice Remote)
  └── interview_sessions (Phiên phỏng vấn AI per round)
  │           ├── questions
  │           │     └── answers
  │           ├── must_ask_tracking
  │           ├── cheat_detection_signals
  │           └── evaluations
  │                 └── hr_reviews
  ├── document_chunks (pgvector – embeddings của JD, CV, Playbook)
  ├── audit_logs (Lịch sử thao tác hệ thống)
  └── webhook_deliveries (Nhật ký gửi webhook sang ATS bên thứ 3)

candidate_accounts  ← Ứng viên tự đăng ký qua Job Board (đăng nhập riêng biệt)
  └── candidate_refresh_tokens
```

---

## Phase 1 – Global Settings & Auth

### `system_settings`
Bảng cấu hình hệ thống toàn cục dành riêng cho môi trường Single-tenant. Lưu trữ thông tin đăng nhập, danh sách email domain được phép truy cập, webhook URL của ATS/Slack/Teams toàn công ty.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | Khởi tạo mặc định qua `uuid_generate_v4()` |
| `key` | VARCHAR(100) UNIQUE | Khóa cấu hình (ví dụ: `allowed_email_domains`, `slack_webhook_url`) |
| `value` | TEXT | Giá trị cấu hình |
| `description` | TEXT | Mô tả chức năng cấu hình |
| `updated_at` | TIMESTAMPTZ | Tự động cập nhật thời gian |

### `users`
HR Admin, Recruiter, SuperAdmin. **Không** bao gồm Candidate – xem `candidate_accounts`. 

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | |
| `email` | VARCHAR UNIQUE | Validate domain theo cấu hình `allowed_email_domains` khi đăng nhập OAuth2 |
| `password_hash` | TEXT | NULL nếu chỉ dùng OAuth2 / OIDC công ty |
| `role` | VARCHAR(50) | `super_admin` \| `hr_admin` \| `recruiter` |
| `department` | VARCHAR | Phòng ban nội bộ |

### `refresh_tokens`
JWT refresh token store cho users. Hash token trước khi lưu.

### `magic_links`
One-time link đăng nhập Candidate Portal. TTL 15 phút.

---

## Phase 2b – Candidate Accounts (Job Board)

### `candidate_accounts`
Candidate tự đăng ký tài khoản trên ARISP để tìm việc và tự ứng tuyển qua Job Board. **Tách biệt hoàn toàn** với bảng `users` (nhân sự công ty).

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | |
| `email` | VARCHAR UNIQUE | Cho phép mọi email cá nhân (gmail, yahoo, v.v.) |
| `password_hash` | TEXT | |
| `full_name` | VARCHAR | |
| `phone` | VARCHAR | optional |
| `headline` | VARCHAR(255) | e.g. "Backend Developer 3 yrs exp" |
| `profile_cv_url` | TEXT | CV mặc định |

> **Lý do tách biệt:** Ứng viên dùng tài khoản cá nhân có vòng đời đăng nhập và cơ chế xác thực riêng biệt, trong khi nhân viên công ty (`users`) đăng nhập qua OAuth2 bắt buộc validate email domain doanh nghiệp.

### `candidate_refresh_tokens`
Refresh tokens riêng cho candidate_accounts.

---

## Phase 2 – Job Posting & Application

### `job_postings`
Tin tuyển dụng, metadata Job Board IT và cấu hình AI Interviewer.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | |
| `created_by_user_id` | UUID FK | Người tạo tin (`users.id`) |
| `title` | VARCHAR(255) | Tên vị trí hiển thị |
| `department` | VARCHAR(255) | Phòng ban nội bộ sở hữu headcount (vd. `IT Engineering`) — optional |
| `job_description` | TEXT | JD raw text, đưa vào RAG / AI phỏng vấn |
| `interview_mode` | VARCHAR(20) | Mặc định `onsite` — phỏng vấn thật bắt buộc On-site |
| `status` | VARCHAR(50) | `draft` \| `active` \| `closed` \| `archived` |
| `is_public_listing` | BOOLEAN | Có hiển thị công khai trên Job Board IT không |
| **Job Board listing** | | |
| `location` | VARCHAR(255) | Thành phố chính (vd. `Ho Chi Minh City`, `Ha Noi`) |
| `work_mode` | VARCHAR(20) | `onsite` \| `remote` \| `hybrid` |
| `salary_min` | NUMERIC(12,2) | NULL khi `salary_is_negotiable = true` |
| `salary_max` | NUMERIC(12,2) | NULL khi `salary_is_negotiable = true` |
| `salary_currency` | VARCHAR(3) | `VND`, `USD` — mặc định `VND` |
| `salary_is_negotiable` | BOOLEAN | `true` → hiển thị "Thỏa thuận" |
| `employment_type` | VARCHAR(30) | `full_time` \| `part_time` \| `contract` \| `internship` |
| `experience_level` | VARCHAR(30) | `fresher` \| `junior` \| `mid` \| `senior` \| `lead` \| `manager` |
| `skills` | TEXT[] | Tag kỹ năng (vd. `C#`, `PostgreSQL`) — filter qua GIN index |
| `job_category` | VARCHAR(100) | IT expertise: `backend`, `frontend`, `devops`, `qa`, `data`, `ai_ml`, `mobile`, `pm`, … |
| `application_deadline` | TIMESTAMPTZ | Hạn nộp hồ sơ |
| `is_urgent` | BOOLEAN | Badge "Tuyển gấp" — mặc định `false` |
| **AI Interview config** | | |
| `detected_language` | VARCHAR(10) | AI tự phát hiện ngôn ngữ yêu cầu từ JD |
| `language_requirement` | TEXT | vd. "TOEIC > 700 hoặc IELTS > 6.5" |
| `language_confirmed` | BOOLEAN | HR đã xác nhận yêu cầu ngôn ngữ |
| `reschedule_deadline_hours` | INT | Hạn dời lịch phỏng vấn thử (Practice) |
| `invite_token_ttl_hours` | INT | TTL magic link mời ứng viên |
| `scoring_rubric` | JSONB | Bộ tiêu chí đánh giá cho Job |
| `persona_name` | VARCHAR(100) | Tên avatar AI |
| `persona_voice_id` | TEXT | ElevenLabs voice ID |
| `persona_style` | TEXT | Phong cách persona |
| `published_at` | TIMESTAMPTZ | Thời điểm publish lên Job Board |
| `created_at` / `updated_at` / `deleted_at` | TIMESTAMPTZ | Audit + soft delete |

> **Single-tenant:** Tên công ty hiển thị trên Job Board lấy từ `system_settings` (global), không lặp trên từng tin. `department` là phòng ban nội bộ, khác `job_category` (phân loại IT cho ứng viên).

### `interview_round_configs`
Config từng vòng phỏng vấn per Job Posting. UNIQUE(job_posting_id, round_number).

| Cột | Ghi chú |
|-----|---------|
| `round_type` | `screening` \| `technical` \| `online_test` (Online Test - Multiple Choice Test) |
| `interview_code_ttl_hours` | TTL của mã code On-site lúc đến thi thật (mặc định 2h) |
| `max_duration_minutes` | Thời gian giới hạn phiên phỏng vấn |

### `applications`
Hồ sơ Candidate ứng tuyển cho một Job Posting.

| Cột | Ghi chú |
|-----|---------|
| `job_posting_id` | FK → `job_postings.id` |
| `candidate_account_id` | FK → `candidate_accounts.id`; NULL nếu HR tạo thủ công ứng viên |
| `source` | `job_board` (ứng viên tự apply) \| `invited` (HR chủ động mời) |
| `status` | Trạng thái ứng tuyển: `invited` → `cv_submitted` → `screening` → `interview` → `pass` / `not_pass` |
| `practice_session_used` | Flag đánh dấu đã dùng lượt phỏng vấn thử (tối đa 1 lần per Application) |
| `cv_text` | Văn bản trích xuất từ CV để đưa vào RAG pipeline |

---

## Phase 3 – Scheduling & Interview Code

### `availability_slots`
HR cấu hình khung giờ rảnh để ứng viên chọn lịch phỏng vấn thử (Practice Remote).

### `interview_bookings`
Mỗi khi ứng viên chọn một slot phỏng vấn thử thì tạo booking. Hỗ trợ dời lịch (`rescheduled_from_id`).

### `interview_codes`
Mã truy cập phỏng vấn thật On-site tại công ty. **Sử dụng duy nhất 1 lần (One-time-use)**. 

| Cột | Ghi chú |
|-----|---------|
| `code` | Định dạng 6-8 ký tự alphanumeric (ví dụ: `ARX-7K2P`), UNIQUE |
| `expires_at` | Thời gian hết hạn của mã (mặc định +2 giờ từ lúc tạo) |
| `used_at` | Ghi nhận thời điểm sử dụng. NULL = chưa dùng, mã còn hiệu lực |

---

## Phase 4 – AI Interview Core

### `interview_sessions`
Một phiên phỏng vấn AI cụ thể ứng với từng vòng của ứng viên.

| Cột | Ghi chú |
|-----|---------|
| `session_type` | `practice` (phỏng vấn thử từ nhà) \| `real` (phỏng vấn thật bắt buộc On-site) |
| `status` | `pending` \| `active` \| `completed` \| `aborted` \| `error` |
| `recording_url` | Đường dẫn lưu trữ video/audio recording |

*Lưu ý về nguồn RAG:*
* `practice`: Chỉ retrieve thông tin từ JD + CV ứng viên. Không nạp Playbook bảo mật của công ty.
* `real`: Thực hiện RAG toàn diện (JD + CV + Playbook đầy đủ các cấp của doanh nghiệp).

### `questions`
Các câu hỏi do AI sinh ra hoặc lấy từ Playbook ngân hàng câu hỏi.

### `answers`
Nội dung câu trả lời của ứng viên trích xuất qua STT. Cột `response_time_ms` ghi nhận thời gian phản xạ hỗ trợ phát hiện gian lận.

### `document_chunks`
Bảng lưu trữ vector embeddings của JD, CV và tài liệu Playbook. Cột `embedding` dùng kiểu dữ liệu `VECTOR(1536)` từ pgvector.

---

## Phase 4b – Interview Playbook

### `playbook_documents`
Tài liệu phỏng vấn nội bộ doanh nghiệp. Được phân loại theo phạm vi `scope` (`org` - toàn hệ thống, `job_posting` - tin tuyển dụng cụ thể, `round` - vòng phỏng vấn cụ thể).

### `must_ask_tracking`
Theo dõi các câu hỏi bắt buộc (must-ask) đã được AI hỏi trong phiên phỏng vấn thật chưa.

---

## Phase 6 – AI Evaluation & HR Review

### `evaluations`
Báo cáo đánh giá của AI sau khi phiên phỏng vấn kết thúc.

| Cột | Ghi chú |
|-----|---------|
| `ai_verdict` | Kết quả đề xuất từ AI: `pass` \| `not_pass` |
| `overall_score` | Điểm đánh giá tổng quan (0–100) |
| `criterion_scores` | Điểm số theo các tiêu chí (JSONB): kỹ thuật, giao tiếp, ngoại ngữ, văn hóa... |
| `cheat_score` | Điểm nghi ngờ gian lận (0–100) tổng hợp từ tín hiệu thu thập |
| `cheat_signals` | Chi tiết các tín hiệu nghi vấn phát hiện được |
| `language_assessment` | Đánh giá năng lực ngoại ngữ (nếu Job có yêu cầu ngôn ngữ) |

### `hr_reviews`
HR Leader phê duyệt (`Confirm`) hoặc thay đổi kết quả AI (`Override`). Bắt buộc nhập `override_reason` khi có thay đổi verdict. Các cờ `share_*` kiểm soát nội dung hiển thị sang phía ứng viên trên portal.

---

## Phase 8 – Cheat Detection

### `cheat_detection_signals`
Nhật ký các tín hiệu gian lận thu thập định kỳ từ thiết bị Kiosk frontend gửi lên trong phiên phỏng vấn: `eye_tracking`, `response_timing`, `speech_pattern` (cadence cadence), `tab_switch`, `focus_loss`.

---

## Phase 10 – System Audit

### `audit_logs`
Nhật ký ghi nhận mọi thao tác quan trọng trên hệ thống để phục vụ quản trị và kiểm toán: đăng nhập, thay đổi cấu hình, tạo mã code, xác nhận/ghi đè đánh giá.

---

## Phase 11 – Integrations

### `webhook_deliveries`
Nhật ký truyền tải dữ liệu tự động sang hệ thống ATS của doanh nghiệp (Workday, SuccessFactors, v.v.) qua webhook khi các sự kiện ứng tuyển/phỏng vấn hoàn tất.

---

## Phase 2c – Online Test (Multiple Choice Quiz)

### `online_test_questions`
Ngân hàng câu hỏi trắc nghiệm riêng biệt cho từng tin tuyển dụng. HR có thể tạo bộ đề thi trắc nghiệm riêng cho từng vị trí.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `job_posting_id` | UUID FK | Liên kết với `job_postings.id` |
| `question_text` | TEXT | Nội dung câu hỏi trắc nghiệm |
| `options` | JSONB | Mảng danh sách các lựa chọn trắc nghiệm (ví dụ: `["A", "B", "C", "D"]`) |
| `correct_option` | INT | Số chỉ mục của đáp án đúng (0, 1, 2, 3) |

### `online_test_submissions`
Lưu kết quả nộp bài thi trắc nghiệm của ứng viên. Hệ thống tự động chấm điểm (`score`) dựa trên việc so khớp đáp án đã chọn với đáp án đúng của bộ đề.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `application_id` | UUID FK | Liên kết hồ sơ ứng tuyển của ứng viên (`applications.id`) |
| `round_number` | INT | Vòng thi thực hiện (ví dụ: 1 hoặc 2) |
| `selected_answers` | JSONB | Bản ghi các đáp án ứng viên đã chọn (ví dụ: `{"q_uuid_1": 2, "q_uuid_2": 0}`) |
| `score` | NUMERIC(5,2) | Điểm số đạt được (0.00 - 100.00) |
| `is_passed` | BOOLEAN | Kết quả đạt hay trượt (Auto-evaluated) |

---

## Changelog cập nhật (Single-tenant & OAuth2 Domain Validation)

| # | Thay đổi | Lý do |
|---|----------|-------|
| 1 | **Xóa bỏ** bảng `organizations` và `subscriptions` | Hệ thống chuyển sang **Single-tenant** (Dành riêng 1 doanh nghiệp sử dụng nội bộ), không cần phân tách dữ liệu đa tổ chức hay quản lý gói cước đa dạng. |
| 2 | **Loại bỏ** cột `organization_id` ở tất cả các bảng | Đảm bảo tính tối giản của Single-tenant, loại bỏ nguy cơ lẫn lộn dữ liệu và giảm tải cấu trúc database. |
| 3 | **Thêm** bảng `system_settings` | Quản trị và lưu trữ các thiết lập toàn hệ thống như danh sách Allowed Domain xác thực OAuth2, Webhook ATS, Slack, Teams toàn cục. |
| 4 | **Loại bỏ** các cột `sso_provider` và `sso_metadata` cũ | Chuyển dịch từ mô hình SAML phức tạp sang OAuth2/OIDC chuẩn hóa và validate domain công ty trực tiếp ở Application layer. |
| 5 | **Thay đổi** mặc định `job_postings.interview_mode` | Mặc định chuyển sang `'onsite'` do toàn bộ quy trình phỏng vấn thật bắt buộc tại công ty. |
| 6 | **Dọn dẹp** toàn bộ indexes liên quan tới `organization_id` | Tối ưu hóa hiệu năng truy vấn, loại bỏ các index partition tenant không còn sử dụng. |
| 7 | **Thêm** hai bảng `online_test_questions` và `online_test_submissions` | Hỗ trợ vòng thi trắc nghiệm trực tuyến (Online Test - Multiple Choice Test) độc lập, sạch sẽ, không ảnh hưởng đến dữ liệu phỏng vấn AI. |
| 8 | **Mở rộng** `job_postings` với metadata Job Board Tier 1 | Bổ sung `location`, `work_mode`, lương, loại hình việc làm, cấp kinh nghiệm, skills, job category, hạn nộp, tuyển gấp — phục vụ Job Board IT. Không thêm `experience_years_*`. Seed mẫu tin tuyển dụng: `backend/ARISP.API/Program.cs`. |

---

## Quy tắc bắt buộc khi viết EF Core migration

1. **Tuyệt đối không sử dụng `organization_id`**. Hệ thống là Single-tenant hoàn chỉnh. Mọi thiết lập dùng chung qua `system_settings` or file cấu hình.
2. Không bao giờ xóa vật lý dữ liệu quan trọng – bắt buộc sử dụng cột `deleted_at` (soft delete) cho các bảng chính.
3. Bật extension `uuid-ossp` và `vector` tại file migration đầu tiên trước khi thiết lập các bảng.
4. Trường `interview_mode` trong `job_postings` mặc định là `'onsite'`. Phân biệt phỏng vấn thử qua cột `session_type = 'practice'` trong `interview_sessions`.
5. Tạo index `idx_webhook_deliveries_next_retry` và `idx_interview_codes_expires_at` với điều kiện lọc (partial index) để đảm bảo tốc độ vận hành cho các tác vụ nền.
6. **Thiết lập hai bảng `online_test_questions` và `online_test_submissions` biệt lập** cho trắc nghiệm thay vì gộp chung vào bảng AI Interview để đảm bảo code gọn gàng, dữ liệu sạch và dễ quản lý dài hạn.
7. **Job Board filters:** Dùng partial index `idx_job_postings_public_filters` và GIN index trên `skills`. Query public listing nên lọc `(application_deadline IS NULL OR application_deadline > NOW())`.

