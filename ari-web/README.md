# ARISP Web - Developer Guide

> Nền tảng tuyển dụng thông minh tích hợp AI Interview Automation
>
> **Monorepo npm workspaces (ADR-046)** — 3 package dưới `ari-web/src/`:
> - **`ARI.CandidateSite`** (`@ari/candidate-site`, port **3000**) — site ứng viên (public, deploy): job board, portal, practice/real interview, kiosk.
> - **`ARI.StaffSite`** (`@ari/staff-site`, port **3001**) — site nội bộ: HR / Recruiter / Super Admin.
> - **`ARI.Shared`** (`@ari/shared`) — dùng chung, import source-level qua `@ari/shared/*` (không build step).
>
> Dev 2 site: `npm install` (ở `ari-web/`) rồi `npm run dev:candidate` và/hoặc `npm run dev:staff` (2 terminal).

---

## Mục Lục

- [Tổng Quan](#tổng-quan)
- [Cấu Trúc Thư Mục](#cấu-trúc-thư-mục)
- [Design System](#design-system)
- [Coding Rules](#coding-rules)
- [State Management](#state-management)
- [API Layer](#api-layer)
- [Routing](#routing)
- [Components](#components)
- [Hướng Dẫn](#hướng-dẫn)

---

## Tổng Quan

### Tech Stack

| Tech          | Version | Purpose                       |
| ------------- | ------- | ----------------------------- |
| React         | 18.x    | UI Library                    |
| TypeScript    | 5.x     | Type safety                   |
| Vite          | 5.x     | Build tool                    |
| Tailwind CSS  | 3.x     | Styling (`darkMode: 'class'`) |
| React Router  | 6.x     | Routing                       |
| Zustand       | 4.x     | State management              |
| Lucide React  | latest  | Icons                         |
| Axios         | 1.x     | HTTP client                   |
| Framer Motion | 11.x    | Animations (optional)         |

### User Roles

| Role            | Path                     | Mô tả                                    |
| --------------- | ------------------------ | ---------------------------------------- |
| **Super Admin** | `/super-admin/*`         | System config, HR account management     |
| **HR Leader**   | `/hr/*`                  | Dashboard, jobs, candidates, evaluations, offers |
| **Hiring Manager** | `/hm/*`               | Duyệt shortlist, ký duyệt JD, chốt kết quả phỏng vấn, duyệt thư mời (ADR-061) |
| **Recruiter**   | `/recruiter/*`           | Create jobs, manage candidates, offers   |
| **Candidate**   | `/candidate/*`           | Applications, portal                     |
| **Public**      | `/`, `/jobs/*`           | Job board, job detail                    |
| **Interview**   | `/interview/*`, `/kiosk` | AI interview room (always dark)          |

---

## Cấu Trúc Thư Mục

```
ari-web/                                  # workspaces root (1 package-lock.json)
├── package.json                          # scripts dev:candidate / dev:staff / build
├── tsconfig.base.json                    # strict options dùng chung
└── src/
    │
    ├── ARI.Shared/                       # @ari/shared — dùng chung (import @ari/shared/*)
    │   ├── tailwind-preset.cjs           # theme ink/brand/ai dùng chung
    │   └── src/
    │       ├── api/apiClient.ts          # Axios instance + interceptors + configureApiClient()
    │       ├── fservices/                # auth, job, application, interview, notification, schedule, profile
    │       ├── ui/                        # designSystem (PageHeader…) + kit (Button/GlassCard…) + common
    │       ├── guards/                    # ProtectedRoute, GuestRoute
    │       ├── document/  media/  realtime/   # DocumentViewer; DeviceCheck+interview hooks; useAppNotifications
    │       ├── store/                     # auth, theme, interview (Zustand)
    │       ├── authflows/                 # OAuthCallback, Forgot/Reset password (2 site cùng route)
    │       ├── types/  config/  utils/  styles/
    │       └── i18n/                      # core (initI18n) + sharedResources + locales chung
    │
    ├── ARI.CandidateSite/                # @ari/candidate-site (port 3000, public)
    │   ├── index.html  vite.config.ts  tailwind.config.js  tsconfig.json
    │   └── src/
    │       ├── main.tsx
    │       ├── app/                       # App.tsx (router) + layouts/ (Candidate*/Interview/Public)
    │       ├── pages/                     # auth, candidate(portal), interview, job-board, kiosk, landing, legal
    │       ├── components/                # sections, three, legal, profile
    │       ├── fservices/                 # job/savedJobService, location/provinceService, settings/settingsService
    │       └── i18n/                      # namespace riêng candidate
    │
    └── ARI.StaffSite/                    # @ari/staff-site (port 3001, nội bộ)
        ├── index.html  vite.config.ts  tailwind.config.js  tsconfig.json
        └── src/
            ├── main.tsx
            ├── app/                       # App.tsx + StaffHomeRedirect + layouts/ (Workspace/Hr/Recruiter/SuperAdmin + useWorkspaceNav)
            ├── pages/                     # auth, hr, recruiter, super-admin
            ├── components/                # StaffNotificationsView
            ├── fservices/                 # dashboard, evaluation, admin, playbook, accountRequest
            ├── utils/adminLabels.ts
            └── i18n/                      # namespace riêng staff
```

> **Quy tắc:** `services/` → **`fservices/`** (prefix "f"). Mỗi folder một nhiệm vụ: `app/` (routing+layouts),
> `pages/` (màn theo domain), `fservices/` (gọi API), `components/` (UI tái dùng). Hướng phụ thuộc 1 chiều: site → Shared.

---

## Design System

### Color Palette

```javascript
// tailwind.config.js
colors: {
  brand: { 50, 100, 400, 500, 600, 700 },  // Primary (indigo)
  ai: { 400, 500, 600 },                   // AI accent (purple)
  ink: { 50, 100, 200, 400, 500, 600, 700, 800, 900, 950 }, // Text/bg scale
}
```

### Dark Mode Pattern

**QUY TẮC: Mọi component phải support cả light và dark mode**

```tsx
// ✅ ĐÚNG
<div className="bg-white dark:bg-white/5">
  <h1 className="text-ink-900 dark:text-white">
  <button className="border-ink-200 dark:border-white/10">
```

```tsx
// ❌ SAI - thiếu dark mode
<div className="bg-white">
<h1 className="text-ink-900">
```

### Card Pattern

```tsx
<div className="rounded-2xl border border-ink-200 dark:border-white/10
                bg-white dark:bg-white/5
                p-6 shadow-card hover:shadow-card-hover transition-all">
```

### Status Badge

```tsx
<span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
  status === 'pass'
    ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
    : status === 'pending'
      ? 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
      : 'bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400'
}`}>
```

### Text Colors

| Element      | Light            | Dark                  |
| ------------ | ---------------- | --------------------- |
| Container bg | `bg-ink-50`      | `dark:bg-ink-950`     |
| Card bg      | `bg-white`       | `dark:bg-white/5`     |
| Heading      | `text-ink-900`   | `dark:text-white`     |
| Body         | `text-ink-600`   | `dark:text-ink-400`   |
| Muted        | `text-ink-500`   | `dark:text-ink-400`   |
| Icon         | `text-brand-600` | `dark:text-brand-400` |

---

## Coding Rules

### 1. Dark Mode - BẮT BUỘC

Mọi component PHẢI có cả light và dark classes.

```tsx
// Page container
<div className="bg-ink-50 dark:bg-ink-950 min-h-screen">

// Text
<h1 className="text-ink-900 dark:text-white">
<p className="text-ink-600 dark:text-ink-400">

// Icon
<Icon className="w-5 h-5 text-brand-600 dark:text-brand-400" />
```

### 2. Import Patterns

```tsx
// ✅ Code dùng chung → @ari/shared/*
import { useAuthStore } from '@ari/shared/store/auth'
import { PageHeader } from '@ari/shared/ui'
import { apiClient } from '@ari/shared/api/apiClient'

// ✅ Code trong site → @/ (alias tới src của site đó)
import CandidateLayout from '@/app/layouts/CandidateLayout'
import { savedJobService } from '@/fservices/job/savedJobService'

// ❌ Relative path dài
import { useAuthStore } from '../../../store/auth/authStore'
```

### 3. Component Naming

| Type    | Pattern             | Ví dụ                 |
| ------- | ------------------- | --------------------- |
| Page    | PascalCase          | `DashboardPage.tsx`   |
| Layout  | PascalCase + Layout | `HrLayout.tsx`        |
| Hook    | `use` + PascalCase  | `useInterviewRoom.ts` |
| Service | camelCase           | `authService.ts`      |
| Store   | camelCase + Store   | `authStore.ts`        |

### 4. Store Pattern (Zustand)

```tsx
// store/example/exampleStore.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface ExampleState {
  items: Item[]
  loading: boolean
  fetchItems: () => Promise<void>
}

export const useExampleStore = create<ExampleState>()(
  persist(
    (set, get) => ({
      items: [],
      loading: false,

      fetchItems: async () => {
        set({ loading: true })
        try {
          const items = await exampleService.getItems()
          set({ items })
        } finally {
          set({ loading: false })
        }
      },
    }),
    { name: 'example-storage' }
  )
)
```

### 5. Service Pattern

```tsx
// services/example/exampleService.ts
import apiClient from '../apiClient'

export const exampleService = {
  async getItems(): Promise<Item[]> {
    const { data } = await apiClient.get<Item[]>('/items')
    return data
  },

  async createItem(payload: CreateRequest): Promise<Item> {
    const { data } = await apiClient.post<Item>('/items', payload)
    return data
  },

  async updateItem(id: string, payload: UpdateRequest): Promise<Item> {
    const { data } = await apiClient.put<Item>(`/items/${id}`, payload)
    return data
  },

  async deleteItem(id: string): Promise<void> {
    await apiClient.delete(`/items/${id}`)
  },
}
```

### 6. Error Handling

```tsx
// ✅ Pattern có error state
const [error, setError] = useState<string | null>(null)

const fetchData = async () => {
  try {
    setLoading(true)
    const data = await service.getData()
    setData(data)
  } catch (err) {
    setError(err instanceof Error ? err.message : 'Lỗi không xác định')
  } finally {
    setLoading(false)
  }
}
```

### 7. List Rendering - KEY PROP

```tsx
// ✅ Unique key từ id
{
  items.map((item) => <Card key={item.id} item={item} />)
}

// ❌ Index làm key
{
  items.map((item, index) => <Card key={index} item={item} />)
}
```

### 8. Props - Định nghĩa rõ ràng

```tsx
// ✅ Props interface
interface CardProps {
  item: Item
  onEdit?: (id: string) => void
  className?: string
}

export function Card({ item, onEdit, className }: CardProps) {
  return <div className={className}>{/* content */}</div>
}
```

### 9. Responsive

```tsx
// Mobile-first breakpoints
<div className="p-4 md:p-6 lg:p-8">

// Responsive grid
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
```

### 10. Forbidden Patterns

```tsx
// ❌ any - dùng type rõ ràng
const data: any  // ❌
const data: Item[]  // ✅

// ❌ fetch trong component
fetch('/api/items').then(...)  // ❌
exampleService.getItems()  // ✅

// ❌ hardcode color
className="bg-gray-800"  // ❌
className="bg-ink-900"  // ✅
```

---

## State Management

### Auth Store

```tsx
import { useAuthStore } from '@store/auth/authStore'

const {
  user, // User | null
  isAuthenticated, // boolean
  setAuth, // (user, tokens) => void
  clearAuth, // () => void
  login, // (user, tokens) => void
  logout, // () => void
} = useAuthStore()
```

### Theme Store

```tsx
import { useThemeStore } from '@store/theme'

const { isDark, toggleTheme } = useThemeStore()

// HrLayout đã sync theme với DOM
// Các component chỉ cần đọc isDark cho conditional styles
```

---

## API Layer

### API Client (`services/apiClient.ts`)

```tsx
// Đã config sẵn:
// - Base URL: VITE_API_URL
// - Request interceptor: gắn JWT token + X-User-Id
// - Response interceptor: xử lý 401 → auto refresh token
```

### Services

| Service              | Endpoints                                       |
| -------------------- | ----------------------------------------------- |
| `authService`        | POST /auth/login, /auth/register, /auth/refresh |
| `jobService`         | GET/POST/PUT/DELETE /job-postings               |
| `applicationService` | GET/POST /applications                          |
| `interviewService`   | POST /interviews/sessions, GET /interviews/:id  |
| `evaluationService`  | GET /evaluations, PUT /evaluations/:id/confirm  |

---

## Routing

### Structure

```
/                            → FindJobPage (job board)
/jobs/:id                    → JobDetailPage

/auth/*                      → Auth pages (login, register)
/auth/candidate/*            → Candidate auth

/super-admin/*              → SuperAdminLayout
/hr/*                        → HrLayout (dark toggle)
/recruiter/*                 → RecruiterLayout
/hm/*                        → HmLayout   (Hiring Manager — ADR-061)

/candidate/*                 → CandidateLayout
/portal/*                   → Magic link auth

/interview/room/:sessionId  → InterviewLayout (always dark)
/kiosk                       → KioskPage
```

### Protected Route

```tsx
<ProtectedRoute allowedRoles={['hr_leader', 'recruiter']}>
  <HrLayout>
    <Outlet />
  </HrLayout>
</ProtectedRoute>
```

---

## Components

### Layouts

| Layout             | Wraps                    | Dark Toggle      |
| ------------------ | ------------------------ | ---------------- |
| `SuperAdminLayout` | `/super-admin/*`         | ❌               |
| `HrLayout`         | `/hr/*`                  | ✅               |
| `RecruiterLayout`  | `/recruiter/*`           | ❌               |
| `HmLayout`         | `/hm/*`                  | ✅               |
| `CandidateLayout`  | `/candidate/*`           | ❌               |
| `InterviewLayout`  | `/interview/*`, `/kiosk` | ❌ (always dark) |

### Shared Components

```tsx
// Import từ @ari/shared/ui
import {
  PageHeader, // Tiêu đề + description
  StatsGrid, // Grid cho stats
  StatsCard, // Stat card
  EmptyState, // Empty state
  LoadingSpinner, // Loading
  ErrorAlert, // Error alert
} from '@ari/shared/ui'
```

---

## Hướng Dẫn

### Tạo Page Mới

```tsx
// src/pages/hr/NewPage.tsx
import { Link } from 'react-router-dom'
import { PageHeader } from '@ari/shared/ui'

export default function NewPage() {
  return (
    // Container - LUÔN có dark mode
    <main className="p-6 bg-ink-50 dark:bg-ink-950">
      {/* Page header (tùy chọn) */}
      <PageHeader title="Tiêu đề" description="Mô tả" />

      {/* Content */}
      <div className="space-y-4">
        {items.map((item) => (
          <Card key={item.id} item={item} />
        ))}
      </div>
    </main>
  )
}
```

### Thêm Route

```tsx
// App.tsx
import NewPage from '@pages/hr/NewPage'

<Route path="/hr/new" element={<NewPage />} />
// Hoặc với layout:
<Route element={<HrLayout><Outlet /></HrLayout>}>
  <Route path="/hr/new" element={<NewPage />} />
</Route>
```

### Tạo Store

```tsx
// src/store/example/exampleStore.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface ExampleState {
  items: Item[]
  loading: boolean
  fetchItems: () => Promise<void>
}

export const useExampleStore = create<ExampleState>()(
  persist(
    (set) => ({
      items: [],
      loading: false,
      fetchItems: async () => {
        set({ loading: true })
        const items = await exampleService.getItems()
        set({ items, loading: false })
      },
    }),
    { name: 'example-storage' }
  )
)
```

### Thêm Service

```tsx
// src/services/example/exampleService.ts
import apiClient from '../apiClient'

export const exampleService = {
  getItems: () => apiClient.get('/items'),
  getItemById: (id: string) => apiClient.get(`/items/${id}`),
  createItem: (data: CreateRequest) => apiClient.post('/items', data),
  updateItem: (id: string, data: UpdateRequest) => apiClient.put(`/items/${id}`, data),
  deleteItem: (id: string) => apiClient.delete(`/items/${id}`),
}
```

---

## Environment Variables

```env
# .env đặt TRONG mỗi package: ari-web/src/ARI.CandidateSite/.env và ari-web/src/ARI.StaffSite/.env
VITE_API_BASE_URL=http://localhost:5000/api
VITE_WS_BASE_URL=ws://localhost:5000
VITE_ENABLE_CHEAT_DETECTION=false
VITE_ENABLE_AVATAR=true
VITE_ENABLE_RECORDING=false
```

---

## Scripts (chạy ở `ari-web/`)

```bash
npm install              # cài toàn bộ workspace (1 lần)
npm run dev:candidate    # Candidate site (port 3000)
npm run dev:staff        # Staff site (port 3001)
npm run build            # Build cả 2 site
npm run build:candidate  # Chỉ candidate   |  npm run build:staff  # Chỉ staff
npm run lint             # ESLint toàn monorepo
```

> Qua Docker/Nginx: Candidate = `http://localhost`, Staff = `http://staff.localhost`.

---

## Checklist Khi Code

- [ ] Có dark mode classes (`dark:` prefix)
- [ ] Dùng alias `@` cho imports
- [ ] Props có type rõ ràng (không `any`)
- [ ] List có unique `key` prop
- [ ] API call qua service layer
- [ ] Error handling khi gọi API
