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

- `ARISP.Domain` — Entities, không phụ thuộc tầng nào khác.
- `ARISP.Application` — Services, DTOs, Interfaces (`I*`), `Common/Result.cs`. Chỉ phụ thuộc Domain.
- `ARISP.Infrastructure` — EF Core (`Data/`), `Repositories/`, `Migrations/`, external providers (`AI/`, `Storage/`, `Services/`). Implement interfaces của Application.
- `ARISP.API` — Controllers, Hubs, Middleware, `Program.cs` (DI wiring).
- `frontend/src` — `pages/` (theo role) → `services/` → `apiClient` → API. Không bao giờ fetch trực tiếp trong component.

## Checklist một vertical slice (backend → frontend)

1. **Domain entity** (`ARISP.Domain/Entities/<Name>.cs`)
   - `Guid Id { get; set; } = Guid.NewGuid();`
   - Bắt buộc `CreatedAt`, `UpdatedAt` (kiểu `DateTimeOffset`, default `UtcNow`).
   - Soft delete: implement `ISoftDelete` → `DateTimeOffset? DeletedAt`. KHÔNG hard delete.
   - Comment inline cho field dạng enum-string (vd `// draft | active | closed`).
   - KHÔNG có `organization_id` (ADR-012, single-tenant).

2. **EF mapping + migration** (`ARISP.Infrastructure`)
   - Map trong `Data/` (DbContext config). Table `snake_case` số nhiều, column `snake_case`.
   - Mọi schema change qua migration — không sửa DB tay:
     `dotnet ef migrations add <Name> -p ARISP.Infrastructure -s ARISP.API`
   - User đã pre-approve việc thêm cột/migration khi task cần (xem memory).

3. **Interface + Service** (`ARISP.Application`)
   - Interface đặt ở `Interfaces/I<Name>.cs`, prefix `I`.
   - Service trả về `Result` / `Result<T>` cho business error — **không throw exception** cho lỗi nghiệp vụ (`Common/Result.cs`). Method async có suffix `Async` + nhận `CancellationToken ct`.
   - Truy cập DB qua `IUnitOfWork.Repository<T>()`; gọi `SaveChangesAsync(ct)`.
   - Projection cột lớn: dùng `repo.QueryAsync(q => q.Where(...).Select(...))` để tránh kéo cột text khổng lồ (vd `ParsedText`, `CvText`). Đếm dùng `CountAsync`.
   - AI/LLM: chỉ qua `IAIProvider` / `IEmbeddingProvider` — KHÔNG gọi OpenAI SDK trực tiếp (rule #8). CV-JD analysis dùng `IGeminiProvider` (Gemini 2.5 Flash, ADR-030). File qua `IFileStorageService` (ADR-036).

4. **Controller** (`ARISP.API/Controllers/<Name>Controller.cs`)
   - `[ApiController]`, `[Route("api/<resource>")]`, `[Authorize(Policy = "...")]` (vd `HrManagement`).
   - Inject qua constructor: `IUnitOfWork`, service, `ICurrentUserService`...
   - Action nhận `CancellationToken ct`. Map `Result.IsFailure` → `BadRequest(result.Error)`; success → `Ok(...)`.
   - Đăng ký service mới vào DI ở `Program.cs`.

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
