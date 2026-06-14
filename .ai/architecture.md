# Architecture – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

## Tổng quan kiến trúc (Strictly Single-tenant)

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
│ (Supabase)  │  └───────────────┘  │ Google STT / ElevenLabs TTS  │
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
- **Practice Session (Remote):** Candidate phỏng vấn thử từ browser tại nhà để làm quen hệ thống. Xác thực qua magic link.
- **Real Interview (On-site):** BẮT BUỘC TẠI CÔNG TY. Candidate đến văn phòng, nhập **Interview Code** tại thiết bị Kiosk.
- **On-site Kiosk:** Frontend app chạy ở chế độ kiosk (full-screen, không expose các route khác) trên thiết bị công ty.
- **Connection Recovery:** Nếu ứng viên mất kết nối, session duy trì trạng thái active. Khi nhập lại code, hệ thống tự resume (dựa vào `must_ask_tracking`).

### ADR-016: Interview Code (On-site Access Control)
- **Format:** 6–8 ký tự alphanumeric, case-insensitive (ví dụ: `ARX-7K2P`).
- **One-time-use:** Vô hiệu hóa ngay sau khi dùng thành công.
- **TTL:** Mặc định 2 giờ, cấu hình được per Job Posting.
- **Binding:** Mỗi code bind với một `application_id` cụ thể.
- **Generation:** HR Admin/Recruiter tạo thủ công hoặc sinh hàng loạt.
- **Audit:** Ghi lại thời điểm code được tạo, dùng, bởi `application_id` nào.

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
- **STT Language:** Google Speech-to-Text config `languageCode` tương ứng.
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
- **Xác thực nội bộ:** Hỗ trợ đăng nhập cho nhóm người dùng công ty (`super_admin`, `hr_admin`, `recruiter`) bằng **OAuth2 / OIDC** (Google Workspace hoặc Microsoft Entra ID - Google Sign-In). SAML 2.0 hoàn toàn bị loại bỏ.
- **Yêu cầu Pre-provisioning (Cấp trước tài khoản)**: Chỉ những email đã được Super Admin hoặc Admin tạo sẵn trong database mới được phép đăng nhập. Nếu đăng nhập bằng Google Sign-In mà email chưa tồn tại trong database (chưa được cấp tài khoản trước đó), hệ thống **chặn đăng nhập và tuyệt đối không tự động đăng ký/tạo tài khoản nháp**.
- **Domain Validation:** Khi đăng nhập qua OAuth2, hệ thống bắt buộc phân tách và kiểm tra phần domain của địa chỉ email (ví dụ: `hr@fsoft.vn` -> lấy ra `fsoft.vn`). Email này phải thuộc danh sách tên miền được phép truy cập (`allowed_email_domains` được quy định trong cấu hình toàn cục `system_settings`). Mọi email domain công cộng hoặc không khớp sẽ bị chặn truy cập lập tức.
- **Ứng viên:** Candidate Portal sử dụng Magic Link gửi qua email cá nhân có TTL ngắn, không áp dụng OAuth2.

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
- **Truy cập:** Chỉ qua magic link sau khi Candidate xác nhận vị trí ứng tuyển. Không xuất hiện trên Job Board hay Candidate Portal công cộng.
- **Lượt dùng:** 1 lần per `application_id`. `ApplicationService` check và disable nếu đã dùng.
- **RAG nguồn:** `practice` – chỉ retrieve JD + CV chunks, không load Playbook. `real` – full RAG (JD + CV + Playbook).
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
[Google Speech Streaming STT (language-configured)]
      │ partial transcripts
      │ (VAD near-end) → [RAG: retrieve từ JD + CV + Playbook]
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
| `ISTTProvider` | Google Speech streaming (primary), Whisper (fallback) |
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

### ADR-029: Firebase Auth Bridge for Candidate Accounts
- **Decision:** Frontend React initializes Firebase Web SDK from `VITE_FIREBASE_*` environment variables. Candidate email/password registration and login can use Firebase Authentication when configured.
- **Backend validation:** ASP.NET Core validates Firebase ID tokens with issuer `https://securetoken.google.com/{project_id}` and audience `{project_id}` through a named JWT bearer scheme `Firebase`.
- **Token exchange:** Firebase identity is not used directly for protected ARISP APIs. `POST /api/auth/firebase/candidate/login` verifies the Firebase ID token, creates or updates the matching `candidate_accounts` row, then issues the existing ARISP JWT.
- **Scope:** Firebase is currently used only as an optional Candidate identity provider. HR/Super Admin SSO remains Google OAuth2 / OIDC with domain validation.
- **Config:** Firebase Web app values are public client configuration and live in frontend env vars. No Firebase service account secret is required for the current validation flow.

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
