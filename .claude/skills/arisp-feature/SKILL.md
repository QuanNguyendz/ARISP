---
name: arisp-feature
description: Scaffold/triển khai một feature vertical-slice trong ARISP đúng quy ước Clean Architecture (.NET 8) backend và React services-layer frontend. Dùng khi cần thêm endpoint, entity, service, migration, page hoặc bất kỳ thay đổi xuyên tầng nào — đảm bảo tuân thủ các ADR, Result Pattern, Repository/UnitOfWork, và quy tắc bắt buộc cập nhật .ai/tasks.md.
---

# ARISP Feature Builder

Skill này mã hoá đúng các quy ước của codebase ARISP để mọi feature mới khớp với code hiện có ngay từ lần đầu. Đọc kỹ trước khi thêm code xuyên tầng.

## Khi nào dùng
- Thêm/sửa endpoint REST hoặc SignalR hub
- Thêm Domain entity + EF migration
- Thêm Application service / DTO / interface
- Thêm page/route/service/store ở frontend
- Bất kỳ thay đổi nào chạm ≥2 tầng

## Bản đồ kiến trúc (luồng phụ thuộc một chiều)

```
Domain  ←  Application  ←  Infrastructure
                ↑              ↑
               API (Controllers/Hubs/Middleware)
Frontend (React) ──HTTP/SignalR──▶ API
```

- `ARI.Domain` — Entities, không phụ thuộc tầng nào khác.
- `ARI.Application` — Services, DTOs, Interfaces (`I*`), `Common/Result.cs`. Chỉ phụ thuộc Domain.
- `ARI.Infrastructure` — EF Core (`Data/`), `Repositories/`, `Migrations/`, external providers (`AI/`, `Storage/`, `Services/`). Implement interfaces của Application.
- `ARI.API` — Controllers, Hubs, Middleware, `Program.cs` (DI wiring).
- `frontend/src` — `pages/` (theo role) → `services/` → `apiClient` → API. Không bao giờ fetch trực tiếp trong component.

## Checklist một vertical slice (backend → frontend)

1. **Domain entity** (`ARI.Domain/Entities/<Name>.cs`)
   - `Guid Id { get; set; } = Guid.NewGuid();`
   - Bắt buộc `CreatedAt`, `UpdatedAt` (kiểu `DateTimeOffset`, default `UtcNow`).
   - Soft delete: implement `ISoftDelete` → `DateTimeOffset? DeletedAt`. KHÔNG hard delete.
   - Comment inline cho field dạng enum-string (vd `// draft | active | closed`).
   - KHÔNG có `organization_id` (ADR-012, single-tenant).

2. **EF mapping + migration** (`ARI.Infrastructure`)
   - Map trong `Data/` (DbContext config). Table `snake_case` số nhiều, column `snake_case`.
   - Mọi schema change qua migration — không sửa DB tay:
     `dotnet ef migrations add <Name> -p ari-service/src/ARI.Infrastructure -s ari-service/src/ARI.API`
   - User đã pre-approve việc thêm cột/migration khi task cần (xem memory).

3. **Command/Query + Handler (CQRS — ADR-045)** (`ARI.Application/<Feature>/Commands|Queries/<UseCase>/`)
   - Mỗi endpoint = 1 `record XxxCommand(...) : IRequest<Result<T>>` + `XxxCommandHandler : IRequestHandler<...>` (cùng file hoặc file riêng), MediatR tự đăng ký qua `AddApplication()`.
   - Handler trả về `Result` / `Result<T>` cho business error — **không throw exception** (`Common/Result.cs`). Dùng `Result.Failure(msg, CommonErrorCodes.NotFound/Conflict/Forbidden/...)` khi controller cần map status ≠ 400.
   - Validation input: FluentValidation `AbstractValidator<XxxCommand>` — `ValidationBehaviour` tự chạy và trả `Result.Failure` (KHÔNG throw).
   - Truy cập DB qua `IUnitOfWork.Repository<T>()`; gọi `SaveChangesAsync(ct)`. Projection cột lớn: `repo.QueryAsync(q => q.Where(...).Select(...))` (tránh kéo `ParsedText`, `CvText`). Đếm dùng `CountAsync`.
   - Logic dùng bởi ≥2 endpoints hoặc SignalR hub → shared service sau interface ở `Services/` + `Interfaces/I*.cs` (vd `IInterviewService` — SessionHub gọi TRỰC TIẾP, không qua MediatR). Logic 1 endpoint → nằm trong handler.
   - AI/LLM: chỉ qua `IAIProvider` / `IEmbeddingProvider` — KHÔNG gọi OpenAI SDK trực tiếp (rule #8). CV-JD analysis dùng `IGeminiProvider` (ADR-030). File qua `IFileStorageService` (ADR-036). JWT/BCrypt qua `ITokenService`/`IPasswordHasher`.

4. **Controller thin** (`ARI.API/Controllers/<Name>Controller.cs`)
   - `[ApiController]`, `[Route("api/<resource>")]`, `[Authorize(Policy = "...")]` (vd `HrManagement`).
   - Chỉ inject `ISender` (+ `ICurrentUserService` nếu cần userId). Trích claims/IFormFile → `_sender.Send(command, ct)` → map `Result` ra HTTP (`result.ErrorCode` switch → 404/403/409..., mặc định `BadRequest(new { message = result.Error })`).
   - KHÔNG viết business logic / query DB trong controller. DI service mới (nếu có) vào `DependencyInjection.cs` của đúng project — không đụng `Program.cs`.

5. **Frontend types + service** (`frontend/src`)
   - Type/interface ở `types/`, PascalCase. KHÔNG dùng `any` (hook arch-guard cảnh báo).
   - Service ở `services/<domain>/<name>Service.ts`, export object với method async gọi `apiClient`. Re-export qua `index.ts`. Dùng path alias `@config`, `@store`, `@/types`.
   - `apiClient` tự gắn Bearer token + refresh 401 — đừng tự xử lý token.

6. **Frontend page/store** (`frontend/src`)
   - Page ở `pages/<role>/` (auth | candidate | hr | recruiter | super-admin | interview | kiosk | job-board). Component PascalCase.
   - Gọi service qua custom hook (`hooks/`) khi logic tái dùng; state global qua Zustand (`store/`).
   - Thêm route ở `routes/`, bọc `ProtectedRoute` đúng role.

7. **[BẮT BUỘC] Cập nhật `.ai/tasks.md`** (rule #19)
   - Đánh dấu `[x]` + ngày `YYYY-MM-DD`; nếu task chưa có → thêm vào Backlog rồi tick.
   - Thêm entry vào `## Completed`.
   - Commit `tasks.md` CÙNG commit code — không tách. Không kết thúc task nếu chưa cập nhật.

## Hằng số nghiệp vụ hay quên
- **Interview Code**: 6 ký tự alphanumeric, one-time-use, TTL 2h, bind `application_id` + vòng; **chỉ dùng cho phỏng vấn thật/Kiosk** (đã bỏ `code_type` — practice không dùng code) (ADR-016).
- **Magic Link**: TTL 15 phút, one-time-use.
- **Practice**: mở **1 lượt / VÒNG** sau khi pass CV + đặt lịch buổi thật của vòng (qua Portal `/practice/:applicationId`, không cần code); RAG chỉ JD+CV, không quay video. **Real**: on-site Kiosk + Interview Code, full RAG (JD+CV+Playbook).
- **Roles**: Super Admin | HR Leader | Recruiter | Candidate. Staff không self-register (pre-provisioning). Candidate đăng ký tự do.
- **Language**: AI detect từ JD — không hardcode mapping ngôn ngữ (rule #13).

## Git
- Branch: `feature/<scope>/<tên>` | `fix/<scope>/<mô-tả>` (scope: `be|fe|docker|infra|db|ai`).
- Commit: `<type>(<scope>): <mô tả>` (type: `feat|fix|refactor|docs|test|chore|setup`).
- Chỉ commit/push khi user yêu cầu. Đang ở `develop`/`main` → tạo branch trước.

## Hooks sẽ chạy (đừng để bị chặn/cảnh báo)
- `secret-guard` (PreToolUse Write/Edit): chặn secret hardcode → dùng env vars / appsettings.
- `arch-guard` (PostToolUse): cảnh báo Supabase SDK, `organization_id`, fetch trực tiếp trong component, `any`, gọi OpenAI SDK trực tiếp.
- `format` (Prettier, async) + `tasks-reminder` (Stop).

## Template code theo tầng
Xem `reference.md` (cùng thư mục skill) để copy template thật cho Entity / Service / Controller / FE service / page.

## Tài liệu nguồn (đọc khi cần chi tiết)
- `CLAUDE.md` — quy tắc tổng hợp + bảng ADR.
- `.ai/architecture.md` — chi tiết từng ADR. `.ai/tasks.md` — trạng thái task. `.ai/coding-rules.md`, `.ai/glossary.md`.
