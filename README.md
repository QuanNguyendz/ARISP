# ARISP – AI-Powered Recruitment and Interview Support Platform for Enterprises

Nền tảng tuyển dụng doanh nghiệp ứng dụng AI. AI tự động phỏng vấn ứng viên, đánh giá Pass/Not Pass, và HR xác nhận quyết định cuối cùng – không cần nhân sự nội bộ tham gia trực tiếp vào buổi phỏng vấn.

---

## Tính năng chính

### Dành cho HR Admin / Recruiter
- Tạo **Job Posting** với JD và cấu hình phỏng vấn (số câu hỏi, thời lượng, tiêu chí đánh giá).
- Mời ứng viên qua **invite link** có token – ứng viên không cần tự đăng ký.
- Xem **Evaluation Report** đầy đủ: Verdict (Pass/Not Pass), Score, Reasoning, phân tích từng câu trả lời.
- **Confirm** hoặc **Override** quyết định của AI (kèm ghi chú lý do khi override).
- Hệ thống tự động gửi **thông báo kết quả** đến ứng viên sau khi HR xác nhận.

### Dành cho Ứng viên
- Nhận invite link → submit **CV** và thông tin cá nhân.
- Tham gia **phỏng vấn AI tự động** qua video/audio với avatar AI đóng vai nhà tuyển dụng.
- Câu hỏi **bám sát JD và CV** của cá nhân, độ khó thích nghi theo chất lượng trả lời.
- Nhận thông báo kết quả sau khi HR xác nhận.

### AI Interview Engine
- AI **chủ động đặt câu hỏi** – không có human interviewer tham gia.
- Pipeline hoàn toàn streaming: STT → RAG → LLM → TTS → Avatar (**~1–1.8 giây latency**).
- AI tự dừng khi đã khai thác đủ context từ JD + CV.
- Sau session: AI tự động generate **Evaluation Report** (Verdict + Score + Reasoning).

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
| **Security** | JWT Auth, Role-based Authorization, BCrypt, HTTPS/SSL, CORS, Multi-tenant isolation |
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

## Tiến độ

### Phase 0 – Foundation
- [ ] Khởi tạo boilerplate (backend + frontend)
- [ ] Setup Docker + Nginx

### Phase 1 – Auth & Multi-tenant
- [ ] Database schema: organizations, users, roles
- [ ] JWT Auth + Role-based authorization
- [ ] Multi-tenant data isolation

### Phase 2 – Job Posting & Application
- [ ] Job Posting CRUD (HR Admin)
- [ ] Candidate invite flow (email link + token)
- [ ] CV upload & parsing

### Phase 3 – AI Interview Core
- [ ] RAG pipeline (JD/CV embedding + pgvector)
- [ ] AI question generation (GPT-4o streaming + adaptive difficulty)
- [ ] Interview session flow

### Phase 4 – AI Evaluation & HR Review
- [ ] AI Evaluation Report (Verdict + Score + Reasoning)
- [ ] HR Dashboard: Review + Confirm / Override
- [ ] Candidate notification (email + in-app)

### Phase 5 – Media & Realtime
- [ ] VAD + audio streaming (Google STT)
- [ ] ElevenLabs TTS streaming
- [ ] HeyGen Avatar (Hybrid Idle Strategy)
- [ ] SignalR session events

### Phase 6 – Infra & Deploy
- [ ] CI/CD (GitHub Actions)
- [ ] Deploy VPS (Docker + Nginx + SSL)
- [ ] Monitoring (Serilog + Grafana)
- [ ] Backup

### Phase 7 – Polish & Scale
- [ ] Interview recording lưu trữ
- [ ] Custom Scoring Rubric
- [ ] Analytics dashboard cho HR
- [ ] Redis caching + Cloudflare CDN

---

## Tài liệu liên quan

| File | Dành cho | Nội dung |
|---|---|---|
| `AGENTS.md` | AI tools | Bridge file cho Antigravity |
| `CLAUDE.md` | AI tools | Bridge file cho Claude Code |
| `.ai/context.md` | AI tools | Bối cảnh dự án, business logic, tech stack |
| `.ai/architecture.md` | AI tools + Dev | Kiến trúc hệ thống, ADR |
| `.ai/tasks.md` | AI tools + Dev | Trạng thái task hiện tại |
| `.ai/coding-rules.md` | AI tools + Dev | Coding standards & conventions |
| `.ai/glossary.md` | AI tools + Dev | Thuật ngữ domain |
