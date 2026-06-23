# ARISP — Template code theo tầng

Copy-paste rồi đổi tên. Tất cả khớp với code hiện có trong repo (đã verify). Thay `Widget` bằng tên thật.

## 1. Domain entity — `ARISP.Domain/Entities/Widget.cs`

```csharp
using System;

namespace ARISP.Domain.Entities
{
    public class Widget : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "draft"; // draft | active | closed
        public Guid OwnerUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; } // soft delete — KHÔNG hard delete
    }
}
```

## 2. EF mapping + migration (`ARISP.Infrastructure/Data/`)

Đăng ký `DbSet<Widget>` và map table/cột `snake_case` trong cấu hình của `ARISPDbContext` (xem các entity khác trong `Data/`). Rồi:

```bash
dotnet ef migrations add AddWidget -p ARISP.Infrastructure -s ARISP.API
# Lưu ý: build migration FAIL nếu ARISP.API.exe đang chạy (file lock) — stop nó trước.
```

## 3. Interface — `ARISP.Application/Interfaces/IWidgetService.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs; // WidgetDto

namespace ARISP.Application.Interfaces
{
    public interface IWidgetService
    {
        Task<Result<WidgetDto>> CreateAsync(CreateWidgetDto dto, Guid userId, CancellationToken ct = default);
        Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default);
    }
}
```

## 4. Service — `ARISP.Application/Services/WidgetService.cs`

```csharp
public class WidgetService : IWidgetService
{
    private readonly IUnitOfWork _unitOfWork;

    public WidgetService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result<WidgetDto>> CreateAsync(CreateWidgetDto dto, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure<WidgetDto>("Tên không được để trống."); // business error → Result, KHÔNG throw

        var widget = new Widget { Name = dto.Name.Trim(), OwnerUserId = userId };
        await _unitOfWork.Repository<Widget>().AddAsync(widget, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new WidgetDto { Id = widget.Id, Name = widget.Name, Status = widget.Status });
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Widget>();
        var widget = await repo.GetByIdAsync(id, ct);
        if (widget is null) return Result.Failure("Không tìm thấy widget.");

        widget.Status = "closed";
        widget.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Update(widget);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

**Projection cột lớn / list** — dùng `QueryAsync` (AsNoTracking, shape ở SQL):

```csharp
var items = await _unitOfWork.Repository<Widget>().QueryAsync(q => q
    .Where(w => w.DeletedAt == null)
    .OrderByDescending(w => w.CreatedAt)
    .Select(w => new WidgetListItemDto { Id = w.Id, Name = w.Name, Status = w.Status }), ct);
```

## 5. Controller — `ARISP.API/Controllers/WidgetsController.cs`

```csharp
[ApiController]
[Route("api/widgets")]
[Authorize(Policy = "HrManagement")] // policy thật xem Program.cs
public class WidgetsController : ControllerBase
{
    private readonly IWidgetService _widgetService;
    private readonly ICurrentUserService _currentUser;

    public WidgetsController(IWidgetService widgetService, ICurrentUserService currentUser)
    {
        _widgetService = widgetService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWidgetDto dto, CancellationToken ct)
    {
        var result = await _widgetService.CreateAsync(dto, _currentUser.UserId, ct);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }
}
```

**DI** — đăng ký trong `Program.cs`: `builder.Services.AddScoped<IWidgetService, WidgetService>();`

## 6. Frontend types — `frontend/src/types/widget.ts`

```typescript
export interface Widget {
  id: string
  name: string
  status: 'draft' | 'active' | 'closed'
}

export interface CreateWidgetRequest {
  name: string
}
```

## 7. Frontend service — `frontend/src/services/widget/widgetService.ts`

```typescript
import { apiClient } from '../apiClient'
import type { Widget, CreateWidgetRequest } from '@/types/widget'

export const widgetService = {
  async list(): Promise<Widget[]> {
    const { data } = await apiClient.get<Widget[]>('/widgets')
    return data
  },
  async create(payload: CreateWidgetRequest): Promise<Widget> {
    const { data } = await apiClient.post<Widget>('/widgets', payload)
    return data
  },
}
```

`index.ts`: `export { widgetService } from './widgetService'`. apiClient tự gắn Bearer + refresh 401 — đừng tự xử lý token. KHÔNG dùng `any`.

## 8. Frontend page — `frontend/src/pages/hr/WidgetsPage.tsx`

```tsx
import { useEffect, useState } from 'react'
import { widgetService } from '@/services/widget/widgetService'
import type { Widget } from '@/types/widget'

export default function WidgetsPage() {
  const [widgets, setWidgets] = useState<Widget[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    widgetService.list().then(setWidgets).finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="p-6 text-ink-500">Đang tải…</div>

  return (
    <div className="p-6">
      <h1 className="text-2xl font-semibold text-ink-900">Widgets</h1>
      {/* ... */}
    </div>
  )
}
```

Route lazy-load trong `App.tsx`, bọc layout + `ProtectedRoute` đúng role. Thêm path vào `routes/index.ts`.
Theme tokens: `ink-*` (xám), `brand-*` (chàm/indigo), `ai-*` (tím). Shadow `card` / `card-hover`.
