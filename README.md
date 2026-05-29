# ARISP – AI-Powered Recruitment and Interview Support Platform for Enterprises

Nền tảng tuyển dụng doanh nghiệp ứng dụng AI tích hợp **Job Board IT** và **AI Interview Automation**. AI tự động phỏng vấn ứng viên, tự động chấm điểm bài thi trắc nghiệm, đánh giá Pass/Not Pass, và HR phê duyệt quyết định cuối cùng – không cần nhân sự nội bộ tham gia trực tiếp vào buổi phỏng vấn.

**Kiến trúc:** Single-tenant – Triển khai nội bộ độc quyền cho 1 doanh nghiệp duy nhất. Hệ thống không sử dụng phân vùng đa tổ chức.

---

## 4 Vai trò người dùng (User Roles)

1. **Super Admin**: Quản trị viên hệ thống toàn cục. Thiết lập các thông số hệ thống (`system_settings` như danh sách Allowed Domain cho OAuth2, webhook ATS, Slack, Teams), quản lý tài khoản HR nhân viên, theo dõi nhật ký hệ thống (`audit_log`).
2. **HR Leader**: Trưởng nhóm nhân sự / HR Admin. Cấu hình Job Posting, quản lý và duyệt Playbook nội bộ, xem chi tiết Evaluation Report, phê duyệt (`Confirm`) hoặc thay đổi đề xuất AI (`Override` - bắt buộc nhập lý do).
3. **Recruiter (HR Staff)**: Chuyên viên tuyển dụng hỗ trợ. Tạo tin tuyển dụng nháp, quản lý hồ sơ ứng viên, cấp mã **Interview Code** khi ứng viên đến thi thật On-site tại văn phòng, xem Evaluation Report.
4. **Candidate**: Ứng viên tuyển dụng. Đăng ký tài khoản cá nhân, tự ứng tuyển qua Job Board; nhận Magic Link đăng nhập Candidate Portal để đặt lịch và làm **Phỏng vấn thử (Practice Remote)** từ nhà; đến văn phòng công ty nhập mã Interview Code để làm **Phỏng vấn thật (Real On-site)**; xem transcript, video, feedback sau khi HR duyệt.

---

## Các chế độ phỏng vấn & Kiểm tra

* **Vòng thi Trắc nghiệm (Online Test - Phase 2c):** Làm trực tuyến trên Candidate Portal. Hệ thống tự động chấm điểm và đánh giá Đạt/Trượt ngay lập tức thông qua so khớp đáp án thực tế và đáp án đúng.
* **Phỏng vấn thử (Practice Session - Remote):** Làm trực tuyến từ nhà qua trình duyệt để làm quen với AI Interviewer (giới hạn tối đa 1 lần per Application). RAG chỉ sử dụng dữ liệu JD + CV ứng viên.
* **Phỏng vấn thật (Real Session - On-site):** Bắt buộc làm việc trực tiếp tại văn phòng. Ứng viên nhập mã **Interview Code** trên thiết bị Kiosk của công ty để vào phỏng vấn AI. RAG nạp đầy đủ dữ liệu JD + CV + Playbook nội bộ doanh nghiệp.

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | React, TypeScript, TailwindCSS / MUI |
| **Backend / APIs** | C#, ASP.NET Core (.NET 8), REST API, SignalR |
| **Database** | PostgreSQL on Supabase (chỉ host DB), Entity Framework Core, pgvector |
| **Servers** | Ubuntu/Linux VPS, Docker Host |
| **Networking** | Nginx (Reverse Proxy, SSL, Routing) |
| **CI/CD** | GitHub Actions |
| **Security & Auth** | JWT Auth, Role-based Authorization, **OAuth2 / OIDC + Domain Validation (Google/Microsoft)** cho HR, Magic Link cho Candidate, BCrypt hash |
| **Containers** | Docker, Docker Compose |
| **Cache** | Redis |
| **AI / LLM** | OpenAI GPT-4o + RAG (pgvector + text-embedding-3-small) |
| **STT** | Google Speech-to-Text (streaming real-time) |
| **TTS** | ElevenLabs Flash v2.5 (streaming) |
| **Avatar** | HeyGen Streaming Avatar (WebRTC, Hybrid Idle Strategy) |
| **Monitoring** | Serilog, Grafana, Health Checks |
| **Backup** | pg_dump schedule, restore script, VPS snapshot |
| **CDN** | Cloudflare CDN *(optional – sau MVP)* |
| **Version Control** | GitHub |

---

## Cấu trúc dự án

```
ARISP/
├── backend/          # ASP.NET Core (.NET 8) – REST API + SignalR
├── frontend/         # React + TypeScript + TailwindCSS / MUI
├── docker/           # Dockerfile, docker-compose files
├── nginx/            # Nginx config
├── scripts/          # CI/CD, backup, restore scripts
├── docs/             # Tài liệu đặc tả kỹ thuật và Database Schema
│   └── database/     # schema.sql và schema.md chi tiết
├── .ai/              # Context dự án dành cho AI tools (Source of Truth)
├── .tools/           # Config riêng cho từng AI tool
├── AGENTS.md         # Bridge cho Antigravity
├── CLAUDE.md         # Bridge cho Claude Code
└── README.md         # File này – tổng quan dự án cho developer
```

---

## Bắt đầu phát triển

> Xem chi tiết hướng dẫn setup môi trường trong từng thư mục `backend/` và `frontend/`.

**Yêu cầu:**
- .NET 8 SDK
- Node.js 20+
- Docker & Docker Compose
- PostgreSQL (hoặc kết nối Supabase)

---

## Tiến độ (Phases)

### Phase 0 – Foundation
- [ ] Khởi tạo boilerplate (backend + frontend)
- [ ] Setup Docker + Nginx

### Phase 1 – Global Settings & Auth Setup
- [ ] Database schema: `users`, `refresh_tokens`, `system_settings`
- [ ] EF Core migrations (không dùng IMultiTenant và các cột organization_id)
- [ ] JWT Auth + Role-based authorization cho 4 role cụ thể
- [ ] **OAuth2 & Domain Validation:** Google/Microsoft OAuth2 + middleware validate domain công ty

### Phase 2 – Job Posting & Application
- [ ] Job Posting CRUD (HR Admin / Recruiter, mặc định `onsite`)
- [ ] Candidate invite flow (email magic link + token)
- [ ] CV upload & parsing (PDF → text)

### Phase 2b – Job Board & Practice Interview
- [ ] Candidate tự đăng ký tài khoản (Job Board)
- [ ] Candidate tìm kiếm, xem chi tiết và ứng tuyển (Self-apply)
- [ ] Phỏng vấn thử (Practice Session - Remote, 1 lần per Application, JD+CV RAG)

### Phase 2c – Online Test (Multiple Choice Quiz)
- [ ] Database schema: `online_test_questions`, `online_test_submissions`
- [ ] HR cấu hình câu hỏi trắc nghiệm riêng cho từng Job
- [ ] Ứng viên làm trắc nghiệm trực tuyến (Tự động chấm điểm & Chuyển vòng phỏng vấn AI)

### Phase 3 – Scheduling (Practice) & Interview Code
- [ ] Đặt lịch phỏng vấn thử (Availability Slots)
- [ ] Tạo & quản lý mã **Interview Code** thi thật On-site (One-time-use, TTL 2h)
- [ ] Frontend On-site Kiosk Mode (Nhập mã Code để thi thật)

### Phase 4 – AI Interview Core
- [ ] pgvector + `IEmbeddingProvider` + `IAIProvider` (GPT-4o streaming)
- [ ] RAG pipeline (JD + CV) + adaptive difficulty
- [ ] Phân biệt luồng RAG `practice` (JD+CV only) vs `real` (JD+CV+Playbook)

### Phase 4b – Interview Playbook (Org Knowledge Base)
- [ ] Database schema: `playbook_documents`, `must_ask_tracking`
- [ ] Upload & parse tài liệu theo scope (Company, Job Posting, Round)
- [ ] Weighted RAG Reranking & Must-ask question enforcement

### Phase 5 – Multi-round & Auto-progression
- [ ] Cấu hình multi-round per Job (`screening`, `technical`, `online_test`)
- [ ] Auto-progression chuyển vòng sau khi HR Leader duyệt Pass

### Phase 6 – AI Evaluation & HR Review
- [ ] AI Evaluation (Score + Verdict + Reasoning + Language Assessment)
- [ ] HR Dashboard duyệt kết quả (Confirm / Override)
- [ ] Email thông báo kết quả thi tới ứng viên

### Phase 7 – Media & Realtime
- [ ] VAD + audio streaming (Google STT)
- [ ] ElevenLabs TTS streaming
- [ ] HeyGen Avatar streaming WebRTC (Hybrid Idle Strategy)
- [ ] SignalR session events & Recording lưu trữ

### Phase 8 – Cheat Detection
- [ ] Frontend: thu thập signals (eye tracking, focus loss, response time, speech cadence)
- [ ] Backend: heuristic + AI analysis ra CheatScore tích hợp Evaluation Report

### Phase 9 – Candidate Portal
- [ ] Candidate Portal giao diện riêng (Magic link auth, xem transcript, video, feedback)

### Phase 10 – System Configuration & Global Audit
- [ ] Super Admin: quản trị tài khoản HR, phân quyền theo phòng ban
- [ ] Super Admin: quản trị `system_settings` (Allowed domains, global webhooks)
- [ ] Audit log dashboard xem lịch sử thao tác toàn hệ thống

### Phase 11 – Integrations
- [ ] Global ATS Webhook push + Slack/Teams notifications

### Phase 12 – Infra & Deploy
- [ ] CI/CD GitHub Actions + SSL Nginx + Ubuntu VPS deploy

### Phase 13 – Polish & Scale
- [ ] Caching, CDN, Bias/Fairness Analysis & HR Analytics Dashboard

---

## Tài liệu liên quan

| File | Dành cho | Nội dung |
|---|---|---|
| `AGENTS.md` | AI tools | Bridge file cho Antigravity |
| `CLAUDE.md` | AI tools | Bridge file cho Claude Code |
| `.ai/context.md` | AI tools | Bối cảnh dự án, business logic, 4 role, tech stack, luồng đăng nhập |
| `.ai/architecture.md` | AI tools + Dev | Kiến trúc hệ thống, ADR (Single-tenant, OAuth2, WebRTC) |
| `.ai/tasks.md` | AI tools + Dev | Trạng thái task hiện tại, phân chia phase cuốn chiếu |
| `.ai/coding-rules.md` | AI tools + Dev | Coding standards & conventions |
| `.ai/glossary.md` | AI tools + Dev | Thuật ngữ domain |
| `docs/database/schema.md` | Dev + DBA | Đặc tả cấu trúc database trọn vẹn, changelog, quy tắc migration |
