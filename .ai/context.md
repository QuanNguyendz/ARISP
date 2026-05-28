# Context – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

## Tổng quan dự án

ARISP là nền tảng tuyển dụng nội bộ doanh nghiệp tích hợp **Job Board IT** và **AI Interview Automation**. Ứng viên tự tìm và ứng tuyển việc làm IT trực tiếp trên nền tảng. AI tự động phỏng vấn ứng viên qua nhiều vòng, đánh giá Pass/Not Pass, HR xác nhận. Không cần nhân sự nội bộ tham gia trực tiếp vào buổi phỏng vấn.

**Mô hình kinh doanh:** Single-tenant – Dành riêng cho 1 doanh nghiệp sử dụng nội bộ (gộp Job Board + AI Interview Platform).

---

## User Roles

| Role | Mô tả |
|---|---|
| **System Admin** | Quản trị nền tảng – System config, quản lý user HR |
| **HR Admin / Recruiter** | Tạo Job Posting, cấu hình phỏng vấn, cấp Interview Code, review & confirm AI decision |
| **Candidate** | Tạo tài khoản, tìm kiếm và ứng tuyển việc IT qua Job Board; tham gia phỏng vấn AI (Practice Remote / Real On-site); xem lại recording & feedback qua Candidate Portal |

---

## Interview Mode – Remote vs On-site

### Remote Interview (Phỏng vấn thử - Practice Session)
- **CHỈ DÀNH CHO PHỎNG VẤN THỬ (PRACTICE).**
- Candidate nhận **magic link** qua email, truy cập vào Candidate Portal từ nhà qua web browser để làm quen với AI.

### On-site Interview (Phỏng vấn thật tại công ty)
- **BẮT BUỘC CHO MỌI VÒNG PHỎNG VẤN THẬT.**
- Candidate đến văn phòng. HR cấp **Interview Code** (one-time, có TTL – mặc định 2 giờ).
- Candidate nhập code tại thiết bị Kiosk của công ty → vào phỏng vấn ngay. Code vô hiệu hóa ngay sau khi dùng.

---

## Business Logic – Recruitment Flow

### Phase 1: Enterprise Setup (HR Admin)

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| Tên vị trí / Lĩnh vực | ✅ | Ví dụ: Backend Developer, IT |
| JD (Job Description) | ✅ | AI phân tích JD để tạo câu hỏi & detect yêu cầu ngôn ngữ |
| Cấu hình vòng phỏng vấn | ✅ | Số vòng, loại vòng (Screening / Technical), ngôn ngữ |
| Phỏng vấn thật | ✅ | Mặc định On-site (Tại công ty) |
| Availability Slots (Practice) | ⬜ | Khung giờ cho phép ứng viên chọn để làm Phỏng vấn thử (Remote) |
| Scoring Rubric | ⬜ | Custom tiêu chí đánh giá per Job Posting |
| Interview Persona | ⬜ | Tên, giọng, phong cách avatar AI |
| **Interview Playbook** | ⬜ | Upload tài liệu phỏng vấn nội bộ (câu hỏi, kịch bản, rubric chi tiết, ...) |

### Phase 2: Candidate Application
- Candidate tự ứng tuyển qua **Job Board** (chủ động) hoặc được HR mời trực tiếp (passive).
- AI tự động phân tích CV, so khớp với JD và chấm điểm (`AI Match Score`).
- Dựa trên bảng xếp hạng điểm AI, HR chọn ứng viên điểm cao và hệ thống tự động gửi **magic link**.
- Candidate nhận magic link → truy cập **Candidate Portal** để:
  - Xem thông tin vị trí ứng tuyển.
  - Chọn khung giờ (Availability Slot) để làm **Phỏng vấn thử (Practice Remote)** (nếu còn lượt – 1 lần per Application).

### Phase 3: Access Real Interview (On-site)
- Candidate đến văn phòng công ty theo lịch hẹn miệng/email với HR.
- HR cấp **Interview Code** → Candidate nhập code tại Kiosk của công ty → vào phỏng vấn thật.

### Phase 4: Multi-round AI Interview

#### Round 1 – Screening (Language-aware)
- **Language-aware:** AI phân tích JD, detect yêu cầu ngôn ngữ (ví dụ: "Giao tiếp tiếng Anh tốt, TOEIC > 700" hoặc "IELTS > 6.5").
  - Nếu có yêu cầu → **phỏng vấn bằng ngôn ngữ đó** (English, Japanese, Korean, v.v.).
  - AI đánh giá cả **nội dung** lẫn **năng lực ngôn ngữ** (fluency, grammar, vocabulary, comprehension).
  - Không có yêu cầu → phỏng vấn tiếng Việt (mặc định).
- Nguồn câu hỏi: JD + CV + Language assessment (nếu có).
- Adaptive difficulty.

#### Round 2 – Technical Deep-dive
- Chỉ kích hoạt khi Candidate **Pass Round 1**.
- Đánh giá chuyên sâu kỹ năng kỹ thuật/chuyên môn.
- Ngôn ngữ: theo cấu hình Job Posting (mặc định tiếng Việt hoặc tiếp tục ngôn ngữ Round 1).

#### Auto-progression
```
Round N kết thúc → AI Evaluation → HR Review
  → Pass: hệ thống tự động invite Round N+1
  → Not Pass: gửi thông báo từ chối cho Candidate
```

### Phase 5: AI Evaluation (mỗi Round)
- **Verdict:** Pass / Not Pass
- **Overall Score** (0–100)
- **Per-criterion scores:** technical, communication, language proficiency (nếu applicable), culture fit
- **Language Assessment** (Round 1): fluency, grammar, vocabulary, comprehension score
- **Per-question analysis**
- **Recommended next step**

### Phase 6: HR Review & Confirm
- HR xem Evaluation Report + recording.
- **Confirm** hoặc **Override** (kèm `override_reason` bắt buộc).
- Sau confirm → hệ thống tự động lưu kết quả, cấp Interview Code mới nếu có vòng sau (Round N+1) hoặc gửi thông báo từ chối.

### Phase 7: Candidate Portal
- Đăng nhập bằng email + magic link (không cần password).
- Xem: recording, transcript, Evaluation Report (phần HR cho phép hiển thị), feedback.

---

## Feature Modules

### Interview Playbook (Org Knowledge Base)

Doanh nghiệp upload tài liệu phỏng vấn nội bộ để AI phỏng vấn đúng phong cách và đúng nội dung mong muốn.

**Phạm vi upload:**

| Cấp | Loại tài liệu | Ví dụ |
|---|---|---|
| **Company** | Interview Style Guide | "Chúng tôi phỏng vấn thân thiện, tập trung vào tư duy" |
| **Company** | Competency Framework | Ma trận kỹ năng theo level (Junior/Mid/Senior) |
| **Company** | Culture & Values Guide | Mission, core values, culture fit indicators |
| **Company** | Compliance / Must-not-ask | Câu hỏi không được hỏi (tuổi, tôn giáo...) |
| **Company** | Red Flag Guide | Dấu hiệu cần probe sâu hoặc loại bỏ ứng viên |
| **Job Posting** | Question Bank | Danh sách câu hỏi bắt buộc/gợi ý per vị trí |
| **Job Posting** | Technical Scenarios | Bài toán / case study cụ thể cho vị trí |
| **Job Posting** | Expected Answer Guide | Câu trả lời tốt cần đề cập gì |
| **Job Posting** | Must-ask Questions | Câu hỏi không thể bỏ qua |
| **Round** | Round-specific Playbook | Screening guide (Round 1), Technical deep-dive (Round 2) |

**Format hỗ trợ:** PDF, DOCX, TXT, Markdown, JSON (question bank format), Interview transcript ẩn danh.

**Cách RAG sử dụng:**
```
Khi AI sinh câu hỏi → retrieve từ:
  ① JD chunks              (weight cao – yêu cầu vị trí)
  ② CV chunks              (weight cao – kinh nghiệm ứng viên)
  ③ Org Playbook chunks    (weight trung bình – style + compliance)
  ④ Job Posting Playbook   (weight cao – câu hỏi bắt buộc, scenario)
  ⑤ Round Playbook         (weight cao – phù hợp vòng hiện tại)
```

### System Admin
- Quản lý team HR: phân quyền theo bộ phận.
- **Audit log:** mọi hành động quan trọng (confirm/override, khi nào, lý do).

### AI Enhancement
- **Custom Interview Persona:** tên, giọng, phong cách phỏng vấn theo văn hóa công ty.
- **Cheat Detection:** phát hiện đọc script, dùng AI trợ lý (speech pattern anomaly, eye tracking, response timing).
- **Bias Detection & Fairness Report:** phân tích kết quả theo nhân khẩu học.

### Integrations
- **ATS Webhook/API:** Workday, SAP SuccessFactors, Greenhouse – push kết quả tự động.
- **SSO:** SAML 2.0, Google Workspace, Microsoft Entra.
- **Slack/Teams Notifications:** HR nhận thông báo khi có Evaluation mới cần Review.

### Job Board (IT-focused)

Ứng viên tạo tài khoản trên ARISP và tìm kiếm việc làm IT trực tiếp trên nền tảng.

- **Thị trường:** Chỉ tập trung IT (Dev, DevOps, QA, Data, Design, PM, ...)
- **Tên công ty:** Hiển thị công khai trên tin tuyển dụng
- **Self-apply:** Ứng viên chủ động ứng tuyển → HR xem CV → HR chủ động gửi magic link
- **Nguồn dữ liệu:** Job Posting nội bộ là nguồn chung cho cả Job Board lẫn AI Interview Platform

### Practice Interview (Phỏng vấn thử)

Tính năng giúp ứng viên làm quen với format phỏng vấn AI trước khi bước vào phỏng vấn thực.

- **Truy cập:** Chỉ qua **magic link** – không xuất hiện trên Job Board hay Candidate Portal
- **Lượt dùng:** **1 lần per Application** – không reset
- **Nguồn câu hỏi:** JD + CV (không dùng Playbook – Playbook chỉ áp dụng cho phỏng vấn thực)
- **Kết quả:** HR **có thể xem** score, recording của Practice Session
- **Chi phí:** Doanh nghiệp chịu – tính vào subscription (không thu thêm từ ứng viên)
- **Phân biệt:** Practice Session không ảnh hưởng đến verdict/kết quả tuyển dụng

---

## Tech Stack

| Layer | Technology | Ghi chú |
|---|---|---|
| **Frontend** | React, TypeScript, TailwindCSS / MUI | |
| **Backend** | C#, ASP.NET Core (.NET 8) | Không dùng Node.js |
| **API Style** | REST API + SignalR | SignalR cho session events |
| **Realtime Media** | WebRTC | Audio stream + avatar video |
| **Database** | PostgreSQL on Supabase | Chỉ host DB – không dùng Supabase SDK |
| **ORM** | Entity Framework Core | |
| **Auth** | JWT + Role-based Authorization | BCrypt hash; Magic link cho Candidate Portal |
| **Cache** | Redis | |
| **AI/LLM** | OpenAI GPT-4o + RAG (pgvector) | text-embedding-3-small cho embedding |
| **STT** | Google Speech-to-Text | Streaming real-time |
| **TTS** | ElevenLabs Flash v2.5 | Streaming |
| **Avatar** | HeyGen Streaming Avatar | Hybrid Idle Strategy |
| **Email** | SendGrid / AWS SES | Invite, magic link, kết quả |
| **Containers** | Docker + Docker Compose | |
| **Servers** | Ubuntu/Linux VPS | |
| **Reverse Proxy** | Nginx | SSL + routing |
| **CI/CD** | GitHub Actions | |
| **Monitoring** | Serilog + Grafana + Health Checks | |
| **Backup** | pg_dump schedule + VPS snapshot | |
| **CDN** | Cloudflare CDN | Optional – sau MVP |
| **Version Control** | GitHub | |

> **Streaming-First:** STT stream → RAG parallel → LLM stream → TTS stream → Avatar stream. Mục tiêu: **~1–1.8 giây** latency sau khi ứng viên dừng nói.

---

## Quy tắc bắt buộc

1. **Không tự ý thay đổi tech stack** khi chưa được user xác nhận.
2. **Không hardcode secrets** – luôn dùng environment variables.
3. **Không dùng Supabase SDK** – kết nối PostgreSQL trực tiếp qua connection string.
4. **Không đề xuất Node.js** cho backend.
5. **Mọi thay đổi kiến trúc** cập nhật vào `.ai/architecture.md`.
6. **Trước khi bắt đầu task mới** – kiểm tra `.ai/tasks.md`.
7. **Khi có quyết định mới** – ghi lại ngay vào file `.ai/` tương ứng.
8. **AI/LLM:** Business logic không gọi trực tiếp OpenAI SDK – qua `IAIProvider` + `IEmbeddingProvider`. Swap qua env var `AI_PROVIDER=openai|local`.
9. **WebRTC** chỉ dùng cho media stream. Session events dùng SignalR.
10. **Streaming-First:** Không chấp nhận batch nếu có alternative streaming khả thi.
11. **Single-tenant:** Hệ thống phục vụ cho 1 công ty duy nhất. Không sử dụng `organization_id`.
12. **Interview Code:** One-time-use, TTL ngắn (mặc định 2 giờ, cấu hình được), vô hiệu hóa ngay sau khi dùng.
13. **Language detection:** AI detect từ JD – không hardcode mapping ngôn ngữ.
14. **Connection Drop Recovery:** Nếu ứng viên mất kết nối, session duy trì trạng thái active. Khi nhập lại code, AI resume lại luồng câu hỏi chưa hỏi (track qua bảng `must_ask_tracking`).

---

## Cấu trúc thư mục dự án

```
ARISP/
├── backend/          # ASP.NET Core (.NET 8) – REST API + SignalR
├── frontend/         # React + TypeScript + TailwindCSS / MUI
├── docker/           # Dockerfile, docker-compose files
├── nginx/            # Nginx config
├── scripts/          # CI/CD, backup, restore scripts
├── .ai/              # Source of truth – mọi AI tool kéo từ đây
├── .tools/           # Adapter riêng cho từng AI tool
├── AGENTS.md         # Bridge cho Antigravity
├── CLAUDE.md         # Bridge cho Claude Code
└── README.md         # Cho con người
```
