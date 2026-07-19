# ARISP Frontend - Developer Guide

> Nền tảng tuyển dụng thông minh tích hợp AI Interview Automation

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
| **HR Leader**   | `/hr/*`                  | Dashboard, jobs, candidates, evaluations |
| **Recruiter**   | `/recruiter/*`           | Create jobs, manage candidates           |
| **Candidate**   | `/candidate/*`           | Applications, portal                     |
| **Public**      | `/`, `/jobs/*`           | Job board, job detail                    |
| **Interview**   | `/interview/*`, `/kiosk` | AI interview room (always dark)          |

---

## Cấu Trúc Thư Mục

```
frontend/src/
│
├── main.tsx                              # Entry point
├── App.tsx                               # Router - định nghĩa routes + layouts
│
├── components/
│   │
│   ├── layout/                          # Layout wrappers
│   │   ├── SuperAdminLayout.tsx         # Super Admin
│   │   ├── HrLayout.tsx                 # HR - sidebar + topbar + dark toggle
│   │   ├── RecruiterLayout.tsx         # Recruiter
│   │   ├── CandidateLayout.tsx          # Candidate
│   │   ├── InterviewLayout.tsx          # Interview (always dark, fullscreen)
│   │   └── AuthLayout.tsx               # Auth pages
│   │
│   ├── shared/                          # Shared components
│   │   └── index.tsx                   # PageHeader, StatsCard, EmptyState, etc.
│   │
│   ├── ui/                              # Base UI
│   │   ├── Button.tsx
│   │   ├── GlassCard.tsx
│   │   └── Container.tsx
│   │
│   ├── common/                          # Common
│   │   ├── LoadingButton.tsx
│   │   ├── LoadingSpinner.tsx
│   │   └── ErrorAlert.tsx
│   │
│   ├── auth/
│   │   └── ProtectedRoute.tsx          # Route protection
│   │
│   └── sections/                        # Landing page sections
│       ├── Hero.tsx
│       ├── Demo.tsx
│       └── ...
│
├── pages/
│   │
│   ├── super-admin/                     # Super Admin pages
│   │   ├── DashboardPage.tsx
│   │   ├── UsersPage.tsx
│   │   └── ...
│   │
│   ├── hr/                              # HR Leader pages
│   │   ├── DashboardPage.tsx           # KPIs, funnel, recent candidates
│   │   ├── EvaluationReviewPage.tsx    # Review & confirm AI verdicts
│   │   └── ...
│   │
│   ├── recruiter/                       # Recruiter pages
│   │   ├── DashboardPage.tsx
│   │   ├── MyJobsPage.tsx
│   │   ├── CandidatesPage.tsx
│   │   └── ...
│   │
│   ├── admin/                           # Legacy/admin pages (đang migrate)
│   │   ├── DashboardPage.tsx
│   │   ├── JobPostingsPage.tsx
│   │   └── ...
│   │
│   ├── candidate/                       # Candidate pages
│   │   ├── DashboardPage.tsx
│   │   ├── MyApplicationsPage.tsx
│   │   └── ...
│   │
│   ├── interview/                       # Interview room
│   │   ├── InterviewRoomPage.tsx        # Real interview
│   │   └── PracticeSessionPage.tsx     # Practice
│   │
│   ├── auth/                            # Auth pages
│   │   ├── LoginPage.tsx
│   │   ├── RegisterPage.tsx
│   │   ├── CandidateLoginPage.tsx
│   │   └── ...
│   │
│   ├── landing/                         # Public landing
│   │   ├── HomePage.tsx                # Employer landing
│   │   └── FindJobPage.tsx             # Job board (/)
│   │
│   └── job-board/                       # Public job pages
│       └── JobDetailPage.tsx
│
├── services/                             # API layer
│   ├── apiClient.ts                    # Axios instance + interceptors
│   ├── auth/authService.ts
│   ├── job/jobService.ts
│   ├── application/applicationService.ts
│   ├── interview/interviewService.ts
│   ├── evaluation/evaluationService.ts
│   └── schedule/scheduleService.ts
│
├── store/                               # Zustand stores
│   ├── auth/authStore.ts              # user, tokens, login/logout
│   └── theme/themeStore.ts            # isDark, toggleTheme
│
├── hooks/                               # Custom hooks
│   ├── interview/useInterviewRoom.ts
│   └── cheat-detection/useCheatDetection.ts
│
├── types/                               # TypeScript types
│   ├── auth/auth.types.ts
│   ├── job/job.types.ts
│   ├── application/application.types.ts
│   ├── interview/interview.types.ts
│   └── evaluation/evaluation.types.ts
│
├── utils/                               # Utilities
│   ├── devAuth.ts                     # Dev mode helpers
│   └── format/format.ts               # Date formatting
│
└── config/
    └── constants.ts                    # Constants, API_URL
```

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
// ✅ Dùng alias @
import { useAuthStore } from '@store/auth/authStore'
import { PageHeader } from '@components/shared'

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
| `CandidateLayout`  | `/candidate/*`           | ❌               |
| `InterviewLayout`  | `/interview/*`, `/kiosk` | ❌ (always dark) |

### Shared Components

```tsx
// Import từ @components/shared
import {
  PageHeader, // Tiêu đề + description
  StatsGrid, // Grid cho stats
  StatsCard, // Stat card
  EmptyState, // Empty state
  LoadingSpinner, // Loading
  ErrorAlert, // Error alert
} from '@components/shared'
```

---

## Hướng Dẫn

### Tạo Page Mới

```tsx
// src/pages/hr/NewPage.tsx
import { Link } from 'react-router-dom'
import { PageHeader } from '@components/shared'

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
# .env trong frontend/
VITE_API_URL=http://localhost:8080/api
VITE_WS_URL=ws://localhost:8080/ws
VITE_DEV_AUTH=false    # true = auto login dev user
```

---

## Scripts

```bash
npm run dev      # Dev server (port 5173)
npm run build    # Production build
npm run preview  # Preview build
```

---

## Checklist Khi Code

- [ ] Có dark mode classes (`dark:` prefix)
- [ ] Dùng alias `@` cho imports
- [ ] Props có type rõ ràng (không `any`)
- [ ] List có unique `key` prop
- [ ] API call qua service layer
- [ ] Error handling khi gọi API
