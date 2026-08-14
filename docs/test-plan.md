# ARISP — Unit Test Plan & Coverage Inventory

> Nguồn: `ari-service/tests/ARI.Application.UnitTests` + `ARI.Domain.UnitTests`. Cập nhật: 2026-08-09.
> Tên test method & assert giữ nguyên tiếng Anh (trích từ code); mô tả nhóm bằng tiếng Việt.

## Tổng quan

| Chỉ số | Số lượng |
|---|---|
| **Test case ĐÃ có** | **356** |
| **Test case ĐỀ XUẤT (còn thiếu)** | **131** |
| Handler đã có test | ~35 |
| Handler chính còn thiếu test | ~40 |

### Phân bố "đã test" theo nhóm

| Nhóm | Handler | Số case |
|---|---|---|
| Online Test (candidate + create) | 3 | 24 |
| Application flow + Screening | 9 | 66 |
| Evaluation review + HR review | 5 | 41 |
| Interview code + Practice interview | 6 | 50 |
| Job board | 5 | 36 |
| Job postings | 6 | 71 |
| Scheduling | 9 | 59 |
| Auth + Common + Domain | 4 | 12 |

### Phân bố "còn thiếu" theo ưu tiên

| Ưu tiên | Nhóm handler |
|---|---|
| 🔴 High | Sinh Evaluation buổi thật, Recording, Cheat signal, Reschedule, Online Test staff (Update/Settings/Result/Import), ApplyToJob, VerifyCvInfo, GetMyEvaluation, toàn bộ Auth core, CreateStaffUser, ApproveAccountRequest, DeactivateUser, UploadPlaybook |
| 🟡 Medium | SendBookingReminder, GenerateCodeBatch, GetInterviewJobs, GetCandidatesInSlot, GetMediaConfig, SynthesizeSpeech, GetOnlineTestBank, DeleteQuestion, SendInterviewInvite, GetHrDashboard, Reject/Activate/Delete/UpdateRole/UpdateSystemSettings |
| ⚪ Low | GetSlotsForJob, Export, AnalyzeCv, Refresh/Forgot/Reset/Logout/Resend, ApproveUser, GetAuditLogs/GetUsers/GetPendingUsers/GetAdminStats, DeletePlaybook/GetPlaybooks |

---

# PHẦN A — TEST CASE ĐÃ CÓ (356)

## A1. Online Test — candidate + create (24)

### CreateOnlineTestQuestionCommandHandler (5)
| Test | Kiểm chứng |
|---|---|
| Invalid_question_is_rejected_before_touching_repo | Request lỗi (blank, <2 hoặc >6 options, không có đáp án đúng, index vượt phạm vi, single mà nhiều đáp án) → fail đúng thông điệp, không chạm repo. |
| Valid_multiple_question_is_normalized_and_persisted | Multiple hợp lệ dedupe+sort CorrectOptions `[0,2]`, lưu `"[0,2]"`, legacy CorrectOption=phần tử đầu, đúng JobPostingId. |
| Admin_can_add_to_any_job | HrAdmin thêm câu vào job không sở hữu → thành công. |
| Non_owner_recruiter_is_forbidden | Recruiter không phải chủ tin → Forbidden, không lưu. |
| Missing_job_returns_not_found | Thêm câu vào job không tồn tại → NotFound. |

### GetCandidateOnlineTestQueryHandler (5)
| Test | Kiểm chứng |
|---|---|
| Cv_passed_returns_questions_without_correct_answers | CV passed → CvPassed=true, trả câu hỏi + options, ẩn đáp án đúng. |
| Cv_not_passed_returns_metadata_but_hides_questions | CV chưa duyệt → CvPassed=false, Questions rỗng nhưng vẫn báo TotalQuestions. |
| Already_submitted_state_is_reflected | Đã nộp → AlreadySubmitted=true kèm Score/IsPassed. |
| Draw_is_deterministic_across_repeated_reads | Bank > perTest → 2 lần đọc ra cùng bộ đề, cùng thứ tự. |
| Unknown_application_returns_not_found | Hồ sơ lạ → NotFound. |

### SubmitOnlineTestCommandHandler (14)
| Test | Kiểm chứng |
|---|---|
| All_correct_scores_100_and_passes | Đúng hết → Score=100, IsPassed, đếm đúng, 1 submission lưu. |
| Score_is_rounded_to_two_decimals_and_below_threshold_fails | 2/3 → 66.67 < 70 → fail. |
| Pass_threshold_is_inclusive | Đúng bằng điểm sàn (50 với passScore 50) → pass (>=). |
| Multiple_answer_requires_exact_set_match | Multiple chỉ đúng khi khớp hoàn toàn; thiếu/thừa/rỗng = 0. |
| Unanswered_questions_count_as_wrong | Câu không trả lời tính sai (1/2 → 50, fail). |
| Only_the_drawn_subset_is_graded | Chỉ chấm bộ đã bốc (perTest=2), không chấm cả 5 câu bank. |
| Second_submission_same_round_conflicts | Nộp lần 2 cùng vòng → Conflict, không thêm bản ghi. |
| Cv_not_passed_is_blocked | CV chưa duyệt → fail, không lưu. |
| Withdrawn_application_is_blocked | Hồ sơ đã rút → fail 'rút'. |
| Empty_question_bank_returns_failure | Bank rỗng → fail, không lưu. |
| Unknown_application_returns_not_found | Hồ sơ lạ → NotFound. |
| Candidate_not_owning_application_is_forbidden | Không sở hữu hồ sơ → Forbidden. |
| Successful_submit_notifies_candidate_and_staff | Nộp thành công báo candidate + recruiter chủ tin + nhóm hr_admin. |
| Notification_failure_does_not_break_submit | Lỗi SignalR không phá việc nộp (vẫn thành công + lưu). |

## A2. Application flow + Screening (66)

### ApplicationService.CheckPracticeEligibilityAsync (4)
| Test | Kiểm chứng |
|---|---|
| App_not_found_fails | Hồ sơ không tồn tại → fail 'Application not found'. |
| Eligible_when_no_practice_session_yet | Chưa có practice → eligible=true. |
| Not_eligible_when_round_already_practiced | Vòng đã practice → false. |
| Other_round_practice_does_not_block | Vòng 2 vẫn eligible dù vòng 1 đã practice. |

### ApplicationService.SubmitApplicationAsync (12)
| Test | Kiểm chứng |
|---|---|
| Job_not_found_fails | Job lạ → fail, không tạo Application. |
| Inactive_job_is_rejected | Job closed → fail 'không hoạt động'. |
| Expired_deadline_is_rejected | Quá deadline → fail 'hết hạn'. |
| Valid_application_is_created_as_cv_submitted | Tạo 1 Application status cv_submitted + email + notice period. |
| Source_is_recorded | Lưu source (vd self_applied). |
| Cv_text_is_ingested_to_rag | Ingest CV vào RAG 1 lần, sourceType 'cv'. |
| No_cv_text_skips_rag | Không có CV text → không ingest. |
| Auto_links_matching_analysis_by_hash | Hash CV khớp → auto link CV-JD analysis. |
| Non_matching_hash_leaves_analysis_unlinked | Hash không khớp → CvJdAnalysisId null. |
| Notifies_hr_group_and_recruiter | Gửi ReceiveNewApplication cho hr_admin + recruiter chủ tin. |
| Self_applied_candidate_gets_notification_record_and_realtime | Self-apply → lưu notification 'applied' + realtime. |
| Anonymous_application_creates_no_candidate_notification | Không có account → không tạo notification candidate. |

### ApplicationService.UpdateApplicationStatusAsync (8)
| Test | Kiểm chứng |
|---|---|
| Empty_status_fails | Status rỗng → fail 'cannot be empty'. |
| Unknown_status_fails | Status lạ → fail 'is invalid', giữ nguyên. |
| Unchanged_status_fails | Trùng status hiện tại → fail 'already in'. |
| Disallowed_transition_fails | Nhảy sai (cv_submitted→pass) → fail. |
| Withdrawn_is_terminal | withdrawn là terminal, không chuyển đi đâu. |
| Not_pass_can_be_reopened_to_screening | not_pass → mở lại screening được. |
| Allowed_transition_succeeds_and_is_case_insensitive | 'INTERVIEW' lưu 'interview' + báo candidate. |
| App_not_found_fails | Hồ sơ lạ → fail. |

### ApplicationService — CV decision: Invite / Accept / Reject (12)
| Test | Kiểm chứng |
|---|---|
| Send_invite_app_not_found_fails | Invite hồ sơ lạ → fail. |
| Send_invite_creates_invite_promotes_to_screening_and_emails | Tạo invite vòng 1 (TTL theo job), → screening, gửi 1 email. |
| Send_invite_deletes_old_unused_invite_of_same_round | Xoá invite cũ chưa dùng cùng vòng, giữ invite mới. |
| Send_invite_keeps_already_scheduled_invite | Giữ invite đã đặt lịch, thêm invite mới (2 invite vòng 1). |
| Accept_app_not_found_fails | Accept hồ sơ lạ → fail. |
| Accept_wrong_status_fails | Accept sai trạng thái → fail, giữ nguyên. |
| Accept_promotes_creates_invite_and_notification | Accept → screening, tạo 1 invite, không email, notif cv_accepted + realtime. |
| Accept_from_invited_status_succeeds | Accept từ invited → screening. |
| Reject_app_not_found_fails | Reject hồ sơ lạ → fail. |
| Reject_wrong_status_fails | Reject hồ sơ đã cv_rejected → fail. |
| Reject_sets_cv_rejected_emails_and_notifies | Reject → cv_rejected, 1 email cảm ơn, notif + realtime. |
| Reject_from_invited_status_succeeds | Reject từ invited → cv_rejected. |

### ApplicationService.GetApplicationsByJobAsync — MapApplications enrichment (7)
| Test | Kiểm chứng |
|---|---|
| Cv_submitted_has_no_current_round | cv_submitted → CurrentRound null. |
| Screening_status_sets_round_1 | screening → CurrentRound 1. |
| Interview_status_uses_highest_invite_or_session_round | interview → max(invite, session round). |
| Scheduled_booking_sets_flag_and_interview_date | Booking scheduled → HasScheduledInterview + InterviewDate. |
| Cancelled_booking_does_not_set_scheduled_flag | Booking cancelled → flag false, date null. |
| Real_evaluation_score_maps_to_interview_score | Eval 'real' → InterviewScore. |
| Practice_evaluation_is_ignored_for_interview_score | Eval 'practice' → InterviewScore null. |

### GetApplicationByIdQueryHandler / GetApplicationsQueryHandler — CQRS read (5)
| Test | Kiểm chứng |
|---|---|
| ById_missing_maps_to_not_found_code | Thiếu → map NotFound code. |
| ById_resolves_cv_file_url | Resolve CV key → URL /files. |
| List_mine_filters_to_creators_jobs | mine → chỉ hồ sơ job của creator. |
| List_without_mine_returns_all | null creator → toàn bộ. |
| List_resolves_cv_file_urls | Resolve CV URL cho từng hồ sơ. |

### ApplicationService.GetApplicationByIdAsync (6)
| Test | Kiểm chứng |
|---|---|
| Not_found_fails | Không tồn tại → fail. |
| Detail_returns_cv_text_and_job_title | Trả CvText + job title. |
| Detail_loads_linked_analysis_for_match_score | Load analysis → MatchScore + CvJdSummary. |
| Detail_surfaces_scheduled_date_and_confirmation_status | Trả ngày phỏng vấn + confirmation 'confirmed'. |
| Detail_surfaces_decline_reason_when_no_scheduled_booking | Booking declined → hiện decline reason. |
| Detail_computes_round_from_invites_for_interview_status | interview → CurrentRound từ invite cao nhất. |

### ApplicationService — list queries (7)
| Test | Kiểm chứng |
|---|---|
| By_job_fails_when_job_not_found | Job lạ → fail. |
| By_job_returns_only_that_jobs_apps_newest_first | Chỉ hồ sơ job đó, mới nhất trước, JobTitle override. |
| By_job_list_omits_cv_text | List bỏ CvText (null). |
| By_job_includes_match_score_and_summary | Gồm MatchScore + CvJdSummary từ analysis. |
| All_orders_by_created_desc_and_resolves_job_titles | Mới nhất trước + resolve JobTitle. |
| For_creator_returns_empty_when_creator_owns_no_jobs | Creator không có job → rỗng. |
| For_creator_only_includes_apps_of_own_jobs | Chỉ hồ sơ job của creator. |

### GetJobApplicationsQueryHandler (5)
| Test | Kiểm chứng |
|---|---|
| Job_not_found_returns_not_found | Job lạ → NotFound. |
| Recruiter_cannot_view_other_owners_job | Recruiter xem tin người khác → Forbidden. |
| Recruiter_can_view_own_job | Recruiter xem tin mình → OK. |
| Hr_admin_can_view_any_job | HrAdmin xem mọi tin. |
| Cv_file_url_is_resolved_via_storage | Resolve CV URL qua storage. |

## A3. Evaluation review + HR review (41)

### GetEvaluationDetailQueryHandler (7)
| Test | Kiểm chứng |
|---|---|
| Not_found_fails | id lạ → 'Evaluation not found'. |
| Found_by_evaluation_id | Tra theo EvaluationId → OK, kèm candidate/job. |
| Falls_back_to_session_id_lookup | id là SessionId → fallback tra session. |
| Rejects_practice_evaluation | Eval practice ẩn khỏi HR → 'not found'. |
| Application_not_found_fails | App chưa seed → fail. |
| Includes_hr_review_when_present | Có review → gồm final verdict + IsOverride. |
| Resolves_recording_url_from_session | Real → resolve recording '/files/...' + expiry. |

### GetEvaluationsByApplicationQueryHandler (4)
| Test | Kiểm chứng |
|---|---|
| Application_not_found_fails | App lạ → fail. |
| Job_not_found_fails | Job thiếu → fail. |
| Excludes_practice_evaluations | Loại practice, chỉ real. |
| Includes_hr_review_status | Đã review → status 'completed' + verdict. |

### GetEvaluationsQueryHandler (8)
| Test | Kiểm chứng |
|---|---|
| Excludes_practice_evaluations | Bỏ practice khỏi list HR. |
| Filters_by_job_posting | Lọc theo job. |
| Status_pending_returns_only_unreviewed | pending → chưa review. |
| Status_completed_returns_only_reviewed_with_final_verdict | completed → dùng verdict HR (override). |
| Status_verdict_filter_respects_hr_override | Lọc verdict tính cả AI-pass lẫn override-pass. |
| Paginates_and_reports_total | Phân trang đúng total/pages, mới nhất trước. |
| Joins_candidate_job_and_review_status | Join candidate/job/status/verdict. |
| Empty_returns_zero_total | Rỗng → total 0. |

### InterviewService.GetSessionsForHrAsync (5)
| Test | Kiểm chứng |
|---|---|
| Excludes_practice_sessions | Ẩn practice khỏi HR. |
| Ordered_newest_first | Mới nhất index 0. |
| Joins_candidate_job_and_latest_verdict | Join candidate/job/eval mới nhất + verdict. |
| Has_recording_flag_reflects_stored_video | HasRecording=true khi có recording. |
| Empty_when_no_sessions | Không session → rỗng. |

### InterviewService.SubmitHrReviewAsync (17)
| Test | Kiểm chứng |
|---|---|
| Confirm_pass_records_review_and_sets_application_pass | Confirm 'pass' → review non-override, app 'pass', audit 'hr_confirm'. |
| Confirm_not_pass_sets_application_not_pass | Confirm 'not_pass' → app 'not_pass'. |
| Recruiter_can_confirm_matching_verdict | Recruiter confirm khớp verdict được → 'pass'. |
| Override_by_hr_admin_with_reason_succeeds | HR Admin override + reason → IsOverride+reason, 'pass', audit 'hr_override'. |
| Override_by_super_admin_succeeds | Super Admin override được. |
| Override_without_reason_fails_and_persists_nothing | Override thiếu reason → fail, không lưu. |
| Override_by_recruiter_is_forbidden | Recruiter override → Forbidden. |
| Missing_evaluation_returns_failure | Eval lạ → fail. |
| Missing_hr_user_returns_failure | HR user thiếu → fail. |
| Pass_real_with_next_round_config_progresses_and_creates_invite | Pass real còn vòng → tạo invite vòng 2, 'interview'. |
| Pass_real_without_next_round_config_does_not_progress | Pass real vòng cuối → không invite, 'pass'. |
| Pass_practice_does_not_progress_even_with_config | Pass practice → không invite, giữ 'interview'. |
| Not_pass_never_progresses | not_pass → không invite, 'not_pass'. |
| Notifies_candidate_realtime_and_creates_notification_record | Bắn realtime + 1 notif 'result' dedup. |
| Notification_is_deduplicated | Dedup-key trùng → không tạo trùng. |
| No_candidate_account_skips_realtime_but_still_succeeds | Không account → bỏ realtime nhưng vẫn 'pass'. |

## A4. Interview code + Practice interview (50)

### InterviewCodeService.GenerateCodeAsync / GenerateBatchAsync (10)
| Test | Kiểm chứng |
|---|---|
| Generate_application_not_found_fails | Hồ sơ lạ → fail. |
| Generate_job_not_found_fails | Job liên kết thiếu → fail. |
| Generate_without_scheduled_booking_is_rejected | Chưa đặt lịch → fail 'chưa đặt lịch', không tạo code. |
| Generate_with_booking_creates_6char_code_and_audit | Có booking → code 6 ký tự (loại ký tự dễ nhầm), expiry tương lai, audit 'interview_code_generated'. |
| Generate_uses_round_config_ttl | Áp TTL round config (5h). |
| Generate_promotes_screening_to_interview | screening → interview khi cấp code. |
| Generate_notifies_candidate_when_account_present | Có account → push ReceiveUserNotification. |
| Generate_infers_next_round_from_completed_sessions | round null → suy max(completed)+1. |
| Batch_empty_list_fails | List rỗng → fail. |
| Batch_returns_only_eligible_codes | Batch chỉ trả code hồ sơ eligible. |

### InterviewCodeService.GetCodesByJobAsync (3)
| Test | Kiểm chứng |
|---|---|
| Returns_summaries_with_status_and_candidate_name | 3 code + tên + status Active/Used/Expired. |
| Only_includes_codes_of_this_job | Chỉ code của job đó. |
| Empty_when_job_has_no_codes | Không code → rỗng. |

### InterviewCodeService.ValidateCodeAsync (7)
| Test | Kiểm chứng |
|---|---|
| Empty_code_fails | Code rỗng → fail. |
| Unknown_code_returns_not_found_reason | Code lạ → Valid=false reason 'not_found'. |
| Used_code_returns_used_reason | Đã dùng → 'used'. |
| Expired_code_returns_expired_reason | Hết hạn → 'expired'. |
| Valid_code_starts_session_marks_used_mints_token_and_audits | Hợp lệ → tạo session 'real', trả chi tiết, đánh dấu used, mint kiosk token, audit 'interview_code_used'. |
| Code_match_is_case_insensitive | Nhập thường khớp code hoa. |
| Start_session_failure_rolls_back_used_flag | StartSession fail → rollback used, không mint token/session. |

### InterviewService — Practice conduct (20)
| Test | Kiểm chứng |
|---|---|
| Start_application_not_found_fails | Start hồ sơ lạ → fail. |
| Start_job_not_found_fails | Job thiếu → fail. |
| Start_creates_active_practice_session | Tạo session practice active, lang từ job/UI, đánh dấu PracticeSessionUsed. |
| Start_blocked_when_round_attempt_already_used | Đã dùng lượt → fail 'đã dùng lượt'. |
| Start_unlimited_when_attempts_option_zero | PracticeAttemptsPerRound=0 → lặp lại được. |
| Start_other_round_is_not_blocked | Vòng 1 đã dùng không chặn vòng 2. |
| Start_practice_does_not_seed_must_ask | Practice không seed MustAsk. |
| Save_answer_persists_transcript | Lưu answer + response time. |
| Save_answer_session_not_found_fails | Session lạ → fail. |
| Save_answer_inactive_session_fails | Session inactive → fail 'not active'. |
| Generate_question_saves_and_publishes_text_plus_audio | Sinh câu 1, publish ReceiveQuestion + Audio, TTS 1 lần, giữ active. |
| Generate_fails_when_session_not_active | Session completed → fail. |
| Generate_force_closes_after_question_cap | Sau cap 12 câu → đóng với closing text, +1 evaluation. |
| Generate_closes_on_end_interview_marker | Marker [END_INTERVIEW] → complete, strip marker. |
| End_completed_generates_practice_evaluation_without_touching_application | Kết thúc practice → eval 'practice', không đổi app status, không báo hr_admin. |
| End_is_idempotent_when_already_completed | End lần 2 → idempotent, không re-eval. |
| End_non_completed_status_skips_evaluation | Status 'abandoned' → set status, bỏ eval. |
| Timeout_rejected_before_threshold | Chưa hết giờ → fail 'Chưa hết thời gian'. |
| Timeout_closes_after_threshold | Quá cap 20' → đóng + 1 eval. |
| Timeout_idempotent_when_completed | Timeout khi đã completed → idempotent. |

### GetMyPracticeReviewQueryHandler (7)
| Test | Kiểm chứng |
|---|---|
| Review_session_not_found | Session lạ → NotFound. |
| Review_rejects_real_session | Session real → NotFound (ẩn). |
| Review_forbidden_for_non_owner | Không sở hữu → Forbidden. |
| Review_returns_turns_and_hides_verdict | Trả turns + eval, ẩn AiVerdict, EvaluationPending=false. |
| Review_evaluation_pending_when_completed_without_eval | Completed chưa có eval → Evaluation null, Pending=true. |
| Review_merges_ai_analysis_into_turn_by_sequence | Ghép AI analysis vào đúng lượt theo sequence. |
| Review_auto_links_legacy_app_by_email | Auto-link hồ sơ legacy theo email token. |

### GetMyPracticeSessionsQueryHandler (3)
| Test | Kiểm chứng |
|---|---|
| Sessions_empty_when_candidate_has_no_applications | Không hồ sơ → rỗng. |
| Sessions_lists_only_practice_newest_first | Chỉ practice, mới nhất trước. |
| Sessions_includes_score_and_turn_count | HasEvaluation + TurnCount đúng. |

## A5. Job board (36)

### GetCvMatchQueryHandler (5)
| Test | Kiểm chứng |
|---|---|
| Unknown_candidate_is_unauthorized | Candidate lạ → Unauthorized. |
| No_cv_in_profile_returns_none | Không CV → HasCv=false, status 'none'. |
| Unreadable_cv_returns_failed | CV không đọc được → status 'failed', AiAvailable=false. |
| Cached_completed_analysis_is_reused | Cache completed khớp hash → reuse, MatchScore 88. |
| Cached_failed_analysis_returns_failed | Cache failed → 'failed' + message. |

### GetJobByIdQueryHandler (8)
| Test | Kiểm chứng |
|---|---|
| Not_found_fails | Job lạ → NotFound. |
| Public_active_job_is_visible_to_anonymous | Anonymous xem job active public. |
| Non_public_job_is_hidden_from_anonymous | Job non-public ẩn với anonymous. |
| Draft_job_is_hidden_from_anonymous | Draft ẩn với anonymous. |
| Staff_can_view_draft_job | hr_admin xem draft non-public. |
| Recruiter_sees_own_draft_but_not_others | Recruiter xem draft mình, không xem của người khác. |
| Staff_gets_resolved_jd_file_url | Staff nhận JD URL resolve. |
| Creator_name_and_rounds_are_included | Gồm tên creator + rounds sort tăng. |

### GetJobFacetsQueryHandler (5)
| Test | Kiểm chứng |
|---|---|
| Only_active_public_jobs_are_counted | Chỉ đếm active+public (TotalJobs=1). |
| Category_facet_reports_counts | Đếm theo category (be=2, fe=1). |
| Skills_facet_aggregates_across_jobs | Gộp skills (C#=2, Redis=1). |
| Experience_levels_merge_by_display_label | intern+fresher gộp 1 facet count 2. |
| Empty_board_returns_zero_total | Board rỗng → TotalJobs=0. |

### GetJobsQueryHandler (13)
| Test | Kiểm chứng |
|---|---|
| Only_active_public_non_expired_jobs_are_returned | Chỉ active/public/chưa hết hạn (TotalCount=1). |
| Search_matches_title | Search khớp title. |
| Search_matches_skill | Search khớp skill. |
| Category_filter_narrows_results | Lọc category. |
| Experience_level_filter_narrows_results | Lọc experience level. |
| Location_filter_is_case_insensitive | Lọc location không phân biệt hoa thường. |
| Language_filter_narrows_results | Lọc language. |
| Pagination_limits_items_and_reports_total | Phân trang giới hạn + total đúng (5). |
| Second_page_returns_different_items | Trang 2 khác trang 1. |
| Salary_desc_sort_orders_by_highest_max_salary | Sort lương giảm dần. |
| Urgent_jobs_come_first_by_default | Urgent lên đầu mặc định. |
| Relevance_sort_orders_by_matching_skills | Sort theo trùng skill candidate. |
| Relevance_without_candidate_skills_falls_back_to_newest | Không có skill → fallback mới nhất. |

### SubmitApplicationCommandHandler (5)
| Test | Kiểm chứng |
|---|---|
| Parses_hashes_saves_and_delegates_to_service | Parse CV, MD5 hash, lưu file, delegate service source 'job_board'. |
| Parse_failure_returns_error_without_saving_or_delegating | Parse lỗi → fail, không lưu/không delegate. |
| Storage_failure_returns_server_error | Lưu lỗi → ServerError, không delegate. |
| Db_failure_cleans_up_saved_file | DB fail → xoá file đã lưu. |
| Null_bytes_in_parsed_text_are_stripped | Strip null bytes trước khi truyền. |

## A6. Job postings (71)

### AnalyzeJdCommandHandler (8)
| Test | Kiểm chứng |
|---|---|
| Successful_extraction_returns_autofill_fields | Gemini OK → auto-fill Title/Category/Skills + JdFileUrl. |
| Parse_failure_returns_error_without_saving_or_calling_gemini | Parse lỗi → fail, không lưu/không gọi Gemini. |
| Storage_failure_returns_server_error | Lưu lỗi → ServerError. |
| Gemini_failure_still_returns_saved_file_with_isvalid_false | Gemini fail → IsValidJd=false, giữ file + text fallback. |
| Pdf_is_sent_inline_to_gemini | PDF gửi inline bytes mime application/pdf. |
| Docx_uses_text_fallback_not_inline_bytes | DOCX gửi text fallback, không inline. |
| JobDescription_prefers_gemini_over_parsed_text | Ưu tiên JD của Gemini. |
| JobDescription_falls_back_to_parsed_when_gemini_empty | Gemini null → fallback parsed text. |

### CreateJobCommandHandler (10)
| Test | Kiểm chứng |
|---|---|
| Valid_request_creates_draft_job_with_rounds | Tạo job draft + 1 RoundConfig/vòng. |
| Creator_not_found_fails_unauthorized | Creator lạ → Unauthorized, không lưu. |
| Jd_is_ingested_to_rag | Ingest JD vào RAG sourceType 'jd'. |
| Notifies_creator | Creator nhận ReceiveJobPostingUpdate. |
| Missing_title_fails_validation | Title rỗng → fail validation. |
| No_rounds_fails_validation | Không round → fail. |
| Invalid_interview_mode_fails | Mode lạ ('hybrid') → fail. |
| Onsite_without_location_fails | Onsite thiếu location → fail. |
| Round_without_language_inherits_detected_language | Round không lang → kế thừa detected. |
| Round_explicit_language_is_kept | Round có lang ('ja') → giữ. |

### CreateJobSlotsCommandHandler (3)
| Test | Kiểm chứng |
|---|---|
| Job_not_found_fails | Job lạ → NotFound. |
| Creates_slots_with_zero_booked_count | Slot BookedCount 0, đúng job/round/capacity. |
| Empty_list_saves_nothing_but_succeeds | List rỗng → không lưu, vẫn success. |

### GetAdminJobsQueryHandler (7)
| Test | Kiểm chứng |
|---|---|
| Returns_all_jobs_for_admin | Không mine → mọi job. |
| Mine_filter_returns_only_own_jobs | mine → chỉ job của user. |
| Includes_pending_and_draft_statuses | Gồm pending + draft. |
| Applicant_count_is_populated | ApplicantCount đúng. |
| Creator_name_and_role_are_resolved | 'Anna (Recruiter)'. |
| Ordered_by_created_at_descending | Mới nhất trước. |
| No_jobs_returns_empty_list | Không job → rỗng. |

### UpdateJobStatusCommandHandler (30)
| Test | Kiểm chứng |
|---|---|
| Empty_status_fails | Status rỗng → fail. |
| Invalid_status_fails | Status lạ → fail. |
| Cannot_transition_back_to_draft | Không về draft. |
| Job_not_found_fails | Job lạ → NotFound. |
| Same_status_fails | Trùng status → fail. |
| Archived_job_cannot_change_status | Archived không đổi status. |
| Unauthorized_user_is_forbidden | Không quyền → Forbidden. |
| Owner_submits_draft_to_pending | Owner draft→pending, hr_admin nhận event. |
| Non_owner_cannot_submit_for_approval | Không phải owner (kể cả HrAdmin) → Forbidden. |
| Pending_only_from_draft_or_rejected | Từ active không pending được. |
| Rejected_job_can_be_resubmitted_and_clears_reason | rejected→pending, xoá RejectionReason. |
| Submitting_notifies_hr_admins_with_notification_record | Submit → lưu notif 'pending' cho hr_admin. |
| Admin_approves_pending_and_notifies_creator | Approve → active + ApprovedBy + PublishedAt + notif 'approved'. |
| Non_admin_cannot_approve | Recruiter không tự duyệt → Forbidden. |
| Approve_from_invalid_status_fails | Approve từ rejected → fail. |
| Approve_with_past_deadline_fails | Deadline quá khứ → fail. |
| Reactivating_closed_job_does_not_re_approve | closed→active không set lại ApprovedBy. |
| Admin_rejects_pending_with_reason_and_notifies_creator | Reject + reason → RejectionReason + notif 'rejected'. |
| Non_admin_cannot_reject | Non-admin reject → Forbidden. |
| Reject_requires_reason | Reject thiếu reason → fail. |
| Reject_only_from_pending | Reject job active → fail. |
| Approving_pdf_job_stamps_and_sets_signed_url | Duyệt PDF → stamp + SignedJdFileUrl. |
| Approving_docx_job_stamps_from_text | Duyệt DOCX → stamp từ text. |
| Stamp_failure_does_not_block_approval | Stamp fail → vẫn active, không SignedJdFileUrl. |
| Approving_job_without_jd_file_skips_stamp | Không JD file → bỏ stamp. |
| Owner_closes_active_job | Owner đóng job active. |
| Archive_blocked_by_active_applications | Có hồ sơ active → chặn archive. |
| Archive_soft_deletes_when_no_active_applications | Archive → status archived + DeletedAt. |

### UpdateJobCommandHandler (13)
| Test | Kiểm chứng |
|---|---|
| Job_not_found_fails | Job lạ → NotFound. |
| Non_owner_non_admin_is_forbidden | Không quyền → Forbidden. |
| Owner_can_update | Owner sửa được. |
| Admin_can_update_others_job | HrAdmin sửa job người khác. |
| Archived_job_cannot_be_updated | Archived không sửa. |
| Invalid_request_fails_validation | Title rỗng → fail. |
| Rounds_are_recreated_when_count_changes | Đổi round → xoá cũ tạo mới. |
| Unchanged_rounds_are_preserved | Round không đổi → giữ nguyên entity. |
| Active_job_update_broadcasts_public_update | Sửa job active → ReceivePublicJobUpdate. |
| Recruiter_cannot_edit_after_submitted_or_approved | Recruiter không sửa pending/active/closed → Forbidden. |
| Recruiter_can_edit_draft_or_rejected | Recruiter sửa draft/rejected. |
| Admin_can_edit_pending_job | Admin sửa pending. |
| Notifies_updater | Updater nhận notif. |

## A7. Scheduling (59)

### GetCandidateScheduleQueryHandler (5)
| Test | Kiểm chứng |
|---|---|
| Scheduled_future_booking_is_upcoming | Booking tương lai → Upcoming. |
| Scheduled_past_booking_is_past | Booking quá khứ → Past. |
| Recently_declined_booking_awaits_reschedule | Declined chưa xếp lại → AwaitingReschedule. |
| Declined_round_with_new_scheduled_booking_is_not_awaiting | Đã xếp lại → không awaiting, vào Upcoming. |
| No_applications_returns_empty_lists | Không hồ sơ → rỗng cả 3. |

### GetAvailabilitySlotsQueryHandler (6)
| Test | Kiểm chứng |
|---|---|
| GetSlots_empty_job_id_fails | jobId rỗng → fail. |
| GetSlots_job_not_found_returns_not_found | Job lạ → NotFound. |
| GetSlots_non_owner_recruiter_is_forbidden | Không sở hữu → Forbidden. |
| GetSlots_owner_gets_slots_ordered_by_start_time | Owner nhận slot sort theo giờ. |
| GetSlots_filters_by_round | Lọc theo round. |
| GetSlots_admin_can_view_any_job | Admin xem mọi job. |

### CreateSlotCommandHandler (9)
| Test | Kiểm chứng |
|---|---|
| Create_empty_job_id_fails | jobId rỗng → fail. |
| Create_end_before_start_fails | End < Start → fail. |
| Create_start_in_past_fails | Start quá khứ → fail. |
| Create_capacity_below_one_fails | Capacity <1 → fail. |
| Create_round_below_one_fails | Round <1 → fail. |
| Create_job_not_found_returns_not_found | Job lạ → NotFound (validate trước). |
| Create_non_owner_recruiter_is_forbidden | Không sở hữu → Forbidden. |
| Create_owner_persists_slot_with_zero_booked | Owner lưu slot BookedCount 0. |
| Create_defaults_timezone_when_blank | Timezone rỗng → Asia/Ho_Chi_Minh. |

### DeleteSlotCommandHandler (4)
| Test | Kiểm chứng |
|---|---|
| Delete_slot_not_found_returns_not_found | Slot lạ → NotFound. |
| Delete_non_owner_recruiter_is_forbidden | Không sở hữu → Forbidden, giữ slot. |
| Delete_booked_slot_is_rejected | Slot đã có booking → không xoá. |
| Delete_empty_slot_succeeds | Slot trống → xoá OK. |

### UpdateSlotCapacityCommandHandler (5)
| Test | Kiểm chứng |
|---|---|
| UpdateCapacity_slot_not_found_returns_not_found | Slot lạ → NotFound. |
| UpdateCapacity_non_owner_recruiter_is_forbidden | Không sở hữu → Forbidden. |
| UpdateCapacity_below_one_fails | Capacity <1 → fail. |
| UpdateCapacity_below_booked_count_fails | Capacity < đã đặt → fail. |
| UpdateCapacity_succeeds_and_persists | Update hợp lệ → lưu. |

### AssignSlotCommandHandler (15)
| Test | Kiểm chứng |
|---|---|
| Assign_books_slot_and_moves_screening_to_interview | Tạo booking scheduled/pending, reserve nguyên tử, screening→interview. |
| Assign_notifies_candidate_with_bell_and_realtime | Bell notif (dedup) + realtime ReceiveUserNotification. |
| Assign_marks_pending_invite_scheduled | Stamp ScheduledAt lên invite. |
| Round_two_assignment_keeps_interview_status | Vòng 2 → giữ interview. |
| Reassign_after_decline_links_to_prior_declined_booking | Reassign → link RescheduledFromId. |
| Full_slot_is_rejected_and_no_booking_created | Slot đầy → fail, không booking. |
| Already_scheduled_round_is_rejected | Vòng đã có booking → fail. |
| Save_failure_compensates_the_reserved_seat | Save fail → trả chỗ đã reserve. |
| Cannot_assign_before_cv_passed | Trạng thái pre-CV-pass → không assign. |
| Slot_of_wrong_round_is_rejected | Slot sai vòng → fail. |
| Past_slot_is_rejected | Slot quá khứ → fail. |
| Non_owner_staff_is_forbidden | Không sở hữu → Forbidden. |
| Admin_can_assign_for_any_job | Admin assign mọi job. |
| Unknown_application_returns_not_found | Hồ sơ lạ → NotFound. |
| Unknown_slot_returns_not_found | Slot lạ → NotFound. |

### ConfirmScheduleCommandHandler (5)
| Test | Kiểm chứng |
|---|---|
| Confirm_sets_confirmed_and_notifies_staff | Confirm → confirmed + RespondedAt + báo staff. |
| Confirm_is_idempotent_when_already_confirmed | Đã confirmed → không báo lại. |
| Confirm_fails_when_booking_not_scheduled | Booking hết hiệu lực → fail. |
| Confirm_by_non_owner_is_forbidden | Không sở hữu → Forbidden. |
| Confirm_unknown_booking_returns_not_found | Booking lạ → NotFound. |

### DeclineScheduleCommandHandler (6)
| Test | Kiểm chứng |
|---|---|
| Decline_releases_slot_and_records_reason | Decline → declined + reason + RespondedAt, trả chỗ, báo hr_admin. |
| Decline_requires_reason_of_at_least_three_chars | Reason <3 ký tự → fail, giữ scheduled. |
| Decline_truncates_overly_long_reason_to_500 | Reason >500 → cắt 500. |
| Decline_fails_when_booking_not_scheduled | Booking hết hiệu lực → fail. |
| Decline_is_locked_after_confirm | Đã confirm → không decline được. |
| Decline_by_non_owner_is_forbidden | Không sở hữu → Forbidden. |

### DismissDeclinedScheduleCommandHandler (4)
| Test | Kiểm chứng |
|---|---|
| Dismiss_marks_declined_booking_hidden | Ẩn booking declined (CandidateDismissedAt). |
| Dismiss_rejects_active_scheduled_booking | Booking scheduled active → fail. |
| Dismiss_is_idempotent_when_already_hidden | Đã ẩn → không ghi đè timestamp. |
| Dismiss_by_non_owner_is_forbidden | Không sở hữu → Forbidden. |

## A8. Auth + Common + Domain (12)

### StaffLoginCommandValidator (2)
| Test | Kiểm chứng |
|---|---|
| Missing_email_or_password_fails_with_original_message | Thiếu email/password → thông điệp verbatim. |
| Valid_credentials_pass | Hợp lệ → IsValid. |

### TokenHashing (3)
| Test | Kiểm chứng |
|---|---|
| Sha256Base64_matches_known_vector | Base64 SHA-256 khớp vector. |
| Sha256Hex_matches_known_vector_and_is_uppercase | HEX SHA-256 hoa khớp vector. |
| Formats_differ_for_same_input | Base64 ≠ HEX cùng input. |

### ValidationBehaviour (4)
| Test | Kiểm chứng |
|---|---|
| Invalid_request_returns_failure_result_without_throwing | Request lỗi → Result.Failure (không throw). |
| Invalid_request_with_generic_result_returns_typed_failure | Result&lt;T&gt; → typed failure. |
| Valid_request_invokes_next_handler | Hợp lệ → gọi next. |
| No_validators_invokes_next_handler | Không validator → bỏ qua, gọi next. |

### Domain Entity Defaults (3)
| Test | Kiểm chứng |
|---|---|
| AppRoles_values_are_stable | AppRoles constants đúng giá trị. |
| New_user_defaults_active_recruiter | User mới IsActive, Role 'recruiter', DeletedAt null. |
| New_job_posting_is_soft_deletable | JobPosting hỗ trợ ISoftDelete. |

---

# PHẦN B — TEST CASE CÒN THIẾU / ĐỀ XUẤT (131)

## 🔴 Ưu tiên CAO

### B1. GenerateEvaluationReportAsync — buổi thật (`InterviewService.cs`)
| Test case (Arrange/Act) | Kỳ vọng |
|---|---|
| Job vi, App 'interview', REAL session R1, StubAI Verdict='not_pass' → EndSessionAsync(id,'completed'). | 1 Evaluation real, AiVerdict='not_pass', OverallScore từ AI; **App.Status VẪN 'interview'** (ADR-053: AI không ghi status kể cả khi not_pass). |
| REAL session completed có Q+A → EndSessionAsync. | GroupEvents chứa (hr_admin, ReceiveSystemEvent) payload 'AiEvaluationComplete' (EvaluationId+AppId). Đối chứng: PRACTICE **không** phát event hr_admin. |
| REAL session, seed CheatDetectionSignal 1×fullscreen_exit, 2×tab_hidden, 1×unknown → EndSession. | Evaluation.CheatScore==37 (8+12·2+5, cap 100); CheatSignals JSON gộp theo loại (không "[]"). |
| REAL session vi, session KHÔNG có câu trả lời → EndSession. | LanguageAssessment==null, AssessLanguageProficiency **không** gọi. Đối chứng: có answer → LanguageAssessment JSON (cefr_level 'B2'…) + gọi 1 lần. |

### B2. SaveRecordingAsync — UploadRecordingCommand (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| PRACTICE session → SaveRecordingAsync(10 bytes,'a.webm','video/webm'). | Failure "Phỏng vấn thử không quay video." (ADR-038.6); storage rỗng; RecordingUrl null. |
| REAL session → content rỗng (length 0). | Failure "Dữ liệu ghi hình rỗng."; không ghi storage. |
| REAL, MaxRecordingSizeMb=1, content 2MB. | Failure chứa "vượt quá 1MB"; không lưu; RecordingUrl không set. |
| REAL, default (retention 7d, 300MB), contentType 'video/webm;codecs=vp9,opus'. | Success; storage lưu baseContentType 'video/webm'; RecordingUrl=key, RecordingSizeBytes=len, RecordingExpiresAt≈now+7d, DeletedAt null; response Saved=true. (Biến thể: có RecordingUrl cũ → DeleteAsync key cũ trước.) |

### B3. RecordCheatSignalAsync — ReportCheatSignalCommand (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| signalType='   ' (whitespace), payload=null. | Failure "Thiếu loại tín hiệu."; không lưu; guard chạy trước fetch. |
| Không seed session → RecordCheatSignal(newId,'tab_hidden',null). | Failure "Không tìm thấy phiên phỏng vấn."; repo rỗng. |
| Session + 1 signal 'fullscreen_exit' cũ → RecordCheatSignal('  FULLSCREEN_EXIT ',null). | Success count==2; signal mới SignalType='fullscreen_exit' (trim+lower), Payload='{}'. |
| payload='DROP TABLE users' (a); payload 2500 ký tự (b). | Cả 2 normalize Payload='{}' (sai prefix / >2000). Payload '{"x":1}' hợp lệ → giữ nguyên. |

### B4. RescheduleBookingAsync (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| Không seed booking. | Failure "Không tìm thấy lịch phỏng vấn."; không mutate slot. |
| target == booking.AvailabilitySlotId. | Failure "Ứng viên đã nằm trong ca này rồi." |
| booking R1, target slot R2 tương lai còn chỗ. | Failure "Không thể dời sang ca phỏng vấn thuộc vòng thi khác." (guard anh em: quá khứ / đầy). |
| booking declined trên slot cũ (Booked=1), target cùng vòng tương lai còn chỗ, có account. | Success; oldSlot.Booked=0, target.Booked=1; booking→target, Confirmation='pending', Status='scheduled', DeclineReason=null, RespondedAt=null; notif 'schedule_rescheduled' + realtime InterviewRescheduled; email best-effort. |

### B5. UpdateOnlineTestQuestionCommandHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| uow rỗng + request single nhưng CorrectOptions={0,1}. | Failure chứa 'đúng 1 đáp án'; SaveChanges==0 (validate TRƯỚC GetById). |
| Job của ownerA + 1 câu → update hợp lệ bởi recruiter khác. | Failure Forbidden 'Bạn không có quyền sửa câu hỏi này.'; không đổi; SaveChanges==0. |
| owner update QuestionText='  Edited?  ', multiple, CorrectOptions={2,0,2}. | Success; DTO text 'Edited?' (trim), CorrectOptions [0,2]; entity lưu '[0,2]', CorrectOption=0, UpdatedAt≈now; SaveChanges==1. |

### B6. UpdateOnlineTestSettingsCommandHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| uow rỗng + PassScore=101 (và -1). | Failure 'Điểm sàn phải nằm trong khoảng 0–100.'; SaveChanges==0 (validate trước lookup). |
| Job hợp lệ + QuestionsPerTest=0/201; Duration=0/301. | PerTest ngoài [1,200] → 'Số câu mỗi bài phải từ 1 đến 200.'; Duration ngoài [1,300] → 'Thời lượng phải từ 1 đến 300 phút.'; SaveChanges==0. |
| owner boundary 100/200/300 (và 0/1/1). | Success; job cập nhật 3 field + UpdatedAt; SaveChanges==1; DTO phản ánh giá trị mới. |
| Job ownerA + settings hợp lệ nhưng recruiter khác. | Failure Forbidden 'Bạn không có quyền cấu hình bài thi của tin này.' |

### B7. GetOnlineTestResultForStaffQueryHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| Job(owner) + App nhưng KHÔNG submission. | Success với Value==null (chưa thi) — khác NotFound. |
| Job(owner,pass70) + App + 2 submission (cũ 40 fail, mới 88 pass). | Success; DTO lấy submission MỚI NHẤT: Score 88, IsPassed, PassScore 70 (từ job). |
| Job ownerA + submission → recruiter khác; và app lạ. | Non-owner → Forbidden; app lạ → NotFound. |

### B8. GetOnlineTestResultsByJobQueryHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| Job ownerA → recruiter khác. | Forbidden 'Bạn không có quyền xem điểm của tin này.' |
| Job(owner,pass70) + 3 câu bank + 3 App + 3 submission (90,50,70). | Success; TotalQuestions=3, SubmissionCount=3, Passed=2, NotPassed=1, Avg=70.0, High=90, Low=50; Rows sort Score desc + name/email/TabSwitchCount. |
| Job(owner) + 2 câu + 2 App nhưng KHÔNG submission. | Success; count/avg/high/low=0, Rows rỗng; TotalQuestions=2 (bank). |

### B9. ImportOnlineTestQuestionsCommandHandler (`OnlineTestImportFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| Job ownerA → recruiter khác; và job lạ. | Non-owner → Forbidden, không đọc file; job lạ → NotFound. |
| Job(owner) + FileBytes rác (không phải xlsx). | Failure 'Không đọc được file Excel...'; SaveChanges==0. |
| .xlsx: header + row valid single (correct 'B') + row remap (C,E filled, D blank, correct 'A,C'). | Success; Imported=2, Failed=0; header skip; row2 CorrectOptions '[1]'; row3 remap qua D trống → '[0,1]', type 'multiple'. |
| .xlsx: header + 1 valid + row 1 option + row correct trỏ option trống + row thiếu correct + row trống. | Success partial; Imported=1, Failed=3, Errors đúng Row 3/4/5 + thông điệp; row trống bỏ qua; SaveChanges==1. |

### B10. ApplyToJobCommand (`PortalApplicationsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| Đã có Application (không withdrawn) cùng (job,candidate) → apply lại kèm CV. | Success AlreadyApplied=true, ExistingApplicationId set, Application=null; không lưu CV, không gọi SubmitApplication. |
| Happy path CV đính kèm, chưa có app. | Success AlreadyApplied=false; lưu 1 file; SubmitApplication source 'job_board', cv_submitted, CvFileHash=MD5; name/phone trim. |
| Không attach + không profile CV. | Failure code 'no_cv' 'Bạn cần tải CV...'; không lưu/không gọi service. |
| CV attach + job closed (service fail). | CV lưu rồi bị DeleteAsync (compensating); Failure propagate; không tạo app. |

### B11. VerifyCvInfoCommand — mismatch Flow 3 (`PortalApplicationsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| CV chứa cả name + phone; name='Nguyen Van A', phone='0900000000'. | Success IsMatch=true, MismatchDetails=null; so khớp bỏ dấu/thường + 9 số cuối; 0 token AI. |
| CV có phone nhưng KHÔNG name; name='Tran Thi B'. | Success IsMatch=false, MismatchDetails có bullet 'Họ và tên'; không có dòng phone. |
| CV có name, phone khác; phone='0912345678'. | IsMatch=false, bullet 'Số điện thoại'; phone <8 số bị bỏ qua. |
| CV không đọc được / rỗng. | Success IsMatch=true (guard). Kèm: acc null → Unauthorized; thiếu CV → 'no_cv'; CV đọc rỗng → 'cv_unreadable'. |

### B12. GetMyEvaluationQuery — IDOR + share gate (`PortalApplicationsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| App owned by owner → gọi bằng account/email khác (không auto-link). | Failure Forbidden 'Forbidden'; không lộ eval. |
| Owner nhưng HrReview.ShareEvaluation=false. | Failure 'Kết quả đánh giá chi tiết chưa được chia sẻ...'; không NotFound. |
| Owner + ShareEvaluation=true. | Success trả eval (AiVerdict, OverallScore, criterion, QuestionAnalyses, LanguageAssessment...). Kèm: session thiếu → NotFound; eval thiếu → NotFound 'chưa được khởi tạo'. |

### B13. Auth core — high (`Auth/Commands/*`)
**VerifyMagicLinkCommandHandler**
| Test case | Kỳ vọng |
|---|---|
| Không candidate khớp email. | Failure 'Candidate account not found.' code not_found; không mint token. |
| Candidate tồn tại, token service trả 'jwt-123'. | Success Value='jwt-123'; mint 1 lần. |
| ⚠ Token rác/hết hạn (handler KHÔNG kiểm token). | Vẫn Success JWT — **ghi nhận gap chủ ý** (không validate TTL/one-time). |
| Email '  USER@X.com '. | Normalize khớp candidate → Success (không NotFound giả). |

**CandidateLoginCommandHandler**
| Test case | Kỳ vọng |
|---|---|
| Không account. | Failure 'Sai email hoặc mật khẩu.' InvalidCredentials; không Verify. |
| PasswordHash rỗng (Google account). | Failure 'đăng ký qua Google...' PasswordlessGoogle. |
| EmailVerified=false, password đúng. | Failure 'chưa được xác minh...' EmailNotVerified; không cấp token. |
| EmailVerified=true, đúng. | Success; LastLoginAt set; refresh token; Role Candidate; FullName fallback 'Candidate'. |

**StaffLoginCommandHandler**
| Test case | Kỳ vọng |
|---|---|
| Email chưa pre-provision. | Failure 'Sai email hoặc mật khẩu.' InvalidCredentials; không tạo draft. |
| IsActive=false. | Failure 'đã bị vô hiệu hóa...' AccountDisabled; check trước Verify. |
| PasswordHash rỗng (SSO-only). | Failure 'chỉ hỗ trợ đăng nhập qua SSO.' SsoOnly. |
| Verify false / true. | Sai → InvalidCredentials; đúng → LastLoginAt, JWT staff, refresh, Role=user.Role. |

**RegisterCandidateCommandHandler**
| Test case | Kỳ vọng |
|---|---|
| Email đã tồn tại. | Failure 'Email already registered.'; không lưu/không email. |
| Password yếu (không special / <8). | Failure đúng thông điệp IsStrongPassword; không lưu. |
| Email free + password mạnh. | Success; account Email lower, EmailVerified=false; gửi MagicLink CandidateEmailVerify TTL~24h + 1 email. |
| Email trùng + password yếu. | Trả lỗi trùng email (check trước strength). |

**VerifyCandidateEmailCommandHandler**
| Test case | Kỳ vọng |
|---|---|
| Không candidate. | Failure 'Liên kết xác minh không hợp lệ.' |
| EmailVerified=true sẵn. | Success idempotent 'đã được xác minh trước đó...' |
| MagicLink hợp lệ chưa dùng chưa hết hạn. | Success; EmailVerified=true; magicLink.UsedAt set (one-time). |
| MagicLink hết hạn/đã dùng/sai audience/sai hash. | Failure 'không hợp lệ hoặc đã hết hạn...'; giữ false. |

### B14. Admin lifecycle — high
**CreateStaffUserCommandHandler** (`CreateStaffUserCommand.cs`)
| Test case | Kỳ vọng |
|---|---|
| Role='super_admin'/null. | Failure "Role phải là 'hr_admin' hoặc 'recruiter'."; không tạo/không audit/không email. |
| Email trùng (' A@X.com '). | Normalize 'a@x.com' → Conflict 'Email này đã được sử dụng...' |
| Email/Name/Role hợp lệ, Role='HR_Admin'. | Success; User email lower, role lower, IsActive, PasswordHash≠plaintext; AuditLog 'staff_account_created'; SaveChanges 1; welcome email; DTO echo. |
| FullName='   '. | Failure 'Họ và tên là bắt buộc.'; short-circuit. |

**ApproveAccountRequestCommandHandler** (`ApproveAccountRequestCommand.cs`)
| Test case | Kỳ vọng |
|---|---|
| Request lạ. | Failure NotFound 'Không tìm thấy yêu cầu.' |
| Request Status='approved'. | Failure 'Yêu cầu này đã được xử lý.' |
| Request pending + email đã có User. | Failure Conflict 'Email này đã có tài khoản...'; giữ pending. |
| Request pending không trùng User. | Success; tạo User; req approved + ReviewedBy/At + CreatedUserId; audit 'account_request_approved'; email; notify leader Status 'approved'. |

**DeactivateUserCommandHandler** (`DeactivateUserCommand.cs`)
| Test case | Kỳ vọng |
|---|---|
| ActorId==Id. | Failure "Bạn không thể khóa chính tài khoản của mình."; guard trước load. |
| Reason null/whitespace. | Failure "Vui lòng nhập lý do khóa tài khoản." |
| User đã IsActive=false. | Failure "Tài khoản đã bị khóa." |
| User active + reason. | Success; IsActive=false, LockReason trim, UpdatedAt; audit 'user_deactivated' (metadata reason); SaveChanges 1. |

**UploadPlaybookCommandHandler** (`UploadPlaybookCommand.cs`)
| Test case | Kỳ vọng |
|---|---|
| Parser throw. | Failure "Không thể đọc nội dung file:..."; không lưu/không ingest. |
| Parser OK + storage OK, Ext='.pdf', Scope='job'. | Success; PlaybookDocument Status='ready', FileFormat='pdf', DocumentType trim; Ingest 1 lần (playbook, scope, type). |
| Parser trả rỗng. | Success; vẫn lưu document nhưng KHÔNG ingest (guard). |
| Parser OK, storage OK, Ingest throw. | Failure ServerError "Xử lý playbook thất bại:..."; DeleteAsync(key) compensating. |

## 🟡 Ưu tiên TRUNG BÌNH

### B15. SendBookingReminderAsync (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| Không booking (hoặc thiếu App/Slot). | Failure tương ứng; Reminder24hSent=false; không notif. |
| Booking R1 chưa nhắc + App có account. | Success; notif 'schedule_reminder' + realtime; email best-effort; Reminder24hSent=true + SaveChanges. |

### B16. GenerateBatchAsync — GenerateInterviewCodeBatchCommand (`InterviewCodeService.cs`)
| Test case | Kỳ vọng |
|---|---|
| null / empty list. | Failure 'Danh sách ApplicationId không được để trống.'; không tạo code. |
| 2 app đều có booking scheduled R2. | Success 2 code (RoundNumber=2, code khác nhau); 2 audit 'interview_code_generated'. |

### B17. GetInterviewJobsAsync (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| Không job. | List rỗng. |
| Job active deadline quá khứ + 2 slot (R1,R2) + 2 booking (1 confirmed) + 1 session completed. | 1 DTO: JobStatus 'closed', TotalSlots 2, Booked 2, Confirmed 1, MaxRound 2, Sessions 1, Completed 1, NextSlotTime=slot tương lai. |

### B18. GetCandidatesInSlotAsync (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| Booking của App 'not_pass' (booking confirmed/scheduled). | DTO override: Confirmation 'declined', DeclineReason 'Đã bị loại khỏi quy trình...', BookingStatus 'cancelled'. |
| Booking App active có REAL session + Eval (score 72.6) + code chưa dùng chưa hết hạn. | DTO map SessionId/Status/Duration, Verdict, OverallScore 73 (round), InterviewCode + expiry; loại practice. |

### B19. GetMediaConfigAsync (`InterviewService.cs`)
| Test case | Kỳ vọng |
|---|---|
| Không session. | Failure 'Không tìm thấy phiên phỏng vấn.' (App thiếu → 'Không tìm thấy hồ sơ...'). |
| Session + App owned by A → gọi bằng B, không kiosk. | Failure 'Bạn không có quyền truy cập...' |
| REAL session, kiosk=true, providers throw. | Success (kiosk bypass); MaxDurationSeconds 1200 (20' — trần gói LiveAvatar Essential), Deepgram/HeyGen null (nuốt lỗi). |
| PRACTICE owned by A, PracticeUseAvatar=false. | Success; HeyGen null (không mint avatar practice); MaxDurationSeconds 1200 (20'). |

### B20. SendInterviewInviteCommand (`ApplicationCommands.cs`)
| Test case | Kỳ vọng |
|---|---|
| Config CandidateBaseUrl set, RoundNumber=2. | Gọi service với appId + baseUrl + round 2; trả kết quả nguyên. |
| Thiếu cả 2 config base URL, service fail. | baseUrl fallback 'http://localhost:3000'; propagate Failure nguyên message. |

### B21. GetOnlineTestBankQueryHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| uow rỗng. | Failure NotFound 'Không tìm thấy tin tuyển dụng.' |
| Job ownerX → recruiter khác. | Failure Forbidden 'Bạn không có quyền quản lý câu hỏi của tin này.' |
| Job(owner,70/50/30) + 2 câu (1 type blank). | Success; DTO đủ setting, Questions sort CreatedAt, blank→'single', lộ đáp án cho staff. |

### B22. DeleteOnlineTestQuestionCommandHandler (`OnlineTestQuestionsFeature.cs`)
| Test case | Kỳ vọng |
|---|---|
| Job ownerA → recruiter khác; và id lạ. | Non-owner → Forbidden, còn câu; id lạ → NotFound. |
| owner (hoặc HrAdmin) xoá. | Success; repo rỗng; SaveChanges 1. |

### B23. GetHrDashboardQueryHandler (`GetHrDashboardQuery.cs`)
| Test case | Kỳ vọng |
|---|---|
| Mọi repo rỗng. | Success mọi số 0; Funnel 5 bước 0; Trend đúng 14 điểm 0; TopJobs rỗng. |
| 4 job: A active(dl mai), B active(dl qua), C draft, D pending. | ActiveJobs 1 (A), DraftJobs 1, PendingJobsCount 1; B Status project 'closed'; D vào PendingJobs. |
| 3 Eval + 1 HrReview FinalVerdict='Pass'. | Hired 1 (case-insensitive), PendingReviews 2. |
| 8 App, 1 có analysis MatchScore 88 + 2 Eval (R2 pass). | RecentCandidates 6 (mới nhất trước); MatchScore 88; LatestRound 2, verdict 'pass'; app không analysis → null. |

### B24. Admin lifecycle — medium
| Handler | Test / Kỳ vọng |
|---|---|
| RejectAccountRequestCommandHandler | Reject reason → Success rejected + audit 'account_request_rejected' + notify leader; reason rỗng → Failure 'Vui lòng nhập lý do từ chối.' |
| ActivateUserCommandHandler | User inactive → IsActive=true + clear LockReason + audit 'user_activated'; đã active → Failure 'Tài khoản đã đang hoạt động.' |
| DeleteUserCommandHandler | ActorId==Id → Failure 'không thể xóa chính...'; happy → Delete + audit 'user_deleted'. |
| UpdateUserRoleCommandHandler | Role lạ → Failure; hợp lệ → lower + audit 'user_role_updated'; ActorId==Id → 'không thể đổi vai trò của mình'. |
| UpdateSystemSettingsCommandHandler | Update key có sẵn + insert key mới + audit 'system_settings_updated'; Items rỗng → Failure 'Danh sách cài đặt trống.' |

## ⚪ Ưu tiên THẤP

### B25. Session read phụ
| Handler | Test / Kỳ vọng |
|---|---|
| GetSlotsForJobAsync | Không slot → rỗng; slot có 3 booking (confirmed/declined/pending) → BookedCount 3, Confirmed/Declined/Pending 1, IsPast=true. |
| GetSpeechAudioAsync | Text whitespace → Success('') short-circuit; owned by A gọi bằng B → Forbidden; TTS throw → Success('') fallback. |

### B26. ExportOnlineTestResultsQueryHandler (`OnlineTestExportFeature.cs`)
| Test / Kỳ vọng |
|---|
| Non-owner → Forbidden; job lạ → NotFound. |
| Job(owner,'Backend Developer') + 1 submission → Success; ContentType xlsx; FileName 'bang-diem-trac-nghiem-backend-developer-{yyyyMMdd}.xlsx'; Content mở lại được (TabSwitch 0 → '-'). |

### B27. AnalyzeCvCommand (`AnalyzeCvCommand.cs`)
| Test / Kỳ vọng |
|---|
| Delegate: gọi AnalyzeAndCacheAsync đúng (jobId, stream, fileName, ct); trả nguyên Result.Success. |
| Service Failure → propagate nguyên message, không throw. |

### B28. Auth phụ (`Auth/Commands/*`)
| Handler | Test / Kỳ vọng |
|---|---|
| RefreshStaffTokenCommandHandler | Token không khớp → Failure InvalidCredentials; hợp lệ → revoke cũ + JWT+refresh mới, Role=user.Role. |
| RefreshCandidateTokenCommandHandler | Token không khớp → Failure; hợp lệ → revoke + token mới, Role Candidate. |
| StaffForgotPasswordCommandHandler | Luôn Success (anti-enum); user active → MagicLink Staff TTL~2h + email; else không gì. |
| CandidateForgotPasswordCommandHandler | Luôn Success; candidate có → MagicLink Candidate + email; không có → không gì. |
| StaffResetPasswordCommandHandler | Token hợp lệ → đổi hash + UsedAt; user thiếu → 'Invalid email or recovery token.'; token xấu → 'Invalid, expired, or already used...'; password yếu → IsStrongPassword. |
| CandidateResetPasswordCommandHandler | Tương tự Staff (audience Candidate). |
| LogoutCommandHandler | Luôn Success; null → no-op; token khớp staff/candidate → RevokedAt set; unknown → no-op. |
| ResendCandidateVerificationCommandHandler | Luôn Success; chỉ chưa verify → gửi MagicLink + email; else không gì. |

### B29. Admin read phụ
| Handler | Test / Kỳ vọng |
|---|---|
| ApproveUserCommandHandler | inactive → IsActive=true (không đụng LockReason) + audit 'user_approved'; thiếu → NotFound; đã active → 'User already active.' |
| GetAuditLogsQueryHandler | Lọc Action + sort desc; PageSize clamp 100; actor null → 'Hệ thống'. |
| GetUsersQueryHandler | Lọc role+active, paged desc, item có LockReason. |
| GetPendingUsersQueryHandler | Chỉ user IsActive=false → PendingUserDto. |
| GetAdminStatsQueryHandler | Đếm TotalUsers/Active/Locked/SuperAdmins/HrAdmins/Recruiters/Candidates/PendingRequests. |
| DeletePlaybookCommandHandler | Soft-delete (DeletedAt+UpdatedAt); id lạ → NotFound 'Không tìm thấy playbook.' |
| GetPlaybooksQueryHandler | Lọc scope (case-insensitive), desc, bỏ ParsedText, resolve uploader name. |

---

## Ghi chú kỹ thuật

- Hạ tầng test tái dùng: `TestSupport/InMemoryUnitOfWork` (LINQ-to-objects, `Seed()`, đếm `SaveChangesCount`), `RecordingNotificationService/EmailService/FileStorage`, `InterviewServiceFactory`/`ApplicationServiceFactory` + `StubAiProvider`. Không dùng mocking lib.
- Các case B1–B4 (buổi thật) cần overload `InterviewServiceFactory.Create(...)` inject `RecordingFileStorage` + options (MaxRecordingSizeMb/RecordingRetentionDays/RealMaxDurationMinutes).
- Sau khi bổ sung: chạy `dotnet test` và cập nhật số pass trong `.ai/tasks.md`.
