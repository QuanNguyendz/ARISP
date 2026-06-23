---
name: arisp-screen
description: Thiết kế/redesign một màn hình React trong ARISP theo đúng design system đã thiết lập (light ink/brand/ai theme, shared components). Dùng khi tạo màn mới cần đồng bộ style với các màn đã có, hoặc redesign màn cũ (dark-glass) về style chuẩn hiện tại. Mockup gốc (design/mockups) đã bị xóa — nguồn style thật giờ là các page đã redesign + components/shared.
---

# ARISP Screen Design System

Mockup `design/mockups/*.html` đã bị xóa. **Nguồn chân lý của style giờ là code đã redesign** — bám theo `components/shared/index.tsx` và các page HR đã làm (vd `pages/hr/JobsPage.tsx`) để màn mới/redesign trông y hệt phần còn lại.

> Backend/wiring xuyên tầng: xem skill `arisp-feature`. Skill này chỉ lo phần **giao diện + style**.

## Quy trình một màn

1. Tìm 1 page đã redesign gần giống nhất (ưu tiên `pages/hr/*`) làm khuôn mẫu.
2. Tái dùng **shared components** thay vì tự dựng markup (xem bên dưới).
3. Dùng đúng **token + idiom** (ink/brand/ai, bo góc, shadow, gradient, motion).
4. Hỗ trợ **dark mode** ở mọi class (`dark:` variant) — toàn bộ design system đã song ngữ sáng/tối.
5. Data qua `services/` (không fetch trong component); skeleton khi loading; `EmptyState`/`ErrorAlert` cho rỗng/lỗi.
6. Màn cũ (dark-glass, vd trong `CandidateLayout`/`RecruiterLayout`/`SuperAdminLayout`): redesign về theme sáng; nếu shell còn tối → migrate cả layout/cụm page cùng lúc để tránh "vỡ" trong shell tối và double-navbar.

## Shared components (luôn ưu tiên) — `@components/shared`

| Component | Dùng cho |
|---|---|
| `PageHeader` | Tiêu đề trang + description + `actions[]` (primary/secondary) + `badge` |
| `StatsGrid` / `StatsCard` | Hàng thẻ số liệu (grid 2/lg:4), mỗi card có `label`, `value`, `change?`, `color` |
| `EmptyState` | Trạng thái rỗng: `icon`, `title`, `description`, `action?` |
| `ErrorAlert` / `NoticeAlert` | Băng lỗi (đỏ) / lưu ý (hổ phách), có `onDismiss?` |
| `LoadingSpinner` | Spinner + message khi tải toàn trang |

UI khác: `@components/ui` (`Button`, `Container`, `GlassCard`, `SearchableSelect`, `Skeleton`). Skeleton riêng theo trang đặt ở `pages/<role>/_skeletons.tsx`.

## Token & idiom chuẩn (copy nguyên class)

- **Nền trang**: `p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen`
- **Card**: `rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all`
- **Nút/nhấn primary (gradient)**: `bg-gradient-to-r from-brand-600 to-ai-600 text-white` — bo `rounded-xl`, hover `opacity-90`.
- **Nút secondary**: `border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10`
- **Avatar/icon ô vuông gradient**: `w-12 h-12 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 ... text-white`
- **Chữ**: tiêu đề `text-ink-900 dark:text-white`; phụ `text-ink-500/600 dark:text-ink-400`.
- **Badge trạng thái** (pill `rounded-full text-xs font-medium`), bảng màu đã dùng:
  - success/active: `bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400`
  - warning/draft: `bg-amber-100 ... text-amber-700 dark:text-amber-400`
  - info: `bg-blue-100 ... text-blue-700 dark:text-blue-400`
  - neutral/closed: `bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400`
  - urgent: `bg-red-100 dark:bg-red-500/20 text-red-600 dark:text-red-400`
- **Filter tab**: pill `rounded-xl`; active = gradient brand→ai trắng chữ; inactive = secondary style + count `text-ink-400`.
- **Motion**: list item dùng `framer-motion` `initial={{opacity:0,y:20}} animate={{opacity:1,y:0}}` với `transition={{ delay: Math.min(i*0.04, 0.3) }}` (stagger nhẹ, cap delay).
- Bo góc: card `rounded-2xl`, nút/tab/input `rounded-xl`, badge `rounded-full`. Shadow chỉ dùng `shadow-card` / `shadow-card-hover`.

## Khung một page (rút từ JobsPage)

```tsx
return (
  <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
    <PageHeader title="…" description="…" actions={[{ label: 'Tạo', href: '…' }]} />

    {loading && <HrStatsSkeleton />}
    {!loading && !error && <StatsGrid stats={stats} />}

    {loading && <ListSkeleton rows={5} />}
    {!loading && error && <ErrorAlert message={error} />}
    {!loading && !error && items.length === 0 && (
      <EmptyState icon={<Briefcase className="w-8 h-8 text-ink-400" />} title="…" description="…" />
    )}
    {!loading && !error && items.length > 0 && (
      <div className="space-y-4">{/* motion.div cards */}</div>
    )}
  </div>
)
```

Pattern fetch: `useEffect` với cờ `active` để tránh set state sau unmount; `try/catch/finally` set `loading/error`.

## Quy ước bắt buộc (không vi phạm — hook arch-guard cảnh báo)
- KHÔNG fetch API trong component → qua `services/`.
- KHÔNG dùng `any` trong TypeScript.
- Component PascalCase ở `pages/<role>/` (auth|candidate|hr|recruiter|super-admin|interview|kiosk|job-board) hoặc reusable ở `components/`.
- Route: lazy-load trong `App.tsx`, bọc layout + `ProtectedRoute` đúng role; thêm path vào `routes/index.ts`.

## GOTCHAs (verify lại — có thể đã đổi)
- `lucide-react` là **v1.x**: KHÔNG có `Linkedin`/`Github`/`Chrome` → dùng `Link2`/`Link`/`Globe`.
- Candidate landing sau login = `/` (job board), không phải sub-route candidate — kiểm tra cả `GuestRoute` + `ProtectedRoute`.
- Chỉ `HrLayout` (+`CandidateAppLayout` mới) đã ở theme sáng; layout candidate/recruiter/super-admin cũ vẫn dark-glass.

## Sau khi xong
Cập nhật `.ai/tasks.md` (rule #19) + commit cùng code. Nếu thêm cột/bảng cho tính năng mới của màn → theo `arisp-feature` (user đã pre-authorize thêm cột DB).
