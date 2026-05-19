# ARISP – Function / Screen List

> Danh sách toàn bộ màn hình & chức năng của dự án ARISP.
> Cập nhật khi có màn hình mới hoặc thay đổi scope.

**Complexity rules:**
- **Simple** : ≤ 7 fields · ≤ 3 transactions
- **Medium** : ≤ 16 fields · ≤ 7 transactions
- **Complex** : > 15 fields · > 7 transactions

_Fields = thành phần trên màn hình hoặc cột DB liên quan._
_Transactions = action buttons, user actions, DB operations._

---

| # | Function / Screen | Feature | Level | Function / Screen Details | Planned | Status |
|---|-------------------|---------|-------|---------------------------|---------|--------|
| 0 | Landing / Home Page | Common | Simple | Trang giới thiệu sản phẩm ARISP: hero section, feature highlights, CTA đăng ký dùng thử / liên hệ sales | Iteration 8 | Pending |
| 1 | HR Admin Login | Auth | Simple | Đăng nhập bằng email + password. JWT issue + refresh token. Redirect đến HR Dashboard sau login thành công | Iteration 1 | Pending |
| 2 | SSO Login | Auth | Medium | Đăng nhập qua Google Workspace, Microsoft Entra (OpenID Connect), hoặc SAML 2.0. Per-organization SSO config. Redirect IdP → callback → JWT issue | Iteration 8 | Pending |
| 3 | Reset Password | Auth | Simple | HR Admin nhập email → nhận link reset → đặt password mới. Link TTL 30 phút, one-time-use | Iteration 1 | Pending |
| 4 | User Profile | Auth | Simple | HR Admin xem/sửa thông tin cá nhân: tên, email, department, avatar. Không thể đổi role | Iteration 1 | Pending |
| 5 | Change Password | Auth | Simple | HR Admin đổi password: nhập mật khẩu hiện tại → nhập mới → xác nhận. BCrypt validation | Iteration 1 | Pending |
| 6 | Job Posting List | Job Management | Medium | Danh sách Job Posting của Organization. Filter theo status (draft/active/closed), department, ngày tạo. Sort. Pagination. Badge số lượng Applications per posting | Iteration 2 | Pending |
| 7 | Create Job Posting | Job Management | Complex | HR tạo tin tuyển dụng: title, department, JD (rich text), interview mode (remote/onsite/both), số vòng, loại vòng, ngôn ngữ, scoring rubric, persona AI, invite TTL, reschedule deadline. Trigger language detection khi submit | Iteration 2 | Pending |
| 8 | Edit Job Posting | Job Management | Complex | Sửa Job Posting đang ở trạng thái draft hoặc active. Các field giống Create. Cảnh báo nếu đang có Application đang xử lý | Iteration 2 | Pending |
| 9 | Language Detection Confirm | Job Management | Simple | Hiển thị kết quả AI detect ngôn ngữ từ JD: ngôn ngữ phát hiện, requirement text, confidence score. HR confirm hoặc chỉnh lại trước khi publish | Iteration 2 | Pending |
| 10 | Round Configuration | Job Management | Medium | Cấu hình chi tiết từng vòng phỏng vấn per Job Posting: round_number, round_type (screening/technical), ngôn ngữ override, max duration, interview code TTL (on-site) | Iteration 2 | Pending |
| 11 | Availability Slots Management | Scheduling | Medium | HR tạo/sửa/xóa khung giờ Remote interview per Job Posting per round: start/end time, timezone, capacity. Hiển thị booked_count. Disable slot đã hết chỗ | Iteration 3 | Pending |
| 12 | Playbook Document List | Playbook | Medium | HR xem danh sách tài liệu Playbook per Organization / Job Posting / Round. Filter theo scope và document_type. Status processing/ready/error. Xóa document | Iteration 4 | Pending |
| 13 | Playbook Document Upload | Playbook | Medium | HR upload tài liệu nội bộ: chọn scope (org/job_posting/round), document_type, file (PDF/DOCX/TXT/MD/JSON). Preview parsed text sau khi xử lý xong | Iteration 4 | Pending |
| 14 | Application List | HR Dashboard | Medium | Danh sách Applications per Job Posting. Filter theo status, round hiện tại. Sort theo ngày apply, score. Quick view: tên, email, CV, round hiện tại, verdict mới nhất. Export CSV | Iteration 5 | Pending |
| 15 | Application Detail | HR Dashboard | Complex | Xem toàn bộ thông tin Application: thông tin cá nhân, CV parsed text, lịch sử tất cả rounds, trạng thái từng vòng, link vào Evaluation Report từng vòng | Iteration 5 | Pending |
| 16 | Interview Code Generator | On-site | Medium | HR generate Interview Code cho Candidate on-site: chọn Application, chọn round, set TTL → code format ARX-7K2P. Batch generate cho nhiều ứng viên. Copy/print code | Iteration 3 | Pending |
| 17 | Interview Code List | On-site | Simple | Danh sách Interview Codes đã tạo: code, application, round, expires_at, trạng thái (unused/used/expired) | Iteration 3 | Pending |
| 18 | Evaluation Report | Evaluation | Complex | HR xem báo cáo AI sau mỗi round: Verdict (Pass/Not Pass), Overall Score, per-criterion scores, language assessment (nếu có), per-question analysis, CheatScore + cheat signals, recommended next step. Xem recording + transcript | Iteration 5 | Pending |
| 19 | HR Review – Confirm / Override | Evaluation | Medium | HR confirm hoặc override verdict AI: chọn final verdict, nếu override phải nhập override_reason. Config share settings: recording/transcript/evaluation/feedback visible to candidate | Iteration 5 | Pending |
| 20 | HR Notification Center | HR Dashboard | Simple | In-app notifications (SignalR): evaluation hoàn thành cần review, candidate schedule/reschedule. Đánh dấu đã đọc. Link thẳng đến Evaluation Report | Iteration 5 | Pending |
| 21 | HR Team Management | Enterprise Admin | Medium | HR Admin mời thành viên HR mới vào Organization qua email. Phân quyền theo department. Deactivate member. Xem danh sách team | Iteration 8 | Pending |
| 22 | Audit Log Dashboard | Enterprise Admin | Medium | Xem toàn bộ audit trail: confirm/override, interview code generated/used, login events, config changes. Filter theo action type, actor, date range. Chỉ SuperAdmin và HR Admin xem được | Iteration 8 | Pending |
| 23 | Subscription & Billing | Enterprise Admin | Complex | Xem plan hiện tại, usage (sessions used / limit, storage), billing period. Invoice history. Upgrade/downgrade plan. Chỉ Organization Admin truy cập | Iteration 8 | Pending |
| 24 | ATS Webhook Config | Integrations | Medium | HR Admin cấu hình ATS webhook URL + secret per Organization. Test webhook. Xem webhook delivery log (success/failure, retry count) | Iteration 9 | Pending |
| 25 | SSO Configuration | Integrations | Complex | HR Admin cấu hình SSO per Organization: chọn provider (Google/Microsoft/SAML), nhập IdP metadata / client ID+secret. Test SSO flow. Enable/disable | Iteration 9 | Pending |
| 26 | Notification Integration Config | Integrations | Simple | HR Admin cấu hình Slack/Teams webhook URL để nhận thông báo khi có Evaluation cần Review hoặc Candidate schedule/reschedule | Iteration 9 | Pending |
| 27 | SuperAdmin – Organization List | SuperAdmin | Medium | SuperAdmin xem toàn bộ Organizations trên platform. Filter theo plan, status. Kích hoạt/khóa Organization | Iteration 8 | Pending |
| 28 | SuperAdmin – Platform Dashboard | SuperAdmin | Complex | Tổng quan platform: số org active, số sessions hôm nay/tháng, tổng revenue, top organizations by usage. Charts | Iteration 9 | Pending |
| 29 | Candidate – Application Form | Candidate | Medium | Candidate nhận invite link → điền thông tin cá nhân (tên, email, phone) → upload CV (PDF). Opt-in demographic data. Submit hồ sơ ứng tuyển | Iteration 2 | Pending |
| 30 | Candidate – Schedule Interview | Candidate | Simple | Candidate chọn slot phỏng vấn Remote từ danh sách Availability Slots còn chỗ. Confirm booking → nhận email xác nhận + reminder | Iteration 3 | Pending |
| 31 | On-site Kiosk – Code Entry | On-site Kiosk | Simple | Màn hình kiosk tại văn phòng: Candidate nhập Interview Code → validate (TTL, one-time-use, binding) → redirect vào Interview Room | Iteration 3 | Pending |
| 32 | Interview Room | Interview | Complex | Màn hình phỏng vấn AI: HeyGen Avatar (WebRTC), audio stream (VAD + Google STT), câu hỏi AI (GPT-4o streaming), TTS ElevenLabs streaming. Hiển thị timer, round info, cheat signal collectors (eye tracking, tab switch). Không cho phép copy/paste | Iteration 4 | Pending |
| 33 | Candidate Portal – Login | Candidate Portal | Simple | Candidate đăng nhập Candidate Portal bằng email → nhận magic link (TTL 15 phút, one-time-use) → click link → vào portal | Iteration 7 | Pending |
| 34 | Candidate Portal – My Applications | Candidate Portal | Simple | Candidate xem danh sách Applications của mình: vị trí, công ty, trạng thái từng round, kết quả (nếu HR đã share) | Iteration 7 | Pending |
| 35 | Candidate Portal – Interview Result | Candidate Portal | Medium | Candidate xem kết quả một round (nếu HR bật share): recording, transcript, evaluation report (phần HR cho phép), feedback text của HR | Iteration 7 | Pending |
| 36 | Analytics Dashboard | Analytics | Complex | HR Admin xem báo cáo: pass rate per Job Posting / round, score distribution, language proficiency benchmark, time-to-hire, cheat detection aggregate stats. Charts, filter theo period | Iteration 9 | Pending |
| 37 | Fairness / Bias Report | Analytics | Complex | Phân tích kết quả theo demographic groups (opt-in). Phát hiện disparate impact. Fairness score per Job Posting. Chỉ SuperAdmin và HR Admin với quyền truy cập | Iteration 9 | Pending |

---

## Tổng hợp theo Iteration

| Iteration | Phase tương ứng | Số màn hình |
|-----------|----------------|-------------|
| Iteration 1 | Phase 1 – Auth & Multi-tenant | #1, #2 (SSO basic), #3, #4, #5 |
| Iteration 2 | Phase 2 – Job Posting & Application | #6, #7, #8, #9, #10, #29 |
| Iteration 3 | Phase 3 – Scheduling & Interview Code | #11, #16, #17, #30, #31 |
| Iteration 4 | Phase 4 & 4b – AI Interview + Playbook | #12, #13, #32 |
| Iteration 5 | Phase 5 & 6 – Multi-round + Evaluation + HR Review | #14, #15, #18, #19, #20 |
| Iteration 6 | Phase 7 & 8 – Media & Cheat Detection | (tích hợp vào #32, signals trong #18) |
| Iteration 7 | Phase 9 – Candidate Portal | #33, #34, #35 |
| Iteration 8 | Phase 10 – Enterprise Admin | #0, #2 (SSO full), #21, #22, #23, #27 |
| Iteration 9 | Phase 11–13 – Integrations + Analytics | #24, #25, #26, #28, #36, #37 |

---

## Tổng hợp theo Feature Module

| Feature | Số màn hình |
|---------|------------|
| Auth | 5 (#1–5) |
| Job Management | 4 (#6–10) |
| Scheduling | 3 (#11, #30) |
| Playbook | 2 (#12–13) |
| HR Dashboard | 4 (#14–15, #18–20) |
| On-site | 3 (#16–17, #31) |
| Evaluation | 2 (#18–19) |
| Enterprise Admin | 3 (#21–23) |
| Integrations | 3 (#24–26) |
| SuperAdmin | 2 (#27–28) |
| Candidate | 2 (#29–30) |
| Candidate Portal | 3 (#33–35) |
| Interview | 1 (#32) |
| Analytics | 2 (#36–37) |
| Common | 1 (#0) |
