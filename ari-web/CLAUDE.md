# Frontend — quy ước (React + TypeScript, npm workspaces)

> Tự nạp khi làm trong `ari-web/`. Quy tắc toàn cục + ADR xem [../CLAUDE.md](../CLAUDE.md). Cấu trúc 3 package (`ARI.Shared` / `ARI.CandidateSite` :3000 / `ARI.StaffSite` :3001) theo ADR-046.

**Naming:** Component: PascalCase | Hook: prefix `use` | Util: camelCase | Type/Interface: PascalCase

- **`services/` → `fservices/`** (quy tắc "f" prefix). `fservices` mirror tên feature slice backend (tầng API).
- **Mỗi folder một nhiệm vụ:** `app/` = routing+layouts, `pages/` = màn theo domain, `fservices/` = gọi API, `components/` = UI tái dùng.
- Code dùng chung ở `ARI.Shared`; hướng phụ thuộc 1 chiều: site → Shared (Shared không import site).
- Dev: `npm run dev:candidate` (3000) / `npm run dev:staff` (3001).

**Patterns:** Không fetch API trong component – qua `fservices/`. Dùng custom hook cho logic tái sử dụng. Không dùng `any`.
