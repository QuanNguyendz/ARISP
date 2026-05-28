# ARISP – Database Schema Documentation

> **Source file:** `docs/database/schema.sql`
> **DB:** PostgreSQL (Supabase hosted) + pgvector extension
> **ORM:** EF Core – mọi thay đổi schema đi qua migration, không sửa DB trực tiếp
> **Convention:** `snake_case` tables/columns · UUID PKs · `created_at` / `updated_at` trên mọi bảng chính · soft delete qua `deleted_at`
> **Last updated:** 2026-05-23

---

## Sơ đồ quan hệ tổng quan

```
users (role: system_admin | hr_admin | recruiter)
job_postings
  ├── interview_round_configs
  ├── availability_slots
  └── playbook_documents (scope: org | job_posting | round)
applications
  ├── [candidate_accounts] ← nếu source = 'job_board'
  ├── interview_codes (On-site)
  ├── interview_bookings (Practice Remote)
  └── interview_sessions (mỗi round = 1 session)
  │           ├── questions
  │           │     └── answers
  │           ├── must_ask_tracking
  │           ├── cheat_detection_signals
  │           └── evaluations
  │                 └── hr_reviews
  ├── document_chunks (pgvector – JD / CV / Playbook embeddings)
  ├── audit_logs
  └── webhook_deliveries

candidate_accounts  ← Candidate tự đăng ký qua Job Board (độc lập với organizations)
  └── candidate_refresh_tokens
```

---

## Phase 1 – Auth & Single-tenant Setup

### `users`
HR Admin, Recruiter, SuperAdmin. **Không** bao gồm Candidate tự đăng ký – xem `candidate_accounts`.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `email` | VARCHAR UNIQUE | |
| `password_hash` | TEXT | NULL nếu SSO-only |
| `role` | VARCHAR(50) | `super_admin` \| `hr_admin` \| `recruiter` |
| `department` | VARCHAR | phân quyền per department |

### `refresh_tokens`
JWT refresh token store cho users. Hash token trước khi lưu.

### `magic_links`
One-time link cho Candidate Portal. TTL 15 phút.

---

## Phase 2b – Candidate Accounts (Job Board)

### `candidate_accounts`
Candidate tự đăng ký tài khoản trên ARISP để dùng Job Board. **Tách biệt** với bảng `users` (HR/Admin).

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | |
| `email` | VARCHAR UNIQUE | |
| `password_hash` | TEXT | |
| `full_name` | VARCHAR | |
| `phone` | VARCHAR | optional |
| `headline` | VARCHAR(255) | e.g. "Backend Developer 3 yrs exp" |
| `profile_cv_url` | TEXT | CV mặc định, tăng tốc khi apply |
| `is_active` | BOOLEAN | |
| `email_verified` | BOOLEAN | |

> **Lý do tách biệt:** Candidate Job Board có vòng đời và auth flow khác (password login, email verify) so với HR users (có thể SSO, thuộc Organization). Candidate được invite trực tiếp từ HR không cần account.

### `candidate_refresh_tokens`
Refresh tokens riêng cho candidate_accounts.

---

## Phase 2 – Job Posting & Application

### `job_postings`
Tin tuyển dụng. Core entity driving toàn bộ flow.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `rejection_reason` | TEXT | **[MỚI]** Lý do khi sếp Reject bản draft |
| `status` | VARCHAR | `draft` \| `active` \| `closed` \| `archived` \| `rejected` |
| `is_public_listing` | BOOLEAN | **[MỚI]** có hiển thị trên Job Board IT không |
| `detected_language` | VARCHAR(10) | AI detect từ JD: `en`, `ja`, ... NULL = Vietnamese |
| `language_requirement` | TEXT | "TOEIC > 700..." |
| `language_confirmed` | BOOLEAN | HR confirm trước khi publish |
| `scoring_rubric` | JSONB | custom criteria array |
| `persona_name` | VARCHAR | tên AI interviewer |
| `persona_voice_id` | TEXT | ElevenLabs voice ID |

### `interview_round_configs`
Config từng vòng phỏng vấn per Job Posting. UNIQUE(job_posting_id, round_number).

| Cột | Ghi chú |
|-----|---------|
| `round_type` | `screening` \| `technical` \| `hr` \| `culture_fit` |
| `interview_language` | override nếu khác language của Job Posting |
| `interview_code_ttl_hours` | On-site code TTL, default 2h |
| `max_duration_minutes` | giới hạn thời gian session |

### `applications`
Hồ sơ Candidate ứng tuyển.

| Cột | Ghi chú |
|-----|---------|
| `candidate_account_id` | **[MỚI]** FK → `candidate_accounts`; NULL nếu HR-invited |
| `source` | **[MỚI]** `job_board` (self-apply) \| `invited` (HR gửi link) |
| `status` | `invited` → `cv_submitted` → `screening` → `interview` → `pass` / `not_pass` / `withdrawn` |
| `rejection_reason_internal` | **[MỚI]** Lý do HR đánh trượt ứng viên |
| `rejection_email_sent_at` | **[MỚI]** Timestamp khi email chia buồn được gửi |
| `cv_text` | parsed text từ PDF – đưa vào RAG |
| `invite_token_hash` | hash của signed JWT invite |
| `practice_session_used` | **[MỚI]** flag 1 lần per application (ADR-027) |
| `demographic_data` | JSONB encrypted, opt-in cho Bias Detection |

---

## Phase 3 – Scheduling & Interview Code

### `availability_slots`
HR cấu hình khung giờ cho Phỏng vấn thử (Practice Remote). `booked_count` tăng khi Candidate chọn.

### `interview_bookings`
Candidate chọn slot cho Phỏng vấn thử → tạo booking. Hỗ trợ reschedule (`rescheduled_from_id` tự refer).

### `interview_codes`
On-site access control. **One-time-use** – `used_at` set ngay sau khi dùng.

| Cột | Ghi chú |
|-----|---------|
| `code` | `ARX-7K2P` format, UNIQUE |
| `expires_at` | default +2h từ lúc tạo |
| `used_at` | NULL = chưa dùng |

> **Index:** `idx_interview_codes_expires_at` (partial – WHERE used_at IS NULL) để validate TTL nhanh.

---

## Phase 4 – AI Interview Core

### `interview_sessions`
Mỗi round = 1 session. Multi-round = nhiều sessions cho 1 application.

| Cột | Ghi chú |
|-----|---------|
| `session_type` | **[MỚI]** `practice` \| `real` (ADR-027) |
| `status` | `pending` \| `active` \| `completed` \| `aborted` \| `error` |
| `interview_language` | ngôn ngữ phỏng vấn thực tế |
| `recording_url` | video/audio lưu trữ |
| `recording_visible_to_candidate` | HR config khi review |

> **`session_type` logic:**
> - `practice`: chỉ retrieve JD + CV chunks, không load Playbook, không ảnh hưởng verdict tuyển dụng.
> - `real`: full RAG (JD + CV + Playbook), mở đúng slot đã đặt, kết quả ảnh hưởng quyết định.

### `questions`
AI-generated hoặc từ Playbook.

| Cột | Ghi chú |
|-----|---------|
| `source` | `ai_generated` \| `playbook_must_ask` \| `playbook_suggested` |
| `difficulty_level` | 1–5, adaptive difficulty |

### `answers`
STT transcript của Candidate. `response_time_ms` dùng cho Cheat Detection.

### `document_chunks`
**pgvector store** cho JD + CV + Playbook. `embedding VECTOR(1536)` – text-embedding-3-small.

| Cột | Ghi chú |
|-----|---------|
| `source_type` | `jd` \| `cv` \| `playbook` |
| `source_id` | job_posting_id / application_id / playbook_document_id |
| `embedding` | VECTOR(1536) – indexed bằng IVFFlat |
| `metadata` | JSONB chứa scope/document_type cho weighted RAG |

---

## Phase 4b – Interview Playbook

### `playbook_documents`
Tài liệu nội bộ doanh nghiệp. Sau upload → parse → chunk → embed vào `document_chunks`.

| `document_type` | Scope |
|-----------------|-------|
| `interview_style_guide` | org |
| `competency_framework` | org |
| `culture_values` | org |
| `compliance_guide` | org |
| `red_flag_guide` | org |
| `past_transcripts` | org / job_posting |
| `question_bank` | job_posting |
| `technical_scenarios` | job_posting |
| `expected_answers` | job_posting |
| `must_ask` | job_posting |
| `round_playbook` | round |

> **[MỚI]** Thêm cột `error_message` để lưu lý do khi `status = 'error'`.

### `must_ask_tracking`
Track câu hỏi bắt buộc đã hỏi chưa trong session. `InterviewService` check trước khi trigger end.

---

## Phase 6 – AI Evaluation & HR Review

### `evaluations`

| Cột | Ghi chú |
|-----|---------|
| `session_type` | **[MỚI]** `practice` \| `real` – phân biệt practice evaluation |
| `ai_verdict` | `pass` \| `not_pass` |
| `overall_score` | 0–100 |
| `criterion_scores` | JSONB: `{technical, communication, culture_fit, language_proficiency, ...}` |
| `question_analyses` | JSONB array: per-question breakdown |
| `cheat_score` | 0–100 |
| `cheat_signals` | JSONB: `[{type, description, severity}]` |
| `language_assessment` | JSONB: `{fluency, grammar, vocabulary, comprehension, overall_score}` |
| `updated_at` | **[MỚI]** theo convention |

### `hr_reviews`
HR confirm hoặc override. `override_reason` bắt buộc khi `is_override = true`.
Các cột `share_*` control những gì Candidate thấy trên Candidate Portal.

| Cột | Ghi chú |
|-----|---------|
| `updated_at` | **[MỚI]** theo convention |

---

## Phase 8 – Cheat Detection

### `cheat_detection_signals`
Raw signals gửi từ frontend trong session:

| `signal_type` | Mô tả |
|---------------|-------|
| `eye_tracking` | gaze estimation data |
| `response_timing` | ms từ question → answer start |
| `speech_pattern` | reading cadence detection |
| `tab_switch` | tab switching event |
| `focus_loss` | browser focus lost |

---

## Phase 10 – System Admin

### `audit_logs`
Ghi mọi hành động quan trọng: `hr_confirm`, `hr_override`, `interview_code_generated`, `sso_login`, v.v.

---

## Phase 11 – Integrations

### `webhook_deliveries`
ATS webhook delivery log với retry tracking. Events: `application.submitted`, `interview.completed`, `evaluation.confirmed`.

---

## Changelog (so với phiên bản trước)

| # | Thay đổi | Lý do |
|---|----------|-------|
| 1 | Thêm `candidate_accounts` + `candidate_refresh_tokens` | ADR-026: Job Board self-registration |
| 2 | Thêm `is_public_listing` vào `job_postings` | tasks.md Phase 2b |
| 3 | Thêm `candidate_account_id` + `source` vào `applications` | Phân biệt job_board vs invited flow |
| 4 | Thêm `practice_session_used` vào `applications` | ADR-027: 1 lần per application |
| 5 | Thêm `session_type` vào `interview_sessions` | ADR-027: practice vs real |
| 6 | Thêm `session_type` vào `evaluations` | Nhất quán với interview_sessions |
| 7 | Thêm `updated_at` vào `evaluations` + `hr_reviews` | Convention bắt buộc |
| 8 | Thêm `error_message` vào `playbook_documents` | UX: hiển thị lý do lỗi parse |
| 9 | Thêm `job_postings_active/limit` vào `subscriptions` | ADR-028: usage tracking |
| 10 | Thêm index `expires_at` (partial) cho `interview_codes` | ADR-016: TTL validation performance |
| 11 | Thêm index `next_retry_at` (partial) cho `webhook_deliveries` | ADR-022: retry logic |
| 12 | Thêm các indexes mới cho `candidate_accounts`, `interview_sessions` session_type, `applications` candidate_account_id | Query performance |
| 13 | Đổi role `candidate` ra khỏi `users.role` enum | Candidate không còn trong `users` table |

---

## Quy tắc bắt buộc khi viết EF Core migration

1. KHÔNG sử dụng `organization_id`. Hệ thống là Single-tenant nội bộ.
2. Không xóa cứng dữ liệu quan trọng – dùng `deleted_at` (soft delete).
3. Tên migration mô tả hành động: `AddInterviewSessionTable`, `AddCheatScoreToEvaluations`.
4. Bật `pgvector` extension trước khi tạo `document_chunks` table.
5. IVFFlat index trên `document_chunks.embedding` cần chạy riêng sau khi có đủ data (hoặc dùng HNSW cho dataset nhỏ).
6. `session_type` phải được enforce ở Application layer (không chỉ DB) để ngăn Candidate dùng practice session > 1 lần.
7. `practice_session_used` flag phải được set **atomic** cùng lúc tạo practice session để tránh race condition.
