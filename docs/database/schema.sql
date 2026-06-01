-- =============================================================================
-- ARISP – Full Database Schema (Strictly Single-tenant)
-- PostgreSQL + vector | Supabase hosted
-- Convention: snake_case tables/columns | UUID PKs | soft delete via deleted_at
-- Last updated: 2026-06-01
-- =============================================================================

-- Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";

-- =============================================================================
-- PHASE 1 – GLOBAL SYSTEM CONFIGURATION & AUTH
-- =============================================================================

-- Global configuration table for Single-tenant system settings
CREATE TABLE system_settings (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    key                 VARCHAR(100)    NOT NULL UNIQUE,
    value               TEXT            NOT NULL,
    description         TEXT,
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Seed default global system configurations
INSERT INTO system_settings (key, value, description) VALUES
('allowed_email_domains', 'fsoft.vn', 'Danh sách các domain email được phép xác thực đăng nhập qua OAuth2 (cách nhau bởi dấu phẩy)'),
('oauth_enabled', 'true', 'Cho phép đăng nhập bằng tài khoản OAuth2 / OpenID Connect công ty'),
('ats_webhook_url', '', 'Global ATS Webhook URL endpoint'),
('ats_webhook_secret', '', 'Global ATS Webhook secret token (stored encrypted)'),
('slack_webhook_url', '', 'Global Slack Webhook URL for real-time HR notifications'),
('teams_webhook_url', '', 'Global Teams Webhook URL for real-time HR notifications');

-- HR Admin / Recruiter / SuperAdmin accounts
-- Candidate tự đăng ký qua Job Board → xem bảng candidate_accounts
CREATE TABLE users (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email               VARCHAR(255)    NOT NULL UNIQUE,
    password_hash       TEXT,                                   -- NULL nếu chỉ dùng OAuth2 / OIDC
    role                VARCHAR(50)     NOT NULL,               -- super_admin | hr_admin | recruiter
    full_name           VARCHAR(255),
    department          VARCHAR(255),
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,
    last_login_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ
);

CREATE TABLE refresh_tokens (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id             UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash          TEXT            NOT NULL UNIQUE,
    expires_at          TIMESTAMPTZ     NOT NULL,
    revoked_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Magic link for Candidate Portal (one-time, TTL 15 min)
CREATE TABLE magic_links (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email               VARCHAR(255)    NOT NULL,
    token_hash          TEXT            NOT NULL UNIQUE,
    expires_at          TIMESTAMPTZ     NOT NULL,
    used_at             TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 2b – CANDIDATE ACCOUNTS (Job Board self-registration)
-- Candidate tự đăng ký → tìm kiếm và self-apply qua Job Board
-- Khác với HR users: không thuộc tổ chức cụ thể, auth riêng biệt
-- =============================================================================

CREATE TABLE candidate_accounts (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email               VARCHAR(255)    NOT NULL UNIQUE,
    password_hash       TEXT            NOT NULL,
    full_name           VARCHAR(255)    NOT NULL,
    phone               VARCHAR(50),
    -- Profile / resume info (optional, tăng tốc khi apply)
    headline            VARCHAR(255),                           -- e.g. "Backend Developer 3 yrs exp"
    profile_cv_url      TEXT,                                   -- uploaded default CV
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,
    email_verified      BOOLEAN         NOT NULL DEFAULT FALSE,
    last_login_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ
);

-- Refresh tokens cho candidate_accounts (Job Board login)
CREATE TABLE candidate_refresh_tokens (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    candidate_account_id UUID           NOT NULL REFERENCES candidate_accounts(id) ON DELETE CASCADE,
    token_hash          TEXT            NOT NULL UNIQUE,
    expires_at          TIMESTAMPTZ     NOT NULL,
    revoked_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 2 – JOB POSTING & APPLICATION
-- =============================================================================

CREATE TABLE job_postings (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    created_by_user_id      UUID            NOT NULL REFERENCES users(id),
    title                   VARCHAR(255)    NOT NULL,
    department              VARCHAR(255),
    job_description         TEXT            NOT NULL,           -- JD raw text, fed to AI
    interview_mode          VARCHAR(20)     NOT NULL DEFAULT 'onsite', -- onsite (real interview is strictly onsite)
    status                  VARCHAR(50)     NOT NULL DEFAULT 'draft',  -- draft | active | closed | archived
    -- Job Board: có hiển thị công khai trên Job Board IT không
    is_public_listing       BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Job Board listing metadata (Tier 1)
    location                VARCHAR(255),                           -- e.g. Ho Chi Minh City, Ha Noi
    work_mode               VARCHAR(20),                            -- onsite | remote | hybrid
    salary_min              NUMERIC(12,2),
    salary_max              NUMERIC(12,2),
    salary_currency         VARCHAR(3)      NOT NULL DEFAULT 'VND',
    salary_is_negotiable    BOOLEAN         NOT NULL DEFAULT FALSE,
    employment_type         VARCHAR(30),                            -- full_time | part_time | contract | internship
    experience_level        VARCHAR(30),                            -- fresher | junior | mid | senior | lead | manager
    skills                  TEXT[],                                 -- e.g. {"C#", ".NET", "PostgreSQL"}
    job_category            VARCHAR(100),                           -- backend | frontend | devops | qa | data | ai_ml | mobile | pm | ...
    application_deadline    TIMESTAMPTZ,
    is_urgent               BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Language detection result (set by LanguageDetectionService, confirmed by HR)
    detected_language       VARCHAR(10),                        -- e.g. 'en', 'ja', 'ko', NULL = Vietnamese
    language_requirement    TEXT,                               -- e.g. "TOEIC > 700 hoặc IELTS > 6.5"
    language_confirmed      BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Scheduling config (Practice Remote)
    reschedule_deadline_hours INT           DEFAULT 24,
    invite_token_ttl_hours  INT             NOT NULL DEFAULT 48,
    -- Scoring rubric (JSON array of criteria)
    scoring_rubric          JSONB,
    -- Interview Persona
    persona_name            VARCHAR(100),
    persona_voice_id        TEXT,                               -- ElevenLabs voice ID
    persona_style           TEXT,
    published_at            TIMESTAMPTZ,
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at              TIMESTAMPTZ
);

-- Multi-round config per Job Posting
-- e.g. [{round_number:1, type:"screening"}, {round_number:2, type:"technical"}]
CREATE TABLE interview_round_configs (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_posting_id      UUID            NOT NULL REFERENCES job_postings(id) ON DELETE CASCADE,
    round_number        INT             NOT NULL,
    round_type          VARCHAR(50)     NOT NULL,               -- screening | technical | online_test
    interview_language  VARCHAR(10),                            -- NULL = inherit from job_posting
    interview_code_ttl_hours INT        NOT NULL DEFAULT 2,
    max_duration_minutes INT            NOT NULL DEFAULT 45,
    UNIQUE (job_posting_id, round_number)
);

-- Candidate application (CV + personal info)
CREATE TABLE applications (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_posting_id          UUID            NOT NULL REFERENCES job_postings(id) ON DELETE CASCADE,
    -- Link tới candidate_accounts nếu self-apply qua Job Board; NULL nếu HR-invited
    candidate_account_id    UUID            REFERENCES candidate_accounts(id),
    candidate_email         VARCHAR(255)    NOT NULL,
    candidate_name          VARCHAR(255)    NOT NULL,
    candidate_phone         VARCHAR(50),
    cv_file_url             TEXT,                               -- uploaded CV (PDF)
    cv_text                 TEXT,                               -- parsed text from CV – đưa vào RAG
    -- Nguồn: job_board (self-apply) | invited (HR gửi magic link)
    source                  VARCHAR(50)     NOT NULL DEFAULT 'invited', -- job_board | invited
    status                  VARCHAR(50)     NOT NULL DEFAULT 'invited',
    -- invited | cv_submitted | screening | interview | pass | not_pass | withdrawn
    invite_token_hash       TEXT,                               -- signed token hash
    invite_expires_at       TIMESTAMPTZ,
    -- Practice Interview: chỉ được dùng 1 lần per application (ADR-027)
    practice_session_used   BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Opt-in demographic data (encrypted, bias detection)
    demographic_data        JSONB,
    demographic_consent     BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at              TIMESTAMPTZ
);

-- =============================================================================
-- PHASE 3 – SCHEDULING & INTERVIEW CODE
-- =============================================================================

-- Availability slots HR configures per Job Posting (Remote phỏng vấn thử)
CREATE TABLE availability_slots (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_posting_id      UUID            NOT NULL REFERENCES job_postings(id) ON DELETE CASCADE,
    round_number        INT             NOT NULL DEFAULT 1,
    start_time          TIMESTAMPTZ     NOT NULL,
    end_time            TIMESTAMPTZ     NOT NULL,
    timezone            VARCHAR(100)    NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    capacity            INT             NOT NULL DEFAULT 1,
    booked_count        INT             NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Booking when Candidate selects a slot
CREATE TABLE interview_bookings (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    application_id          UUID            NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
    availability_slot_id    UUID            NOT NULL REFERENCES availability_slots(id),
    round_number            INT             NOT NULL DEFAULT 1,
    interview_link          TEXT,                               -- generated access URL
    status                  VARCHAR(50)     NOT NULL DEFAULT 'scheduled',
    -- scheduled | completed | cancelled | rescheduled
    reminder_24h_sent       BOOLEAN         NOT NULL DEFAULT FALSE,
    reminder_1h_sent        BOOLEAN         NOT NULL DEFAULT FALSE,
    rescheduled_from_id     UUID            REFERENCES interview_bookings(id),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- On-site Interview Code (ADR-016)
-- One-time-use, TTL configurable, bound to application_id
CREATE TABLE interview_codes (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    application_id      UUID            NOT NULL REFERENCES applications(id),
    round_number        INT             NOT NULL DEFAULT 1,
    code                VARCHAR(20)     NOT NULL UNIQUE,        -- e.g. ARX-7K2P
    expires_at          TIMESTAMPTZ     NOT NULL,
    used_at             TIMESTAMPTZ,                            -- NULL = not used yet
    created_by_user_id  UUID            NOT NULL REFERENCES users(id),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 4 – AI INTERVIEW CORE
-- =============================================================================

CREATE TABLE interview_sessions (
    id                              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    application_id                  UUID            NOT NULL REFERENCES applications(id),
    round_number                    INT             NOT NULL DEFAULT 1,
    round_type                      VARCHAR(50)     NOT NULL,
    -- session_type: phân biệt practice vs real (ADR-027)
    -- practice: JD + CV only, không load Playbook, không ảnh hưởng verdict
    -- real: full RAG (JD + CV + Playbook), ảnh hưởng đến quyết định tuyển dụng
    session_type                    VARCHAR(20)     NOT NULL DEFAULT 'real', -- practice | real
    interview_language              VARCHAR(10)     NOT NULL DEFAULT 'vi',
    status                          VARCHAR(50)     NOT NULL DEFAULT 'pending',
    -- pending | active | completed | aborted | error
    started_at                      TIMESTAMPTZ,
    ended_at                        TIMESTAMPTZ,
    duration_seconds                INT,
    recording_url                   TEXT,                       -- video/audio recording
    recording_visible_to_candidate  BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at                      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at                      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE TABLE questions (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id          UUID            NOT NULL REFERENCES interview_sessions(id) ON DELETE CASCADE,
    sequence_number     INT             NOT NULL,
    question_text       TEXT            NOT NULL,
    question_type       VARCHAR(50),                            -- behavioral | technical | language_probe | scenario
    difficulty_level    INT             NOT NULL DEFAULT 3,     -- 1-5, adaptive difficulty
    source              VARCHAR(50)     NOT NULL DEFAULT 'ai_generated',
    -- ai_generated | playbook_must_ask | playbook_suggested
    playbook_chunk_id   UUID,                                   -- reference if sourced from playbook
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE TABLE answers (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    question_id         UUID            NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
    session_id          UUID            NOT NULL REFERENCES interview_sessions(id) ON DELETE CASCADE,
    transcript          TEXT,                                   -- STT output
    audio_url           TEXT,
    response_time_ms    INT,                                    -- ms từ question → answer start (Cheat Detection)
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Document chunks (JD, CV, Playbook) stored in pgvector (ADR-004)
-- source_type = 'jd'      → source_id = job_posting_id
-- source_type = 'cv'      → source_id = application_id
-- source_type = 'playbook' → source_id = playbook_document_id
CREATE TABLE document_chunks (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_type         VARCHAR(50)     NOT NULL,               -- jd | cv | playbook
    source_id           UUID            NOT NULL,
    chunk_index         INT             NOT NULL,
    chunk_text          TEXT            NOT NULL,
    embedding           VECTOR(1536),                           -- text-embedding-3-small (ADR-004)
    -- Metadata cho weighted RAG retrieval (ADR-025)
    metadata            JSONB,
    -- e.g. { "scope": "org|job_posting|round", "document_type": "must_ask|question_bank|..." }
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- IVFFlat index cho cosine similarity search
CREATE INDEX ON document_chunks USING ivfflat (embedding vector_cosine_ops);

-- =============================================================================
-- PHASE 4b – INTERVIEW PLAYBOOK (ORG KNOWLEDGE BASE) (ADR-025)
-- =============================================================================

CREATE TABLE playbook_documents (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    scope               VARCHAR(50)     NOT NULL,               -- org | job_posting | round
    scope_ref_id        UUID,
    -- NULL nếu scope = 'org'
    -- job_posting_id nếu scope = 'job_posting' hoặc 'round'
    round_number        INT,                                    -- chỉ set khi scope = 'round'
    document_type       VARCHAR(50)     NOT NULL,
    -- org scope:          interview_style_guide | competency_framework | culture_values
    --                     compliance_guide | red_flag_guide | past_transcripts
    -- job_posting scope:  question_bank | technical_scenarios | expected_answers | must_ask | past_transcripts
    -- round scope:        round_playbook
    file_name           VARCHAR(255)    NOT NULL,
    file_url            TEXT            NOT NULL,
    file_format         VARCHAR(20)     NOT NULL,               -- pdf | docx | txt | md | json
    parsed_text         TEXT,
    status              VARCHAR(50)     NOT NULL DEFAULT 'processing', -- processing | ready | error
    error_message       TEXT,                                   -- lý do lỗi nếu status = 'error'
    uploaded_by_user_id UUID            NOT NULL REFERENCES users(id),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ
);

-- Must-ask question tracking per session (ADR-025)
-- InterviewService check trước khi trigger end-of-session
CREATE TABLE must_ask_tracking (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id              UUID            NOT NULL REFERENCES interview_sessions(id) ON DELETE CASCADE,
    playbook_document_id    UUID            NOT NULL REFERENCES playbook_documents(id),
    question_text           TEXT            NOT NULL,
    asked_at                TIMESTAMPTZ,                        -- NULL = chưa hỏi
    question_id             UUID            REFERENCES questions(id)
);

-- =============================================================================
-- PHASE 5 – MULTI-ROUND & AUTO-PROGRESSION
-- Auto-progression logic lives in InterviewService / HRReviewService
-- Multi-round được capture qua interview_sessions (round_number) + evaluations
-- Không cần bảng riêng – flow quản lý qua application.status + session per round
-- =============================================================================

-- =============================================================================
-- PHASE 6 – AI EVALUATION & HR REVIEW
-- =============================================================================

CREATE TABLE evaluations (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id              UUID            NOT NULL UNIQUE REFERENCES interview_sessions(id),
    application_id          UUID            NOT NULL REFERENCES applications(id),
    round_number            INT             NOT NULL,
    -- Phân biệt practice vs real (practice evaluation không ảnh hưởng verdict tuyển dụng)
    session_type            VARCHAR(20)     NOT NULL DEFAULT 'real', -- practice | real
    ai_verdict              VARCHAR(20)     NOT NULL,           -- pass | not_pass
    overall_score           NUMERIC(5,2),                       -- 0–100
    -- Per-criterion scores (JSON): {technical, communication, culture_fit, language_proficiency, ...}
    criterion_scores        JSONB,
    reasoning               TEXT,                               -- AI narrative reasoning
    recommended_next_step   TEXT,
    -- Per-question analysis (JSON array)
    question_analyses       JSONB,
    -- Cheat detection summary (ADR-019)
    cheat_score             NUMERIC(5,2),                       -- 0–100
    cheat_signals           JSONB,                              -- [{type, description, severity}]
    -- Language assessment (Round 1 với language requirement – ADR-018)
    language_assessment     JSONB,
    -- {fluency, grammar, vocabulary, comprehension, overall_score}
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE TABLE hr_reviews (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    evaluation_id           UUID            NOT NULL REFERENCES evaluations(id),
    reviewed_by_user_id     UUID            NOT NULL REFERENCES users(id),
    final_verdict           VARCHAR(20)     NOT NULL,           -- pass | not_pass
    is_override             BOOLEAN         NOT NULL DEFAULT FALSE,
    override_reason         TEXT,                               -- bắt buộc khi is_override = true (ADR-014)
    -- Config share với Candidate Portal (ADR-021)
    share_recording         BOOLEAN         NOT NULL DEFAULT FALSE,
    share_transcript        BOOLEAN         NOT NULL DEFAULT FALSE,
    share_evaluation        BOOLEAN         NOT NULL DEFAULT FALSE,
    share_feedback          BOOLEAN         NOT NULL DEFAULT FALSE,
    candidate_feedback      TEXT,                               -- optional custom feedback text
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 8 – CHEAT DETECTION SIGNALS (ADR-019)
-- =============================================================================

CREATE TABLE cheat_detection_signals (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id          UUID            NOT NULL REFERENCES interview_sessions(id) ON DELETE CASCADE,
    signal_type         VARCHAR(50)     NOT NULL,
    -- eye_tracking | response_timing | speech_pattern | tab_switch | focus_loss
    payload             JSONB           NOT NULL,               -- raw signal data từ frontend
    recorded_at         TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 10 – SYSTEM AUDIT
-- =============================================================================

-- Audit log (mọi hành động quan trọng – ADR-014, ADR-016)
CREATE TABLE audit_logs (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    actor_user_id       UUID            REFERENCES users(id),
    action              VARCHAR(100)    NOT NULL,
    -- hr_confirm | hr_override | interview_code_generated | interview_code_used
    -- job_posting_created | job_posting_published | user_invited | oauth_login
    -- ats_webhook_sent | playbook_uploaded | system_setting_changed | ...
    entity_type         VARCHAR(50),
    entity_id           UUID,
    metadata            JSONB,                                  -- action-specific context
    ip_address          INET,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 11 – INTEGRATIONS
-- =============================================================================

-- ATS Webhook delivery log (ADR-022)
CREATE TABLE webhook_deliveries (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_type          VARCHAR(100)    NOT NULL,
    -- application.submitted | interview.completed | evaluation.confirmed
    payload             JSONB           NOT NULL,
    response_status     INT,
    response_body       TEXT,
    attempt_count       INT             NOT NULL DEFAULT 1,
    delivered_at        TIMESTAMPTZ,
    next_retry_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PHASE 2c – ONLINE TEST (MULTIPLE CHOICE QUIZ)
-- =============================================================================

-- Ngân hàng câu hỏi trắc nghiệm của tin tuyển dụng
CREATE TABLE online_test_questions (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_posting_id      UUID            NOT NULL REFERENCES job_postings(id) ON DELETE CASCADE,
    question_text       TEXT            NOT NULL,               -- Nội dung câu hỏi trắc nghiệm
    options             JSONB           NOT NULL,               -- Danh sách đáp án: ["A", "B", "C", "D"]
    correct_option      INT             NOT NULL,               -- Vị trí đáp án đúng (0, 1, 2, 3)
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- Kết quả nộp bài trắc nghiệm của ứng viên
CREATE TABLE online_test_submissions (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    application_id      UUID            NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
    round_number        INT             NOT NULL DEFAULT 1,
    selected_answers    JSONB           NOT NULL,               -- Đáp án ứng viên chọn: {"q_uuid_1": 2, "q_uuid_2": 0}
    score               NUMERIC(5,2)    NOT NULL,               -- Điểm số tự động chấm (ví dụ: 80.00)
    is_passed           BOOLEAN         NOT NULL DEFAULT FALSE, -- Kết quả tự động đạt/không đạt dựa trên điểm sàn
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- INDEXES
-- =============================================================================

-- Global Query Optimization Indexes
CREATE INDEX idx_applications_job_posting_id        ON applications(job_posting_id);
CREATE INDEX idx_applications_candidate_account_id  ON applications(candidate_account_id);
CREATE INDEX idx_interview_sessions_application_id  ON interview_sessions(application_id);
CREATE INDEX idx_interview_sessions_session_type    ON interview_sessions(session_type);
CREATE INDEX idx_evaluations_application_id         ON evaluations(application_id);
CREATE INDEX idx_evaluations_session_type           ON evaluations(session_type);
CREATE INDEX idx_audit_logs_actor_user_id           ON audit_logs(actor_user_id);
CREATE INDEX idx_document_chunks_source             ON document_chunks(source_type, source_id);

-- Interview Code lookups (TTL validation, one-time-use check – ADR-016)
CREATE INDEX idx_interview_codes_code               ON interview_codes(code);
CREATE INDEX idx_interview_codes_application_id     ON interview_codes(application_id);
CREATE INDEX idx_interview_codes_expires_at         ON interview_codes(expires_at) WHERE used_at IS NULL;

-- Playbook Scope queries (no tenant partition)
CREATE INDEX idx_playbook_documents_scope           ON playbook_documents(scope, scope_ref_id);

-- Scheduling Slots
CREATE INDEX idx_availability_slots_job_posting     ON availability_slots(job_posting_id);
CREATE INDEX idx_interview_bookings_application_id  ON interview_bookings(application_id);

-- Cheat detection
CREATE INDEX idx_cheat_signals_session              ON cheat_detection_signals(session_id);

-- Webhook
CREATE INDEX idx_webhook_deliveries_next_retry      ON webhook_deliveries(next_retry_at) WHERE delivered_at IS NULL;

-- Job Board
CREATE INDEX idx_job_postings_public_active         ON job_postings(is_public_listing, status) WHERE deleted_at IS NULL;
CREATE INDEX idx_job_postings_public_filters        ON job_postings(is_public_listing, status, work_mode, experience_level, job_category)
    WHERE deleted_at IS NULL AND is_public_listing = TRUE;
CREATE INDEX idx_job_postings_skills                ON job_postings USING GIN (skills);
CREATE INDEX idx_job_postings_salary                ON job_postings(salary_min, salary_max)
    WHERE deleted_at IS NULL AND salary_is_negotiable = FALSE;
CREATE INDEX idx_candidate_accounts_email           ON candidate_accounts(email);

-- Soft-delete indexes for active records
CREATE INDEX idx_job_postings_active_deleted        ON job_postings(deleted_at);
CREATE INDEX idx_applications_active_deleted        ON applications(deleted_at);
CREATE INDEX idx_users_active_deleted               ON users(deleted_at);
CREATE INDEX idx_playbook_docs_active_deleted       ON playbook_documents(deleted_at);
CREATE INDEX idx_candidate_accounts_active          ON candidate_accounts(email) WHERE deleted_at IS NULL;

-- Online Test
CREATE INDEX idx_online_test_questions_job          ON online_test_questions(job_posting_id);
CREATE INDEX idx_online_test_submissions_app        ON online_test_submissions(application_id);

