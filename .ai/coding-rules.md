# Coding Rules – AI-Powered Recruitment and Interview Support Platform for Enterprises (ARISP)

## Backend (C# / ASP.NET Core .NET 8)

### Naming Conventions
- **Namespace:** `ARISP.<Layer>.<Module>` (ví dụ: `ARISP.Application.Interview`)
- **Class:** PascalCase – `InterviewService`, `UserController`
- **Interface:** prefix `I` – `IInterviewService`, `IUserRepository`
- **Method:** PascalCase – `CreateSessionAsync`, `GetFeedbackById`
- **Private field:** camelCase với prefix `_` – `_context`, `_logger`
- **Constant:** UPPER_SNAKE_CASE – `MAX_RETRY_COUNT`
- **Async method:** luôn suffix `Async` – `GetUserAsync`

### Project Structure (Clean Architecture)
```
backend/
├── ARISP.API/               # Controllers, Middleware, Program.cs
├── ARISP.Application/       # Use Cases, DTOs, Interfaces, Validators
├── ARISP.Domain/            # Entities, Value Objects, Domain Events
└── ARISP.Infrastructure/    # EF Core, Repositories, External Services
```

### Patterns bắt buộc
- **Repository Pattern** cho data access – không query DbContext trực tiếp ở Application layer.
- **CQRS** (Command/Query) nếu logic phức tạp – dùng MediatR.
- **Result Pattern** cho error handling – không throw exception cho business errors.
- **Dependency Injection** – không dùng service locator.
- **Async/Await** – tất cả I/O operations phải async.

### Security
- Không hardcode connection string, API key, secret – luôn dùng `appsettings.json` + env vars.
- JWT validation bắt buộc cho mọi protected endpoint.
- CORS config chặt – chỉ allow frontend domain.

---

## Frontend (React + TypeScript)

### Naming Conventions
- **Component:** PascalCase – `InterviewSession.tsx`, `FeedbackCard.tsx`
- **Hook:** prefix `use` – `useInterview`, `useAudioRecorder`
- **Util/Helper:** camelCase – `formatDuration.ts`, `parseScore.ts`
- **Type/Interface:** PascalCase – `InterviewSession`, `FeedbackResult`
- **CSS class (Tailwind):** kebab-case nếu custom class

### File Structure
```
frontend/src/
├── components/     # Reusable UI components
├── pages/          # Route-level components
├── hooks/          # Custom hooks
├── services/       # API calls (axios/fetch wrappers)
├── store/          # State management (nếu dùng Zustand/Redux)
├── types/          # TypeScript interfaces & types
└── utils/          # Pure utility functions
```

### Patterns
- Không fetch API trực tiếp trong component – đi qua `services/` layer.
- Dùng custom hook cho logic tái sử dụng.
- Tránh `any` – luôn type rõ ràng.

---

## Database (PostgreSQL + EF Core)

### Naming Conventions
- **Table:** snake_case, số nhiều – `users`, `interview_sessions`, `feedback_results`
- **Column:** snake_case – `created_at`, `user_id`, `session_status`
- **Migration:** tên mô tả hành động – `AddInterviewSessionTable`, `AddIndexOnUserId`

### Quy tắc
- Mọi thay đổi schema qua **EF Core Migration** – không sửa DB trực tiếp.
- Luôn có `created_at`, `updated_at` trên mọi entity chính.
- Soft delete (`deleted_at` nullable) thay vì hard delete cho dữ liệu quan trọng.
- **Multi-tenant:** Mọi entity thuộc về một Enterprise phải có cột `organization_id`. Repository layer phải enforce filter theo `organization_id` của user đang đăng nhập – không để lọt data chéo giữa các Enterprise.

---

## Git & CI/CD

### Branch naming
- `main` – production
- `develop` – integration branch
- `feature/<tên-feature>` – ví dụ: `feature/interview-session-flow`
- `fix/<mô-tả-lỗi>` – ví dụ: `fix/jwt-refresh-token-expiry`

### Commit message
```
<type>(<scope>): <mô tả ngắn>

type: feat | fix | refactor | docs | test | chore
scope: auth | interview | ai | frontend | infra | ...

Ví dụ:
feat(interview): add AI question generation endpoint
fix(auth): correct JWT expiry calculation
```
