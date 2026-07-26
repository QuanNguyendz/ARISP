# Backend — quy ước (ARI.* / Clean Architecture .NET 8)

> Tự nạp khi làm trong `ari-service/`. Quy tắc toàn cục + ADR xem [../CLAUDE.md](../CLAUDE.md). Layering Clean Architecture (`src/ARI.{API,Application,Domain,Infrastructure}` + `tests/`) theo ADR-045.

**Naming:**
- Namespace: `ARI.<Layer>.<Module>` (ví dụ: `ARI.Application.Interview`) — PascalCase
- Class: PascalCase | Interface: prefix `I` | Method: PascalCase + suffix `Async` cho async
- Private field: `_camelCase` | Constant: `UPPER_SNAKE_CASE`

**Patterns bắt buộc:** Repository Pattern, CQRS (MediatR nếu phức tạp), Result Pattern (không throw exception cho business errors), Dependency Injection, Async/Await cho mọi I/O.

**Security:** Không hardcode secrets – luôn dùng `appsettings.json` + env vars. JWT bắt buộc mọi protected endpoint. CORS chặt – chỉ allow frontend domain.
