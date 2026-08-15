# Architecture – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

## Tổng quan kiến trúc (Strictly Single-tenant)

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Client Layer                                │
│            React + TypeScript + TailwindCSS                         │
│                                                                     │
│  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────────┐   │
│  │  HR Admin Portal│  │ Candidate Portal  │  │ On-site Kiosk    │   │
│  │ (Job Posting,   │  │ (Schedule, View   │  │ (Interview Code  │   │
│  │  Review, Audit) │  │  Recording, Feed) │  │  entry point)    │   │
│  └────────┬────────┘  └────────┬─────────┘  └────────┬─────────┘   │
│           │                   │                      │              │
│  ┌────────▼───────────────────▼──────────────────────▼──────────┐  │
│  │           Interview Room (AI Session UI)                      │  │
│  │   App UI / SignalR (events) + WebRTC Peer Conn (media)        │  │
│  └──────────────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ HTTPS / WS / WebRTC
┌──────────────────────────▼──────────────────────────────────────────┐
│                     Nginx (Reverse Proxy)                            │
│               SSL Termination + Routing                              │
└──────┬─────────────────────────┬───────────────────────────────────┘
       │ REST API / SignalR       │ WebRTC (pass-through)
┌──────▼─────────────────────────▼───────────────────────────────────┐
│               ASP.NET Core .NET 8 – Backend                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  Auth    │  │ Job &    │  │  Interview   │  │ AI Orchestrat.│  │
│  │  Module  │  │ App Mgmt │  │  Session Mgmt│  │ (IAIProvider) │  │
│  │  (OAuth2)│  │          │  │              │  │               │  │
│  │  └──────────┘  └──────────┘  └──────────────┘  └───────┬───────┘  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────────┐          │          │
│  │  │Interview │  │Evaluation│  │  System      │  ┌───────▼───────┐  │
│  │  │Code Svc  │  │& HR Rvw  │  │  Settings    │  │  RAG Pipeline │  │
│  │  └──────────┘  └──────────┘  └──────────────┘  └───────────────┘  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────────┐                      │
│  │  │  Cheat   │  │Integrat. │  │  Scheduling  │                      │
│  │  │Detection │  │(ATS/Slk) │  │  Service     │                      │
│  │  └──────────┘  └──────────┘  └──────────────┘                      │
│  └──────────────────────────────────────────────────────────────────┘
└──────┬──────────────────┬──────────────────────────────────────────┘
       │ EF Core          │ Redis / External APIs
┌──────▼──────┐  ┌────────▼──────┐  ┌──────────────────────────────┐
│ PostgreSQL  │  │  Redis Cache  │  │ OpenAI (GPT-4o + Embeddings) │
│ (Supabase)  │  └───────────────┘  │ Deepgram STT / ElevenLabs TTS│
│ + pgvector  │                     │ HeyGen Avatar / SendGrid     │
│ (No tenant) │                     │ ATS Webhooks / OAuth2 / Slack│
└─────────────┘                     └──────────────────────────────┘
```

---

## Architecture Decision Records (ADR)

### ADR-001: Backend Framework
- **Quyết định:** ASP.NET Core .NET 8
- **Lý do:** Type safety, performance, enterprise ecosystem.
- **Thay thế đã loại:** Node.js/Express, Go

### ADR-002: Database Hosting
- **Quyết định:** PostgreSQL hosted on Supabase, kết nối trực tiếp qua connection string.
- **Ràng buộc:** Tuyệt đối không import/dùng Supabase client SDK.
- ⚠️ **Phần "hosted on Supabase" đã bị ADR-055 thay thế (2026-08-12):** production nay chạy Postgres tự host trên VPS, Supabase lùi về môi trường test. Ràng buộc "không dùng SDK" **vẫn giữ nguyên** và nay áp cho cả hai môi trường.

### ADR-003: Realtime Communication – SignalR vs WebRTC
- **SignalR:** session lifecycle events, question delivery, status updates, HR notifications.
- **WebRTC:** audio/video stream real-time (HeyGen Avatar lip-sync, audio ứng viên → STT).
- **Lý do tách biệt:** SignalR không phù hợp media stream. WebRTC không phù hợp control messaging.

### ADR-004: AI/LLM Integration – Provider Strategy
- **MVP:** OpenAI API + RAG (pgvector + `text-embedding-3-small` + GPT-4o).
- **RAG Flow:** JD + CV → chunk → embed → pgvector → retrieve khi sinh câu hỏi → GPT-4o.
- **Ràng buộc:** abstract qua `IAIProvider` + `IEmbeddingProvider`. Swap qua `AI_PROVIDER=openai|local`.

### ADR-005: STT, TTS & Avatar
- **Quyết định:** **Deepgram Nova-3** (STT streaming) + **ElevenLabs Flash v2.5** (TTS, ~75ms realtime) + **HeyGen Streaming Avatar** với Hybrid Idle Strategy.
- **STT:** Deepgram Nova-3 thay Google Speech-to-Text — **gộp luôn VAD + endpointing** (`vad_events`/`endpointing`/`utterance_end`) nên **không cần thư viện VAD riêng** (Silero/WebRTC). Cấu hình live: `interim_results=true`, `vad_events=true`, `endpointing=300`, `utterance_end_ms=1000`.
- **Thay thế đã loại:** Google STT (chuyển Deepgram), Azure TTS, D-ID, HeyGen Batch API, ElevenLabs Multilingual v2, **ElevenLabs v3** (biểu cảm hơn nhưng trễ cao hơn Flash v2.5 — không hợp live), VAD library rời.

### ADR-006: Streaming-First Latency Strategy
- **Mục tiêu (đã siết):** Ứng viên dừng nói → avatar bắt đầu nói trong **~0.8–1.2 giây** (cascaded tối ưu — xem ADR-043).

  | Bước | Công nghệ | Target latency |
  |------|-----------|---------------|
  | VAD + endpointing | **Deepgram Nova-3** (tích hợp sẵn — `vad_events`/`endpointing`/`utterance_end`) | ~100–300ms |
  | STT | **Deepgram Nova-3 streaming** (`interim_results` → retrieve sớm) | ~150–300ms sau dừng nói |
  | RAG | Hybrid RAG service Python (parallel với STT, ADR-039) | ~0ms additional |
  | LLM | GPT-4o streaming (Claude là option — ADR-043) | TTFT ~400–800ms |
  | TTS | ElevenLabs Flash v2.5 streaming | ~75–150ms |
  | Avatar | HeyGen Streaming (WebRTC) | ~100–200ms |
- **Đòn bẩy độ trễ (bất kể chọn LLM nào):** (1) **partial-STT → RAG song song** (bắt đầu retrieve khi VAD báo sắp dứt câu, không đợi final); (2) **TTS first-sentence** — phát audio ngay câu đầu LLM stream ra; (3) **prompt caching** prefix ổn định (JD+CV+Playbook+system) cắt prefill → giảm TTFT mỗi lượt; (4) **TẮT thinking** ở model sinh câu hỏi live (adaptive/extended thinking cộng vài giây vào TTFT); (5) gọi thẳng OpenAI, **không qua Azure** (TTFT GPT-4o Azure ~2.4s vs OpenAI ~0.76s).

### ADR-007: Containerization
- **Quyết định:** Docker + Docker Compose.

### ADR-008: WebRTC cho Media Streaming
- Backend không relay WebRTC media. Backend chỉ cung cấp signaling (ICE, SDP) qua SignalR/REST.

### ADR-009: AI Provider Abstraction
```csharp
public interface IAIProvider
{
    IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx, CancellationToken ct);
    Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct);
    Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct);
    Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct);
    Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct);
}

public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    // Retrieve không còn organizationId
    Task<IEnumerable<DocumentChunk>> RetrieveAsync(Guid? sourceId, float[] queryVector, int topK, CancellationToken ct);
}
```

### ADR-010: Observability & Logging
- **Quyết định:** Serilog + OpenTelemetry. Track latency từng bước pipeline.

### ADR-011: HeyGen Hybrid Idle Strategy
- Chỉ kết nối HeyGen Streaming khi AI nói (~3 phút/session). Khi im: client phát idle video loop.
- Tiết kiệm ~$2.78/session (~90% HeyGen cost).

### ADR-012: Single-tenant Architecture
- **Quyết định:** Hệ thống được thiết kế độc quyền cho **1 doanh nghiệp duy nhất sử dụng nội bộ** (Single-tenant).
- **Chi tiết:** Xóa bỏ hoàn toàn các bảng `organizations` và `subscriptions`. Không sử dụng cột `organization_id` ở bất cứ thực thể nào.
- **Global Config:** Các cấu hình toàn doanh nghiệp (tên miền cho phép đăng nhập, webhook ATS, Slack/Teams) được lưu tại bảng `system_settings` hoặc file cấu hình ứng dụng (`appsettings.json`).

### ADR-013: Candidate Invite Flow
- HR tạo Job Posting → duyệt CV → gửi **magic link/invite token** mời ứng viên vào Portal **chọn khung giờ (Availability Slot) cho buổi phỏng vấn thật của vòng đó**.
- **Đặt lịch xong sẽ mở cho ứng viên 1 lượt phỏng vấn thử (Practice Remote) cho vòng đó** — dùng được trong cửa sổ từ lúc đặt lịch đến giờ phỏng vấn thật (xem ADR-020/027).
- Ứng viên đến văn phòng đúng lịch đã đặt → HR cấp Interview Code (On-site) cho phỏng vấn thật.

### ADR-014: AI Evaluation & HR Confirm Flow
- AI generate Evaluation Report sau mỗi Round.
- HR Leader phê duyệt kết quả hoặc ghi đè verdict (Override bắt buộc nhập `override_reason` cho audit trail).
- Notification: email + in-app (SignalR) khi Evaluation hoàn thành.

### ADR-015: Interview Mode – Practice (Remote) vs Real (On-site)
- **Practice Session (Remote):** Candidate phỏng vấn thử từ browser để làm quen hệ thống. **[Cập nhật ADR-020/027/038]** Mở **tự động cho từng vòng** sau khi ứng viên **đã pass CV + đặt lịch buổi phỏng vấn thật của vòng đó** (qua Portal). Vào thẳng bằng route Portal (`/practice/:applicationId`) — **KHÔNG cần Interview Code, không cần magic link riêng**. Giới hạn **1 lượt / VÒNG**; cửa sổ dùng: từ lúc đặt lịch đến giờ phỏng vấn thật của vòng.
- **Real Interview (On-site):** BẮT BUỘC TẠI CÔNG TY. Candidate đến văn phòng đúng lịch đã đặt, nhập **Interview Code** tại thiết bị Kiosk.
- **On-site Kiosk:** Frontend app chạy ở chế độ kiosk (full-screen, không expose các route khác) trên thiết bị công ty.
- **Connection Recovery:** Nếu ứng viên mất kết nối, session duy trì trạng thái active. Khi nhập lại code, hệ thống tự resume (dựa vào `must_ask_tracking`).

### ADR-016: Interview Code (Access Control — Real On-site only)
- **Phạm vi:** Interview Code **CHỈ dùng cho phỏng vấn thật tại Kiosk**. Phỏng vấn thử (Practice) **không dùng code** — vào qua Portal sau khi đặt lịch (ADR-015/020/027). Entity `InterviewCode` **không có** trường `code_type` (đã bỏ — practice là Portal-driven).
- **Format:** 6 ký tự alphanumeric, case-insensitive (ví dụ: `ARX7K2`).
- **One-time-use:** Vô hiệu hóa ngay sau khi dùng thành công.
- **TTL:** Mặc định 2 giờ, cấu hình được per Job Posting.
- **Binding:** Mỗi code bind với một `application_id` + `round_number` cụ thể.
- **Generation:** HR Admin/Recruiter tạo thủ công hoặc sinh hàng loạt khi ứng viên đến văn phòng.
- **Audit:** Ghi lại thời điểm code được tạo, dùng, bởi `application_id`/vòng nào.

### ADR-017: Multi-round Interview
- HR cấu hình số vòng và loại vòng per Job Posting (ví dụ: `[{round: 1, type: "screening"}, {round: 2, type: "technical"}]`).
- Các loại vòng phỏng vấn (`round_type`) hệ thống hỗ trợ sẵn bao gồm:
  - `screening`: Phỏng vấn sơ loại (chú trọng kỹ năng mềm và giao tiếp, kiểm tra ngôn ngữ).
  - `technical`: Phỏng vấn chuyên môn sâu (chú trọng kỹ năng kỹ thuật, giải quyết bài toán).
  - `online_test`: Vòng Online Test - Multiple Choice Test (Làm trắc nghiệm trực tuyến).
- Mỗi vòng là một `InterviewSession` độc lập với `session_config` riêng.
- **Auto-progression:** Sau khi HR Leader duyệt Pass ở Round N → hệ thống lưu trạng thái.
- **Scheduling:** HR hẹn lịch offline với ứng viên và sinh Interview Code mới cho Round N+1 khi ứng viên đến công ty.

### ADR-018: Language-aware AI Interviewer
- **Detection:** Khi Job Posting được tạo, `IAIProvider.DetectLanguageRequirementAsync(jdText)` phân tích JD.
  - Output: `{ language: "en", requirement: "TOEIC > 700 hoặc IELTS > 6.5", confidence: 0.95 }` hoặc `null`.
  - HR xem kết quả detect và confirm/chỉnh trước khi Job Posting publish.
- **Interview language:** Nếu language requirement được confirm → Round 1 phỏng vấn bằng ngôn ngữ đó.
- **AI System Prompt:** Tự động điều chỉnh system prompt sang ngôn ngữ tương ứng.
- **TTS Language:** ElevenLabs hỗ trợ multilingual – chọn voice phù hợp ngôn ngữ.
- **STT Language:** Deepgram Nova-3 config `language` tương ứng.
- **Language Assessment:** `IAIProvider.AssessLanguageProficiencyAsync()` đánh giá riêng:
  - Fluency, Grammar accuracy, Vocabulary range, Comprehension score.
  - Được đưa vào Evaluation Report như một criterion độc lập.

### ADR-019: Cheat Detection
- **Signal collection:** Frontend thu thập signals trong session:
  - **Eye tracking:** webcam-based gaze estimation (thư viện JS như `WebGazer.js`).
  - **Response timing:** thời gian giữa khi câu hỏi được đặt và ứng viên bắt đầu trả lời.
  - **Speech pattern:** STT partial transcript analysis – phát hiện reading cadence (đọc văn bản thay vì nói tự nhiên).
  - **Tab switching / focus loss:** browser visibility API.
- **Analysis:** Backend `CheatDetectionService` tổng hợp signals, chạy heuristic + AI analysis.
- **Output:** `CheatScore` (0–100) + `CheatSignals[]` (danh sách signals phát hiện được).
- **Integration:** CheatScore và CheatSignals xuất hiện trong Evaluation Report (section riêng) cho HR xem xét.
- **Policy:** ARISP không tự động fail ứng viên chỉ dựa trên CheatScore – HR quyết định cuối.

### ADR-020: Scheduling Service (Real Interview per round → unlocks Practice)
- **[Sửa 2026-06-27 — bản cũ "Practice Session Only" SAI so với code]**
- HR cấu hình `AvailabilitySlots` **per Job Posting + per `round_number`**: khung giờ trống cho buổi **phỏng vấn thật** của từng vòng (`AvailabilitySlot.RoundNumber`).
- Sau khi pass CV, ứng viên được mời (magic link/invite token) vào Portal **chọn 1 slot cho buổi phỏng vấn thật của vòng đó**; `InterviewBooking` (cũng có `RoundNumber`) ghi nhận lịch. Chốt chỗ nguyên tử chống overbooking (UPDATE `booked_count` có điều kiện `booked_count < capacity`).
- **Đặt lịch thành công sẽ MỞ 1 lượt phỏng vấn thử (Practice) cho vòng đó.** Cửa sổ dùng thử = từ lúc đặt lịch đến giờ phỏng vấn thật của vòng (xem ADR-027).
- **Lặp theo từng vòng:** pass vòng N → mời chọn lịch vòng N+1 → mở 1 lượt thử vòng N+1 → … đến khi được nhận.
- Reminder email 24h và 1h trước giờ phỏng vấn thật.
- Buổi phỏng vấn thật vẫn diễn ra **on-site tại Kiosk**: đến đúng lịch đã đặt, HR cấp Interview Code (ADR-016). Lịch đặt qua Portal thay cho điều phối thủ công.

### ADR-021: Candidate Portal
- **Auth:** Magic link qua email (không cần password). Magic link có TTL 15 phút, one-time-use.
- **Access control:** Candidate chỉ xem data của chính mình (`application_id`-scoped).
- **Content:** Recording (nếu HR bật), transcript, Evaluation Report (phần HR cho phép share), feedback.

### ADR-022: ATS Integration (Webhook/API)
- ARISP push events sang ATS của công ty qua Webhook: `application.submitted`, `interview.completed`, `evaluation.confirmed`.
- Payload chuẩn hóa (JSON). Webhook URL và Secret được cấu hình toàn cục trong `system_settings`.
- Retry logic với exponential backoff nếu ATS endpoint lỗi.

### ADR-023: SSO & OAuth2 Corporate Domain Validation
- **Xác thực nội bộ:** Nhóm người dùng công ty (`super_admin`, `hr_admin`, `recruiter`) đăng nhập bằng **Email + Mật khẩu**. Hỗ trợ thêm **Google OAuth2** (Google Sign-In). SAML 2.0 hoàn toàn bị loại bỏ.
- **Yêu cầu Pre-provisioning (Cấp trước tài khoản)**: Chỉ những email đã được Super Admin hoặc Admin tạo sẵn trong database mới được phép đăng nhập. Nếu đăng nhập bằng Google Sign-In mà email chưa tồn tại trong database (chưa được cấp tài khoản trước đó), hệ thống **chặn đăng nhập và tuyệt đối không tự động đăng ký/tạo tài khoản nháp**.
- **Domain Validation:** Khi đăng nhập qua OAuth2, hệ thống bắt buộc phân tách và kiểm tra phần domain của địa chỉ email (ví dụ: `hr@fsoft.vn` -> lấy ra `fsoft.vn`). Email này phải thuộc danh sách tên miền được phép truy cập (`allowed_email_domains` được quy định trong cấu hình toàn cục `system_settings`). Mọi email domain công cộng hoặc không khớp sẽ bị chặn truy cập lập tức.
- **Ứng viên:** Candidate Portal sử dụng Magic Link gửi qua email cá nhân có TTL ngắn, không áp dụng OAuth2.
- **Password recovery (bổ sung 2026-06-18):** Quên/đặt lại mật khẩu **tách riêng** theo cổng đăng nhập, không dùng chung endpoint:
  - Candidate: `POST /auth/candidate/forgot-password` + `/auth/candidate/reset-password` (bảng `candidate_accounts`).
  - Staff nội bộ: `POST /auth/staff/forgot-password` + `/auth/staff/reset-password` (bảng `users`); chỉ gửi khi tài khoản tồn tại & `is_active`; tài khoản SSO-only vẫn đặt được mật khẩu lần đầu qua link.
  - Token reset lưu chung bảng `magic_links` nhưng có cột phân loại **`audience`** (`candidate`|`staff`, default `candidate`); endpoint reset lọc đúng `audience` để token 2 cổng không dùng nhầm cho nhau (chống lẫn lộn khi email trùng ở cả 2 bảng). TTL 2 giờ, one-time-use.
  - Lý do tách riêng: 2 bảng tài khoản khác nhau + staff có đặc quyền cao → ranh giới bảo mật rõ ràng, tránh account enumeration chéo.

### ADR-024: Bias Detection & Fairness
- **Data collected:** Evaluation scores theo demographic groups (nếu Candidate cung cấp và đồng ý).
- **Analysis:** Statistical analysis tìm disparate impact – nếu pass rate của một nhóm thấp bất thường.
- **Report:** Fairness Report per Job Posting (cho SuperAdmin và HR Admin).
- **Privacy:** Demographic data phải được Candidate đồng ý cung cấp (opt-in) và được mã hóa.

### ADR-025: Interview Playbook – Org Knowledge Base
- **Quyết định:** HR Admin upload tài liệu phỏng vấn nội bộ theo 3 cấp scope: Company / Job Posting / Round. Tài liệu được chunk, embed vào pgvector và retrieve trong RAG pipeline để AI phỏng vấn đúng phong cách + nội dung mong muốn của doanh nghiệp.
- **Document types hỗ trợ:**

  | Type key | Mô tả | Scope |
  |---|---|---|
  | `interview_style_guide` | Phong cách, tone, approach phỏng vấn | Company |
  | `competency_framework` | Ma trận kỹ năng theo level | Company |
  | `culture_values` | Văn hóa, giá trị cốt lõi, culture fit indicators | Company |
  | `compliance_guide` | Câu hỏi không được hỏi (pháp lý) | Company |
  | `red_flag_guide` | Dấu hiệu cần probe sâu hoặc loại bỏ | Company |
  | `question_bank` | Ngân hàng câu hỏi gợi ý per vị trí | Job Posting |
  | `technical_scenarios` | Bài toán / case study cụ thể | Job Posting |
  | `expected_answers` | Hướng dẫn câu trả lời tốt cần đề cập | Job Posting |
  | `must_ask` | Câu hỏi bắt buộc phải hỏi trước khi kết thúc | Job Posting |
  | `round_playbook` | Playbook cụ thể per Round | Round |
  | `past_transcripts` | Transcript phỏng vấn ẩn danh (AI học từ mẫu thành công) | Company / Job Posting |

- **Format upload:** PDF, DOCX, TXT, Markdown, JSON (question bank format).
- **RAG weighting khi retrieve:**
  - JD + CV: weight cao (candidate-specific)
  - Company Playbook (style, compliance, values): weight trung bình (brand consistency)
  - Job Posting Playbook (question_bank, scenarios, must_ask): weight cao (content accuracy)
  - Round Playbook: weight cao (phù hợp vòng hiện tại)
- **Must-ask enforcement:** `PlaybookService` track danh sách `must_ask` questions đã hỏi. `InterviewService` nhận signal "còn câu bắt buộc chưa hỏi" trước khi trigger điều kiện dừng.
- **Ràng buộc:** Không lọt dữ liệu tài liệu phỏng vấn ra ngoài hệ thống.

### ADR-026: Job Board (IT-focused)
- **Quyết định:** Tích hợp Job Board IT vào nền tảng ARISP. Ứng viên tạo tài khoản, tìm kiếm và tự ứng tuyển.
- **Flow:** Candidate self-apply → HR review CV → HR chủ động gửi magic link (không tự động).
- **Nguồn dữ liệu:** Job Posting của doanh nghiệp là nguồn chung – không tạo entity riêng cho Job Board listing.
- **Thị trường:** Chỉ tập trung IT (không phải job board tổng quát).

### ADR-027: Practice Interview Session (Phỏng vấn thử)
- **Quyết định:** Thêm `session_type` enum (`practice` | `real`) vào `InterviewSession` entity (`InterviewSession.SessionType`, `RoundNumber`).
- **Truy cập:** **[Cập nhật ADR-020 — 2026-06-27]** Mở **tự động cho từng vòng** sau khi ứng viên **đã pass CV + đặt lịch buổi phỏng vấn thật của vòng đó** (ADR-020). Vào qua **Portal** (route `/practice/:applicationId`) — **KHÔNG cần Interview Code**. Không xuất hiện công khai trên Job Board.
- **Lượt dùng:** **1 lượt / VÒNG** (không phải 1 lượt / hồ sơ). Điều kiện chặn = đã tồn tại `InterviewSession` `session_type='practice'` cho `(application_id, round_number)` (`CheckPracticeEligibilityAsync` / `StartSessionAsync`). Cờ cũ `Application.PracticeSessionUsed` chỉ giữ cho tương thích ngược, **không còn dùng làm điều kiện chặn**.
- **Cấu hình vòng — practice GIỐNG HỆT buổi thật sắp tới của vòng đó:** practice và real của cùng vòng dùng chung `RoundNumber` → cùng `InterviewRoundConfig`, nên **cùng `round_type`** (vòng thật là `technical` thì practice cũng `technical`; vòng thật là sơ loại/ngôn ngữ thì practice cũng sơ loại/ngôn ngữ) **và cùng ngôn ngữ phỏng vấn**. Mục đích: ứng viên luyện đúng dạng vòng + đúng ngôn ngữ sắp phải thi thật (`StartSessionAsync` set `RoundType`/`InterviewLanguage` theo cùng round).
- **RAG nguồn:** `practice` – chỉ retrieve JD + CV chunks, không load Playbook. `real` – full RAG (JD + CV + Playbook). (Khác biệt duy nhất giữa practice & real cùng vòng là **nguồn RAG** + **không quay video**; loại vòng và ngôn ngữ thì y hệt.)
- **Công nghệ:** **Đầy đủ pipeline như Real** (Deepgram Nova-3 STT+VAD → Hybrid RAG → GPT-4o → ElevenLabs Flash v2.5 → HeyGen Avatar + Hybrid Idle). Không cắt giảm tech.
- **Recording:** Practice **không quay video** — chỉ lưu **transcript** + Evaluation Report (giảm storage). Real lưu đầy đủ.
- **Kết quả:** Practice Session có Evaluation Report riêng; HR xem được. Không ảnh hưởng đến verdict tuyển dụng.

### ADR-028: Usage Tracking Model
- **Quyết định:** Nền tảng được cấu hình giới hạn sử dụng (Usage counters) toàn hệ thống thay vì quản lý gói cước (subscriptions) cho nhiều công ty.
- **Usage tracking:** Theo dõi tổng số interview sessions đã thực hiện (practice + real), số job postings active, tài nguyên lưu trữ video recording.

---

## AI Media Pipeline – Full Streaming Interview Loop

```
[Ứng viên ĐANG NÓI]
      │ audio chunks (WebSocket stream)
      ▼
[Deepgram Nova-3 Streaming STT + VAD/endpointing (language-configured)]
      │ partial transcripts (interim_results)
      │ (VAD/SpeechStarted near-end) → [Hybrid RAG service: retrieve JD + CV + Playbook]
      │ (speech_final / UtteranceEnd ~150–300ms sau dừng)
      ▼
[GPT-4o Streaming (thinking OFF, prompt-cached)] ◄── context + retrieved chunks + system prompt
      │ token stream (first-sentence → TTS ngay)
      ▼
[ElevenLabs Flash v2.5 Streaming TTS (language voice)]
      │ audio stream
      ▼
[HeyGen Streaming Avatar via WebRTC]
      ▼
[Ứng viên nghe + thấy avatar] ← ~0.8–1.2 giây sau khi dừng nói
```

---

## Multi-round Flow

```
[Job Posting: Round 1 = Screening, Round 2 = Technical]
        │
[Candidate submit CV → access interview]
        │
[Round 1 Session] ← language-aware (detect từ JD)
        │ session end
[Round 1 Evaluation (AI)] + [Cheat Detection Report]
        │
[HR Leader Review Round 1]
  ├── Not Pass → email từ chối → DONE
  └── Pass → Auto-invite Round 2
             │
         [Round 2 Session] ← technical deep-dive (On-site)
             │ session end
         [Round 2 Evaluation (AI)]
             │
         [HR Leader Review Round 2]
           ├── Not Pass → email từ chối
           └── Pass → email mời vòng tiếp / offer
```

---

## Service Boundaries

| Service / Interface | Trách nhiệm |
|---|---|
| `AuthService` | JWT, role management, magic link (Candidate Portal), **OAuth2 OIDC Integration & Domain validation** |
| `SystemSettingService` | Quản trị và truy xuất cấu hình hệ thống toàn cục (`allowed_email_domains`, global webhooks) |
| `JobPostingService` | CRUD Job Posting, round config, interview mode (default `onsite`), availability slots, persona, **JD file upload (PDF/DOCX)** |
| `ApplicationService` | Candidate application (CV + info), invite flow, practice session eligibility check (**1 lượt / vòng**, theo `(application_id, round_number)`), **đính kèm CV-JD Analysis vào Application** |
| `CvJdAnalysisService` | **[NEW]** Nhận CV file + JD (file/text) → gọi Gemini API phân tích → trả matchScore + summary. Cache kết quả per CV hash + JobPosting |
| `IGeminiProvider` | **[NEW]** Interface abstract cho Google Gemini API. Method: `AnalyzeCvJdMatchAsync(cvFile, jdContent, ct)` |
| `JobBoardService` | Job listing (public view of Job Postings), candidate self-apply, job search & filter |
| `OnlineTestService` | Quản lý câu hỏi trắc nghiệm (`online_test_questions`), lưu kết quả nộp bài (`online_test_submissions`), tự động chấm điểm và đánh giá đạt/trượt |
| `InterviewCodeService` | Generate, validate, expire Interview Code (on-site flow Kiosk) |
| `SchedulingService` | Availability slots, booking, reminder emails, reschedule |
| `InterviewService` | Session lifecycle, multi-round flow, adaptive difficulty, auto-progression, must-ask enforcement |
| `PlaybookService` | Upload, parse, chunk, embed tài liệu Playbook per scope (Company/Job Posting/Round); track must-ask questions đã hỏi |
| `IAIProvider` | Stream question, analyze answer, generate evaluation, detect language, assess language |
| `IEmbeddingProvider` | Embed + retrieve từ pgvector (JD/CV/Playbook chunks) - không dùng organization_id |
| `ISTTProvider` | Deepgram Nova-3 streaming + VAD/endpointing (primary), Whisper (fallback) |
| `RagService` | Chunk JD/CV/Playbook, embed, store, retrieve context theo weighted scope |
| `LanguageDetectionService` | Gọi AI detect ngôn ngữ từ JD, lưu kết quả vào Job Posting |
| `TTSService` | ElevenLabs Flash streaming, hỗ trợ multilingual voice |
| `AvatarService` | HeyGen Streaming Avatar API, WebRTC signaling |
| `EvaluationService` | Generate Evaluation Report sau mỗi Round (Verdict + Score + Reasoning + Language Assessment) |
| `CheatDetectionService` | Tổng hợp signals từ frontend, generate CheatScore + CheatSignals |
| `HRReviewService` | Confirm/Override, audit trail, auto-progression trigger |
| `NotificationService` | Email (SendGrid/SES) + in-app SignalR: invite, reminder, evaluation ready, result |
| `IntegrationService` | ATS webhook push (global), Slack/Teams webhook notification (global) |
| `BiasDetectionService` | Fairness analysis per Job Posting (post-MVP) |
| `AuditLogService` | Ghi lại mọi hành động quan trọng toàn hệ thống |
| `WebRTCSignalingHub` | SignalR Hub: ICE candidates, SDP offer/answer |
| `SessionHub` | SignalR Hub: session lifecycle events |

---

## Recent Architecture Notes

### ADR-029: [DEPRECATED] Previous External Auth Bridge for Candidate Accounts
- **Status:** DEPRECATED (Superseded by local email/password authentication and direct Google OAuth2 SSO).
- **History:** Previously, frontend React initialized an external authentication Web SDK and the backend validated its ID tokens. This integration has been completely removed to simplify the infrastructure and rely on direct authentication.

### ADR-030: Gemini CV-JD Match Analysis
- **Quyết định:** Sử dụng **Google Gemini 2.5 Flash** để phân tích mức độ phù hợp giữa CV của ứng viên và JD của vị trí tuyển dụng.
- **Mục đích:** Cung cấp cho candidate một **bản đánh giá nhanh** về mức độ phù hợp trước khi ứng tuyển. Dù điểm cao hay thấp, candidate vẫn có thể ứng tuyển.
- **Reuse principle:** Kết quả phân tích được lưu vào bảng `cv_jd_analyses`. Khi candidate submit Application, hệ thống link `analysis_id` vào Application – HR nhận được kết quả y hệt mà không cần chạy lại Gemini.
- **Auto-analysis on apply:** Nếu candidate ứng tuyển mà chưa từng chạy analysis, hệ thống tự động gọi Gemini 1 lần rồi đính kèm.
- **Input:** CV file (PDF/DOCX) + JD file gốc (PDF/DOCX) hoặc JD text.
- **Output (JSON):**
  ```json
  {
    "matchScore": 78,
    "summary": "Hồ sơ phù hợp tốt với yêu cầu vị trí...",
    "skillsMatched": ["C#", ".NET Core", "PostgreSQL"],
    "skillsGaps": ["Docker", "Kubernetes"],
    "experienceRelevance": "3 năm kinh nghiệm backend phù hợp với yêu cầu Mid-Senior",
    "overallRecommendation": "Phù hợp tốt. Nên bổ sung kỹ năng containerization."
  }
  ```
- **Provider abstraction:** Gemini được gọi qua `IGeminiProvider` interface. Không gọi Gemini SDK trực tiếp trong business logic.
- **Tại sao Gemini mà không GPT-4o?** Gemini 2.5 Flash hỗ trợ multimodal file input (PDF nạp trực tiếp) với chi phí thấp hơn GPT-4o cho tác vụ phân tích document. GPT-4o vẫn được dùng cho RAG + phỏng vấn AI (streaming).

  ```csharp
  public interface IGeminiProvider
  {
      Task<CvJdAnalysisResult> AnalyzeCvJdMatchAsync(
          Stream cvFileStream,
          string cvFileName,
          Stream? jdFileStream,      // null nếu không có file JD
          string? jdFileName,
          string jdText,             // fallback text JD
          CancellationToken ct);
  }
  ```

### ADR-031: JD File Upload & Storage
- **Quyết định:** Mở rộng `JobPosting` entity hỗ trợ upload file JD gốc (PDF/DOCX) bên cạnh trường `JobDescription` (text).
- **Lý do:** Gemini AI phân tích từ file gốc (giữ được formatting, bảng biểu, bullet points) cho kết quả chính xác hơn so với plain text.
- **Thêm cột mới vào `job_postings`:**
  - `jd_file_url` (string, nullable): URL/path tới file JD gốc đã upload.
  - `jd_file_name` (string, nullable): Tên file gốc (ví dụ: "JD_Backend_Senior.pdf").
  - `jd_file_format` (string, nullable): Định dạng file ("pdf", "docx").
- **Logic:** Khi HR tạo/sửa Job Posting, có thể paste text JD hoặc upload file JD, hoặc cả hai.
- **Gemini sử dụng:** Ưu tiên file JD gốc (nếu có) → fallback sang `job_description` text.
- **Format hỗ trợ:** PDF, DOCX (giới hạn 10MB).

### ADR-032: CvJdAnalysis Entity & Database Schema
- **Bảng mới: `cv_jd_analyses`**

  | Cột | Kiểu | Mô tả |
  |------|------|-------|
  | `id` | UUID PK | |
  | `candidate_account_id` | UUID FK → `candidate_accounts` | Candidate thực hiện phân tích (nullable nếu chưa login) |
  | `job_posting_id` | UUID FK → `job_postings` | Job được phân tích |
  | `application_id` | UUID FK → `applications`, nullable | Link với Application sau khi ứng tuyển |
  | `cv_file_url` | string | URL tới file CV đã upload |
  | `cv_file_name` | string | Tên file CV gốc |
  | `match_score` | decimal (0–100) | Điểm phù hợp tổng thể |
  | `summary` | text | Tóm tắt đánh giá tổng quan |
  | `skills_matched` | jsonb | `["C#", ".NET", "SQL"]` |
  | `skills_gaps` | jsonb | `["Docker", "K8s"]` |
  | `experience_relevance` | text | Đánh giá kinh nghiệm |
  | `overall_recommendation` | text | Khuyến nghị tổng quan |
  | `raw_response` | jsonb | Response gốc từ Gemini (lưu để debug/audit) |
  | `created_at` | timestamptz | |

- **Thêm cột vào `applications`:**
  - `cv_jd_analysis_id` (UUID FK → `cv_jd_analyses`, nullable): Link tới kết quả phân tích đã chạy.

- **Index:** `(candidate_account_id, job_posting_id)` – để lookup nhanh kết quả đã phân tích (tránh chạy lại).

### ADR-033: Candidate UI Localization (i18n) – VI/EN
- **Quyết định:** Giao diện Candidate (Job Board, Job Detail, Candidate Portal) hỗ trợ chuyển ngôn ngữ **Tiếng Việt / English** qua một locale switcher trên header.
- **Phân biệt với ADR-018:** ADR-018 nói về ngôn ngữ AI **phỏng vấn** (detect từ JD, ảnh hưởng system prompt + TTS voice + STT). ADR-033 chỉ là ngôn ngữ **hiển thị UI** cho ứng viên — hai thứ độc lập. Ví dụ: UI để Tiếng Việt nhưng phỏng vấn vẫn bằng tiếng Anh nếu JD tiếng Anh.
- **Phạm vi:** Chỉ UI candidate-facing. Workspace nội bộ (HR/Admin) mặc định Tiếng Việt, chưa cần i18n.
- **Lưu lựa chọn:** `localStorage` (`locale`) + tùy chọn lưu vào `candidate_accounts.preferred_locale` (nullable, default `vi`) khi đã đăng nhập.
- **Kỹ thuật (FE):** react-i18next, default `vi`, fallback `vi`.

### ADR-034: Saved Jobs (Bookmark) cho Candidate
- **Quyết định:** Ứng viên (đã đăng nhập) có thể **lưu/bỏ lưu** Job Posting để xem lại; hiển thị số lượng trên header và trang "Việc đã lưu".
- **Bảng mới: `saved_jobs`**

  | Cột | Kiểu | Mô tả |
  |------|------|-------|
  | `id` | UUID PK | |
  | `candidate_account_id` | UUID FK → `candidate_accounts` | |
  | `job_posting_id` | UUID FK → `job_postings` | |
  | `created_at` | timestamptz | |

- **Ràng buộc:** UNIQUE `(candidate_account_id, job_posting_id)` – không lưu trùng. Soft delete không cần (bỏ lưu = hard delete row).
- **Guest:** Khi chưa đăng nhập, nút lưu điều hướng sang đăng nhập (không lưu ẩn danh).

### ADR-035: Candidate Google OAuth2 (không ràng buộc domain)
- **Quyết định:** Bổ sung **Google Sign-In cho Candidate** trên Job Board login, **KHÔNG** áp `allowed_email_domains`.
- **Phân biệt với ADR-023:** ADR-023 (Google OAuth nội bộ HR/Recruiter/Admin) **bắt buộc** email thuộc `allowed_email_domains` + pre-provisioning. ADR-035 dành cho ứng viên: chấp nhận **mọi** tài khoản Google cá nhân, và nếu chưa có `candidate_accounts` thì **tự tạo** (self-registration), giống đăng ký email/password tự do.
- **Lý do:** Giảm ma sát đăng ký cho ứng viên; vẫn giữ email/password là phương thức chính.
- **Bảo mật:** Provider validation bình thường; không có domain allowlist cho luồng candidate.
- **Một email = một tài khoản, hai cách đăng nhập (bổ sung 2026-08-15).** Không có "tài khoản Google" tách biệt: `candidate_accounts` unique theo email, Google Sign-In tìm thấy tài khoản sẵn có thì đăng nhập vào chính hồ sơ đó. Tài khoản tạo qua Google có `PasswordHash` rỗng — muốn dùng thêm mật khẩu thì đặt qua **Hồ sơ → Đặt mật khẩu** (`ChangeCandidatePasswordCommand` cho phép `CurrentPassword` null khi chưa có mật khẩu) hoặc **"Quên mật khẩu?"** (`CandidateResetPasswordCommand` chỉ gán `PasswordHash`, không đòi mật khẩu cũ).
- **Ảnh đại diện lấy từ Google (2026-08-15).** `CandidateAccount.AvatarUrl` chứa **một trong hai dạng**: URL tuyệt đối http(s) lấy từ claim `picture` của Google, hoặc storageKey của ảnh ứng viên tự tải lên (`StorageFolder.Avatar` → `avatars/`). Phân biệt bằng tiền tố `http` ở `PortalSupport.ResolveAvatarUrlAsync` — storageKey phải qua `GetUrlAsync` vì R2 cần presign. Claim `picture` **không** nằm trong bảng ClaimActions mặc định của handler Google nên đọc thẳng JSON userinfo trong `OnCreatingTicket` (không phụ thuộc mặc định của từng phiên bản package). Ảnh Google chỉ **điền vào chỗ trống**, không bao giờ ghi đè ảnh ứng viên đã tự tải — nếu không mỗi lần đăng nhập Google sẽ đạp mất lựa chọn của họ.
- **CHỐNG CHIẾM TÀI KHOẢN TRƯỚC — không được gỡ (2026-08-15).** `CompleteExternalCandidateSignInCommand`: khi Google Sign-In gặp tài khoản đã tồn tại mà **`EmailVerified == false`**, phải **xoá `PasswordHash`** rồi mới đặt `EmailVerified = true`. Lý do: kẻ xấu đăng ký form web bằng email người khác và đặt mật khẩu của hắn; tài khoản nằm im vì chưa xác minh; đến khi chủ email thật đăng nhập Google thì thao tác đánh dấu đã-xác-minh vô tình **xác minh hộ mật khẩu của kẻ xấu**, cho hắn quyền đọc CV/hồ sơ/kết quả phỏng vấn của nạn nhân. Mật khẩu đặt trên tài khoản chưa từng xác minh email là mật khẩu không ai chứng minh được quyền sở hữu. Người dùng ngay tình (đăng ký mật khẩu → chưa bấm link xác minh → đăng nhập Google) mất mật khẩu vừa đặt: đánh đổi có chủ ý, vì họ đang đăng nhập được và đặt lại ngay trong Hồ sơ, còn kịch bản kia là mất tài khoản. Có 2 test khoá hành vi này trong `ExternalSignInCommandHandlerTests`.

### ADR-036: File Storage Abstraction (Local / Cloudflare R2)
- **Vấn đề:** Trước đây file upload (CV ứng tuyển, CV hồ sơ candidate) ghi thẳng vào thư mục `./uploads` trên đĩa backend. Không scale ngang (mỗi container có disk riêng), mất khi redeploy, đầy ổ VPS, không CDN/backup. (Lưu ý: `uploads/` đã `.gitignore` nên **không** làm nặng repo.)
- **Quyết định:** Trừu tượng hoá qua **`IFileStorageService`** (cùng pattern `IAIProvider`/`IGeminiProvider`), 2 implementation chọn qua cấu hình `Storage:Provider`:
  - **`LocalFileStorageService`** (dev): ghi `./uploads`, `storageKey` = `/uploads/<guid>.ext`, phục vụ tĩnh qua `UseStaticFiles(RequestPath="/uploads")`.
  - **`S3FileStorageService`** (prod): upload lên **object storage S3-compatible**, file **private**, hiển thị qua **presigned URL** có thời hạn (`UrlExpiryMinutes`, mặc định 60).
- **Nhà cung cấp prod: Cloudflare R2** (S3-compatible, dùng `AWSSDK.S3`). Lý do chọn R2 thay vì Supabase Storage:
  1. **Egress miễn phí** – quan trọng vì recording phỏng vấn (Phase 7) bị xem lại nhiều lần.
  2. Giữ Supabase thuần Postgres, không làm sâu lock-in.
  3. Cùng chuẩn S3 nên code không đổi nếu sau này chuyển AWS S3/MinIO.
- **Hợp đồng:** DB lưu **`storageKey`** (không lưu URL tuyệt đối). API gọi `GetUrlAsync(key)` khi trả response → Local trả đường dẫn tương đối, S3 trả presigned URL. Frontend `resolveAssetUrl()` xử lý cả hai (tương đối → ghép `ASSET_BASE_URL`; tuyệt đối `http` → giữ nguyên).
- **Cấu hình:** `Storage:Provider` = `Local` | `S3`; secrets `Storage:S3:{Endpoint,AccessKeyId,SecretAccessKey,Bucket,Region,KeyPrefix,UrlExpiryMinutes}` qua **user-secrets/env** (rule #2). Khi `Provider=S3` mà thiếu cấu hình bắt buộc → **fail-fast** lúc startup.
- **Bảo mật token R2:** API token quyền **Object Read & Write**, scope đúng 1 bucket; production siết thêm IP allowlist (IP VPS tĩnh).
- **CORS trên bucket là bắt buộc khi `Provider=S3`** (bổ sung 2026-08-06): presigned URL lo phần *xác thực*, không lo phần *CORS* — bucket R2 mặc định không có rule nào nên trình duyệt chặn JS đọc file. Chỉ đường dẫn tải bằng **JavaScript** mới hỏng: `DocumentViewer` xem PDF bằng `<iframe>`, ảnh bằng `<img>`, "Tải về" bằng `<a href>` (đều không dính CORS), **riêng DOCX phải `fetch()` lấy blob cho `docx-preview`** → thiếu CORS thì chỉ DOCX chết, rất dễ tưởng nhầm là ổn. Dev không tái hiện được vì `Provider=Local` cùng origin. Rule cần đặt (`GET`/`HEAD`, liệt kê đủ 3 origin, **không** dùng `*` vì bucket chứa CV ứng viên) + cách kiểm chứng: xem [docs/r2-storage-cors-setup.md](../docs/r2-storage-cors-setup.md).
- **Đã refactor:** `ApplicationsController` (CV ứng tuyển), `CandidatePortalController` (CV hồ sơ + resolve URL ở GET applications/detail/profile). Xoá CV cũ khi upload CV mới (tránh tích rác).
- **Follow-up (chưa làm):** Phía HR/staff (`ApplicationService` trả `CvFileUrl`) hiện trả `storageKey` thô — cần resolve presigned URL khi bật S3 cho prod. Dev (`Local`) không ảnh hưởng.
- **Cấm `Provider=Local` ở Production + không mặc định âm thầm** (2026-08-06, phòng ngừa): `configuration["Storage:Provider"] ?? "Local"` khiến thiếu/sai biến môi trường là rơi về ghi đĩa `./uploads` — mà `docker-compose.prod.yml` đặt `volumes: !reset []` nên thư mục này nằm trong lớp container và mất sạch mỗi lần deploy, trong khi upload vẫn trả 200 và vẫn ghi row DB → sẽ hỏng hoàn toàn im lặng. Nay `AddInfrastructure` chỉ nhận `S3`/`Local` tường minh, giá trị lạ → throw, và `Local` + `ASPNETCORE_ENVIRONMENT=Production` → throw ngay lúc boot. *(Prod hiện đã cấu hình đúng `S3` — đây là chốt chặn cho tương lai, không phải sửa sự cố.)*
- **Bẫy khi chẩn đoán provider đang chạy:** `GetJobByIdQuery` chỉ resolve URL file JD **khi `isStaff`**; gọi `GET /api/jobs/{id}` ẩn danh sẽ thấy **key thô** (`cv/<guid>`) và rất dễ kết luận nhầm là prod đang chạy `Local`. Muốn biết provider thật thì đọc thẳng biến môi trường trong container (`docker exec arisp-backend printenv Storage__Provider`).
- **Tách thư mục theo loại file** (2026-08-13): trước đây `SaveAsync` ghép key bằng **`KeyPrefix` duy nhất** (đặt `cv`), nên **mọi** file — CV ứng viên, file JD (kể cả bản đã ký duyệt), **video ghi hình buổi thật**, playbook — đổ chung vào `cv/`; không lọc/backup/dọn theo loại được, và nhìn vào bucket thì tưởng toàn CV. Nay `IFileStorageService.SaveAsync` nhận thêm tham số **bắt buộc** `StorageFolder` (`Cv` | `Jd` | `Recording` | `Playbook`) — không có giá trị mặc định, nên thêm call site mới là **buộc phải chọn** thư mục ở compile time. Ánh xạ tên thư mục nằm một chỗ (`StorageFolderExtensions.ToSegment`): `cv/`, `jd/`, `recordings/`, `playbooks/` — dùng chung cho cả 2 provider (S3 → `{KeyPrefix?}/{folder}/{guid}.ext`, Local → `/uploads/{folder}/{guid}.ext`).
  - **`KeyPrefix` đổi nghĩa:** nay là prefix gốc **tuỳ chọn của cả bucket** (vd `prod` khi dùng chung bucket giữa các môi trường), mặc định **rỗng** — không còn là nơi phân loại file. Env cũ `Storage__S3__KeyPrefix=cv` phải xoá giá trị, nếu không file mới rơi vào `cv/jd/…`, `cv/recordings/…`.
  - **Không cần migrate dữ liệu cũ:** DB lưu key đầy đủ và `GetUrlAsync/DeleteAsync/ReadAllBytesAsync` dùng key nguyên trạng, nên file cũ nằm ở `cv/<guid>` vẫn đọc/xoá bình thường; chỉ file **mới** đi theo thư mục mới. (Muốn gom bucket cho gọn thì phải copy object **và** update cột `cv_file_url`/`jd_file_url`/`recording_url`/`file_url` tương ứng — chưa làm.)
  - **`LocalFileStorageService` sửa kèm:** `DeleteAsync`/`ReadAllBytesAsync` trước dùng `Path.GetFileName(storageKey)` (vứt luôn thư mục) nên sẽ **không tìm thấy file** ngay khi key có thư mục con. Nay resolve theo đường dẫn tương đối kể từ `uploads/`, có chặn path traversal, vẫn đọc được key cũ dạng phẳng `/uploads/<guid>.ext`.
- **Không phải endpoint nào cũng resolve:** `GetApplicationByIdQueryHandler`, `GetJobApplicationsQuery`, `PortalApplicationsFeature` có gọi `GetUrlAsync`; nhưng `ApplicationService` vẫn gán `CvFileUrl = application.CvFileUrl` thô ở vài nhánh. Dưới `Local` việc trả key thô vô hại (key `/uploads/<guid>` tình cờ cũng là đường dẫn phục vụ được), dưới `S3` thì key không phải URL → FE ghép thành `https://<origin>/cv/<guid>` và rơi vào fallback SPA. Đây là lý do cùng một màn hình chạy tốt ở local nhưng hỏng trên prod.

### ADR-037: Địa giới hành chính VN — Provinces Open API v2 (sau sáp nhập 07/2025)
- **Quyết định:** Trường "Địa điểm" của Candidate dùng **[Provinces Open API v2](https://provinces.open-api.vn/)** — dữ liệu **sau sáp nhập tỉnh 07/2025**: cấu trúc **2 cấp Tỉnh/Thành → Phường/Xã** (bỏ cấp Quận/Huyện), **34 tỉnh/thành**.
- **Endpoint dùng:** `GET /api/v2/p/` (danh sách tỉnh), `GET /api/v2/p/{code}?depth=2` (tỉnh kèm phường). Field chính: `code` (int), `name`, `province_code`.
- **Frontend:** service `provinceService` (gọi thẳng open-api, có cache trong phiên — **không** qua `apiClient` của backend). Trường địa điểm là **2 dropdown phụ thuộc** (chọn tỉnh → load phường; đổi tỉnh → reset phường).
- **Database (`candidate_accounts`):** thêm `province_code` (int?), `province_name`, `ward_code` (int?), `ward_name`. Giữ cột cũ **`location`** nhưng chuyển thành **chuỗi hiển thị suy ra tự động** `"Phường X, Tỉnh Y"` (denormalized, tương thích ngược) — không còn nhập tay. Migration `AddCandidateAdminDivision`.
- **Dữ liệu cũ:** rows có `location` text tự do trước đây vẫn còn nhưng không map sang code → ứng viên chọn lại tỉnh/phường 1 lần là có dữ liệu chuẩn.

### ADR-038: Tối ưu chi phí Phỏng vấn thử — gating theo phễu, không cắt công nghệ
- **Bối cảnh:** Mỗi buổi phỏng vấn (thử & thật) ngốn chi phí streaming đáng kể (HeyGen ~$3/buổi, ElevenLabs TTS, Deepgram STT, GPT-4o). Doanh nghiệp trả tiền cho **cả practice lẫn real** → **mỗi vòng = 1 lượt thử + 1 lượt thật** (multi-round thì nhân theo số vòng ứng viên đi qua).
- **Nguyên tắc:** Practice **không ảnh hưởng verdict** nhưng vẫn cần **đầy đủ công nghệ** để ứng viên làm quen đúng trải nghiệm thật → **không tối ưu bằng cách cắt tech**, mà tối ưu bằng cách **giảm số lượng buổi (phễu)**.
- **Quyết định:**
  1. **Gating theo phễu:** Practice chỉ mở cho ứng viên **đã pass vòng CV** (HR review matchScore + CV → chọn) **và đã đặt lịch buổi phỏng vấn thật của vòng đó** (ADR-020). Vào qua **Portal, không cấp Interview Code** cho practice. Không mở đại trà cho mọi ứng viên job board → chỉ trả tiền thử cho hồ sơ thật sự đi tiếp.
  2. **1 lượt / VÒNG** (mở lại mỗi khi pass vòng + đặt lịch vòng kế); điều kiện chặn theo `(application_id, round_number)` (ADR-027).
  3. **Đầy đủ pipeline** cả practice & real (xem ADR-027). Practice RAG = JD + CV; Real RAG = JD + CV + Playbook.
  4. **Hybrid Idle (ADR-011)** áp dụng cho cả 2 mode — tiết kiệm ~90% HeyGen mà UX giữ nguyên (không phải "cắt tech"). **[SỬA bởi ADR-050]** Practice nay **bỏ hẳn avatar** (audio-only) vì rủi ro cạnh tranh concurrency LiveAvatar với buổi thật > lợi ích UX; điểm 3-4 phần avatar-cho-practice không còn áp dụng.
  5. **Trần cứng** cho practice: giới hạn số câu hỏi + thời lượng để tránh đốt token/STT-phút. **[HIỆN THỰC bởi ADR-050]** trần thời lượng 20 phút (`Interview:PracticeMaxDurationMinutes`) — hết giờ AI nói câu kết rồi đóng phiên.
  6. **Recording practice: không quay video, chỉ transcript** + Evaluation Report.
  7. Tái dùng embeddings JD+CV đã sinh ở bước CV-JD Analysis (không embed lại).
- **Thay đổi liên quan:** Cập nhật ADR-013/015/020 (scheduling = phỏng vấn thật theo vòng, đặt lịch mở practice), ADR-016 (bỏ `code_type`; Interview Code chỉ cho real), ADR-027 (truy cập Portal + 1 lượt/vòng + recording).

### ADR-039: RAG tách thành microservice Python riêng (đã MỞ RỘNG ranh giới)
- **Quyết định:** Pipeline RAG tách khỏi backend .NET thành **service Python độc lập** (FastAPI + LangChain + LangGraph), ở thư mục `rag-service/`. Backend .NET gọi qua HTTP/REST nội bộ.
- **Lý do:** Hệ sinh thái RAG/embedding/LLM-tooling phong phú hơn ở Python; tách service để scale & deploy độc lập, không nặng backend chính.
- **Ranh giới (MỞ RỘNG 2026-06-26):** Python sở hữu **TOÀN BỘ** pipeline: chunk + embed + **hybrid retrieve** + **sinh câu hỏi/đánh giá/đánh giá ngôn ngữ** (không chỉ retrieval/embedding như bản gốc). .NET chỉ orchestrate session/SignalR/persistence. Giữ nguyên abstraction `IAIProvider` + `IEmbeddingProvider` (ADR-004, rule #8): thêm impl `RagServiceProvider` (HTTP/SSE client → Python) + interface mới `IRagIngestionService`. `OpenAIProvider` giữ làm fallback in-process qua cờ `AI:Provider` (`rag` | `openai` | `local`); khi không dùng rag, ingestion chạy in-process qua `LocalRagIngestionService`. Gemini (CV-JD/JD-extract, ADR-030/042) **không đổi**, vẫn ở .NET.
- **Lộ trình (3 giai đoạn, cùng 1 LangGraph StateGraph):**
  - **Giai đoạn 1 (đã làm):** Hybrid RAG = dense (pgvector cosine `<=>`) + sparse (Postgres full-text `ts_rank`) → hợp nhất Reciprocal Rank Fusion + weighting theo scope (ADR-025).
  - **Giai đoạn 2:** CRAG — chèn node `grade_documents` + corrective (rewrite query / re-retrieve).
  - **Giai đoạn 3:** Agentic — router/tool nodes (agent quyết định bước tiếp theo).
- **Hợp đồng (đã triển khai):** `POST /ingest` (chunk+embed+lưu, idempotent theo (source_type, source_id)), `POST /retrieve` (query text + scope → chunks xếp hạng), `POST /next-question` (**SSE stream** token, chạy LangGraph), `POST /analyze-answer` · `/evaluate` · `/detect-language` · `/assess-language` · `/complete-json` · `/embed`. Practice: scope JD+CV; Real: thêm Playbook (ADR-025/027). Wire JSON camelCase.
- **Dữ liệu:** pgvector vẫn trên PostgreSQL/Supabase; service Python kết nối trực tiếp qua asyncpg (không Supabase SDK). Bảng `document_chunks` **vẫn do EF Core (.NET) sở hữu schema**; Python đọc/ghi dữ liệu. Sparse cần GIN index FTS → migration `20260626000000_AddDocumentChunksFtsIndex` (`to_tsvector('simple', chunk_text)`).
- **Hạ tầng:** container `rag-service` (port 8000) trong Docker Compose, mạng `arisp-network`; biến `RAG_SERVICE_URL`; **không** expose ra Nginx (chỉ nội bộ).
- **Trạng thái:** **Giai đoạn 1 đã triển khai** (2026-06-26). Giai đoạn 2 (CRAG) & 3 (Agentic) còn backlog.
- **Ràng buộc:** Không vi phạm "không Node.js cho backend" (Python microservice cho RAG; backend chính vẫn .NET 8). Mock mode (thiếu `OPENAI_API_KEY`) để test pipeline không cần key.

### ADR-040: Cổng kiểm tra thiết bị bắt buộc (mic + cam) trước phỏng vấn
- **Quyết định:** Ứng viên **chỉ được vào phỏng vấn (cả thử & thật)** khi **camera và micro hoạt động**. Bắt buộc qua bước Device Check trước khi vào phòng.
- **Triển khai FE:** component dùng chung `components/interview/DeviceCheck.tsx` — `getUserMedia({video,audio})`, preview camera + đo mức âm mic (Web Audio AnalyserNode), chặn nút "Bắt đầu" cho đến khi cả hai track `live`; xử lý từ chối quyền / thiếu thiết bị + nút Thử lại; bàn giao luôn `MediaStream` đang chạy cho phòng phỏng vấn (tránh prompt quyền lần hai).
- **Áp dụng:** Practice (`PracticeSessionPage`) đã tích hợp; Real/Kiosk sẽ tái dùng cùng component khi dựng (Phase 7).

### ADR-041: Vòng đời tài khoản staff — Yêu cầu tạo (HR→SA) tách khỏi Khóa/Mở khóa
- **Bối cảnh:** Pre-provisioning (ADR-023) khiến không có "user mới chờ duyệt" thật; cờ `User.IsActive=false` chỉ phát sinh khi Super Admin **khóa** tài khoản. Trang "Duyệt User mới" trước đây query `!IsActive` nên hiển thị nhầm tài khoản bị khóa thành "chờ duyệt".
- **Quyết định:** Tách 2 vòng đời độc lập:
  1. **Yêu cầu tạo tài khoản** (entity mới `AccountRequest`, bảng `account_requests`): HR Leader gửi yêu cầu (lẻ hoặc bulk cùng `BatchId`) → Super Admin **duyệt** (tạo `User` active + email mật khẩu tạm) hoặc **từ chối** (kèm lý do). Mỗi dòng = 1 tài khoản đề xuất. Trang "Duyệt tài khoản mới" chỉ hiển thị `account_requests` status=`pending`.
  2. **Khóa / mở khóa**: thêm `User.LockReason` (bắt buộc nhập lý do khi khóa); quản lý trong "Tất cả người dùng" (badge "Bị khóa" + lý do + nút "Mở khóa"). Khóa = `user_deactivated`, mở = `user_activated` (xóa LockReason).
- **API:** `POST/GET /api/hr/account-requests` (policy HrManagement); `GET /api/admin/account-requests?status=`, `POST .../{id}/approve|reject` (SuperAdminOnly). Mọi hành động ghi `AuditLog`.
- **FE (2026-06-21):** màn HR Leader **"Nhóm HR"** (`/hr/team`) đã làm: gửi yêu cầu lẻ/hàng loạt + **import CSV** (kèm tải template) + theo dõi trạng thái yêu cầu của mình (pending/approved/rejected + lý do từ chối).
- **Chưa làm (phase sau):** cơ chế **kháng cáo mở khóa** (người bị khóa gửi lý do xin gỡ) — dự kiến `UnlockAppealReason` / bảng appeals riêng.

### ADR-042: Recruiter workspace cụm Job + Gemini trích xuất JD để auto-fill (mở rộng ADR-030)
- **Bối cảnh:** Recruiter cần (1) dashboard chỉ hiển thị tin **của chính mình**, (2) xem **ứng viên theo từng job** (không phải toàn bộ), (3) khi tạo tin phải đính kèm **file JD (PDF/DOCX)** và muốn tự động điền các trường từ JD. Workflow duyệt tin (Recruiter `draft→pending` → HR Leader `active/rejected`) đã có sẵn ở `JobsController`.
- **Quyết định:**
  1. **Scope theo người tạo:** `GET /api/jobs/admin?mine=true` lọc `CreatedByUserId == currentUser` (Recruiter); không có `mine` = toàn bộ (HR/SA).
  2. **Ứng viên theo job:** `GET /api/jobs/{id}/applications` (InternalStaff) — owner-or-admin check; trả `ApplicationResponse` kèm `MatchScore`, resolve `CvFileUrl` (storageKey → URL).
  3. **Phân tích JD (mở rộng ADR-030/rule 18):** `POST /api/jobs/analyze-jd` (multipart) → parse text (`IDocumentParserService`) + lưu file (`IFileStorageService`) + gọi **Gemini 2.5 Flash** (`IGeminiProvider.ExtractJobFromJdAsync`, PDF gửi inline / DOCX fallback text) trích xuất `title, department, jobDescription, jobCategory, experienceLevel, employmentType, workMode, location, skills, languageRequirement, salary*`. Trả `AnalyzeJdResponse` gồm storageKey file JD + dữ liệu auto-fill. Gemini từ nay dùng cho **CV-JD Analysis _và_ JD extraction** (không dùng cho phỏng vấn AI/RAG — vẫn GPT-4o).
  4. **File JD lưu vào job:** `JobPosting.JdFileUrl/JdFileName/JdFileFormat` được điền qua Create/Update (UpdateJob chỉ ghi đè khi request gửi file mới). FE bắt buộc upload+phân tích JD trước khi **tạo** tin.
- **FE:** `RecruiterLayout` chuyển sang `WorkspaceLayout` dùng chung (theme sáng/tối). Cụm màn: Dashboard (lưới tin của tôi), Tin tuyển dụng (list + filter trạng thái), **Job Detail mới** (`/recruiter/my-jobs/:id`: phễu ứng viên theo trạng thái + danh sách ứng viên của job + gửi magic link + đổi trạng thái tin), Create/Edit (`/recruiter/my-jobs/:id/edit`) với card upload & phân tích JD auto-fill.
- **Chưa làm (phase sau):** màn HR Leader duyệt tin (đã có API), màn "Cấp Interview Code", redesign Candidates/Evaluations/Interviews của Recruiter.

### ADR-043: Chốt media stack phỏng vấn realtime — Cascaded, Deepgram Nova-3, Flash v2.5, LLM GPT-4o (Claude là option)
- **Bối cảnh:** Ứng viên phỏng vấn **trực tiếp** với AI → cần chất lượng + độ trễ càng gần realtime càng tốt. Cân nhắc 2 kiến trúc: **(A) Cascaded** (STT→RAG→LLM→TTS→Avatar, rời) vs **(B) Speech-to-Speech realtime** (OpenAI Realtime / Gemini Live, audio-in→audio-out, ~0.3–0.8s).
- **Quyết định — chọn (A) Cascaded tối ưu** (~0.8–1.2s), KHÔNG dùng speech-to-speech. Lý do: bảo toàn **kiểm soát RAG/must-ask/đánh giá/language assessment/transcript** và avatar HeyGen — vốn là lõi bài toán phỏng vấn; tận dụng hạ tầng đã có (HeyGen, Hybrid RAG service). Để dành (B) cho thử nghiệm Practice mode sau.
- **Stack chốt:**
  - **STT + VAD/endpointing:** **Deepgram Nova-3** — gộp 1 dịch vụ (VAD/endpointing tích hợp sẵn), bỏ thư viện VAD rời (xem ADR-005).
  - **TTS:** **ElevenLabs Flash v2.5** (~75ms, tối ưu realtime). Loại v3 (trễ cao hơn).
  - **RAG:** Hybrid RAG microservice Python (ADR-039).
  - **LLM "bộ não":** **giữ GPT-4o** (ADR-004). **Claude là option chiến lược dành sau** nếu cần cải thiện độ trễ/chất lượng.
  - **Avatar:** HeyGen Hybrid Idle (ADR-011).
- **So sánh độ trễ LLM (benchmark 2026, median TTFT — soi theo voice nên TTFT là yếu tố chính, throughput KHÔNG phải nút thắt vì tốc độ nói ~5–8 token/s):**

  | Model | TTFT | Vai trò |
  |---|---|---|
  | GPT-4o (hiện tại) | ~0.4–0.8s | Live |
  | Claude Haiku 4.5 | **~0.28–0.6s** (thấp nhất) | Option live nếu cần TTFT thấp nhất + rẻ |
  | Claude Sonnet 4.6 | ~0.5–0.8s (≈ GPT-4o) | Option live cân bằng chất lượng |
  | Claude Opus 4.8 | ~0.8–1.2s+ (×2.5 Fast Mode) | Option cho đánh giá cuối vòng |
- **Kết luận:** GPT-4o ≈ Claude Sonnet 4.6 về độ trễ → giữ GPT-4o hợp lý. Muốn **giảm trễ thật** → đường nhanh nhất là **Haiku 4.5** (không phải Sonnet/Opus). Lợi thế ẩn của Claude: **tail-latency ổn định** (P50≈P99, ít "đứng hình" giữa buổi) + prompt caching mạnh. Swap-point đã sẵn ở RAG service (`graph.py`/`llm.py` đổi `ChatOpenAI`→`ChatAnthropic`) + .NET `IAIProvider`/`RagServiceProvider`.
- **Đòn bẩy độ trễ chung (làm trước, không phụ thuộc LLM):** partial-STT→RAG song song; TTS first-sentence; prompt caching prefix ổn định; **TẮT thinking** ở model live; gọi thẳng OpenAI (không Azure). Xem ADR-006.

### ADR-044: Nối media stack thực tế — client-SDK + BE mint token (triển khai phỏng vấn thử)
- **Bối cảnh:** Triển khai luồng phỏng vấn (bắt đầu với **Practice**) end-to-end với Deepgram + ElevenLabs + HeyGen. Cần mô hình tích hợp chuẩn, độ trễ thấp, **không lộ API key ra FE**.
- **Quyết định — client-SDK + BE token broker:**
  - **BE giữ toàn bộ API key thật** (`Media:Deepgram|ElevenLabs|HeyGen` + `AI:OpenAI`), chỉ **mint token ngắn hạn** cho FE qua `GET /api/interview/session/{id}/media-config` (xác thực ứng viên sở hữu phiên).
  - **STT — Deepgram**: FE dùng `@deepgram/sdk` live, auth bằng **ephemeral token** (`/v1/auth/grant`, TTL ~60s) do `DeepgramTokenService` cấp. FE stream mic (MediaRecorder) thẳng tới Deepgram; final transcript → `SubmitAnswerText` (SignalR).
  - **Avatar — HeyGen LiveAvatar (cập nhật 2026-06-30):** Streaming Avatar API cũ (`/v1/streaming.*` + `@heygen/streaming-avatar`) đã **sunset (410)** → migrate sang **LiveAvatar** chế độ **LITE**. `HeyGenAvatarService` mint session token: `POST https://api.liveavatar.com/v1/sessions/token` header `X-API-KEY`, body `{mode:"LITE", avatar_id, is_sandbox}` → `data.session_token`. FE dùng `@heygen/liveavatar-web-sdk`: `new LiveAvatarSession(token, {voiceChat:false, apiUrl})` → `session.attach(videoEl)` (SESSION_STREAM_READY) → `session.repeatAudio(pcm24kBase64)`. **LITE = giữ não RAG/GPT-4o của ta** (avatar chỉ lip-sync audio ta cấp), KHÔNG dùng agent built-in. `avatar_id` LiveAvatar là **UUID** (vd `dd73ea75-...`), khác avatar id Streaming cũ. `is_sandbox=true` cho dev.
  - **TTS — ElevenLabs**: `ElevenLabsTTSService.TextToSpeechBase64PcmAsync` gọi `/v1/text-to-speech/{voice}/with-timestamps?output_format=pcm_24000` → `audio_base64` (PCM 24k) — đúng định dạng `repeatAudio`. Endpoint `POST /session/{id}/tts {text}` → `{audio}`; FE fetch rồi đẩy vào avatar. ElevenLabs là **giọng chính** của avatar (LITE), không còn phụ thuộc TTS nội bộ HeyGen.
  - **LLM**: giữ `IAIProvider` (OpenAIProvider in-process `AI:Provider=openai`, hoặc RagServiceProvider khi `=rag`). "AI API ở bước cuối" chỉ là điền key.
  - **Sự kiện realtime**: `SignalRNotificationService` (thật, thay `MockNotificationService`) đẩy `ReceiveQuestion`/`ReceiveSessionStatus` tới `SessionHub` group = `sessionId`.
- **Fallback mềm:** thiếu HeyGen (key/avatarId) → browser `speechSynthesis` + bot tĩnh; thiếu Deepgram → nhập tay + nút "Gửi trả lời"; thiếu OpenAI key → mock câu hỏi. Mỗi provider bật độc lập theo việc có key hay không (DI chọn real vs `Mock*`).
- **Vì sao client-SDK thay vì BE relay WebRTC/audio:** đúng cách HeyGen/Deepgram SDK hoạt động, độ trễ thấp nhất, BE nhẹ (không relay media — nhất quán ADR-008). Giữ nguyên abstraction `ISTTProvider`/`ITTSService`/`IAvatarService`.
- **Recording practice:** không quay video (ADR-027) — chỉ transcript (lưu khi `SubmitAnswerText`) + Evaluation Report.
- **Ghi chú gói:** đã migrate `@heygen/streaming-avatar` (sunset) → **`@heygen/liveavatar-web-sdk`** (kéo theo `livekit-client`). FE chunk practice ~590KB (lazy-load, chấp nhận được cho trang media).

### ADR-045: Refactor Clean Architecture chuẩn JT template — `ari-service/` + `ARI.*` + CQRS/MediatR
- **Ngày:** 2026-07-19. **Branch:** `refactor/be/clean-architecture` (base `origin/develop`, tag rollback `pre-clean-arch-refactor`).
- **Bối cảnh:** Hướng phụ thuộc 4 project đã đúng Clean Architecture nhưng ruột sai layer: 14 controllers (~6.900 dòng) ôm business logic (inline BCrypt/JWT, query + mapping thủ công ~300 call sites, orchestration Gemini/RAG/storage); DI viết tay 521 dòng trong `Program.cs` kèm raw SQL bootstrap bù schema thiếu migration.
- **Quyết định (refactor thuần cấu trúc — routes/DTO shapes/auth policies/hub paths/DB schema KHÔNG đổi):**
  1. **Rename:** `backend/` → `ari-service/` layout JT (`src/` + `tests/`); project/namespace `ARISP.*` → `ARI.*` (PascalCase); `ARISPDbContext` → `AriDbContext`. Brand values giữ nguyên (JWT Issuer `ARISP`, cookie `ARISP.External`, swagger title, email templates). Migration IDs + `ef_migrations_history` không đổi.
  2. **CQRS + MediatR** (pin cứng **[12.5.0]** — bản Apache-2.0 cuối, v13+ commercial license Lucky Penny) + FluentValidation 11.x. **KHÔNG AutoMapper** (v15 cũng commercial; projection thủ công là load-bearing). Vertical-slice feature folders: `Auth/ Admin/ AccountRequests/ Applications/ CandidatePortal/ CvAnalysis/ Dashboard/ Evaluations/ Interviews/ Jobs/ Playbooks/ Scheduling/ StaffNotifications/` (~110 commands/queries). Controllers thin: trích claims + guard IFormFile + `ISender.Send` + map `Result`→HTTP y hệt status/body cũ.
  3. **Pipeline Behaviours:** UnhandledException → Logging (pre-processor) → **Validation trả `Result.Failure` thay vì throw** (deviation JT có chủ đích — giữ Result Pattern rule) → Performance (warn >500ms). `Result.ErrorCode` (additive) để controller map failure → đúng 401/403/404/409/500 cũ.
  4. **DI theo JT:** `AddApplication()` / `AddInfrastructure()` / `AddWebServices()`; `Program.cs` 521 → ~60 dòng; `AriDbContextInitialiser` (migrate-retry); `ValidateScopes/ValidateOnBuild` ở Development.
  5. **Schema 100% do migrations sở hữu:** raw SQL bootstrap gộp vào migration `ReconcileStartupBootstrap` (idempotent `IF NOT EXISTS` — an toàn DB bootstrap đầy đủ/dở dang/trống); 16 index bootstrap khai báo tường minh trong `OnModelCreating`; hợp nhất index trùng `IX_applications_cv_jd_analysis_id`.
  6. **Service dùng chung sau interface** (rule: 1 consumer → absorb vào handler; ≥2 consumers hoặc hub → giữ service): `IInterviewService` (**SessionHub gọi TRỰC TIẾP, không qua MediatR — critical path ADR-006**), `IInterviewCodeService`, `ICvJdAnalysisService`, `IApplicationService` (deviation: giữ nguyên thay vì dissolve — logic vốn đã ở Application layer; dissolve toàn phần là follow-up). Đã XÓA: `EvaluationService`, `PlaybookService` (1 consumer). Mới: `ITokenService`/`JwtTokenService`, `IPasswordHasher`/`BcryptPasswordHasher` (Infrastructure/Identity), `TokenHashing` (Sha256Base64 cho refresh token, Sha256Hex cho invite token — 2 format cùng tồn tại trong DB).
- **Không làm (follow-up):** tách `IEntityTypeConfiguration` khỏi `OnModelCreating` (convention loop snake_case chạy trước override là load-bearing — cần fingerprint verification riêng); dissolve toàn phần `ApplicationService`; move các file `DTOs/` còn lại vào feature folders.
- **Verify từng phase:** build xanh, swagger.json diff = RỖNG so baseline (98 paths — chống vỡ FE), model fingerprint trước/sau rename identical, migration reconcile áp lên dev DB đúng 1 row history + scaffold thử ra migration rỗng, smoke ~90 cases so status/body verbatim bằng JWT tự mint.

### ADR-046: Refactor Frontend — `ari-web/` monorepo + tách ARI.CandidateSite / ARI.StaffSite / ARI.Shared
- **Ngày:** 2026-07-20. **Branch:** `refactor/fe/clean-architecture` (base `origin/develop`, tag rollback `pre-fe-clean-arch-refactor`). Mirror ADR-045 phía FE.
- **Bối cảnh:** `frontend/` là 1 SPA Vite/React/TS duy nhất (~177 file, ~34k dòng) trộn mọi role; site ứng viên (public, cần deploy) và site nội bộ (HR/Recruiter/Super Admin) đóng gói chung một bundle. Cần tách 2 site deploy độc lập + tầng dùng chung, khớp Clean Architecture của backend.
- **Quyết định (refactor thuần cấu trúc — URL routes / API paths / DTO / localStorage keys `arisp-auth`,`theme`,`arisp-language` / hub paths / env var names KHÔNG đổi):**
  1. **Rename:** `frontend/` → `ari-web/` (mirror `backend/`→`ari-service/`), là **npm workspaces root** (1 `package-lock.json` duy nhất). Ba package dưới `ari-web/src/`: **`ARI.CandidateSite`** (`@ari/candidate-site`, port 3000, public — job board, portal, practice/real interview, **kiosk**, candidate auth); **`ARI.StaffSite`** (`@ari/staff-site`, port 3001, nội bộ — HR/Recruiter/Super Admin, staff auth); **`ARI.Shared`** (`@ari/shared`, không build step, import source-level qua alias). Folder giữ PascalCase `ARI.*`; brand "ARISP" giữ nguyên.
  2. **Quy tắc "f" prefix:** folder service-like thêm `f` → **`services/` → `fservices/`** (mọi package). Các folder khác giữ tên thường.
  3. **Tách concern (mỗi folder một nhiệm vụ), 2 site cùng khuôn:** `app/` (composition root: `main.tsx` + `App.tsx` router + `layouts/`) + `pages/` (theo domain/role) + `fservices/` (tầng API) + `components/` (UI tái dùng) + `i18n/`; StaffSite thêm `utils/`. **`fservices` mirror tên feature slice backend** (Jobs/Applications/Evaluations/Dashboard/Playbooks/Admin/AccountRequest/… = tầng API ↔ ARI.Application slices).
  4. **ARI.Shared** = phần dùng chung đo bằng import thực tế: `api/apiClient` (+ `configureApiClient({refreshPath})`), `fservices/` chung (auth, job, application, interview, notification, schedule, profile), `ui/` (design system `designSystem` + primitives kit + common), `guards/`, `document/`, `media/` (DeviceCheck + practice/room/cheat hooks — pipeline SignalR/Deepgram/HeyGen gom 1 nơi), `realtime/useAppNotifications`, `store/` (auth/theme/interview), `types/`, `config/`, `utils/`, `authflows/` (OAuthCallback/Forgot/Reset — 2 site cùng route), `i18n/` core (`initI18n` + `sharedResources`), `styles/`, `tailwind-preset.cjs`. **Import qua subpath** (`@ari/shared/ui`, `@ari/shared/fservices/job`…), KHÔNG mega-barrel (giữ lazy-chunk, tránh kéo HeyGen SDK vào mọi chunk). Resolution = tsconfig `paths` + vite `alias` + `resolve.dedupe` react runtime; hướng phụ thuộc 1 chiều: site → Shared, Shared không bao giờ import site.
  5. **`configureApiClient({ refreshPath })`:** apiClient dùng chung, mỗi site cấu hình endpoint refresh lúc bootstrap. Staff = `/auth/refresh`, **Candidate = `/auth/candidate/refresh`** (E-2: sửa lỗi cũ — apiClient mặc định gọi endpoint staff nên phiên candidate bị đá ra khi refresh; backend đã có sẵn endpoint candidate).
  6. **Router 2 bảng, URL byte-for-byte không đổi:** candidate giữ `/`, `/jobs*`, `/candidate/*`, `/interview/*`, `/kiosk`, `/portal/schedule/*`, candidate auth, legal, landing; staff giữ `/auth/login`, `/hr/*`, `/recruiter/*`, `/super-admin/*`. **Xóa `StaffRedirect`** (2 origin riêng → token staff không tồn tại trên origin candidate → component unreachable); thay bằng **`StaffHomeRedirect`** ở route `/` của StaffSite (đã login → dashboard theo role, chưa → `/auth/login`) — route glue MỚI duy nhất, reuse path `/` sẵn có nên union route == baseline (65).
  7. **Deploy host-based (không path-prefix):** vì `/auth/*` + `/assets/*` trùng path giữa 2 SPA. Nginx 2 server block: `localhost`→candidate:3000, `staff.localhost`→staff:3001; cùng share `/api/`,`/hubs/`,`/uploads/`. 1 Dockerfile với ARG `PKG/SITE/PORT`, context = workspace root (1 lock, layer-cache manifests trước `npm ci`); compose `frontend-candidate` + `frontend-staff`.
  8. **CORS/OAuth 2 origin (fallback-safe):** thêm key `Frontend:CandidateBaseUrl` (3000) vào `allowedOrigins` — **KHÔNG comma-list `Authentication:AdminFrontendUrl`** (3001 staff) vì `BuildRedirectUrl`/email builders dùng raw string (StartsWith); `BuildRedirectUrl` chấp nhận returnUrl thuộc AdminFrontendUrl HOẶC CandidateBaseUrl; email candidate (verify-email, reset-password) ưu tiên CandidateBaseUrl. Khi 2 key trỏ cùng origin (prod hiện tại) → behavior y hệt hôm nay.
- **Waves (mỗi commit build xanh, `git mv` giữ history):** 0 (rename + workspaces) → A (ARI.Shared) → B (ARI.StaffSite carve-out) → C (ARI.CandidateSite tách concern) → D (infra + CORS/OAuth) → E (close-out: xóa stale + fix candidate refresh + docs).
- **Không làm (follow-up):** thống nhất `authService` (đang raw `fetch`) về apiClient + sửa bug URL logout backslash (`authService.ts` `\auth\logout` → `.../apiauthlogout`, POST fail âm thầm); gỡ `@stomp/stompjs` (không dùng); thêm ESLint `no-restricted-imports` chặn Shared→site; nested `features/<Feature>/{pages,components,fservices}` sâu hơn (hiện dừng ở tách concern top-level do coupling `_jobUi`/`_skeletons` theo role).
- **Verify từng wave:** `npm run build` mọi package xanh + `npm run lint` 0 error; **route freeze**: union path 2 `App.tsx` == baseline Wave 0 (65=65, không thêm URL mới); **i18n freeze**: namespace mỗi site ⊆ ns đăng ký, không cheo giữa 2 site; backend build 0 error; tailwind emit đúng token từ Shared (`content` glob gồm `../ARI.Shared/src/**`).

### ADR-047: CI/CD GitHub Actions — build ở runner → GHCR → VPS pull; `main` là production
- **Ngày:** 2026-07-22. **Branch:** `chore/infra/prod-deploy` (base `origin/develop`).
- **Bối cảnh:** VPS `arisp.io.vn` (4 vCPU / 3.8GB RAM / 4GB swap) deploy **thủ công 100%** từ nhánh `develop`: `git pull && docker compose up -d --build`. Không có workflow nào (`.github/workflows/` rỗng). Bốn file config prod bị sửa trực tiếp trên server bằng `nano`/`sed` và chưa từng quay lại repo — `git reset --hard origin/develop` (đã từng chạy) sẽ xoá sạch và làm sập site. Sau khi merge ADR-046, lần deploy kế tiếp còn là bước nhảy kiến trúc 1 SPA → **2 SPA trên 2 origin**, cần build 2 bản Vite + .NET publish + pip install cùng lúc — vượt khả năng của 3.8GB RAM và gây downtime ~10 phút.
- **Quyết định:**
  1. **Build ở GitHub Actions runner, KHÔNG build trên VPS.** `deploy.yml` build song song 4 image (`arisp-backend`, `arisp-candidate`, `arisp-staff`, `arisp-rag`) với buildx + cache `type=gha`, push GHCR tag `:${{ github.sha }}` và `:latest`. VPS chỉ `docker compose pull && up -d` (~30 giây). Auth GHCR bằng `GITHUB_TOKEN` — không cần secret thêm.
  2. **`main` = production, `develop` = integration.** `deploy.yml` chỉ trigger trên `push: main`. Server đổi upstream từ `develop` sang `main`.
  3. **Compose khai báo cả `image:` lẫn `build:`.** `image: ghcr.io/quannguyendz/arisp-<svc>:${IMAGE_TAG:-dev}` — dev vẫn `docker compose up --build` như cũ (tag `:dev`), prod `IMAGE_TAG=<sha> ... pull && up -d`.
  4. **Tách `nginx/conf.d.prod/` khỏi `nginx/conf.d/`.** Nguyên nhân gốc của drift: prod override mount `../nginx/conf.d` vốn chứa config **dev** (`localhost`/`staff.localhost`), buộc phải sửa tại chỗ trên VPS. Prod compose nay mount `../nginx/conf.d.prod` → config prod nằm trong git, `git reset --hard` trong pipeline an toàn. Hai origin: `arisp.io.vn` → `frontend-candidate:3000`, `staff.arisp.io.vn` → `frontend-staff:3001`, dùng chung 1 cert (`certbot --expand`) và chung `/api/`, `/hubs/`, `/uploads/`.
  5. **`VITE_API_BASE_URL=/api` (tương đối) làm build-arg**, không phải URL tuyệt đối. Vite inline `import.meta.env.VITE_*` lúc compile nên phải truyền ở build time; giá trị tương đối giúp **một image dùng được cho mọi domain** (prod/staging) thay vì khoá cứng. Cả 2 SignalR hub dẫn xuất từ `API_BASE_URL` nên cũng thành relative → tự nâng `wss://` theo origin.
  6. **`ports: !reset []`** (Compose spec ≥ 2.24) thay cho `ports: []`. Danh sách trong override được **nối thêm chứ không thay thế**, nên `ports: []` chưa bao giờ có tác dụng — đó là lý do `rag-service` và `redis` vẫn hở ra `0.0.0.0` trên prod. Sau khi sửa, prod chỉ publish 80/443.
  7. **`ci.yml` trên mọi PR vào `develop`/`main`:** build + test .NET, build cả 2 workspace FE, import-check rag-service. Chặn đúng loại lỗi mà PR #66 (`Fix syntax error frontend`) đã phải vá gấp trên `develop`.
  8. **Deploy phải `restart nginx` sau `up -d`.** Nginx chỉ resolve hostname upstream **một lần lúc khởi động** khi `proxy_pass` dùng hostname tĩnh. Deploy tạo lại `frontend-*`/`backend` → container nhận IP mới trong bridge network, nhưng nginx không được tạo lại (image `nginx:1.27-alpine` không đổi) nên vẫn gọi IP cũ → `502 Host is unreachable`. Phát hiện ở lần deploy tự động đầu tiên: nginx gọi `172.18.0.7:3001` trong khi `frontend-staff` thật ở `172.18.0.5`; candidate thoát nạn do trùng IP ngẫu nhiên. Chọn `restart nginx` (~1 giây) thay vì chuyển toàn bộ `proxy_pass` sang biến + `resolver 127.0.0.11` — cách sau đổi ngữ nghĩa rewrite URI của các location có tiền tố (`/api/`, `/hubs/`, `/uploads/`), rủi ro cao hơn lợi ích.
  9. **Rollback = `workflow_dispatch` với `image_tag` = sha cũ** → bỏ qua job build, VPS pull lại image cũ trên GHCR (~30 giây). `deploy.yml` tự health-check cả 2 origin 120 giây sau khi up.
- **Hệ quả / lưu ý vận hành:**
  - Secrets cần có: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` (deploy key **riêng** cho Actions, không tái dùng key cá nhân).
  - `docker/.env` vẫn nằm ngoài git và sống sót qua `git reset --hard`. `docker/.env.example` được khôi phục, ghi rõ **hai hệ tên biến**: backend .NET dùng `__` (map sang `IConfiguration`), rag-service Python dùng `UPPER_SNAKE` phẳng (`DATABASE_*`, `OPENAI_API_KEY`, `APP_ENV`). Không suy ra được của nhau — thiếu nhóm Python từng khiến `arisp-rag` crash-loop 6928 lần rồi chạy mock mode âm thầm.
  - Prerequisite hạ tầng: DNS `A staff.arisp.io.vn → 161.248.147.38` + `certbot --expand -d arisp.io.vn -d www.arisp.io.vn -d staff.arisp.io.vn`; `.env` đặt `Authentication__AdminFrontendUrl=https://staff.arisp.io.vn` (nguồn CORS, `DependencyInjection.cs:206`); Google Console thêm `https://staff.arisp.io.vn` vào JavaScript origins (redirect URI trỏ backend nên không đổi).
- **Không làm (follow-up):** `vite.config.ts` đang bật `sourcemap: true` nên `.map` chứa toàn bộ source được ship lên prod (đáng lưu ý nhất với StaffSite) — nên tắt hoặc chỉ upload cho error tracking; UI quản lý trực quan (Portainer/Dozzle) tách riêng để không trộn hai thay đổi hạ tầng; siết `sshd` `PasswordAuthentication no` sau khi xác nhận cả 2 key vào được.

### ADR-050: Phỏng vấn thử (Practice) — audio-only, trần 20 phút + AI câu kết, nhập kép voice/keyboard
- **Ngày:** 2026-07-24. **Branch:** `feature/ai/practice-audio-timer-input` (base `origin/develop`). *(Ban đầu đánh số ADR-048; khi merge vào develop bị TRÙNG với ADR-048 "HR gán cứng lịch"; ADR-049 lại đã bị "Online Test" chiếm → đổi thành **ADR-050**.)*
- **Bối cảnh:** Practice chạy remote, số ứng viên thi thử đồng thời **không lường trước được**. LiveAvatar (LITE, 1 credit/phút) giới hạn **concurrency theo tài khoản** (Essential 20 / Business 40). Nếu practice cũng gọi avatar, một đợt đông thi thử có thể **chiếm hết slot concurrency → buổi phỏng vấn THẬT on-site không còn tài nguyên avatar**, đồng thời đốt credit khó kiểm soát. ADR-038 điểm 3-4 trước đây yêu cầu practice giữ đủ tech *gồm avatar* — nay đảo lại vì rủi ro production > lợi ích UX.
- **Quyết định:**
  1. **Practice audio-only (bỏ avatar):** `GetMediaConfigAsync` KHÔNG mint avatar token khi `SessionType=="practice"` → `HeyGen=null` → FE phát audio ElevenLabs qua WebAudio (`playPcmViaWebAudio`), hiển thị bot tĩnh nhấp nháy theo `aiSpeaking`. **Giữ đủ** STT Deepgram + RAG + GPT-4o + **giọng ElevenLabs** + transcript. Cờ `Interview:PracticeUseAvatar=false` cho phép bật lại khi cần. **Real luôn có avatar.**
  2. **Trần thời lượng 20 phút** (`Interview:PracticeMaxDurationMinutes=20`, config toàn cục — không HR-knob per-job): media-config trả `MaxDurationSeconds` → FE vẽ đếm ngược (đếm theo giờ máy, tránh lệch clock). **Hết giờ → khoá mic → AI nói 1 câu kết thúc → đóng phiên.** Hai lớp enforce: (a) FE `NotifyTimeout` (hub) cho trường hợp ứng viên im lặng, guard server `elapsed ≥ 95% cap`; (b) server tự chặn trong `GenerateAndSendNextQuestionAsync` (`forceClosing` khi `elapsed ≥ cap`) cho trường hợp nói quá giờ. Câu kết dùng chung `CloseWithFarewellAsync` (idempotent, chống race) — tách từ khối closing inline; `EndSessionAsync` cũng guard `Status=="completed"` → no-op.
  3. **Nhập kép (voice + keyboard):** transcript Deepgram append vào **một nguồn `answerText`** hiển thị trong `<textarea>` ứng viên **sửa/gõ tay được** trước khi Gửi (đúng "nhập tay" mà ADR-044 hứa nhưng chưa build). Nút **Mic on/off**: tắt mic = ngừng gửi audio + ngừng append → gõ phím tự do; sửa lại đoạn thu âm nghe sai. Không auto-submit (giữ nút "Gửi trả lời" thủ công).
- **KHÔNG làm (đã cân nhắc & loại):** ghi âm practice → Cloud Storage + tự xoá 7 ngày — **huỷ**, giữ nguyên ADR-038 điểm 6 (practice chỉ transcript, không recording).
- **Khuyến nghị gói LiveAvatar (ghi lại, chưa mua):** sau khi practice off-avatar, gói chỉ cover **real** (on-site, có lịch, ít đồng thời). Giờ giữ `is_sandbox=true` + Free để dev/test. Khi real chạy thật: **Essential $99** (1.000 credit, bỏ watermark, cap 20'/session, 20 concurrency) nếu làm Hybrid Idle + vòng ≤20' + ≤20 đồng thời; ngược lại **Business $475** (5.000 credit, cap 60', 40 concurrency, +1 custom avatar branding). Free/Starter loại vì **có watermark** (buổi thật đại diện công ty). Chặn cứng: avatar hiện nối suốt buổi (Hybrid Idle ADR-011 chưa code) → vòng > cap phút/session sẽ đứt.
- **Ảnh hưởng ADR khác:** SỬA ADR-038 điểm 3-4 (avatar-cho-practice) + HIỆN THỰC điểm 5 (trần thời lượng); ADR-011 Hybrid Idle nay chỉ còn ý nghĩa cho **real** (practice không avatar); ADR-044 fallback WebAudio nay là **đường chính** của practice chứ không phải fallback.
- **Dev test (seed):** `POST /api/dev/seed-practice` — endpoint **chỉ hoạt động khi `IsDevelopment()`** (prod trả 404, `AllowAnonymous` vì bootstrap trước login), tạo idempotent 1 `CandidateAccount` (`EmailVerified=true`) + `JobPosting` (`active`, JD vi, **`SalaryCurrency="VND"`** vì cột NOT NULL) + `Application` (`Status="interview"` → `PracticeEligible`) + `AvailabilitySlot`/`InterviewBooking`, trả `practiceUrl` + tài khoản. `?fresh=true` tạo application mới. Kết hợp `Interview:PracticeAttemptsPerRound=0` (đã có) để test lặp vô hạn. Xoá ma sát dựng data thủ công qua toàn phễu. Đã verify E2E trên Supabase thật (seed OK, idempotent, fresh).
- **File chạm:** BE `InterviewService.cs` (guard avatar, `CloseWithFarewellAsync`, `PracticeTimeoutCloseAsync`, enforce cap, `EndSessionAsync` idempotent), `InterviewOptions.cs`, `InterviewDTOs.cs`, `SessionHub.cs` (`NotifyTimeout`), `IInterviewService.cs`, `appsettings.json`, `docker/.env.example`, `Dev/SeedPractice/*` + `DevController.cs` (seed). FE `usePracticeSession.ts` (answerText/timer/mic), `PracticeSessionPage.tsx` (đếm ngược, textarea, mic toggle), `interviewService.ts`, i18n `practice.json` (vi/en).

### ADR-048: HR gán cứng lịch phỏng vấn cho ứng viên (đảo chiều ADR-015 phần đặt lịch)
- **Ngày:** 2026-07-23. **Branch:** `fix/interview-code`.
- **Bối cảnh:** Theo ADR-015, sau khi qua CV ứng viên tự vào Portal chọn 1 khung giờ trống (`AvailabilitySlot` có `capacity`) → tạo `InterviewBooking`. Yêu cầu nghiệp vụ mới: **nhân sự chủ động ấn định 1 giờ cụ thể cho từng ứng viên** thay vì để ứng viên tự chọn (kiểm soát lịch phỏng vấn thật tại văn phòng chặt hơn).
- **Quyết định:**
  1. **HR gán từ kho slot có sẵn.** Giữ nguyên phần Recruiter/HR tạo `AvailabilitySlot` per job+vòng (`POST /api/schedules/slots`). Thêm `POST /api/schedules/assign` (`AssignSlotCommand`, policy `InternalStaff`) — staff chọn 1 slot trong kho ấn định cho `application_id` + vòng. Tái dùng nguyên `AvailabilitySlot`/`InterviewBooking`/`capacity`, không đổi schema.
  2. **Bỏ hẳn luồng ứng viên tự đặt.** Xoá `GET /api/schedule/{id}/slots` + `POST /api/schedule/{id}/book` (và handler `GetOpenSlotsQuery`/`BookSlotCommand`, helper `AuthorizeCandidateAsync` dùng token lời mời để đặt). `CandidateScheduleController` chỉ còn `GET /api/candidate/schedule` (read-only). FE `SchedulePage` chuyển sang chỉ hiển thị giờ đã gán.
  3. **Giữ nguyên side-effects cũ để không vỡ downstream.** `AssignSlotCommand` lặp lại đúng hành vi booking trước đây: chốt chỗ **nguyên tử** chống overbooking (`UPDATE ... WHERE booked_count < capacity`), chặn trùng vòng (1 booking `scheduled`/vòng), chuyển `screening → interview` (hết mâu thuẫn "đã có lịch mà vẫn sàng lọc"), đánh dấu `InterviewInvite.ScheduledAt`. Nhờ vậy gating cấp Interview Code (`HasScheduledInterview`) và mở phỏng vấn thử vẫn hoạt động y hệt.
  4. **Thông báo ứng viên do phía gán phát.** Assign đẩy realtime `ReceiveUserNotification {Type:"InterviewScheduled"}` + tạo `Notification` (bell, dedup `schedule_assigned:{app}:{round}`) + gửi email kèm giờ hẹn (giờ VN, best-effort). Email "duyệt CV" (`SendInterviewInviteAsync`) bỏ pick-link, đổi thành "nhân sự sẽ xếp lịch"; vẫn tạo `InterviewInvite` để giữ cơ chế đánh dấu vòng (`highestRoundInvites` tính `CurrentRound`).
  5. **`InterviewInvite`/magic-link không còn dùng để đặt lịch** — chỉ còn vai trò đánh dấu vòng đã mở. Ứng viên xem lịch + luyện tập qua đăng nhập Portal (không còn token đặt lịch).
  6. **Ứng viên xác nhận / báo bận lịch (bổ sung 2026-07-25).** Sau khi HR gán, ứng viên **XÁC NHẬN** hoặc **TỪ CHỐI kèm lý do** để nhân sự xếp lịch khác. Thêm 3 cột trên `InterviewBooking` (migration `AddBookingConfirmation`): `confirmation_status` (`pending`|`confirmed`|`declined`, default `pending`), `decline_reason`, `responded_at`. 2 endpoint ứng viên (`CandidateOnly`): `POST /api/candidate/schedule/{bookingId}/confirm`, `POST /api/candidate/schedule/{bookingId}/decline` (`ConfirmScheduleCommand`/`DeclineScheduleCommand`, xác thực booking thuộc hồ sơ của ứng viên). **Decline** đặt `Status="declined"` + **trả 1 chỗ** cho slot (`GREATEST(booked_count-1,0)`) → gỡ khỏi partial-unique `ux_..._scheduled` để staff gán lại bình thường qua chính `AssignSlotCommand` (không cần luồng huỷ riêng — giải quyết follow-up cũ). Assign khi có booking `declined` trước đó set `RescheduledFromId`. `GET /api/candidate/schedule` đổi shape → `{upcoming, past, awaitingReschedule}` (kèm `bookingId`+`confirmationStatus`+`declineReason`). Realtime cho nhân sự: `ReceiveScheduleResponse` (chủ tin + nhóm `hr_admin`); staff bell sync thêm mục `schedule_response:{bookingId}`. `AssignSlotCommand` đổi dedup notification sang `schedule_assigned:{booking.Id}` (xếp-lại luôn báo lại) + link ứng viên `/portal/schedule/{app}`.
- **Hệ quả / lưu ý:**
  - UI gán nằm ở cột thao tác trang chi tiết ứng viên (HR + Recruiter), qua component chung `@ari/shared/ui/AssignSchedulePanel`: nạp `getSlots(jobPostingId, round)`, lọc slot tương lai còn chỗ, `assign()` xong refetch hồ sơ (`hasScheduledInterview`/`interviewDate`). Panel hiện **trạng thái xác nhận** của ứng viên (chờ/đã xác nhận) và **banner lý do báo bận** (từ `scheduleConfirmationStatus`/`scheduleDeclineReason` trên `ApplicationResponse`) khi đang chờ xếp lại. Candidate `SchedulePage` có nút Xác nhận / "Tôi bận, đổi lịch" (nhập lý do).
  - `useAppNotifications` còn invalidate query key `open-slots` (nay vô hại — không còn ai đặt). Chưa dọn để giảm nhiễu.
  - Luồng **đổi lịch** phía staff nay đã có (qua decline của ứng viên). Chưa có nút staff **tự huỷ** lịch đã xác nhận (không do ứng viên báo bận) — follow-up nhỏ.
- **Bổ sung 2026-08-07 — Email gộp + nút Confirm/Reject + khoá quyết định + auto-reject (không migration):** giữ nguyên `confirmation_status`/`decline_reason`/`responded_at`.
  1. **Email mời gộp** (`AssignSlotCommand` inject `IConfiguration`): thay email "đã xếp lịch" bằng một email gộp — dòng "qua vòng CV" (vòng 1) + giờ hẹn + **mô tả quy trình theo từng vòng** (đọc `InterviewRoundConfig`, nhãn VN theo `RoundType`: screening/technical/online_test/hr/culture_fit, đánh dấu vòng được mời) + **2 nút deep-link** `{CandidateBaseUrl}/portal/schedule/{appId}?booking={bookingId}&action=confirm|decline` + hộp cảnh báo "mỗi lịch phản hồi 1 lần, không sửa lại được + tự huỷ sau {ConfirmDeadlineHours} giờ".
     - **Chỉ 1 email duy nhất cho luồng duyệt CV → gán lịch:** bước **duyệt CV không còn gửi email** (`SendInterviewInviteAsync` thêm cờ `sendEmail = true`; `AcceptApplicationAsync` gọi `sendEmail:false` — chỉ đổi trạng thái `screening` + tạo `InterviewInvite` token + chuông "qua CV, chờ xếp lịch"). Email gộp ở bước gán slot là **email duy nhất** ứng viên nhận (gộp tin qua CV + lịch). Standalone "Mời" (`SendInterviewInviteCommand`) vẫn `sendEmail:true`. Vì gán slot yêu cầu status ≥ `screening` (đã duyệt CV), không thể gán trước khi duyệt → email luôn có đủ lịch để hiển thị.
  2. **Khoá quyết định (mỗi lịch 1 lần):** `DeclineScheduleCommand` chặn khi booking đã `confirmed`; confirm-sau-decline vốn đã bị chặn bởi guard `Status != "scheduled"`. FE ẩn nút + hiện khoá (`Lock`) khi đã xác nhận. *(Đã CHỦ ĐÍCH bỏ chặn xác nhận trùng khung giờ theo yêu cầu 2026-08-07 — ứng viên được xác nhận nhiều lịch trùng giờ.)*
  3. **Auto-reject quá hạn** = `ScheduleConfirmationHostedService` (Infrastructure, quét 30'/lần, startup delay 1'): booking `scheduled`+`ConfirmationStatus=="pending"` mà `CreatedAt <= now - ConfirmDeadlineHours` → xử lý **y như decline** (Status=`declined`, `DeclineReason="[Hệ thống] Ứng viên không xác nhận lịch trong thời hạn."`, trả 1 chỗ slot, thông báo ứng viên bell `schedule_auto_rejected:{bookingId}` + realtime `InterviewScheduleAutoRejected`, báo nhân sự `ReceiveScheduleResponse {auto:true}`) → vào `awaitingReschedule`, **giữ mô hình reschedule** (Reject = xin đổi lịch, không phải rời quy trình). Cấu hình `SchedulingOptions.ConfirmDeadlineHours` (section `Scheduling`, mặc định 48, `<=0` = tắt), bind ở `ARI.Application/DependencyInjection`.
  4. **FE:** `SchedulePage` — hộp thoại "bạn đã chắc chắn chưa? Sau khi bấm không thể sửa lại" cho cả confirm & decline; **tự mở** hộp thoại đúng lịch theo query `?booking=&action=` (deep-link từ email, dọn query sau khi xử lý); **badge 3 trạng thái** (Đã xác nhận / Chưa xác nhận / Đã từ chối); state "cần đăng nhập" (401) có nút về `/auth/candidate-login?returnUrl=` (path nội bộ). `CandidateLoginPage` hỗ trợ `returnUrl`. `AssignSchedulePanel` (staff) badge tri-state rõ. **Nút email = deep-link cần đăng nhập** (route `/portal/schedule/:applicationId` là public nhưng `GET /candidate/schedule` cần `CandidateOnly`).
- **Ứng viên xoá lịch đã bị huỷ/từ chối khỏi danh sách (bổ sung 2026-08-08).** Soft-dismiss phía ứng viên, **không xoá dữ liệu**: thêm cột `InterviewBooking.CandidateDismissedAt` (nullable, migration `AddBookingCandidateDismissedAt`). `GetCandidateScheduleQuery` lọc thêm `CandidateDismissedAt == null` ở nhánh `declined` → thẻ "Đã từ chối" biến khỏi `awaitingReschedule` sau khi ẩn, **nhân sự vẫn thấy** booking `Status="declined"` để xếp lại (`AssignSlotCommand` đọc `priorDeclined` theo Status, không đụng). CQRS `DismissDeclinedScheduleCommand` (guard sở hữu qua `CandidateBookingSupport.LoadOwnedAsync`; chỉ ẩn khi Status `declined`/`cancelled`; idempotent), endpoint `DELETE /api/candidate/schedule/{bookingId}` (`CandidateOnly`). FE `scheduleService.dismissSchedule()` + nút "Xoá khỏi danh sách" (`Trash2`) trên mỗi thẻ `awaitingReschedule` ở `SchedulePage`.

### ADR-049: Online Test (thi trắc nghiệm) — ngân hàng câu hỏi per-job + tự chấm (Phase 2c)
- **Ngày:** 2026-07-24. **Branch:** `feature/be/online-test`.
- **Bối cảnh:** 2 entity `OnlineTestQuestion` / `OnlineTestSubmission` + 2 bảng đã tồn tại từ `InitialCreate` nhưng **không có tầng Application/API/FE nào** — bảng chờ suông, chưa dòng logic nào tạo đề hay chấm điểm. Phase 2c (CRUD câu hỏi, ứng viên làm bài, auto-scoring, auto-progression) còn `[ ]` toàn bộ.
- **Quyết định:**
  1. **Ngân hàng câu hỏi theo JOB, không theo vòng.** `OnlineTestQuestion` chỉ mang `JobPostingId` (giữ nguyên thiết kế entity) → bank dùng chung cho job; vòng thi (`round_number` của submission) resolve từ `InterviewRoundConfig` có `RoundType == "online_test"` (mặc định 1 nếu không cấu hình).
  2. **Điểm sàn (`OnlineTestPassScore`) đặt trên `JobPosting`** (mặc định 70), không trên round config — khớp mô hình bank job-wide, 1 migration cột đơn, HR chỉnh ngay trong trang ngân hàng câu hỏi. Migration backfill job cũ = 70 (tránh điểm sàn 0 = ai cũng đạt).
  3. **Chấm điểm bình đẳng:** `score = round(correct / total * 100, 2)`, `isPassed = score >= passScore`. Đáp án đúng (`CorrectOption`) **không bao giờ** rời backend — DTO ứng viên (`CandidateTestQuestionDto`) ẩn hẳn.
  4. **1 lượt / vòng:** unique index `(application_id, round_number)` trên `online_test_submissions` + handler chặn nộp trùng (`Conflict`). Đồng nhất triết lý "1 lượt" với practice.
  5. **CQRS thuần theo ADR-045:** feature `ARI.Application/OnlineTest/` — HR (`GetOnlineTestBank/Create/Update/Delete/UpdatePassScore/GetResultForStaff`) qua `OnlineTestController` (`InternalStaff`, quyền chủ tin/admin qua `OnlineTestSupport.CanManageAsync`); ứng viên (`GetCandidateOnlineTest/SubmitOnlineTest`) qua `CandidateOnlineTestController` (`CandidateOnly`, `AuthorizeCandidateAsync` + auto-link email). Không thêm service/DI — handler tự đăng ký.
  6. **Auto-progression = mềm (không đổi status máy móc):** nộp bài chỉ lưu `IsPassed` + đẩy realtime `OnlineTestGraded`; DB Notification sinh **idempotent** qua `SyncNotificationsAsync` (dedupKey `online_test:{submissionId}`), không tạo notification thủ công. Kết quả hiện cho HR (`GET .../result`) để HR quyết định bước tiếp — không tự reject/không tự chuyển vòng (tôn trọng "HR Review & Confirm" Phase 6).
- **Bảng tổng hợp điểm theo job (HR):** `GetOnlineTestResultsByJobQuery` → `GET /online-test/jobs/{jobId}/results` trả summary (số đã thi, đạt/chưa, tỉ lệ, điểm TB/cao/thấp) + danh sách từng ứng viên (tên, email, vòng, số câu đúng, điểm, đạt/chưa, giờ nộp), sort điểm giảm dần. Trang `JobOnlineTestResultsPage` (route `.../online-test/results`), link từ header trang ngân hàng câu hỏi; mỗi dòng link sang trang chi tiết ứng viên.
- **FE:** shared `types/onlineTest` + `fservices/onlineTest`; StaffSite `JobOnlineTestPage` + `JobOnlineTestResultsPage` (route `/{recruiter/my-jobs|hr/jobs}/:id/online-test[/results]`, link từ trang chi tiết job); CandidateSite `OnlineTestPage` (route `/candidate/online-test/:applicationId`) + `OnlineTestEntry` (chỉ hiện khi job có đề) nhúng trong trang chi tiết hồ sơ. **i18n VI/EN** (namespace `modules/recruiter/onlineTest` + `modules/candidate/onlineTest`) theo bộ chuyển ngôn ngữ như các trang khác.
- **Cập nhật 2026-07-24 — Screening Test (bốc đề ngẫu nhiên + multi-choice + hẹn giờ + export Excel):** migration `AddOnlineTestScreening`.
  1. **Bốc N câu ngẫu nhiên/lượt (mặc định 20):** `JobPosting.OnlineTestQuestionsPerTest`. Bốc **deterministic** theo (questionId, applicationId, round) bằng FNV-1a (`OnlineTestSupport.DrawQuestions`) → cùng ứng viên/vòng luôn nhận cùng bộ đề (không đổi khi refresh) và **chấm lại đúng bộ đó lúc nộp**, không cần bảng "attempt".
  2. **Single & multiple choice:** `OnlineTestQuestion.QuestionType` (`single|multiple`) + `CorrectOptions` (jsonb mảng index; `CorrectOption` legacy giữ đồng bộ = phần tử đầu). Chấm **all-or-nothing**: đúng khi tập chọn KHỚP HOÀN TOÀN tập đáp án đúng (áp cả single lẫn multiple). `SelectedAnswers` đổi ngữ nghĩa sang `{questionId:[indices]}`.
  3. **Hẹn giờ ~30 phút:** `JobPosting.OnlineTestDurationMinutes`. FE đếm ngược + **tự nộp khi hết giờ** (kể cả chưa trả lời hết). Enforcement server-side (startedAt) là follow-up.
  4. **Chấm chính xác theo lượt:** thêm `OnlineTestSubmission.CorrectCount` + `TotalQuestions` (số câu đã bốc) → results/export đọc trực tiếp, không suy từ score×bankSize.
  5. **Export Excel (.xlsx):** `GET /online-test/jobs/{id}/results/export` dựng bằng OpenXML SDK (`DocumentFormat.OpenXml` đã có sẵn), FE tải blob. Endpoint pass-score cũ thay bằng `PUT .../settings` (passScore + questionsPerTest + durationMinutes).
- **Cập nhật 2026-07-24 — Import ngân hàng câu hỏi từ Excel (giảm nhập tay):** `OnlineTestImportFeature.cs`.
  1. **Upload .xlsx → thêm hàng loạt:** `POST /online-test/jobs/{id}/questions/import` (multipart `IFormFile`, ≤5MB, chỉ `.xlsx`). Đọc worksheet đầu bằng OpenXML (`SpreadsheetDocument.Open` read-only, xử lý cả SharedString/InlineString, map cell theo cột từ `CellReference`).
  2. **Layout cột:** A=Câu hỏi · B=Loại (single/multiple, để trống → suy từ số đáp án đúng) · C–H=Phương án A–F (tối đa 6) · I=Đáp án đúng (`A` hoặc `A,C`; nhận cả số 1-based). Nén phương án rỗng + **remap chỉ số đáp án đúng** (đáp án trỏ vào ô trống → báo lỗi dòng).
  3. **Dùng lại validation câu hỏi** (`ValidateQuestion`/`NormalizeType`/`NormalizeCorrect`) — mỗi dòng lỗi trả `{row, message}`, dòng hợp lệ mới ghi; kết quả `OnlineTestImportResultDto(imported, failed, errors[])`. Bỏ qua dòng trống + dòng tiêu đề (nhận diện mềm).
  4. **File mẫu:** `GET /online-test/jobs/{id}/questions/template` (OpenXML, header + 2 ví dụ single/multiple). FE `JobOnlineTestPage`: card "Nhập từ file Excel" (upload + tải mẫu + hiển thị imported/failed + danh sách lỗi dòng); i18n `bank.import.*` VI/EN.
- **Cập nhật 2026-07-24 — HrAdmin truy cập ngân hàng/điểm:** thêm nút "Ngân hàng câu hỏi" trên `pages/hr/JobPostingDetailPage` (→ `/hr/jobs/:id/online-test`, i18n `modules/hr/jobPostingDetail.onlineTestBank` VI/EN). BE + route `/hr/...` + `JobOnlineTest(Results)Page` (`isHr`) đã hỗ trợ sẵn HrAdmin (quyền admin qua `CanManageAsync` cho mọi job) — trước đó chỉ thiếu link điều hướng nên phải gõ URL trực tiếp; nay ngang recruiter (xem điểm + export Excel).
- **Cập nhật 2026-07-24 — Gate CV pass trước khi làm bài (ô mờ "Chờ duyệt CV"):** ứng viên chỉ làm/nộp bài thi trắc nghiệm khi hồ sơ **đã qua vòng duyệt CV**. Helper `OnlineTestSupport.IsCvPassed(status)` (allow-list `invited/screening/interview/pass/not_pass`; chặn `cv_submitted`/`cv_rejected`/`withdrawn` + trạng thái lạ). Trước đó 2 handler chỉ kiểm tra quyền sở hữu → ứng viên chưa duyệt/CV bị loại vẫn làm bài được (lỗ hổng).
  - **`SubmitOnlineTestCommand`:** chặn cứng (Result.Failure) khi chưa pass — phòng thủ dù gọi API trực tiếp.
  - **`GetCandidateOnlineTestQuery`:** KHÔNG fail; thêm cờ `CvPassed` vào `CandidateOnlineTestDto`, khi chưa pass vẫn trả metadata (số câu, điểm sàn, thời lượng) nhưng `Questions` **rỗng** (không lộ đề/đáp án). FE `OnlineTestEntry` hiện **ô mờ khoá "Chờ duyệt CV"** (không ẩn) khi `!cvPassed`; trang `OnlineTestPage` hiện khối khoá "Chờ duyệt CV" nếu vào URL trực tiếp. i18n `page.lockedTitle/lockedDetail` + `entry.locked` VI/EN.
- **Cập nhật 2026-07-24 — Realtime cho STAFF khi ứng viên nộp bài:** trước đó chỉ ứng viên có realtime (`OnlineTestGraded` → chuông + refetch hồ sơ); staff không nhận gì. Bổ sung theo pattern "ứng viên mới ứng tuyển": `SubmitOnlineTestCommand` bắn `ReceiveOnlineTestSubmitted` (payload `applicationId/jobPostingId/candidateName/roundNumber/score/isPassed`) tới **recruiter chủ tin** (`PublishUserEventAsync(job.CreatedByUserId)`) + **nhóm `hr_admin`** (`PublishGroupEventAsync`). Chuông staff sinh trong `GetStaffNotificationsQuery.SyncNotificationsAsync` (section 4, idempotent dedup `onlinetest:{submissionId}`, link tới bảng điểm theo route recruiter/hr). FE `useAppNotifications` xử lý `ReceiveOnlineTestSubmitted` → `refreshStaffBell()` + invalidate `applications`/`my-jobs`/`hr-dashboard` + phát window event `STAFF_ONLINE_TEST_REFRESH_EVENT`; `JobOnlineTestResultsPage` nghe event → refetch nền (không nháy spinner) → **bảng điểm tự cập nhật realtime** khi đang mở.
- **Không làm (follow-up):** chưa gắn FK cứng `online_test_submissions.application_id → applications`; chưa thêm `online_test` vào enum `RoundType` (comment); hẹn giờ chưa enforce phía server (client tự nộp); bốc đề có thể đổi nếu ngân hàng bị sửa giữa lúc đang thi.

### ADR-051: Buổi phỏng vấn thử — transcript + nhận xét AI riêng tư cho ứng viên, lưu vĩnh viễn
- **Ngày:** 2026-08-05. **Branch:** `feature/be/practice-transcript-review`.
- **Bối cảnh:** Buổi thử là "hộp đen" hai đầu. (a) Transcript **đã** nằm bền vững trong `questions` + `answers.transcript` nhưng **không endpoint nào đọc ra** — kết thúc buổi thử là màn hình cảm ơn rồi mất sạch; `HrReview.ShareTranscript`/`TranscriptShared` là cờ treo không có API phía sau. (b) AI **đã** chấm buổi thử (`EndSessionAsync` → `GenerateEvaluationReportAsync`, không phân biệt loại phiên) nhưng ứng viên không bao giờ xem được vì `GetMyEvaluationQueryHandler` chặn cứng mọi evaluation chưa có `HrReview{ShareEvaluation=true}` — mà buổi thử không đi qua luồng HR review. (c) Ngược lại, nhân sự nội bộ **đang thấy** buổi thử ở mọi surface (danh sách phiên HR, danh sách + chi tiết Evaluation, trang chi tiết ứng viên) — trái tinh thần ADR-038/050 "practice không ảnh hưởng verdict".
- **Quyết định:**
  1. **Buổi thử là không gian riêng của ứng viên.** Transcript + nhận xét AI **chỉ** ứng viên sở hữu xem được, **không giới hạn số lần**, qua 2 endpoint mới trên Portal: `GET /api/portal/practice/sessions[?applicationId=]` và `GET /api/portal/practice/sessions/{sessionId}` (`ARI.Application/CandidatePortal/PortalPracticeFeature.cs`, policy `CandidateOnly` + IDOR check). Endpoint từ chối (`NotFound`) mọi phiên `session_type != 'practice'` — transcript buổi **thật** vẫn nằm sau cổng `HrReview.ShareTranscript`, không mở ở đây.
  2. **Nhân sự nội bộ không thấy buổi thử.** Lọc `session_type/SessionType != "practice"` tại nguồn: `GetSessionsForHrAsync` (cả phần evaluation join), `GetEvaluationsQuery`, `GetEvaluationsByApplicationQuery`, `GetEvaluationDetailQuery` (cả nhánh fallback theo `SessionId` → trả NotFound). Chỉ giữ cờ `Application.PracticeSessionUsed` ("đã dùng lượt thử") — thông tin vận hành, không lộ nội dung.
  3. **Không hiện verdict Pass/Not Pass cho buổi thử.** DTO xem lại **cố ý bỏ `AiVerdict`** (verdict không rời BE), chỉ trả điểm tổng, điểm theo tiêu chí, phân tích từng câu, đánh giá ngôn ngữ, gợi ý cải thiện — kèm nhãn "chỉ mang tính tham khảo". Tránh ứng viên hiểu nhầm buổi luyện tập là kết quả tuyển dụng.
  4. **Buổi thử tuyệt đối không chạm pipeline thật.** `GenerateEvaluationReportAsync` chỉ đổi `application.Status` và chỉ bắn `hr_admin/AiEvaluationComplete` khi `SessionType == "real"` — trước đó một buổi thử điểm thấp **ghi đè trạng thái hồ sơ thành `not_pass`** (bug thật, sửa trong ADR này). `GetMyApplicationDetailQueryHandler` cũng tách `realSessions` khỏi `practiceSessions`: tiến trình vòng chỉ tính phiên thật (trước đó `FirstOrDefault` theo `RoundNumber` khiến phiên thử che trạng thái phiên thật), phiên thử trả riêng ở `PracticeSessions[]`.
  5. **Transcript lưu vĩnh viễn, không có job xoá.** Giữ nguyên "practice chỉ transcript, không recording" (ADR-038 điểm 6) — điểm đó nói về **recording**, transcript thì giữ mãi. Bổ sung `InterviewSession.ClosingText` (lưu câu chào kết thúc AI đã nói — trước chỉ bắn qua SignalR rồi mất) + index `ix_questions_session_id`, `ix_answers_session_id` cho đường đọc mới (migration `20260805022000_AddPracticeTranscriptReview`).
- **FE:** trang mới `pages/candidate/PracticeReviewPage.tsx` (route `/candidate/practice/:sessionId`, trong `CandidateAppLayout` + `ProtectedRoute[Candidate]`): header + banner "chỉ tham khảo", panel nhận xét AI (kèm trạng thái "AI đang chấm" khi `evaluationPending`), panel transcript dạng hội thoại + nút sao chép. Lối vào: nút **"Xem lại & nhận xét AI"** ở màn kết thúc buổi thử (hook `usePracticeSession` nay expose `sessionId`) + card "Phỏng vấn thử" trong trang chi tiết hồ sơ. Helper hiển thị tách ra `pages/candidate/_reportUi.ts` + `components/CriterionBar.tsx` (dùng chung với `ApplicationDetailPage`). i18n namespace mới `modules/candidate/practiceReview` (VI/EN). StaffSite gỡ nhãn practice/real đã chết ở `InterviewSessionsPage` + `CandidateDetailPage` (HR & Recruiter).
- **Sửa kèm:** `PracticeSessionPage` trước hardcode `roundNumber = 1` dù `ApplicationsPage` truyền `?round=N` → nay đọc query param (buổi thử vòng 2+ ghi đúng vòng).
- **Cập nhật 2026-08-05 — chất lượng báo cáo AI (sau review màn xem lại thực tế):**
  1. **Một ngôn ngữ duy nhất, không trộn Việt–Anh.** Tách "ngôn ngữ phỏng vấn" khỏi "ngôn ngữ viết báo cáo": FE gửi `uiLanguage` (i18n hiện tại) khi `POST /interview/session/start` → lưu `InterviewSession.ReportLanguage` → `SessionContext.ReportLanguage` → prompt bắt buộc *"write EVERY human-readable string in {report language}, never mix languages"*. Fallback: ngôn ngữ phỏng vấn → `vi`. Câu hỏi/câu trả lời vẫn giữ nguyên ngôn ngữ phỏng vấn (đó là nội dung thật của buổi).
  2. **Phân tích từng câu: schema cứng + ghép theo lượt.** Prompt cũ để `"question_analyses": [<optional objects>]` — model tự bịa khoá nên .NET parse ra rỗng → UI hiện 5 ô trống không giải thích. Nay prompt chốt schema `{sequence_number, score, analysis, feedback}` (KHÔNG chép lại câu hỏi/câu trả lời), rag-service `normalize_question_analyses()` quy mọi biến thể khoá về PascalCase .NET đọc được + bỏ mục rỗng, và `GetMyPracticeReviewQuery` **ghép nhận xét vào đúng lượt hỏi–đáp theo `SequenceNumber`** (fallback theo thứ tự) — câu hỏi/câu trả lời luôn lấy từ DB, AI chỉ đóng góp điểm + phân tích + gợi ý. `QuestionAnalysisDto` thêm `SequenceNumber`.
  3. **Đánh giá ngôn ngữ phải chấm được và chấm đúng.** Prompt `assess_language_prompt` nay chỉ đưa **câu trả lời của ứng viên** (bỏ câu hỏi của AI — câu hỏi không phải bằng chứng năng lực), có neo thang 0–10 ↔ CEFR, buộc `overall_score` nhất quán với 4 chỉ số, yêu cầu `cefr_level` + `evidence` (trích nguyên văn) + `language_adherence`; rag-service tự suy lại CEFR từ `overall_score` nếu model bỏ trống (`cefr_from_score`). `InterviewService` **bỏ qua hẳn** bước này khi phiên không có câu trả lời nào (không để AI đoán bừa một bậc năng lực). Trước đây FE tự suy bậc từ `overallScore` (`langLevel`) nên điểm thành phần 7–6 mà bậc lại rơi về A2.
  4. **Bố cục màn xem lại:** 2 cột từ `lg` — trái (sticky) là tổng quan điểm/tiêu chí/ngôn ngữ/gợi ý, phải là hội thoại với **nhận xét AI nằm ngay dưới câu trả lời tương ứng** (`TurnNote`); bỏ mục "Phân tích từng câu hỏi" tách rời (trước đây lặp lại Q&A và bắt cuộn qua lại). Thẻ ngôn ngữ hiện đủ 4 chỉ số + CEFR + dẫn chứng + câu nhận định tuân thủ ngôn ngữ.
  - Dữ liệu cũ (sinh trước bản sửa) vẫn xem lại được: nhận xét rỗng bị ẩn thay vì hiện ô trống, CEFR ẩn khi không có. Muốn có báo cáo theo chuẩn mới thì chạy lại một buổi thử.

### ADR-052: Kiosk phỏng vấn THẬT — token phạm vi phiên, ghi hình + tự xoá theo hạn lưu
- **Ngày:** 2026-08-05. **Branch:** `feature/be/practice-transcript-review`.
- **Bối cảnh:** Kiosk mới chỉ là vỏ: `KioskPage` nhập 6 ký tự rồi `navigate('/interview/room/{code}')`, còn `InterviewRoomPage` là **mock hoàn toàn** (mảng câu hỏi cứng, không gọi API). Backend thì đã có `POST /validate-code` tạo phiên thật, nhưng mọi endpoint trong phòng (`media-config`/`tts`/`answer`/`end`) và `SessionHub` đều đòi role `Candidate` — máy Kiosk đặt ở lễ tân **không có ai đăng nhập** nên không thể dùng. Ngoài ra `InterviewSession.RecordingUrl` chưa bao giờ được ghi.
- **Quyết định:**
  1. **Token phạm vi MỘT phiên thay cho đăng nhập.** `validate-code` (vẫn `AllowAnonymous`) nay trả `KioskSessionResponse` gồm `sessionId` + **JWT role `Kiosk_session`** mang claim `session_id`/`application_id`, hạn `Interview:KioskSessionTokenHours` (mặc định 3h) — ngắn vì máy đặt nơi công cộng. Policy mới **`InterviewParticipant`** (`Candidate` HOẶC `Kiosk_session`) áp cho media-config/tts/answer/end/recording; controller + `SessionHub` kiểm thêm `session_id` claim phải **khớp đúng phiên** đang thao tác, nên một token Kiosk không chạm được phiên khác. FE giữ token trong `sessionStorage` + `setInterviewSessionToken()` (apiClient ưu tiên token phiên hơn token người dùng; SignalR `accessTokenFactory` cũng vậy).
  2. **Mã sai/đã dùng/hết hạn phân biệt rõ** (`reason: not_found|used|expired`) để Kiosk hướng dẫn đúng việc cần làm thay vì một câu chung chung.
  3. **Ghi hình buổi thật + tự xoá.** Kiosk quay cam+mic bằng `MediaRecorder` (webm VP9/VP8, chunk 5s) song song với recorder audio-only đang đẩy Deepgram; kết thúc → `POST /interview/session/{id}/recording` (multipart) → `IFileStorageService.SaveAsync` → lưu `recording_url` + `recording_size_bytes` + **`recording_expires_at = now + Interview:RecordingRetentionDays` (mặc định 7)**. `RecordingRetentionHostedService` quét 12h/lần: quá hạn → xoá file khỏi storage, xoá `recording_url`, ghi `recording_deleted_at` (HR hiểu vì sao mất video thay vì tưởng lỗi). **Chỉ xoá file media — transcript và bản đánh giá giữ nguyên.** Buổi THỬ vẫn không quay (ADR-038 điểm 6) — endpoint từ chối thẳng.
  4. **HR xem được bản ghi.** `GetEvaluationDetailQuery` trả `recordingUrl` (presigned qua `IFileStorageService`) + `recordingExpiresAt`/`recordingDeletedAt`; khối "Bản ghi & Transcript" ở `EvaluationReviewPage` trước là placeholder chết (nút Play không gắn gì) nay phát video thật, kèm dòng nhắc hạn xoá.
  5. **Trần thời lượng buổi thật** `Interview:RealMaxDurationMinutes` dùng chung cơ chế hết giờ của practice (`PracticeTimeoutCloseAsync` nay áp cho cả hai loại phiên, mỗi loại lấy trần riêng) — trước đây real không có trần, phiên treo là chạy vô hạn. **Mặc định hạ 45' → 20' ngày 2026-08-14** cho khớp trần *20 phút/session* của gói LiveAvatar Essential: quá trần thì nhà cung cấp cắt avatar còn buổi phỏng vấn vẫn chạy tiếp không hình không tiếng — hết giờ do ta tự đóng thì AI còn nói được câu kết. Trần này **buộc phải theo gói đang mua**, đổi gói là đổi số.
  6. **Hook phỏng vấn dùng chung.** `usePracticeSession` tách thành `useInterviewSession({ existingSessionId, sessionType, recordVideo })` — Kiosk **tham gia** phiên đã tạo từ mã (không tạo phiên mới), bật avatar (real luôn có avatar) và bật ghi hình; `usePracticeSession(applicationId, round)` giữ nguyên chữ ký cũ cho màn phỏng vấn thử.
- **UX Kiosk (đề xuất kèm theo, đã làm):** màn chờ hiển thị tên ứng viên + vị trí + vòng; kiểm tra thiết bị bắt buộc trước khi vào phòng (ADR-040); chỉ báo **"Đang ghi hình"** + đồng hồ đếm ngược (đổi màu khi còn ≤5'); màn kết thúc hiện tiến trình lưu video rồi **tự quay về màn nhập mã sau 30s** để sẵn sàng cho ứng viên kế tiếp; **khôi phục phiên đang dở** khi lỡ reload/mất điện (token còn hạn trong `sessionStorage` — mã 6 ký tự one-time-use không nhập lại được); xoá sạch token/phiên khỏi máy ngay khi kết thúc; bỏ hẳn khối "demo codes" trong UI cũ.
- **Không làm (follow-up):** chưa nén/chuyển mã video phía server (upload thẳng webm); chưa upload theo chunk khi đang phỏng vấn (mất điện giữa buổi = mất video, transcript vẫn còn); chưa có PIN nhân sự để thoát chế độ Kiosk; chưa chặn phím tắt/menu chuột phải của trình duyệt.

### ADR-053: "Đạt" chỉ khi qua HẾT vòng — AI không tự đổi trạng thái hồ sơ
- **Ngày:** 2026-08-05. **Branch:** `feature/be/practice-transcript-review`.
- **Bối cảnh:** Hồ sơ hiện "Đạt" dù chưa phỏng vấn thật vòng nào. Ba lỗ hổng chồng nhau: (a) `SubmitHrReviewAsync` đặt `Status="pass"` ngay khi HR xác nhận **một vòng bất kỳ**, không biết job có mấy vòng; (b) `TriggerAutoProgressionAsync` mới là chỗ ghi đè về `"interview"`, nhưng **chỉ chạy khi tồn tại `InterviewRoundConfig` cho vòng N+1** — job không khai báo vòng nào (như job seed cũ) thì không có gì ghi đè; (c) `GenerateEvaluationReportAsync` để **AI tự đặt `not_pass`** ngay khi chấm xong, trước cả khi HR nhìn — trái Phase 6 "HR Review & Confirm" và mâu thuẫn với chính UI "chờ HR xác nhận".
- **Quyết định:**
  1. **AI không bao giờ ghi `Application.Status`.** Chấm xong chỉ lưu `Evaluation` + báo nhóm `hr_admin`. Hồ sơ ở `interview` cho tới khi HR xác nhận; FE hiện "chờ HR xác nhận" qua `pendingHrReview` (đã có sẵn).
  2. **HR review một đánh giá của buổi THỬ cũng không đổi trạng thái hồ sơ** (ADR-051) — lỗ hổng lộ ra khi merge `develop`: unit test `Pass_practice_does_not_progress_even_with_config` kỳ vọng hồ sơ thành `pass` sau khi review một buổi thử. Nay chỉ `SessionType == "real"` mới ghi trạng thái; test đã cập nhật theo hành vi đúng.
  3. **Trạng thái đích tính MỘT lần trong `SubmitHrReviewAsync`** theo tổng số vòng (`ResolveTotalRoundsAsync` = `max(InterviewRoundConfig.RoundNumber)`, không có config thì coi là 1 vòng): không đạt → `not_pass`; đạt & còn vòng sau → `interview`; **đạt & đúng vòng cuối → `pass`**. `TriggerAutoProgressionAsync` giữ nhiệm vụ tạo invite vòng kế, không còn sửa trạng thái (hết cảnh ghi rồi ghi đè).
  4. **Hiển thị tiến độ giữa chừng:** portal trả thêm `TotalRounds` + `PassedRounds` (số vòng THẬT có `HrReview.FinalVerdict == "pass"`); thẻ hồ sơ hiện **"Qua vòng N/M"** thay vì "Đang phỏng vấn" chung chung. "Đạt" chỉ xuất hiện khi backend thực sự đặt `pass`.
- **Sửa kèm cùng đợt:**
  - **Điểm từng câu "0/100 · Cần cải thiện" dù nhận xét tích cực:** báo cáo cũ không có khoá `score` → `QuestionAnalysisDto.Score` (non-nullable) về 0 → FE tô đỏ. Nay portal chỉ gán `Score` khi `> 0` (còn lại `null`), FE ẩn chip điểm và dùng khung trung tính; rag-service nhận thêm biến thể khoá điểm (`score|rating|points|overall_score`) và kẹp về 0–100.
  - **Tên tiêu chí lọt tiếng Anh giữa màn tiếng Việt:** prompt (cả rag-service lẫn `OpenAIProvider`) nay **ghim bộ khoá** `technical|communication|problem_solving|culture_fit|experience|language|attitude|teamwork`; `CriterionBar` thêm alias cho biến thể model hay trả (`cultural_fit`, `technical_skills`, `problem_solving_ability`…). Transcript vẫn giữ nguyên ngôn ngữ buổi phỏng vấn; khi `ReportLanguage` lệch ngôn ngữ UI thì hiện dòng ghi chú.
  - **Cỡ chữ:** `LanguageMetric` dùng chung thang với `CriterionBar` (nhãn `text-sm`, thanh `h-2`).
  - **Banner "quá hạn"** không hiện trên hồ sơ đã đóng; bổ sung nhãn `cv_rejected` (trước đây lọt ra chuỗi thô).
- **Công cụ dev:** `POST /api/dev/seed-interview-job` dựng job `[DEV] Kiosk Sandbox` **3 vòng** (screening → online_test → technical) + 10 câu trắc nghiệm mẫu + lịch cho vòng 1/3 + **mã Kiosk vòng 1** + tài khoản HR **đăng nhập được** (seed cũ tạo owner không mật khẩu). `POST /api/dev/regrade-session/{id}?lang=vi` chấm lại phiên cũ bằng prompt hiện tại (chặn nếu HR đã xác nhận). Hướng dẫn thao tác: [docs/kiosk-interview-setup.md](../docs/kiosk-interview-setup.md).
### ADR-054: Khoá màn hình Kiosk + ghi nhận mọi lần rời khỏi buổi phỏng vấn
- **Ngày:** 2026-08-05. **Branch:** `feature/be/practice-transcript-review`.
- **Bối cảnh:** Buổi phỏng vấn thật cần ứng viên tập trung, không mở tab/ứng dụng khác. Hạ tầng chống gian lận đã có **entity `CheatDetectionSignal` + bảng + `Evaluation.CheatScore/CheatSignals` + `CheatSignalDto`** nhưng **chưa nối**: `SessionHub.ReportCheatSignal` chỉ broadcast cảnh báo rồi bỏ, `GenerateEvaluationReportAsync` ghi cứng `CheatSignals = "[]"` và tính điểm bằng `signals.Any() ? 10 : 0` — không có gì xuống DB nên báo cáo luôn trống.
- **Giới hạn phải nói thẳng:** một trang web **không** chặn được Alt+Tab, phím Windows/Command, Ctrl+Alt+Del hay tắt máy. Khoá cứng chỉ đạt được ở tầng OS/trình duyệt (Chrome `--kiosk`, Windows Assigned Access, MDM). Vì vậy thiết kế theo 2 lớp: **ngăn lối thoát dễ** + **luôn để lại dấu vết** khi vẫn lách được.
- **Quyết định:**
  1. **Bật toàn màn hình ngay trong cú click "Bắt đầu phỏng vấn"** ở `KioskPage` — `requestFullscreen()` chỉ được chấp nhận trong thao tác người dùng, gọi sau khi điều hướng sẽ bị từ chối.
  2. **Rời toàn màn hình → lớp phủ chặn toàn bộ giao diện phỏng vấn** (`KioskInterviewPage`), chỉ tiếp tục được khi bấm "Quay lại toàn màn hình" (bắt buộc là nút vì trình duyệt đòi thao tác người dùng). Kèm chặn menu chuột phải, phím tắt bắt được (F11/F5/Ctrl+P/S/U/F/T/N/W/R) và cảnh báo `beforeunload`.
  3. **Ghi nhận mọi lần rời đi** qua hook chung `useKioskLockdown`: `fullscreen_exit`, `tab_hidden` (chuyển tab/thu nhỏ), `window_blur`, `shortcut_blocked`, `page_unload`. Gửi về `POST /api/interview/session/{id}/signals` (policy `InterviewParticipant` + kiểm khớp `session_id`); riêng lúc đóng trang dùng `fetch keepalive` vì request thường bị huỷ (`sendBeacon` không đặt được header Authorization).
  4. **Nối hạ tầng cũ cho chạy thật:** `IInterviewService.RecordCheatSignalAsync` lưu `CheatDetectionSignal` (chặn payload > 2000 ký tự, ép JSON hợp lệ cho cột jsonb); `SessionHub.ReportCheatSignal` gọi nó trước khi broadcast; `GenerateEvaluationReportAsync` tính `CheatScore` theo **trọng số từng loại** (`tab_hidden` 12, `page_unload` 15, `fullscreen_exit` 8, `window_blur` 5, `shortcut_blocked` 3; trần 100) và ghi `CheatSignals` gộp theo loại kèm số lần → HR đọc được trong màn đánh giá.
  5. **Minh bạch với ứng viên:** màn kết thúc hiện "Ghi nhận N lần rời khỏi màn hình phỏng vấn — thông tin này được gửi kèm kết quả".
- **Không làm (follow-up):** chưa chặn được Alt+Tab/phím Windows (cần cấu hình OS — nên chạy Kiosk bằng `chrome --kiosk` + Windows Assigned Access); chưa có ngưỡng tự động huỷ phiên khi vượt quá N lần rời; chưa hiện cảnh báo realtime cho HR đang theo dõi.

- **Ghi chú vòng trắc nghiệm:** slice Online Test (ADR-049) đã có đủ trên nhánh này; điểm yếu còn lại là **form tạo job chỉ chọn được `screening|technical`** nên vòng `online_test` phải tạo qua seed/SQL — follow-up nên bổ sung vào dropdown `CreateJobPostingPage`.
- **Ảnh hưởng ADR khác:** bổ sung ADR-038 (làm rõ điểm 6: transcript giữ vĩnh viễn) và ADR-050 (buổi thử nay có màn xem lại thay vì chỉ "hiển thị trong mục Kết quả"); không đổi ADR-016/048/049.
- **Không làm (follow-up):** `POST /api/interview/session/{id}/end` vẫn chỉ `[Authorize]` (không kiểm tra chủ sở hữu) — chưa siết vì Kiosk/real dùng chung đường này; `GET /api/applications/practice-eligibility/{id}` vẫn `[AllowAnonymous]`.

### ADR-055: Production DB bỏ Supabase — Postgres tự host trên VPS, bind loopback cho SSH tunnel
- **Ngày:** 2026-08-12. **Thay thế:** phần hosting của ADR-002.
- **Bối cảnh:** Production nối tới Supabase từ đầu dự án. Yêu cầu mới: đưa dữ liệu production về hạ tầng tự quản, bỏ phụ thuộc bên thứ ba. DB `arisp_db` đã được dựng sẵn trên VPS bằng EF migrations, dữ liệu để sạch (chủ ý không mang data rác của Supabase sang). Đồng thời phát hiện DB đang nghe ở `161.248.147.38:8443` **mở thẳng ra Internet** bằng tài khoản `postgres` superuser — tái diễn đúng sự cố `5433` mà ADR-047 điểm 6 đã vá.
- **Quyết định:**
  1. **Container, không cài thẳng lên host.** Toàn hệ thống đã chạy bằng `docker compose` + pipeline deploy; cài lên host tạo mô hình vận hành thứ hai (systemd/apt/`pg_hba.conf`) phải học song song. Base compose đã khai báo sẵn `pgvector/pgvector:pg17` + healthcheck `pg_isready` — việc cần làm là cấu hình lại, không phải dựng mới. Phiên bản DB bị ghim trong git, `apt upgrade` trên host không thể vô tình nâng minor version.
  2. **Bind mount `/var/lib/arisp/pgdata` thay named volume.** `docker compose down -v` không xoá được dữ liệu production, và biết chính xác đường dẫn để `pg_dump`/snapshot. Đổi ngay lúc DB còn trống vì sau này có dữ liệu thật thì tốn công hơn nhiều. Biến `PGDATA_PATH` cho phép máy dev Windows/macOS trỏ sang `./pgdata`.
  3. **`ports: !override ["127.0.0.1:5432:5432"]`** — KHÔNG phải `!reset []` cũng không phải `ports:` thường. `ports:` thường **nối thêm** vào `"5433:5432"` (bind `0.0.0.0`) của base → DB lại hở ra Internet, đúng cái bẫy ADR-047 điểm 6. `!reset []` đóng sạch nhưng host không thấy cổng nào nên SSH tunnel cũng không tới được. `!override` thay hẳn list, giữ đúng một binding loopback. **Prefix `127.0.0.1:` là thứ giữ an toàn:** Docker chỉ tạo rule DNAT trên loopback nên cổng không ra Internet, bất kể ufw cấu hình thế nào (ufw không chặn được cổng do Docker publish).
  4. **Truy cập nhóm bằng SSH tunnel, không mở cổng DB.** User `arisp` trên VPS, mỗi thành viên một public key trong `authorized_keys` — thu hồi từng người bằng cách xoá một dòng, không phải đổi mật khẩu DB cho cả nhóm. DBeaver: tab Main `localhost:5432`, tab SSH trỏ `161.248.147.38:22` + private key. Hướng dẫn đầy đủ ở `docs/postgres-production-setup.md`.
  5. **Ngân sách RAM cân lại cho VPS 4GB:** backend 1024M + postgres 1024M + rag 768M (hạ từ 1G) + redis 192M + candidate 128M + staff 128M (hạ từ 256M — chỉ là nginx-alpine phục vụ file tĩnh) + nginx 64M = **3328M**, còn ~500M cho OS. RAM là nút thắt chứ không phải CPU; thêm dịch vụ hoặc gặp OOM thì nâng VPS 8GB chứ không siết tiếp.
  6. **`SSL Mode=Disable` cho chuỗi kết nối nội bộ.** Container không phục vụ certificate; giữ `Require` như chuỗi Supabase cũ làm mọi kết nối chết ngay lúc boot. Traffic không rời bridge network. Phía Python, `DATABASE_SSLMODE=disable` được `_ssl_context()` hiểu đúng là tắt SSL.
  7. **`backend.depends_on` thêm `postgres: service_healthy`.** Backend chạy EF migrations lúc boot (`AriDbContextInitialiser`, retry 3 lần × 3s ≈ 9 giây) — `initdb` trên thư mục rỗng ở lần deploy đầu lâu hơn cửa sổ đó.
  8. **Backup trở thành việc bắt buộc.** Trước đây Supabase lo; nay volume Postgres là thứ **duy nhất** trên VPS không dựng lại được từ git + GHCR. `scripts/backup-db.sh`: `pg_dump -Fc` → `/var/backups/arisp/`, ghi `.tmp` rồi mới đổi tên (không bao giờ có dump dở dang trông như hợp lệ), **tự đọc mục lục bằng `pg_restore --list` để bắt lỗi hỏng ngay hôm nay thay vì lúc cần restore**, giữ 14 bản. Cron 03:00.
- **Hệ quả / lưu ý vận hành:**
  - Mật khẩu DB phải viết ở **3 chỗ** cùng giá trị: `POSTGRES_PASSWORD` (khởi tạo container), `ConnectionStrings__DefaultConnection` (.NET), `DATABASE_PASSWORD` (Python). Lệch một chỗ là service tương ứng không nối được.
  - Named volume `postgres-data` cũ trên VPS **vẫn còn** sau thay đổi này. Phải copy/restore dữ liệu sang bind mount trước khi xoá nó.
  - EF migrations tự tạo extension `vector` + `uuid-ossp` và toàn bộ schema, nên bind mount rỗng tự thành DB hoàn chỉnh khi backend khởi động. Repo có 25 migration, mới nhất `20260808091756_AddBookingCandidateDismissedAt`.
  - Backup nằm cùng máy với DB không cứu được khi mất VPS — vẫn phải copy ra ngoài.
- **Không làm (follow-up):** tách quyền `arisp_app`/`arisp_dev` khỏi `postgres` superuser đã soạn SQL trong `docs/postgres-production-setup.md` nhưng chưa áp; chưa tự động copy backup ra R2. ~~thư mục `supabase/` còn sót lại từ giai đoạn đầu~~ → **đã dọn 2026-08-13**: `config.toml` + file migration đều là scaffold Supabase CLI không ai chạy, và file "migration" có **0 `CREATE TABLE`** (chỉ extension/grant boilerplate lúc schema còn rỗng) nên không mất gì; thư mục còn link nhầm sang project `axmvshinerfsebdcljqe` thay vì project test `mwdfddlmkfdmzdckfpgx` đang dùng.

---

### ADR-056: Khôi phục khoá ngoại + ràng buộc UNIQUE + index vận hành vào migration EF

- **Ngày:** 2026-08-13
- **Trạng thái:** Đã triển khai (chưa áp lên production)
- **Bối cảnh:** Đối chiếu Supabase (môi trường test, sống liên tục từ tháng 5) với DB production dựng lại ở ADR-055 phát hiện production **thiếu nguyên một lớp schema**:

  | | Supabase | Production (dựng từ migration EF) |
  |---|---|---|
  | Khoá ngoại | 29 | **1** |
  | Ràng buộc UNIQUE | 44 | **36** |
  | Index `idx_*` (đặt tay) | 28 | **0** |
  | Index vector ANN | ivfflat | **không có** |

  Nguyên nhân: lớp này được **áp tay bằng SQL thẳng lên Supabase**, không bao giờ nằm trong migration EF — vi phạm chính quy tắc "mọi thay đổi schema qua EF Core Migration". ADR-055 dựng production từ số 0 bằng migration trên bind mount rỗng, nên EF chỉ tạo được những gì nó biết. Lớp thủ công bốc hơi im lặng.

  Bằng chứng khẳng định giả thuyết: 6 bảng không có FK nào trên Supabase (`account_requests`, `cv_jd_analyses`, `interview_invites`, `notifications`, `saved_jobs`, `document_chunks`) — 5 cái đầu **đúng là 5 bảng thiếu trong `docs/database/schema.sql`** (file nay đã xoá, xem cuối ADR này), tức bảng sinh ra *sau* khi lớp thủ công được áp nên không bao giờ được thêm FK.

  Nghiêm trọng nhất không phải FK mà là 8 ràng buộc UNIQUE bị mất: `users(email)`, `candidate_accounts(email)`, `interview_codes(code)`, `system_settings(key)`, `evaluations(session_id)` và 3 `token_hash`. Production hiện **cho phép trùng email tài khoản và trùng mã phỏng vấn 6 ký tự** — mã Kiosk vốn dựa vào tính duy nhất để định danh phiên.

- **Quyết định:**
  1. **Toàn bộ lớp vào `OnModelCreating`** (`ConfigureRelationships` + `ConfigureOperationalIndexes`), không phải SQL rời. Migration `20260813075228_RestoreForeignKeysIndexesAndUniqueConstraints`: 38 khoá ngoại, 43 index (8 unique), **0 `AlterColumn`/`AddColumn`/`DropColumn`** — thuần bổ sung ràng buộc, không chạm dữ liệu; `Down()` đối xứng đủ.
  2. **Quan hệ khai KHÔNG dùng navigation property** — `HasOne<T>().WithMany().HasForeignKey(x => x.XId)`. Entity giữ nguyên `Guid` trần, tầng service join thủ công như cũ: DB được thêm ràng buộc mà không một dòng query nào phải đổi.
  3. **Delete behavior:** `Cascade` (19) cho quan hệ cha–con thật; `NoAction` (19) cho tham chiếu cần giữ. Khớp từng cái một với Supabase. Hệ thống dùng soft delete nên cascade hầu như không kích hoạt trong vận hành — nó là lưới an toàn cho xoá cứng.
  4. **Bổ sung 10 FK mới** cho các bảng chưa từng có (Supabase cũng thiếu). `account_requests` để `NoAction` cả 3 tham chiếu vì đó là hồ sơ kiểm toán ai-xin-ai-duyệt (ADR-041), không được biến mất theo người dùng.
  5. **ivfflat → HNSW** cho `document_chunks.embedding`. ivfflat phải học phân cụm từ dữ liệu lúc tạo; production đang trắng nên tạo bây giờ ra index rác phải `REINDEX` sau. HNSW xây tăng dần theo từng lần chèn, không cần huấn luyện lại. Đây là thời điểm duy nhất đổi được mà không tốn gì.
  6. **Năm cột cố tình KHÔNG đặt FK:** `audit_logs.entity_id`, `playbook_documents.scope_ref_id`, `document_chunks.source_id` (đều đa hình theo cột `*_type`/`scope` đi kèm); `account_requests.batch_id` (id gom nhóm, không có bảng đích); `questions.playbook_chunk_id` — `rag-service` **xoá cứng** chunk mỗi lần nạp lại tài liệu (`DELETE FROM document_chunks WHERE source_type=$1 AND source_id=$2`) nên FK ở đây sẽ chặn đứng việc nạp lại; cột này hiện cũng chưa dùng ở đâu trong code.
- **Kiểm chứng:** dựng container `pgvector/pgvector:pg17` trắng, áp cả 26 migration, rồi so **từng ràng buộc** với Supabase theo chữ ký (bảng.cột → bảng đích [hành vi xoá]) vì tên index/constraint hai bên khác nhau. Kết quả: **0 khoá ngoại thiếu, 0 UNIQUE thiếu**. 12 index báo "thiếu" đã truy từng cái: 6 là **trùng lặp sẵn bên Supabase** (cặp `idx_*` + `ix_*` cùng cột), 6 còn lại là hợp nhất có chủ ý (bỏ index thường khi đã có UNIQUE cùng cột; `online_test_submissions(application_id)` nằm trong composite unique dẫn đầu; ivfflat→HNSW). Test: **680/680 pass**.
- **Chênh lệch CÒN LẠI, cố ý không khôi phục:** Supabase có **51 cột `character varying(n)`**, bản dựng từ EF để `text` hết. Trong Postgres `text` và `varchar(n)` **giống hệt nhau về hiệu năng lẫn lưu trữ**, chỉ khác ở chỗ chặn độ dài — mà độ dài đã được validate ở tầng ứng dụng. Các cột `varchar` đó cũng do lớp SQL tay tạo ra, không phải ý định của model EF (entity không khai `HasMaxLength`). Khôi phục 51 giới hạn độ dài là thay đổi riêng, rủi ro riêng (đặt sai một con số là từ chối dữ liệu hợp lệ lúc chạy), nên tách khỏi đợt này.
- **Trước khi áp lên production:** dữ liệu hiện có (13 tài khoản nhân sự + 6 dòng `system_settings`) được chép từ Supabase vốn đã có sẵn UNIQUE nên về nguyên tắc không thể trùng, nhưng production đã chạy ~1 ngày **không có ràng buộc** — chạy kiểm tra trước cho chắc:
  ```sql
  SELECT 'users' t, email v, count(*) FROM users GROUP BY email HAVING count(*)>1
  UNION ALL SELECT 'settings', key, count(*) FROM system_settings GROUP BY key HAVING count(*)>1;
  ```
  Rỗng thì `ADD CONSTRAINT` chạy sạch; có dòng nào thì phải gộp/xoá bản trùng trước.
- **Dọn kèm — xoá `docs/database/` (`schema.sql` + `schema.md`), 2026-08-13:** hai file này chính là bản ghi chép bằng văn bản của lớp thủ công nói trên, và cũng là thứ đã lệch xa nhất (dừng ở 2026-06-15: 25/30 bảng, thiếu 5 bảng mới + các cột của ADR-051/052). Giữ chúng lúc chưa khôi phục thì còn giá trị tham chiếu, nhưng khi lớp khoá ngoại đã nằm trong migration EF thì giá trị độc nhất đó hết. **Không thay bằng file mô tả khác** — đẻ thêm một tài liệu schema viết tay chính là tái lập đúng cái nguyên nhân gốc. Cần SQL đầy đủ thì sinh bằng `dotnet ef migrations script --idempotent` (đọc thẳng từ code, không cần kết nối DB, luôn khớp 100%); hướng dẫn đặt trong README thay cho dòng trỏ tới file cũ.
- **SỬA SAU KHI DEPLOY (2026-08-13, cùng ngày):** migration bản đầu **chỉ được kiểm chứng trên DB trắng** — thiếu hẳn kịch bản DB **đã mang sẵn lớp thủ công**. Production không có lớp đó nên deploy sạch (đã xác minh `/api/jobs` trả 200 sau khi merge), nhưng máy dev trỏ vào **Supabase** thì `CREATE INDEX idx_webhook_deliveries_next_retry` nổ `42P07 relation already exists` — index đó có sẵn từ lớp tay. EF bọc migration trong transaction nên Supabase **rollback nguyên vẹn** (kiểm lại: vẫn 29 FK, `ix_evaluations_session_id` còn, migration chưa ghi vào `ef_migrations_history`) → sửa thẳng file migration là đúng, không cần migration đền bù.
  **Bản vá:** thêm khối `migrationBuilder.Sql` ở đầu `Up()` gỡ lớp cũ trước khi EF dựng lớp của nó — 28 khoá ngoại `*_fkey`, 8 ràng buộc UNIQUE `*_key`, 28 index `idx_*`, index ivfflat. **Mọi lệnh đều `IF EXISTS` nên là no-op trên production/DB trắng.** Chỉ gỡ thứ migration tự dựng lại ngay sau đó; **cố ý giữ `interview_round_configs_job_posting_id_round_number_key`** vì migration này không tạo lại nó — gỡ đi là Supabase mất ràng buộc 1-vòng/lần-cấu-hình. Thêm `CREATE INDEX IF NOT EXISTS ix_evaluations_session_id` để lệnh `DropIndex` do EF sinh ra luôn có thứ để xoá (Supabase từng thay index này bằng constraint).
  **Kiểm chứng lại bằng 2 đường song song** trên cùng một container: (A) DB trắng → áp đủ 26 migration; (B) áp tới migration trước, **bơm nguyên lớp thủ công lấy từ Supabase** (29 FK + 28 index tay — tái hiện đúng lỗi), rồi áp bản vá. Cả hai đều chạy sạch và **`diff` ba nhóm (khoá ngoại / index / unique constraint) ra rỗng tuyệt đối**: 39 FK, 97 index, giống hệt nhau. Test 680/680. *(Bên lề: lúc bơm lớp cũ, chính Postgres cảnh báo `ivfflat index created with little data — This will cause low recall` — xác nhận lý do đổi sang HNSW ở điểm 5.)*

### ADR-057: Realtime ở TẦNG DATABASE — trigger + LISTEN/NOTIFY, không phải push thủ công

- **Ngày:** 2026-08-15
- **Trạng thái:** Đã triển khai ở local/`develop` (**chưa áp lên production** — chờ kiểm chứng xong mới merge `main`)
- **Bối cảnh:** Hệ thống đã có realtime nhưng nguồn phát nằm **hoàn toàn ở tầng ứng dụng**: mỗi command sau khi ghi DB phải tự nhớ gọi `PublishUserEventAsync(...)` — khoảng 40 điểm gọi rải khắp `ARI.Application`. Mô hình "nhớ thì push" có hai điểm yếu cấu trúc:

  1. **Quên là hỏng im lặng.** `ReassignJobCommand` phát `eventType = "JobReassigned"` nhưng FE không có `case` nào bắt → rơi vào `default: console.warn`, chuông của cả hai recruiter chỉ sáng sau khi F5, dù row `Notification` đã nằm trong DB. Không có test nào bắt được lỗi loại này vì hai đầu nằm ở hai ngôn ngữ khác nhau.
  2. **Đường ghi không đi qua .NET thì không có sự kiện.** `rag-service` (Python) ghi thẳng `document_chunks`; hosted service, migration, sửa SQL tay cũng vậy.

  Ngoài ra đây là **yêu cầu của giảng viên hướng dẫn đồ án**: hệ thống phải dùng "realtime database".

- **Quyết định:** đưa nguồn phát sự kiện xuống chính Postgres, giữ nguyên đường vận chuyển SignalR sẵn có.

  ```
  ai đó ghi DB ──COMMIT──> AFTER trigger ──pg_notify('arisp_changes', {khoá})──>
      DbChangeListenerHostedService ──> DbChangeRouter (gửi cho ai?) ──>
      INotificationService ──> AppNotificationHub ──> FE: case 'ReceiveDbChange'
  ```

  1. **Một hàm trigger cho toàn bộ 30 bảng.** `arisp_notify_change()` đọc cột qua `to_jsonb(rec) ->> 'ten_cot'` nên bảng không có cột đó chỉ trả NULL thay vì lỗi — không phải viết 30 hàm. Gắn trigger bằng `arisp_attach_change_triggers()` quét `pg_class` thay vì liệt kê cứng tên bảng: **migration sau này thêm bảng mới chỉ cần gọi lại hàm đó là có realtime**, không phải sửa code C#.
  2. **Payload chỉ chứa KHOÁ** (`{t, op, id, r:{...id định tuyến, status}}`). Hai lý do: `pg_notify` giới hạn cứng **8000 byte**, và kênh này không được phép trở thành đường rò dữ liệu song song với API.
  3. **`document_chunks` dùng trigger STATEMENT-level.** `rag-service` xoá sạch rồi nạp lại hàng trăm chunk mỗi lần ingest — row-level sẽ bắn hàng trăm NOTIFY cho một thao tác.
  4. **Listener giữ connection RIÊNG, `Pooling=false` + `Multiplexing=false`.** Trạng thái `LISTEN` gắn với đúng một connection và phải mở suốt đời ứng dụng; lấy từ pool EF thì connection bị trả về rồi tái dùng cho query khác → **mất LISTEN im lặng**. Callback `Notification` của Npgsql là đồng bộ nên payload đi qua `Channel` có trần 2000 (đầy thì bỏ bản ghi cũ nhất) rồi mới xử lý tuần tự.
  5. **Định tuyến là chốt chặn bảo mật, mặc định ĐÓNG.** `DbChangeRouter` map bảng → người nhận (`user_{id}` / `role_hr_admin` / `role_super_admin`); bảng **chưa được map thì không gửi cho ai** và chỉ ghi log Debug. Trigger gắn trên toàn bộ bảng nghĩa là `refresh_tokens`, `magic_links`, `audit_logs` cũng bắn NOTIFY — chúng dừng lại ở listener, không bao giờ ra socket. `Clients.All` chỉ dùng đúng một chỗ (`job_postings`, cho Job Board công khai) và với **payload rút gọn chỉ còn `{t, op, id}`**. Router tách khỏi listener, không phụ thuộc Npgsql/SignalR, có 29 unit test.
  6. **Bù sự kiện mất khi listener rớt.** NOTIFY là fire-and-forget: những gì phát ra trong lúc listener chưa nối lại **mất vĩnh viễn**. Nên sau mỗi lần (re)connect, listener phát `op = "resync"` để client nạp lại toàn bộ cache — nếu không, màn hình đang mở sẽ giữ dữ liệu cũ mà không ai biết.
  7. **Giữ song song ~40 lệnh push thủ công cũ**, không gỡ trong đợt này (tránh hồi quy giữa kỳ đồ án). Hai nhánh chồng nhau vô hại: react-query gộp các lần refetch trùng khoá. Gỡ dần là việc về sau.

- **Vì sao KHÔNG dùng Firebase RTDB / Supabase Realtime:**
  - **Firebase** tạo **nguồn sự thật thứ hai** cạnh Postgres → phát sinh bài toán đồng bộ, trái quy tắc stack.
  - **Supabase Realtime** thì hoặc phải quay lại Supabase hosted (đảo ngược ADR-055 vừa làm), hoặc tự dựng container Elixir + `wal_level=logical`. Ngân sách RAM production đã chốt **3328M/3.8GB**, thêm dịch vụ là phải nâng VPS lên 8GB. Nặng hơn cả RAM là **mô hình bảo mật**: Supabase Realtime lọc quyền bằng **RLS trong DB**, còn toàn bộ phân quyền ARISP nằm ở .NET (JWT + role + kiểm chủ tin + chống IDOR trong từng feature) — cho client subscribe thẳng DB thì phải viết lại lớp đó thành policy SQL, rủi ro lộ dữ liệu ứng viên rất cao.
  - `LISTEN/NOTIFY` cho đúng thứ cần (**DB tự đẩy, không polling, transactional**) mà **không thêm một byte RAM nào**, không đổi DB, giữ phân quyền ở nơi đã được kiểm thử.

- **Tính chất đáng lưu ý:** NOTIFY **transactional** — chỉ gửi khi transaction COMMIT, rollback thì không sinh sự kiện giả. Đây là thứ mà cơ chế push thủ công ở tầng ứng dụng không bảo đảm được (code có thể push xong rồi transaction mới rollback).
- **Sửa kèm:** thêm `case 'JobReassigned'` ở FE (lỗ hổng nêu trong Bối cảnh). Thực ra chỉ riêng trigger trên bảng `notifications` đã làm chuông sáng đúng, vì `ReassignJobCommand` có ghi row `Notification` — minh hoạ trực tiếp cho việc lớp lỗi này biến mất chứ không phải được vá từng cái.
- **Kiểm chứng:** 29 unit test mới cho `DbChangeRouter` → **721/721 pass**; typecheck cả staff lẫn candidate site pass. Chạy thật trên container `pgvector/pgvector:pg17` **trắng**: áp đủ 27 migration → **30 trigger** (`document_chunks` đúng mức STATEMENT, 29 bảng còn lại ROW); `INSERT`/`UPDATE`/`DELETE` đều phát NOTIFY kèm `op` = `I`/`U`/`D` và khoá định tuyến (`recipient_user_id` xuất hiện đúng trong `r`); **`ROLLBACK` không phát gì** — xác nhận tính transactional; chèn **200 dòng** `document_chunks` chỉ sinh **1** event `op="S"` — xác nhận trigger statement-level chặn được bão sự kiện lúc rag-service nạp lại tài liệu; `Down()` gỡ sạch về **0 trigger**, áp lại rồi gọi lại `arisp_attach_change_triggers()` vẫn đúng **30** (không nhân đôi).
- **Cấu hình:** `Realtime:DbListener:Enabled` (mặc định `true`) + `Realtime:DbListener:Channel` (`arisp_changes`). Tên kênh đi thẳng vào câu lệnh `LISTEN` (không tham số hoá được) nên bị chặn bằng regex `^[a-z_][a-z0-9_]*$`, sai định dạng thì rơi về mặc định.
- **Đồng bộ môi trường kèm theo:** `docker/.env` máy dev vốn còn trỏ **Supabase** bằng tên biến cũ `DB_CONNECTION_STRING` (chạy được nhờ nhánh fallback ở `Infrastructure/DependencyInjection.cs`), nay chuyển sang container `pgvector:pg17` chạy local với `PGDATA_PATH=./pgdata` — local và production dùng chung một cấu hình, dữ liệu vẫn tách rời hoàn toàn (`docker/pgdata` trên máy dev vs `/var/lib/arisp/pgdata` trên VPS). Thêm `docker/pgdata/` vào `.gitignore`. **Quy tắc bất di bất dịch:** không bao giờ đặt chuỗi kết nối VPS vào `docker/.env` — backend chạy EF migration lúc boot nên cắm nhầm là migration áp thẳng lên dữ liệu thật.
