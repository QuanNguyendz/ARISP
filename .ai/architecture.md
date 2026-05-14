# Architecture – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

## Tổng quan kiến trúc

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Client Layer                                │
│            React + TypeScript + TailwindCSS / MUI                   │
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
│  └──────────┘  └──────────┘  └──────────────┘  └───────┬───────┘  │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐          │          │
│  │Interview │  │Evaluation│  │  Enterprise  │  ┌───────▼───────┐  │
│  │Code Svc  │  │& HR Rvw  │  │  Admin       │  │  RAG Pipeline │  │
│  └──────────┘  └──────────┘  └──────────────┘  └───────────────┘  │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐                      │
│  │  Cheat   │  │Integrat. │  │  Scheduling  │                      │
│  │Detection │  │(ATS/SSO) │  │  Service     │                      │
│  └──────────┘  └──────────┘  └──────────────┘                      │
└──────┬──────────────────┬──────────────────────────────────────────┘
       │ EF Core          │ Redis / External APIs
┌──────▼──────┐  ┌────────▼──────┐  ┌──────────────────────────────┐
│ PostgreSQL  │  │  Redis Cache  │  │ OpenAI (GPT-4o + Embeddings) │
│ (Supabase)  │  └───────────────┘  │ Google STT / ElevenLabs TTS  │
│ + pgvector  │                     │ HeyGen Avatar / SendGrid     │
└─────────────┘                     │ ATS Webhooks / SSO / Slack   │
                                    └──────────────────────────────┘
```

---

## Architecture Decision Records (ADR)

### ADR-001: Backend Framework
- **Quyết định:** ASP.NET Core .NET 8
- **Lý do:** Type safety, performance, enterprise ecosystem, team quen thuộc.
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

### ADR-005: TTS & Avatar
- **Quyết định:** ElevenLabs Flash v2.5 + HeyGen Streaming Avatar với Hybrid Idle Strategy.
- **Thay thế đã loại:** Azure TTS, D-ID, HeyGen Batch API, ElevenLabs Multilingual v2.

### ADR-006: Streaming-First Latency Strategy
- **Mục tiêu:** Ứng viên dừng nói → avatar bắt đầu nói trong **~1–1.8 giây**.

  | Bước | Công nghệ | Target latency |
  |------|-----------|---------------|
  | STT | Google Speech-to-Text streaming | ~300ms sau dừng nói |
  | RAG | pgvector (parallel với STT) | ~0ms additional |
  | LLM | GPT-4o streaming | TTFT ~400–800ms |
  | TTS | ElevenLabs Flash v2.5 streaming | ~150–300ms |
  | Avatar | HeyGen Streaming (WebRTC) | ~100–200ms |

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
    Task<IEnumerable<DocumentChunk>> RetrieveAsync(float[] queryVector, int topK, CancellationToken ct);
}
```

### ADR-010: Observability & Logging
- **Quyết định:** Serilog + OpenTelemetry. Track latency từng bước pipeline.

### ADR-011: HeyGen Hybrid Idle Strategy
- Chỉ kết nối HeyGen Streaming khi AI nói (~3 phút/session). Khi im: client phát idle video loop.
- Tiết kiệm ~$2.78/session (~90% HeyGen cost).

### ADR-012: Multi-tenant Data Isolation
- Mọi entity thuộc Enterprise có `organization_id`. Repository layer enforce filter.
- Application layer không tự quản lý isolation.

### ADR-013: Candidate Invite Flow
- HR tạo Job Posting → hệ thống sinh invite link (signed JWT, 24–72h) → gửi email.
- Candidate nhấn link → submit CV → chọn slot (Remote) hoặc nhận Interview Code (On-site).

### ADR-014: AI Evaluation & HR Confirm Flow
- AI generate Evaluation Report sau mỗi Round.
- HR Confirm/Override (override bắt buộc có `override_reason` cho audit trail).
- Notification: email + in-app (SignalR) khi Evaluation hoàn thành.

### ADR-015: Interview Mode – Remote vs On-site
- **Remote:** Candidate phỏng vấn từ browser tại nhà. Xác thực qua invite link JWT.
- **On-site:** Candidate đến văn phòng, nhập **Interview Code** tại thiết bị công ty.
- Cùng một Job Posting có thể bật cả hai mode.
- **On-site Kiosk:** Frontend app chạy ở chế độ kiosk (full-screen, không expose các route khác) trên thiết bị công ty.

### ADR-016: Interview Code (On-site Access Control)
- **Format:** 6–8 ký tự alphanumeric, case-insensitive (ví dụ: `ARX-7K2P`).
- **One-time-use:** Vô hiệu hóa ngay sau khi dùng thành công.
- **TTL:** Mặc định 2 giờ, cấu hình được per Job Posting.
- **Binding:** Mỗi code bind với một `application_id` cụ thể – không dùng code người khác.
- **Generation:** HR Admin tạo thủ công hoặc batch generate cho nhiều ứng viên.
- **Audit:** Ghi lại thời điểm code được tạo, dùng, bởi `application_id` nào.

### ADR-017: Multi-round Interview
- HR cấu hình số vòng và loại vòng per Job Posting: `[{round: 1, type: "Screening"}, {round: 2, type: "Technical"}]`.
- Mỗi vòng là một `InterviewSession` độc lập với `session_config` riêng.
- **Auto-progression:** Sau khi HR confirm Pass ở Round N → hệ thống tạo invite Round N+1 tự động.
- **Scheduling:** Candidate chọn slot mới cho Round N+1 (Remote) hoặc nhận Interview Code mới (On-site).

### ADR-018: Language-aware AI Interviewer
- **Detection:** Khi Job Posting được tạo, `IAIProvider.DetectLanguageRequirementAsync(jdText)` phân tích JD.
  - Output: `{ language: "en", requirement: "TOEIC > 700 hoặc IELTS > 6.5", confidence: 0.95 }` hoặc `null`.
  - HR xem kết quả detect và confirm/chỉnh trước khi Job Posting publish.
- **Interview language:** Nếu language requirement được confirm → Round 1 phỏng vấn bằng ngôn ngữ đó.
- **AI System Prompt:** Tự động điều chỉnh system prompt sang ngôn ngữ tương ứng.
- **TTS Language:** ElevenLabs hỗ trợ multilingual – chọn voice phù hợp ngôn ngữ.
- **STT Language:** Google Speech-to-Text config `languageCode` tương ứng.
- **Language Assessment:** `IAIProvider.AssessLanguageProficiencyAsync()` đánh giá riêng:
  - Fluency, Grammar accuracy, Vocabulary range, Comprehension score.
  - Được đưa vào Evaluation Report như một criterion độc lập.

### ADR-019: Cheat Detection
- **Signal collection:** Frontend thu thập signals trong session:
  - **Eye tracking:** webcam-based gaze estimation (thư viện JS như `GazeCloudAPI` hoặc `WebGazer.js`).
  - **Response timing:** thời gian giữa khi câu hỏi được đặt và ứng viên bắt đầu trả lời.
  - **Speech pattern:** STT partial transcript analysis – phát hiện reading cadence (đọc văn bản thay vì nói tự nhiên).
  - **Tab switching / focus loss:** browser visibility API.
- **Analysis:** Backend `CheatDetectionService` tổng hợp signals, chạy heuristic + AI analysis.
- **Output:** `CheatScore` (0–100) + `CheatSignals[]` (danh sách signals phát hiện được).
- **Integration:** CheatScore và CheatSignals xuất hiện trong Evaluation Report (section riêng) cho HR xem xét.
- **Policy:** ARISP không tự động fail ứng viên chỉ dựa trên CheatScore – HR quyết định cuối.

### ADR-020: Scheduling Service (Availability Slots)
- HR cấu hình `AvailabilitySlots` per Job Posting: danh sách khung giờ (start, end, timezone, capacity).
- Candidate chọn slot → slot bị giảm capacity → khi hết slot không cho chọn nữa.
- Reminder email 24h và 1h trước giờ phỏng vấn.
- Hỗ trợ reschedule (trong thời hạn cho phép, cấu hình được per Job Posting).

### ADR-021: Candidate Portal
- **Auth:** Magic link qua email (không cần password). Magic link có TTL 15 phút, one-time-use.
- **Access control:** Candidate chỉ xem data của chính mình (`application_id`-scoped).
- **Content:** Recording (nếu HR bật), transcript, Evaluation Report (phần HR cho phép share), feedback.

### ADR-022: ATS Integration (Webhook/API)
- ARISP push events sang ATS qua Webhook: `application.submitted`, `interview.completed`, `evaluation.confirmed`.
- Payload chuẩn hóa (JSON). HR Admin cấu hình webhook URL + secret per Organization.
- Retry logic với exponential backoff nếu ATS endpoint lỗi.
- Supported ATS: Workday, SAP SuccessFactors, Greenhouse (và bất kỳ ATS nào hỗ trợ webhook).

### ADR-023: SSO Integration
- Hỗ trợ SAML 2.0, OpenID Connect (Google Workspace, Microsoft Entra).
- SSO chỉ áp dụng cho HR Admin/Recruiter – Candidate dùng magic link.
- Per-Organization SSO config (IdP metadata, client ID/secret lưu encrypted).

### ADR-024: Bias Detection & Fairness
- **Data collected:** Evaluation scores theo demographic groups (nếu Candidate cung cấp và đồng ý).
- **Analysis:** Statistical analysis tìm disparate impact – nếu pass rate của một nhóm thấp bất thường.
- **Report:** Fairness Report per Job Posting (cho SuperAdmin và HR Admin).
- **Privacy:** Demographic data phải được Candidate đồng ý cung cấp (opt-in) và được mã hóa.

### ADR-025: Interview Playbook – Org Knowledge Base
- **Quyết định:** HR Admin upload tài liệu phỏng vấn nội bộ theo 3 cấp scope: Organization / Job Posting / Round. Tài liệu được chunk, embed vào pgvector và retrieve trong RAG pipeline để AI phỏng vấn đúng phong cách + nội dung mong muốn của doanh nghiệp.
- **Document types hỗ trợ:**

  | Type key | Mô tả | Scope |
  |---|---|---|
  | `interview_style_guide` | Phong cách, tone, approach phỏng vấn | Org |
  | `competency_framework` | Ma trận kỹ năng theo level | Org |
  | `culture_values` | Văn hóa, giá trị cốt lõi, culture fit indicators | Org |
  | `compliance_guide` | Câu hỏi không được hỏi (pháp lý) | Org |
  | `red_flag_guide` | Dấu hiệu cần probe sâu hoặc loại bỏ | Org |
  | `question_bank` | Ngân hàng câu hỏi gợi ý per vị trí | Job Posting |
  | `technical_scenarios` | Bài toán / case study cụ thể | Job Posting |
  | `expected_answers` | Hướng dẫn câu trả lời tốt cần đề cập | Job Posting |
  | `must_ask` | Câu hỏi bắt buộc phải hỏi trước khi kết thúc | Job Posting |
  | `round_playbook` | Playbook cụ thể per Round | Round |
  | `past_transcripts` | Transcript phỏng vấn ẩn danh (AI học từ mẫu thành công) | Org / Job Posting |

- **Format upload:** PDF, DOCX, TXT, Markdown, JSON (question bank format).
- **RAG weighting khi retrieve:**
  - JD + CV: weight cao (candidate-specific)
  - Org Playbook (style, compliance, values): weight trung bình (brand consistency)
  - Job Posting Playbook (question_bank, scenarios, must_ask): weight cao (content accuracy)
  - Round Playbook: weight cao (phù hợp vòng hiện tại)
- **Must-ask enforcement:** `PlaybookService` track danh sách `must_ask` questions đã hỏi. `InterviewService` nhận signal "còn câu bắt buộc chưa hỏi" trước khi trigger điều kiện dừng.
- **Ràng buộc:** Tài liệu scope theo `organization_id` (Org level) hoặc `job_posting_id` (Job/Round level). Không lẫy lẫn giữa Organizations.

---

## AI Media Pipeline – Full Streaming Interview Loop

```
[Ứng viên ĐANG NÓI]
      │ audio chunks (WebSocket stream)
      ▼
[Google Speech Streaming STT (language-configured)]
      │ partial transcripts
      │ (VAD near-end) → [RAG: retrieve từ JD + CV + Org Playbook + Job Playbook + Round Playbook]
      │ (final transcript ~300ms sau dừng)
      ▼
[GPT-4o Streaming] ◄── context + retrieved chunks (JD/CV/Playbook) + system prompt
      │ token stream
      ▼
[ElevenLabs Flash Streaming TTS (language voice)]
      │ audio stream
      ▼
[HeyGen Streaming Avatar via WebRTC]
      ▼
[Ứng viên nghe + thấy avatar] ← ~1–1.8 giây sau khi dừng nói
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
[HR Review Round 1]
  ├── Not Pass → email từ chối → DONE
  └── Pass → Auto-invite Round 2
             │
         [Round 2 Session] ← technical deep-dive
             │ session end
         [Round 2 Evaluation (AI)]
             │
         [HR Review Round 2]
           ├── Not Pass → email từ chối
           └── Pass → email mời vòng tiếp / offer
```

---

## Service Boundaries (dự kiến)

| Service / Interface | Trách nhiệm |
|---|---|
| `AuthService` | JWT, role management, magic link (Candidate Portal), SSO integration |
| `OrganizationService` | Enterprise accounts, team HR management, subscription, billing |
| `JobPostingService` | CRUD Job Posting, round config, interview mode, availability slots, persona |
| `ApplicationService` | Candidate application (CV + info), invite flow |
| `InterviewCodeService` | Generate, validate, expire Interview Code (on-site flow) |
| `SchedulingService` | Availability slots, booking, reminder emails, reschedule |
| `InterviewService` | Session lifecycle, multi-round flow, adaptive difficulty, auto-progression, must-ask enforcement |
| `PlaybookService` | Upload, parse, chunk, embed tài liệu Playbook per scope (Org/Job Posting/Round); track must-ask questions đã hỏi |
| `IAIProvider` | Stream question, analyze answer, generate evaluation, detect language, assess language |
| `IEmbeddingProvider` | Embed + retrieve từ pgvector (JD/CV/Playbook chunks) |
| `ISTTProvider` | Google Speech streaming (primary), Whisper (fallback) |
| `RagService` | Chunk JD/CV/Playbook, embed, store, retrieve context theo weighted scope |
| `LanguageDetectionService` | Gọi AI detect ngôn ngữ từ JD, lưu kết quả vào Job Posting |
| `TTSService` | ElevenLabs Flash streaming, hỗ trợ multilingual voice |
| `AvatarService` | HeyGen Streaming Avatar API, WebRTC signaling |
| `EvaluationService` | Generate Evaluation Report sau mỗi Round (Verdict + Score + Reasoning + Language Assessment) |
| `CheatDetectionService` | Tổng hợp signals từ frontend, generate CheatScore + CheatSignals |
| `HRReviewService` | Confirm/Override, audit trail, auto-progression trigger |
| `NotificationService` | Email (SendGrid/SES) + in-app SignalR: invite, reminder, evaluation ready, result |
| `IntegrationService` | ATS webhook push, SSO config per Organization |
| `BiasDetectionService` | Fairness analysis per Job Posting (post-MVP) |
| `AuditLogService` | Ghi lại mọi hành động quan trọng (confirm/override/login/config change) |
| `WebRTCSignalingHub` | SignalR Hub: ICE candidates, SDP offer/answer |
| `SessionHub` | SignalR Hub: session lifecycle events |
