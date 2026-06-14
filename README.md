# ARISP – AI-Powered Recruitment and Interview Support Platform

<!-- TODO: Thêm badges sau khi CI/CD (Phase 12) hoàn thiện -->
<!-- ![Build](https://github.com/QuanNguyendz/ARISP/actions/workflows/ci.yml/badge.svg) -->
<!-- ![.NET](https://img.shields.io/badge/.NET-8.0-blue) -->
<!-- ![Node](https://img.shields.io/badge/Node.js-18+-green) -->
<!-- ![License](https://img.shields.io/badge/license-Proprietary-red) -->

Nền tảng tuyển dụng nội bộ doanh nghiệp tích hợp **Job Board IT** và **AI Interview Automation**. AI tự động phỏng vấn ứng viên qua nhiều vòng, đánh giá Pass/Not Pass – HR xác nhận kết quả cuối cùng mà không cần tham gia trực tiếp vào buổi phỏng vấn.

**Kiến trúc:** Single-tenant – triển khai nội bộ độc quyền cho 1 doanh nghiệp duy nhất.

---

## Demo

> 📌 **TODO:** Thêm screenshot / GIF giao diện sau khi MVP hoàn thiện (Phase 7+).

---

## User Roles

| Role | Mô tả |
|---|---|
| **Super Admin** | Quản trị hệ thống – cấu hình allowed domains, webhook ATS/Slack/Teams, quản lý tài khoản HR, xem audit log |
| **HR Leader** | Cấu hình Job Posting & Playbook, xem Evaluation Report, `Confirm` hoặc `Override` kết quả AI |
| **Recruiter** | Tạo tin tuyển dụng nháp, quản lý hồ sơ ứng viên, cấp Interview Code cho phỏng vấn On-site |
| **Candidate** | Tự ứng tuyển qua Job Board, làm Practice Interview (remote) và Real Interview (on-site), xem kết quả |

---

## Interview Modes

| Mode | Hình thức | RAG Data |
|---|---|---|
| **Online Test** | Trắc nghiệm trực tuyến – tự động chấm điểm & chuyển vòng | — |
| **Practice Session** | Remote từ nhà, tối đa 1 lần/Application, dùng để làm quen AI | JD + CV |
| **Real Session** | On-site tại văn phòng – nhập Interview Code tại Kiosk | JD + CV + Playbook |

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | React 18, TypeScript, TailwindCSS, MUI |
| **Backend** | C# ASP.NET Core .NET 8, REST API, SignalR |
| **Database** | PostgreSQL (Supabase host), Entity Framework Core, pgvector |
| **Auth** | JWT + Role-based, Google OAuth2 + Domain Validation (HR), Magic Link (Candidate), BCrypt |
| **AI / LLM** | OpenAI GPT-4o + RAG (pgvector, text-embedding-3-small) |
| **CV Analysis** | Google Gemini 2.5 Flash (CV-JD Match Score) |
| **STT** | Google Speech-to-Text (streaming real-time) |
| **TTS** | ElevenLabs Flash v2.5 (streaming) |
| **Avatar** | HeyGen Streaming Avatar – Hybrid Idle Strategy |
| **Realtime** | WebRTC (media stream), SignalR (session events) |
| **Cache** | Redis |
| **Containers** | Docker, Docker Compose |
| **Networking** | Nginx (Reverse Proxy, SSL, Routing) |
| **Monitoring** | Serilog, Grafana, Health Checks |
| **CI/CD** | GitHub Actions |

---

## Cấu trúc dự án

```
ARISP/
├── backend/                  # ASP.NET Core (.NET 8) – Clean Architecture
│   ├── ARISP.API/            # Controllers, Middleware, Program.cs
│   ├── ARISP.Application/    # Use Cases, DTOs, Interfaces, Validators
│   ├── ARISP.Domain/         # Entities, Value Objects, Domain Events
│   └── ARISP.Infrastructure/ # EF Core, Repositories, External Services
├── frontend/                 # React + TypeScript + TailwindCSS / MUI
├── docker/                   # Dockerfile, docker-compose files
├── nginx/                    # Nginx config
├── docs/                     # Tài liệu đặc tả kỹ thuật
│   └── database/             # schema.sql và schema.md chi tiết
├── public/                   # Static assets
├── supabase/                 # Supabase config & migrations
├── .ai/                      # Context dự án dành cho AI tools (Source of Truth)
├── AGENTS.md                 # Bridge file cho Antigravity
├── CLAUDE.md                 # Bridge file cho Claude Code
└── README.md
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js ≥ 18](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- Kết nối PostgreSQL (Supabase hoặc local)

### 1. Clone repo

```bash
git clone https://github.com/QuanNguyendz/ARISP.git
cd ARISP
```

### 2. Cấu hình Backend

```bash
cd backend/ARISP.API

# Copy file template, sau đó điền key thật (liên hệ team lead để lấy key)
cp appsettings.Development.json.example appsettings.Development.json
```

Mở `appsettings.Development.json` và thay thế tất cả giá trị `<...>`. Xem hướng dẫn chi tiết trong chính file đó.

### 3. Chạy Backend

```bash
cd backend
dotnet restore
dotnet run --project ARISP.API
# API chạy tại: http://localhost:5000
```

### 4. Chạy Frontend

```bash
cd frontend
npm install
npm run dev
# Web chạy tại: http://localhost:3000
```

### 5. Chạy bằng Docker *(alternative)*

```bash
cd docker
docker-compose up --build
```

---

## Environment Variables

Tất cả secrets được quản lý qua `appsettings.Development.json` (không commit lên git).
Template tại: [`backend/ARISP.API/appsettings.Development.json.example`](backend/ARISP.API/appsettings.Development.json.example)

| Key | Mô tả | Lấy từ đâu |
|---|---|---|
| `ConnectionStrings.DefaultConnection` | PostgreSQL connection string | Supabase Dashboard → Database |
| `JWT.Secret` | JWT signing key (256-bit) | Team lead |
| `Authentication.Google.ClientId` | Google OAuth2 Client ID | Google Cloud Console |
| `Authentication.Google.ClientSecret` | Google OAuth2 Client Secret | Google Cloud Console |
| `EmailSettings.AppPassword` | Gmail App Password | Google Account → Security → App passwords |

---

## Architecture Overview

> 📌 **TODO:** Thêm sơ đồ kiến trúc hệ thống (component diagram, sequence diagram cho AI Interview flow).

Chi tiết ADR và quyết định kiến trúc: xem [`.ai/architecture.md`](.ai/architecture.md)

**Latency target (Streaming-First):**
```
Candidate nói xong → VAD → STT (300ms) → RAG (parallel) → GPT-4o stream (400–800ms) → ElevenLabs TTS (150–300ms) → HeyGen Avatar
Tổng: ~1–1.8 giây
```

---

## API Documentation

> 📌 **TODO:** Tích hợp Swagger UI sau khi các core API hoàn thiện. Endpoint sẽ tại `/swagger`.

---

## Contributing

### Branch Naming

| Prefix | Mục đích |
|---|---|
| `feature/<scope>/<name>` | Tính năng mới |
| `fix/<scope>/<desc>` | Bug fix |
| `chore/<scope>/<desc>` | Config, CI, dependency |
| `docs/<desc>` | Tài liệu thuần |

> Scope: `be` \| `fe` \| `docker` \| `infra` \| `db` \| `ai`

### Commit Convention

```
<type>(<scope>): <mô tả ngắn>
```

Type: `feat` | `fix` | `refactor` | `docs` | `test` | `chore` | `setup`

---

## Deployment

> 📌 **TODO:** Hướng dẫn deploy lên Ubuntu VPS + Nginx + SSL sẽ được bổ sung khi Phase 12 hoàn thiện.

---

## Tiến độ (Phases)

> Chi tiết checkbox từng task: xem [`.ai/tasks.md`](.ai/tasks.md)

### Phase 0 – Foundation ✅
- [x] Khởi tạo boilerplate (backend Clean Architecture + frontend Vite/React)
- [x] Setup Docker + Nginx

### Phase 1 – Auth ✅
- [x] Database schema: `users`, `refresh_tokens`, `system_settings`
- [x] JWT Auth + Role-based authorization (4 roles)
- [x] Google OAuth2 + Domain Validation cho HR
- [x] Magic Link auth cho Candidate
- [x] Pre-provisioning (không cho phép self-register đối với HR/Admin)

### Phase 2 – Job Posting & Application ✅
- [x] Job Posting CRUD (multi-round config, language detection)
- [x] Application flow + CV upload (PDF/DOCX)
- [x] Candidate invite flow (email magic link)

### Phase 2a – CV-JD Analysis (Gemini)
- [ ] Tích hợp Gemini 2.5 Flash phân tích CV-JD
- [ ] Bảng `cv_jd_analyses`, Match Score + Summary
- [ ] Hiển thị kết quả phân tích trên Job Detail

### Phase 2b – Job Board & Practice Interview
- [ ] Candidate tự đăng ký tài khoản (Job Board)
- [ ] Candidate tìm kiếm, xem chi tiết và tự ứng tuyển (Self-apply)
- [ ] Practice Session – Remote (1 lần/Application, JD+CV RAG)

### Phase 2c – Online Test (Multiple Choice)
- [ ] Database schema: `online_test_questions`, `online_test_submissions`
- [ ] HR cấu hình câu hỏi trắc nghiệm per Job
- [ ] Candidate làm trắc nghiệm – tự động chấm điểm & chuyển vòng

### Phase 3 – Interview Code & Kiosk
- [x] Tạo & quản lý Interview Code (6 ký tự, one-time-use, TTL 2h) – backend
- [ ] Đặt lịch phỏng vấn thử (Availability Slots)
- [ ] Frontend On-site Kiosk Mode

### Phase 4 – AI Interview Core
- [x] Domain entities (22 entities), IAIProvider + IEmbeddingProvider, pgvector
- [x] SignalR hubs (SessionHub + WebRTCSignalingHub)
- [ ] RAG pipeline (JD + CV) + adaptive difficulty
- [ ] Phân biệt luồng RAG `practice` (JD+CV) vs `real` (JD+CV+Playbook)

### Phase 4b – Interview Playbook
- [x] PlaybookService, MustAskTracking – backend infra
- [ ] Upload & parse tài liệu Playbook (Company / Job Posting / Round scope)
- [ ] Weighted RAG Reranking & Must-ask enforcement

### Phase 5 – Multi-round & Auto-progression
- [ ] Cấu hình multi-round per Job (`screening`, `technical`, `online_test`)
- [ ] Auto-progression sau khi HR Leader duyệt Pass

### Phase 6 – AI Evaluation & HR Review
- [x] EvaluationService, HrReview entity, AuditLog entity, Language Assessment DTOs – backend
- [ ] HR Dashboard duyệt kết quả (Confirm / Override)
- [ ] Email thông báo kết quả tới ứng viên

### Phase 7 – Media & Realtime
- [ ] VAD + Google STT streaming
- [ ] ElevenLabs TTS streaming
- [ ] HeyGen Streaming Avatar + Hybrid Idle Strategy
- [ ] Recording lưu trữ

### Phase 8 – Cheat Detection
- [ ] Frontend signals: eye tracking, focus loss, response time, speech cadence
- [ ] Backend: heuristic + AI analysis → CheatScore trong Evaluation Report

### Phase 9 – Candidate Portal
- [ ] Candidate Portal (Magic link auth, xem transcript / video / feedback)

### Phase 10 – System Configuration & Audit
- [ ] Super Admin: quản trị tài khoản HR, phân quyền
- [ ] Super Admin: cấu hình `system_settings` (Allowed domains, webhooks)
- [ ] Audit log dashboard

### Phase 11 – Integrations
- [ ] ATS Webhook push + Slack/Teams notifications

### Phase 12 – Infra & Deploy
- [ ] CI/CD GitHub Actions + SSL Nginx + Ubuntu VPS deploy

### Phase 13 – Polish & Scale
- [ ] Redis caching, CDN, Bias/Fairness Analysis, HR Analytics Dashboard

---

## Tài liệu liên quan

| File | Dành cho | Nội dung |
|---|---|---|
| [`CLAUDE.md`](CLAUDE.md) | AI tools | Bridge file cho Claude Code |
| [`AGENTS.md`](AGENTS.md) | AI tools | Bridge file cho Antigravity |
| [`.ai/context.md`](.ai/context.md) | AI tools | Bối cảnh dự án, business logic, auth flow |
| [`.ai/architecture.md`](.ai/architecture.md) | AI tools + Dev | ADR, kiến trúc hệ thống |
| [`.ai/tasks.md`](.ai/tasks.md) | AI tools + Dev | Trạng thái task chi tiết theo phase |
| [`.ai/coding-rules.md`](.ai/coding-rules.md) | AI tools + Dev | Coding standards & conventions |
| [`.ai/glossary.md`](.ai/glossary.md) | AI tools + Dev | Thuật ngữ domain |
| [`docs/database/schema.md`](docs/database/schema.md) | Dev + DBA | Đặc tả database schema, changelog, migration rules |
