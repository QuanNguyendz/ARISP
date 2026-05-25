# Docker – ARISP

## Quick Start

```bash
# 1. Copy env template
cp .env.example .env

# 2. Fill in your secrets in .env

# 3. Start all services (development)
docker compose up --build

# 4. Access
#    Frontend:  http://localhost (qua nginx) hoặc http://localhost:3000 (direct)
#    Backend:   http://localhost/api (qua nginx) hoặc http://localhost:5000 (direct)
```

## Services

| Service | Container | Port (host) | Mô tả |
|---|---|---|---|
| **backend** | `arisp-backend` | 5000 | ASP.NET Core .NET 8 – REST API + SignalR |
| **frontend** | `arisp-frontend` | 3000 | React + TypeScript (Vite dev server) |
| **redis** | `arisp-redis` | 6379 | Cache (session, rate limiting) |
| **nginx** | `arisp-nginx` | 80 | Reverse proxy (routing frontend + backend) |

> **PostgreSQL** hosted trên Supabase – không containerize. Kết nối qua `DATABASE_*` env vars.

## Compose Files

| File | Mục đích | Command |
|---|---|---|
| `docker-compose.yml` | Development (hot-reload, source mount) | `docker compose up --build` |
| `docker-compose.prod.yml` | Production override (multi-stage, resource limits) | `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build` |

## Development Features

- **Backend:** `dotnet watch` – hot-reload khi thay đổi code C#
- **Frontend:** Vite HMR – hot-reload khi thay đổi code React/TS
- **Redis:** Health check tự động, data persist qua volume

## Environment Variables

Xem `.env.example` để biết danh sách đầy đủ. Các nhóm chính:
- `DATABASE_*` – Supabase PostgreSQL connection
- `REDIS_*` – Redis cache
- `JWT_*` – Authentication
- `AI_PROVIDER`, `OPENAI_API_KEY` – AI/LLM
- `ELEVENLABS_*`, `HEYGEN_*`, `GOOGLE_STT_*` – Media services
- `SENDGRID_*` – Email
