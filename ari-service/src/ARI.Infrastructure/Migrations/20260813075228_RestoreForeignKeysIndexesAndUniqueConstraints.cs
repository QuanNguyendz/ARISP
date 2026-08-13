using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreForeignKeysIndexesAndUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------------------
            // Gỡ LỚP THỦ CÔNG CŨ trước khi EF dựng lại lớp của chính nó.
            //
            // Vì sao cần: lớp khoá ngoại/index/unique này từng được áp TAY bằng SQL lên
            // Supabase (môi trường test) nên DB đó ĐÃ CÓ SẴN các đối tượng trùng tên —
            // `CREATE INDEX idx_webhook_deliveries_next_retry` sẽ nổ 42P07 "already exists".
            // Production dựng lại ở ADR-055 thì không có gì trong số này, nên toàn bộ khối
            // dưới đây là no-op ở đó (mọi lệnh đều `IF EXISTS`).
            //
            // Chỉ gỡ những thứ migration này TỰ DỰNG LẠI ngay sau đó. Cố ý GIỮ
            // `interview_round_configs_job_posting_id_round_number_key`: migration này không
            // tạo lại nó, gỡ đi là Supabase mất luôn ràng buộc 1 vòng/lần cấu hình.
            // ---------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                -- Khoá ngoại cũ (tên mặc định Postgres *_fkey) — EF tạo lại với tên FK_*
                ALTER TABLE answers                  DROP CONSTRAINT IF EXISTS answers_question_id_fkey;
                ALTER TABLE answers                  DROP CONSTRAINT IF EXISTS answers_session_id_fkey;
                ALTER TABLE applications             DROP CONSTRAINT IF EXISTS applications_candidate_account_id_fkey;
                ALTER TABLE applications             DROP CONSTRAINT IF EXISTS applications_job_posting_id_fkey;
                ALTER TABLE audit_logs               DROP CONSTRAINT IF EXISTS audit_logs_actor_user_id_fkey;
                ALTER TABLE availability_slots       DROP CONSTRAINT IF EXISTS availability_slots_job_posting_id_fkey;
                ALTER TABLE candidate_refresh_tokens DROP CONSTRAINT IF EXISTS candidate_refresh_tokens_candidate_account_id_fkey;
                ALTER TABLE cheat_detection_signals  DROP CONSTRAINT IF EXISTS cheat_detection_signals_session_id_fkey;
                ALTER TABLE evaluations              DROP CONSTRAINT IF EXISTS evaluations_application_id_fkey;
                ALTER TABLE evaluations              DROP CONSTRAINT IF EXISTS evaluations_session_id_fkey;
                ALTER TABLE hr_reviews               DROP CONSTRAINT IF EXISTS hr_reviews_evaluation_id_fkey;
                ALTER TABLE hr_reviews               DROP CONSTRAINT IF EXISTS hr_reviews_reviewed_by_user_id_fkey;
                ALTER TABLE interview_bookings       DROP CONSTRAINT IF EXISTS interview_bookings_application_id_fkey;
                ALTER TABLE interview_bookings       DROP CONSTRAINT IF EXISTS interview_bookings_availability_slot_id_fkey;
                ALTER TABLE interview_bookings       DROP CONSTRAINT IF EXISTS interview_bookings_rescheduled_from_id_fkey;
                ALTER TABLE interview_codes          DROP CONSTRAINT IF EXISTS interview_codes_application_id_fkey;
                ALTER TABLE interview_codes          DROP CONSTRAINT IF EXISTS interview_codes_created_by_user_id_fkey;
                ALTER TABLE interview_round_configs  DROP CONSTRAINT IF EXISTS interview_round_configs_job_posting_id_fkey;
                ALTER TABLE interview_sessions       DROP CONSTRAINT IF EXISTS interview_sessions_application_id_fkey;
                ALTER TABLE job_postings             DROP CONSTRAINT IF EXISTS job_postings_created_by_user_id_fkey;
                ALTER TABLE must_ask_tracking        DROP CONSTRAINT IF EXISTS must_ask_tracking_playbook_document_id_fkey;
                ALTER TABLE must_ask_tracking        DROP CONSTRAINT IF EXISTS must_ask_tracking_question_id_fkey;
                ALTER TABLE must_ask_tracking        DROP CONSTRAINT IF EXISTS must_ask_tracking_session_id_fkey;
                ALTER TABLE online_test_questions    DROP CONSTRAINT IF EXISTS online_test_questions_job_posting_id_fkey;
                ALTER TABLE online_test_submissions  DROP CONSTRAINT IF EXISTS online_test_submissions_application_id_fkey;
                ALTER TABLE playbook_documents       DROP CONSTRAINT IF EXISTS playbook_documents_uploaded_by_user_id_fkey;
                ALTER TABLE questions                DROP CONSTRAINT IF EXISTS questions_session_id_fkey;
                ALTER TABLE refresh_tokens           DROP CONSTRAINT IF EXISTS refresh_tokens_user_id_fkey;

                -- Ràng buộc UNIQUE cũ — EF tạo lại thành unique index tên ux_*
                ALTER TABLE users                    DROP CONSTRAINT IF EXISTS users_email_key;
                ALTER TABLE candidate_accounts       DROP CONSTRAINT IF EXISTS candidate_accounts_email_key;
                ALTER TABLE system_settings          DROP CONSTRAINT IF EXISTS system_settings_key_key;
                ALTER TABLE interview_codes          DROP CONSTRAINT IF EXISTS interview_codes_code_key;
                ALTER TABLE refresh_tokens           DROP CONSTRAINT IF EXISTS refresh_tokens_token_hash_key;
                ALTER TABLE candidate_refresh_tokens DROP CONSTRAINT IF EXISTS candidate_refresh_tokens_token_hash_key;
                ALTER TABLE magic_links              DROP CONSTRAINT IF EXISTS magic_links_token_hash_key;
                ALTER TABLE evaluations              DROP CONSTRAINT IF EXISTS evaluations_session_id_key;

                -- Index đặt tay: 14 cái EF tạo lại NGUYÊN TÊN, 14 cái còn lại là bản trùng
                -- của index EF đã quản lý (ix_*) hoặc của chính unique/FK phía trên.
                DROP INDEX IF EXISTS idx_applications_active_deleted;
                DROP INDEX IF EXISTS idx_applications_candidate_account_id;
                DROP INDEX IF EXISTS idx_applications_job_posting_id;
                DROP INDEX IF EXISTS idx_audit_logs_actor_user_id;
                DROP INDEX IF EXISTS idx_availability_slots_job_posting;
                DROP INDEX IF EXISTS idx_candidate_accounts_active;
                DROP INDEX IF EXISTS idx_candidate_accounts_email;
                DROP INDEX IF EXISTS idx_cheat_signals_session;
                DROP INDEX IF EXISTS idx_document_chunks_source;
                DROP INDEX IF EXISTS idx_evaluations_application_id;
                DROP INDEX IF EXISTS idx_evaluations_session_type;
                DROP INDEX IF EXISTS idx_interview_bookings_application_id;
                DROP INDEX IF EXISTS idx_interview_codes_application_id;
                DROP INDEX IF EXISTS idx_interview_codes_code;
                DROP INDEX IF EXISTS idx_interview_codes_expires_at;
                DROP INDEX IF EXISTS idx_interview_sessions_application_id;
                DROP INDEX IF EXISTS idx_interview_sessions_session_type;
                DROP INDEX IF EXISTS idx_job_postings_active_deleted;
                DROP INDEX IF EXISTS idx_job_postings_public_active;
                DROP INDEX IF EXISTS idx_job_postings_public_filters;
                DROP INDEX IF EXISTS idx_job_postings_salary;
                DROP INDEX IF EXISTS idx_job_postings_skills;
                DROP INDEX IF EXISTS idx_online_test_questions_job;
                DROP INDEX IF EXISTS idx_online_test_submissions_app;
                DROP INDEX IF EXISTS idx_playbook_docs_active_deleted;
                DROP INDEX IF EXISTS idx_playbook_documents_scope;
                DROP INDEX IF EXISTS idx_users_active_deleted;
                DROP INDEX IF EXISTS idx_webhook_deliveries_next_retry;

                -- Index vector cũ: ivfflat → thay bằng HNSW ở phần dưới của migration này.
                DROP INDEX IF EXISTS document_chunks_embedding_idx;

                -- ix_evaluations_session_id: DropIndex ngay sau đây sẽ nổ nếu DB chưa có nó
                -- (Supabase từng thay bằng constraint evaluations_session_id_key). Tạo tạm để
                -- lệnh DropIndex do EF sinh ra luôn có thứ để xoá.
                CREATE INDEX IF NOT EXISTS ix_evaluations_session_id ON evaluations (session_id);
            ");

            migrationBuilder.DropIndex(
                name: "ix_evaluations_session_id",
                table: "evaluations");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_deliveries_next_retry",
                table: "webhook_deliveries",
                column: "next_retry_at",
                filter: "delivered_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_users_active_deleted",
                table: "users",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_system_settings_key",
                table: "system_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_jobs_job_posting_id",
                table: "saved_jobs",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_playbook_docs_active_deleted",
                table: "playbook_documents",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "idx_playbook_documents_scope",
                table: "playbook_documents",
                columns: new[] { "scope", "scope_ref_id" });

            migrationBuilder.CreateIndex(
                name: "IX_playbook_documents_uploaded_by_user_id",
                table: "playbook_documents",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_online_test_questions_job_posting_id",
                table: "online_test_questions",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_must_ask_tracking_playbook_document_id",
                table: "must_ask_tracking",
                column: "playbook_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_must_ask_tracking_question_id",
                table: "must_ask_tracking",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_must_ask_tracking_session_id",
                table: "must_ask_tracking",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ux_magic_links_token_hash",
                table: "magic_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_job_postings_active_deleted",
                table: "job_postings",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "idx_job_postings_public_active",
                table: "job_postings",
                columns: new[] { "is_public_listing", "status" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_job_postings_public_filters",
                table: "job_postings",
                columns: new[] { "is_public_listing", "status", "work_mode", "experience_level", "job_category" },
                filter: "deleted_at IS NULL AND is_public_listing = true");

            migrationBuilder.CreateIndex(
                name: "idx_job_postings_salary",
                table: "job_postings",
                columns: new[] { "salary_min", "salary_max" },
                filter: "deleted_at IS NULL AND salary_is_negotiable = false");

            migrationBuilder.CreateIndex(
                name: "idx_job_postings_skills",
                table: "job_postings",
                column: "skills")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_approved_by_user_id",
                table: "job_postings",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_interview_sessions_session_type",
                table: "interview_sessions",
                column: "session_type");

            migrationBuilder.CreateIndex(
                name: "idx_interview_codes_expires_at",
                table: "interview_codes",
                column: "expires_at",
                filter: "used_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_interview_codes_created_by_user_id",
                table: "interview_codes",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_interview_codes_code",
                table: "interview_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interview_bookings_availability_slot_id",
                table: "interview_bookings",
                column: "availability_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_interview_bookings_rescheduled_from_id",
                table: "interview_bookings",
                column: "rescheduled_from_id");

            migrationBuilder.CreateIndex(
                name: "IX_hr_reviews_reviewed_by_user_id",
                table: "hr_reviews",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_evaluations_session_type",
                table: "evaluations",
                column: "session_type");

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_session_id",
                table: "evaluations",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_document_chunks_embedding_hnsw",
                table: "document_chunks",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_document_chunks_source",
                table: "document_chunks",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cheat_detection_signals_session_id",
                table: "cheat_detection_signals",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_refresh_tokens_candidate_account_id",
                table: "candidate_refresh_tokens",
                column: "candidate_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_refresh_tokens_token_hash",
                table: "candidate_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_accounts_email",
                table: "candidate_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_availability_slots_job_posting_id",
                table: "availability_slots",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_applications_active_deleted",
                table: "applications",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_answers_question_id",
                table: "answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_requests_created_user_id",
                table: "account_requests",
                column: "created_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_requests_requested_by_user_id",
                table: "account_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_requests_reviewed_by_user_id",
                table: "account_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_requests_users_created_user_id",
                table: "account_requests",
                column: "created_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_requests_users_requested_by_user_id",
                table: "account_requests",
                column: "requested_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_requests_users_reviewed_by_user_id",
                table: "account_requests",
                column: "reviewed_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_answers_interview_sessions_session_id",
                table: "answers",
                column: "session_id",
                principalTable: "interview_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_answers_questions_question_id",
                table: "answers",
                column: "question_id",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_applications_candidate_accounts_candidate_account_id",
                table: "applications",
                column: "candidate_account_id",
                principalTable: "candidate_accounts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_applications_job_postings_job_posting_id",
                table: "applications",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_availability_slots_job_postings_job_posting_id",
                table: "availability_slots",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_refresh_tokens_candidate_accounts_candidate_accou~",
                table: "candidate_refresh_tokens",
                column: "candidate_account_id",
                principalTable: "candidate_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cheat_detection_signals_interview_sessions_session_id",
                table: "cheat_detection_signals",
                column: "session_id",
                principalTable: "interview_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cv_jd_analyses_job_postings_job_posting_id",
                table: "cv_jd_analyses",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_evaluations_applications_application_id",
                table: "evaluations",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_evaluations_interview_sessions_session_id",
                table: "evaluations",
                column: "session_id",
                principalTable: "interview_sessions",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_hr_reviews_evaluations_evaluation_id",
                table: "hr_reviews",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_hr_reviews_users_reviewed_by_user_id",
                table: "hr_reviews",
                column: "reviewed_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_bookings_applications_application_id",
                table: "interview_bookings",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_interview_bookings_availability_slots_availability_slot_id",
                table: "interview_bookings",
                column: "availability_slot_id",
                principalTable: "availability_slots",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_bookings_interview_bookings_rescheduled_from_id",
                table: "interview_bookings",
                column: "rescheduled_from_id",
                principalTable: "interview_bookings",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_codes_applications_application_id",
                table: "interview_codes",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_codes_users_created_by_user_id",
                table: "interview_codes",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_interview_invites_applications_application_id",
                table: "interview_invites",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_interview_round_configs_job_postings_job_posting_id",
                table: "interview_round_configs",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_interview_sessions_applications_application_id",
                table: "interview_sessions",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_job_postings_users_approved_by_user_id",
                table: "job_postings",
                column: "approved_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_job_postings_users_created_by_user_id",
                table: "job_postings",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_must_ask_tracking_interview_sessions_session_id",
                table: "must_ask_tracking",
                column: "session_id",
                principalTable: "interview_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_must_ask_tracking_playbook_documents_playbook_document_id",
                table: "must_ask_tracking",
                column: "playbook_document_id",
                principalTable: "playbook_documents",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_must_ask_tracking_questions_question_id",
                table: "must_ask_tracking",
                column: "question_id",
                principalTable: "questions",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_candidate_accounts_candidate_account_id",
                table: "notifications",
                column: "candidate_account_id",
                principalTable: "candidate_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_online_test_questions_job_postings_job_posting_id",
                table: "online_test_questions",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_online_test_submissions_applications_application_id",
                table: "online_test_submissions",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_playbook_documents_users_uploaded_by_user_id",
                table: "playbook_documents",
                column: "uploaded_by_user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_questions_interview_sessions_session_id",
                table: "questions",
                column: "session_id",
                principalTable: "interview_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_users_user_id",
                table: "refresh_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_jobs_candidate_accounts_candidate_account_id",
                table: "saved_jobs",
                column: "candidate_account_id",
                principalTable: "candidate_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_jobs_job_postings_job_posting_id",
                table: "saved_jobs",
                column: "job_posting_id",
                principalTable: "job_postings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_requests_users_created_user_id",
                table: "account_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_account_requests_users_requested_by_user_id",
                table: "account_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_account_requests_users_reviewed_by_user_id",
                table: "account_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_answers_interview_sessions_session_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "FK_answers_questions_question_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "FK_applications_candidate_accounts_candidate_account_id",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "FK_applications_job_postings_job_posting_id",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_actor_user_id",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_availability_slots_job_postings_job_posting_id",
                table: "availability_slots");

            migrationBuilder.DropForeignKey(
                name: "FK_candidate_refresh_tokens_candidate_accounts_candidate_accou~",
                table: "candidate_refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_cheat_detection_signals_interview_sessions_session_id",
                table: "cheat_detection_signals");

            migrationBuilder.DropForeignKey(
                name: "FK_cv_jd_analyses_job_postings_job_posting_id",
                table: "cv_jd_analyses");

            migrationBuilder.DropForeignKey(
                name: "FK_evaluations_applications_application_id",
                table: "evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_evaluations_interview_sessions_session_id",
                table: "evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_reviews_evaluations_evaluation_id",
                table: "hr_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_hr_reviews_users_reviewed_by_user_id",
                table: "hr_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_bookings_applications_application_id",
                table: "interview_bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_bookings_availability_slots_availability_slot_id",
                table: "interview_bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_bookings_interview_bookings_rescheduled_from_id",
                table: "interview_bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_codes_applications_application_id",
                table: "interview_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_codes_users_created_by_user_id",
                table: "interview_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_invites_applications_application_id",
                table: "interview_invites");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_round_configs_job_postings_job_posting_id",
                table: "interview_round_configs");

            migrationBuilder.DropForeignKey(
                name: "FK_interview_sessions_applications_application_id",
                table: "interview_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_job_postings_users_approved_by_user_id",
                table: "job_postings");

            migrationBuilder.DropForeignKey(
                name: "FK_job_postings_users_created_by_user_id",
                table: "job_postings");

            migrationBuilder.DropForeignKey(
                name: "FK_must_ask_tracking_interview_sessions_session_id",
                table: "must_ask_tracking");

            migrationBuilder.DropForeignKey(
                name: "FK_must_ask_tracking_playbook_documents_playbook_document_id",
                table: "must_ask_tracking");

            migrationBuilder.DropForeignKey(
                name: "FK_must_ask_tracking_questions_question_id",
                table: "must_ask_tracking");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_candidate_accounts_candidate_account_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_recipient_user_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_online_test_questions_job_postings_job_posting_id",
                table: "online_test_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_online_test_submissions_applications_application_id",
                table: "online_test_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_playbook_documents_users_uploaded_by_user_id",
                table: "playbook_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_questions_interview_sessions_session_id",
                table: "questions");

            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_users_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_saved_jobs_candidate_accounts_candidate_account_id",
                table: "saved_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_saved_jobs_job_postings_job_posting_id",
                table: "saved_jobs");

            migrationBuilder.DropIndex(
                name: "idx_webhook_deliveries_next_retry",
                table: "webhook_deliveries");

            migrationBuilder.DropIndex(
                name: "idx_users_active_deleted",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_system_settings_key",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_saved_jobs_job_posting_id",
                table: "saved_jobs");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "idx_playbook_docs_active_deleted",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "idx_playbook_documents_scope",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "IX_playbook_documents_uploaded_by_user_id",
                table: "playbook_documents");

            migrationBuilder.DropIndex(
                name: "IX_online_test_questions_job_posting_id",
                table: "online_test_questions");

            migrationBuilder.DropIndex(
                name: "IX_must_ask_tracking_playbook_document_id",
                table: "must_ask_tracking");

            migrationBuilder.DropIndex(
                name: "IX_must_ask_tracking_question_id",
                table: "must_ask_tracking");

            migrationBuilder.DropIndex(
                name: "IX_must_ask_tracking_session_id",
                table: "must_ask_tracking");

            migrationBuilder.DropIndex(
                name: "ux_magic_links_token_hash",
                table: "magic_links");

            migrationBuilder.DropIndex(
                name: "idx_job_postings_active_deleted",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "idx_job_postings_public_active",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "idx_job_postings_public_filters",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "idx_job_postings_salary",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "idx_job_postings_skills",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "IX_job_postings_approved_by_user_id",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "idx_interview_sessions_session_type",
                table: "interview_sessions");

            migrationBuilder.DropIndex(
                name: "idx_interview_codes_expires_at",
                table: "interview_codes");

            migrationBuilder.DropIndex(
                name: "IX_interview_codes_created_by_user_id",
                table: "interview_codes");

            migrationBuilder.DropIndex(
                name: "ux_interview_codes_code",
                table: "interview_codes");

            migrationBuilder.DropIndex(
                name: "IX_interview_bookings_availability_slot_id",
                table: "interview_bookings");

            migrationBuilder.DropIndex(
                name: "IX_interview_bookings_rescheduled_from_id",
                table: "interview_bookings");

            migrationBuilder.DropIndex(
                name: "IX_hr_reviews_reviewed_by_user_id",
                table: "hr_reviews");

            migrationBuilder.DropIndex(
                name: "idx_evaluations_session_type",
                table: "evaluations");

            migrationBuilder.DropIndex(
                name: "ix_evaluations_session_id",
                table: "evaluations");

            migrationBuilder.DropIndex(
                name: "idx_document_chunks_embedding_hnsw",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "idx_document_chunks_source",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_cheat_detection_signals_session_id",
                table: "cheat_detection_signals");

            migrationBuilder.DropIndex(
                name: "IX_candidate_refresh_tokens_candidate_account_id",
                table: "candidate_refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ux_candidate_refresh_tokens_token_hash",
                table: "candidate_refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ux_candidate_accounts_email",
                table: "candidate_accounts");

            migrationBuilder.DropIndex(
                name: "IX_availability_slots_job_posting_id",
                table: "availability_slots");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_actor_user_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "idx_applications_active_deleted",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "IX_answers_question_id",
                table: "answers");

            migrationBuilder.DropIndex(
                name: "IX_account_requests_created_user_id",
                table: "account_requests");

            migrationBuilder.DropIndex(
                name: "IX_account_requests_requested_by_user_id",
                table: "account_requests");

            migrationBuilder.DropIndex(
                name: "IX_account_requests_reviewed_by_user_id",
                table: "account_requests");

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_session_id",
                table: "evaluations",
                column: "session_id");
        }
    }
}
