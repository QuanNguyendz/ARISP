# ARISP – Database Schema Documentation

> **Source file:** `docs/database/schema.sql`
> **DB:** PostgreSQL (Supabase hosted) + pgvector extension
> **ORM:** EF Core – mọi thay đổi schema đi qua migration, không sửa DB trực tiếp
> **Convention:** `snake_case` tables/columns · UUID PKs · `created_at` / `updated_at` trên mọi bảng chính · soft delete qua `deleted_at`

---

## Sơ đồ quan hệ tổng quan

```
organizations
  ├── users (role: super_admin | hr_admin | recruiter | candidate)
  ├── subscriptions
  ├── job_postings
  │     ├── interview_round_configs (multi-round config)
  │     ├── availability_slots (Remote scheduling)
  │     └── playbook_documents (scope: org | job_posting | round)
  ├── applications (Candidate hồ sơ)
  │     ├── interview_codes (On-site access)
  │     ├── interview_bookings (Remote scheduling)
  │     └── interview_sessions (mỗi round = 1 session)
  │           ├── questions
  │           │     └── answers
  │           ├── must_ask_tracking
  │           ├── cheat_detection_signals
  │           └── evaluations
  │                 └── hr_reviews
  ├── document_chunks (pgvector – JD / CV / Playbook embeddings)
  ├── audit_logs
  └── webhook_deliveries
```

---

## Phase 1 – Auth & Multi-tenant

### `organizations`
Tenant gốc. Mỗi doanh nghiệp = 1 row.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `id` | UUID PK | |
| `name` | VARCHAR(255) | |
| `slug` | VARCHAR(100) UNIQUE | URL-safe identifier |
| `plan` | VARCHAR(50) | `basic` \| `professional` \| `enterprise` |
| `is_active` | BOOLEAN | |
| `sso_provider` | VARCHAR(50) | `saml` \| `google` \| `microsoft` \| NULL |
| `sso_metadata` | TEXT | encrypted IdP config |
| `ats_webhook_url` | TEXT | |
| `ats_webhook_secret` | TEXT | encrypted |
| `slack_webhook_url` | TEXT | |
| `teams_webhook_url` | TEXT | |

### `users`
HR Admin, Recruiter, SuperAdmin. Candidate không có account thường – dùng magic link.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `organization_id` | UUID FK | NULL chỉ cho `super_admin` |
| `email` | VARCHAR UNIQUE | |
| `password_hash` | TEXT | NULL nếu SSO-only |
| `role` | VARCHAR(50) | `super_admin` \| `hr_admin` \| `recruiter` \| `candidate` |
| `department` | VARCHAR | phân quyền per department |

### `refresh_tokens`
JWT refresh token store. Hash token trước khi lưu.

### `magic_links`
One-time link cho Candidate Portal. TTL 15 phút.

---

## Phase 2 – Job Posting & Application

### `job_postings`
Tin tuyển dụng. Core entity driving toàn bộ flow.

| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| `organization_id` | UUID FK | **multi-tenant isolation** |
| `job_description` | TEXT | fed vào AI để sinh câu hỏi |
| `interview_mode` | VARCHAR | `remote` \| `onsite` \| `both` |
| `status` | VARCHAR | `draft` \| `active` \| `closed` \| `archived` |
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
| `status` | `invited` → `cv_submitted` → `screening` → `interview` → `pass` / `not_pass` / `withdrawn` |
| `cv_text` | parsed text từ PDF – đưa vào RAG |
| `invite_token_hash` | hash của signed JWT invite |
| `demographic_data` | JSONB encrypted, opt-in cho Bias Detection |

---

## Phase 3 – Scheduling & Interview Code

### `availability_slots`
HR cấu hình khung giờ Remote interview. `booked_count` tăng khi Candidate chọn.

### `interview_bookings`
Candidate chọn slot → tạo booking. Hỗ trợ reschedule (`rescheduled_from_id` tự refer).

### `interview_codes`
On-site access control. **One-time-use** – `used_at` set ngay sau khi dùng.

| Cột | Ghi chú |
|-----|---------|
| `code` | `ARX-7K2P` format, UNIQUE |
| `expires_at` | default +2h từ lúc tạo |
| `used_at` | NULL = chưa dùng |

---

## Phase 4 – AI Interview Core

### `interview_sessions`
Mỗi round = 1 session. Multi-round = nhiều sessions cho 1 application.

| Cột | Ghi chú |
|-----|---------|
| `status` | `pending` \| `active` \| `completed` \| `aborted` \| `error` |
| `interview_language` | ngôn ngữ phỏng vấn thực tế |
| `recording_url` | video/audio lưu trữ |
| `recording_visible_to_candidate` | HR config khi review |

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
| `source_id` | ID của job_posting / application / playbook_document |
| `embedding` | VECTOR(1536) – indexed bằng IVFFlat |

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
| `question_bank` | job_posting |
| `technical_scenarios` | job_posting |
| `expected_answers` | job_posting |
| `must_ask` | job_posting |
| `round_playbook` | round |
| `past_transcripts` | org / job_posting |

### `must_ask_tracking`
Track câu hỏi bắt buộc đã hỏi chưa trong session. `InterviewService` check trước khi trigger end.

---

## Phase 6 – AI Evaluation & HR Review

### `evaluations`

| Cột | Ghi chú |
|-----|---------|
| `ai_verdict` | `pass` \| `not_pass` |
| `overall_score` | 0–100 |
| `criterion_scores` | JSONB: `{technical, communication, culture_fit, ...}` |
| `question_analyses` | JSONB array: per-question breakdown |
| `cheat_score` | 0–100 |
| `cheat_signals` | JSONB: `[{type, description, severity}]` |
| `language_assessment` | JSONB: `{fluency, grammar, vocabulary, comprehension, overall_score}` |

### `hr_reviews`
HR confirm hoặc override. `override_reason` bắt buộc khi `is_override = true`.
Các cột `share_*` control những gì Candidate thấy trên Candidate Portal.

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

## Phase 10 – Enterprise Admin

### `subscriptions`
1-to-1 với organization. Track plan, usage, billing period.

### `audit_logs`
Ghi mọi hành động quan trọng: `hr_confirm`, `hr_override`, `interview_code_generated`, `sso_login`, v.v.

---

## Phase 11 – Integrations

### `webhook_deliveries`
ATS webhook delivery log với retry tracking. Events: `application.submitted`, `interview.completed`, `evaluation.confirmed`.

---

## Quy tắc bắt buộc khi viết EF Core migration

1. Mọi entity có `organization_id` → Repository **phải** filter theo `organization_id` của user hiện tại.
2. Không xóa cứng dữ liệu quan trọng – dùng `deleted_at` (soft delete).
3. Tên migration mô tả hành động: `AddInterviewSessionTable`, `AddCheatScoreToEvaluations`.
4. Bật `pgvector` extension trước khi tạo `document_chunks` table.
5. IVFFlat index trên `document_chunks.embedding` cần chạy riêng sau khi có đủ data (hoặc dùng HNSW cho dataset nhỏ).
