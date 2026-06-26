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
- HR tạo Job Posting → duyệt CV → gửi magic link cho ứng viên làm quen phỏng vấn thử (Practice Remote).
- Ứng viên đến văn phòng theo lịch hẹn → HR cấp Interview Code (On-site) cho phỏng vấn thật.

### ADR-014: AI Evaluation & HR Confirm Flow
- AI generate Evaluation Report sau mỗi Round.
- HR Leader phê duyệt kết quả hoặc ghi đè verdict (Override bắt buộc nhập `override_reason` cho audit trail).
- Notification: email + in-app (SignalR) khi Evaluation hoàn thành.

### ADR-015: Interview Mode – Practice (Remote) vs Real (On-site)
- **Practice Session (Remote):** Candidate phỏng vấn thử từ browser tại nhà để làm quen hệ thống. **[Cập nhật ADR-038]** Chỉ mở cho ứng viên **đã pass vòng CV** và được HR cấp **Interview Code 6 ký tự (type=`practice`)** — không còn dùng magic link cho practice.
- **Real Interview (On-site):** BẮT BUỘC TẠI CÔNG TY. Candidate đến văn phòng, nhập **Interview Code** tại thiết bị Kiosk.
- **On-site Kiosk:** Frontend app chạy ở chế độ kiosk (full-screen, không expose các route khác) trên thiết bị công ty.
- **Connection Recovery:** Nếu ứng viên mất kết nối, session duy trì trạng thái active. Khi nhập lại code, hệ thống tự resume (dựa vào `must_ask_tracking`).

### ADR-016: Interview Code (Access Control — Practice & Real)
- **Format:** 6 ký tự alphanumeric, case-insensitive (ví dụ: `ARX7K2`).
- **One-time-use:** Vô hiệu hóa ngay sau khi dùng thành công.
- **TTL:** Mặc định 2 giờ, cấu hình được per Job Posting.
- **Binding:** Mỗi code bind với một `application_id` cụ thể.
- **[Cập nhật ADR-038] Phân loại:** code có `code_type` (`practice` | `real`).
  - `practice` — mở từ **browser (remote)**, dùng cho phỏng vấn thử; chỉ cấp cho ứng viên đã pass CV.
  - `real` — chỉ nhập tại **Kiosk on-site** cho phỏng vấn thật.
- **Generation:** HR Admin/Recruiter tạo thủ công hoặc sinh hàng loạt.
- **Audit:** Ghi lại thời điểm code được tạo, dùng, bởi `application_id` nào, `code_type` gì.

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

### ADR-020: Scheduling Service (Practice Session Only)
- HR cấu hình `AvailabilitySlots` per Job Posting: danh sách khung giờ trống để ứng viên làm Phỏng vấn thử (Remote).
- Candidate chọn slot trên Portal → slot bị giảm capacity → khi hết slot không cho chọn nữa.
- Reminder email 24h và 1h trước giờ phỏng vấn thử.
- Đối với Phỏng vấn thật (On-site), tính năng này KHÔNG áp dụng. HR tự điều phối lịch trực tiếp với ứng viên.

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
- **Quyết định:** Thêm `session_type` enum (`practice` | `real`) vào `InterviewSession` entity.
- **Truy cập:** **[Cập nhật ADR-038]** Chỉ ứng viên **đã pass vòng CV** + HR cấp **Interview Code type=`practice`** (remote, mở từ browser). Không xuất hiện công khai trên Job Board / Portal.
- **Lượt dùng:** 1 lần per `application_id`. `ApplicationService` check và disable nếu đã dùng; code one-time vô hiệu sau lần dùng.
- **RAG nguồn:** `practice` – chỉ retrieve JD + CV chunks, không load Playbook. `real` – full RAG (JD + CV + Playbook).
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
| `ApplicationService` | Candidate application (CV + info), invite flow, practice session eligibility check (1 lần per application), **đính kèm CV-JD Analysis vào Application** |
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
- **Đã refactor:** `ApplicationsController` (CV ứng tuyển), `CandidatePortalController` (CV hồ sơ + resolve URL ở GET applications/detail/profile). Xoá CV cũ khi upload CV mới (tránh tích rác).
- **Follow-up (chưa làm):** Phía HR/staff (`ApplicationService` trả `CvFileUrl`) hiện trả `storageKey` thô — cần resolve presigned URL khi bật S3 cho prod. Dev (`Local`) không ảnh hưởng.

### ADR-037: Địa giới hành chính VN — Provinces Open API v2 (sau sáp nhập 07/2025)
- **Quyết định:** Trường "Địa điểm" của Candidate dùng **[Provinces Open API v2](https://provinces.open-api.vn/)** — dữ liệu **sau sáp nhập tỉnh 07/2025**: cấu trúc **2 cấp Tỉnh/Thành → Phường/Xã** (bỏ cấp Quận/Huyện), **34 tỉnh/thành**.
- **Endpoint dùng:** `GET /api/v2/p/` (danh sách tỉnh), `GET /api/v2/p/{code}?depth=2` (tỉnh kèm phường). Field chính: `code` (int), `name`, `province_code`.
- **Frontend:** service `provinceService` (gọi thẳng open-api, có cache trong phiên — **không** qua `apiClient` của backend). Trường địa điểm là **2 dropdown phụ thuộc** (chọn tỉnh → load phường; đổi tỉnh → reset phường).
- **Database (`candidate_accounts`):** thêm `province_code` (int?), `province_name`, `ward_code` (int?), `ward_name`. Giữ cột cũ **`location`** nhưng chuyển thành **chuỗi hiển thị suy ra tự động** `"Phường X, Tỉnh Y"` (denormalized, tương thích ngược) — không còn nhập tay. Migration `AddCandidateAdminDivision`.
- **Dữ liệu cũ:** rows có `location` text tự do trước đây vẫn còn nhưng không map sang code → ứng viên chọn lại tỉnh/phường 1 lần là có dữ liệu chuẩn.

### ADR-038: Tối ưu chi phí Phỏng vấn thử — gating theo phễu, không cắt công nghệ
- **Bối cảnh:** Mỗi buổi phỏng vấn (thử & thật) ngốn chi phí streaming đáng kể (HeyGen ~$3/buổi, ElevenLabs TTS, Deepgram STT, GPT-4o). Doanh nghiệp trả tiền cho **cả practice lẫn real** → 1 ứng viên = 2 lượt tính phí phỏng vấn.
- **Nguyên tắc:** Practice **không ảnh hưởng verdict** nhưng vẫn cần **đầy đủ công nghệ** để ứng viên làm quen đúng trải nghiệm thật → **không tối ưu bằng cách cắt tech**, mà tối ưu bằng cách **giảm số lượng buổi (phễu)**.
- **Quyết định:**
  1. **Gating:** Practice chỉ mở cho ứng viên **đã pass vòng CV** (HR review matchScore + CV → chọn) và được HR **cấp Interview Code 6 ký tự type=`practice`** (remote). Không mở đại trà cho mọi ứng viên job board → chỉ trả tiền thử cho hồ sơ đáng phỏng vấn.
  2. **1 lần / application**, code one-time, vô hiệu ngay sau dùng.
  3. **Đầy đủ pipeline** cả practice & real (xem ADR-027). Practice RAG = JD + CV; Real RAG = JD + CV + Playbook.
  4. **Hybrid Idle (ADR-011)** áp dụng cho cả 2 mode — tiết kiệm ~90% HeyGen mà UX giữ nguyên (không phải "cắt tech").
  5. **Trần cứng** cho practice: giới hạn số câu hỏi + thời lượng để tránh đốt token/STT-phút.
  6. **Recording practice: không quay video, chỉ transcript** + Evaluation Report.
  7. Tái dùng embeddings JD+CV đã sinh ở bước CV-JD Analysis (không embed lại).
- **Thay đổi liên quan:** Cập nhật ADR-015 (practice qua code thay vì magic link), ADR-016 (`code_type` practice|real), ADR-027 (truy cập + recording).

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
