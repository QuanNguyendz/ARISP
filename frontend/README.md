# ARISP Frontend - Tài Liệu Dự Án

> **ARISP** - AI-Powered Recruitment and Interview Support Platform
> Nền tảng hỗ trợ tuyển dụng và phỏng vấn bằng AI

## Mục Lục

1. [Giới Thiệu](#1-giới-thiệu)
2. [Cấu Trúc Thư Mục](#2-cấu-trúc-thư-mục)
3. [Routing - Điều Hướng](#3-routing---điều-hướng)
4. [Quản Lý Trạng Thái](#4-quản-lý-trạng-thái)
5. [Services - Giao Tiếp API](#5-services---giao-tiếp-api)
6. [Types - Kiểu Dữ Liệu](#6-types---kiểu-dữ-liệu)
7. [Hooks Tùy Chỉnh](#7-hooks-tùy-chỉnh)
8. [Utils - Tiện Ích](#8-utils---tiện-ích)
9. [Cài Đặt và Chạy](#9-cài-đặt-và-chạy)

---

## 1. Giới Thiệu

### 1.1 Tổng Quan

ARISP là nền tảng tuyển dụng thông minh sử dụng AI để hỗ trợ quy trình phỏng vấn. Ứng dụng có 3 nhóm người dùng chính:

- **Nhà tuyển dụng (Admin/Hr)**: Quản lý tin tuyển dụng, xem ứng viên, đánh giá kết quả phỏng vấn
- **Ứng viên (Candidate)**: Tìm việc, ứng tuyển, tham gia phỏng vấn AI, xem kết quả
- **Phỏng vấn AI**: Phòng phỏng vấn thông minh với real-time transcription và đánh giá tự động

### 1.2 Công Nghệ Sử Dụng

| Công nghệ | Phiên bản | Mô tả |
|---|---|---|
| React | 18.x | Thư viện UI chính |
| TypeScript | 5.x | Ngôn ngữ lập trình chính |
| Vite | 5.x | Công cụ build nhanh |
| Tailwind CSS | 3.x | Utility-first CSS framework |
| Framer Motion | 11.x | Animation library |
| React Router | 6.x | Routing / điều hướng |
| Zustand | 4.x | State management |
| Axios | 1.x | HTTP client |
| Lucide React | 0.x | Icon library |

### 1.3 Kiến Trúc Tổng Quan

```
main.tsx (Entry point)
    │
    └── App.tsx (Router - tất cả routes)
              │
              ├── pages/landing/      → Trang chủ công khai (Home, Tìm việc)
              ├── pages/auth/         → Đăng nhập / Đăng ký (Nhà tuyển dụng & Ứng viên)
              ├── pages/admin/        → Dashboard quản lý (Nhà tuyển dụng)
              ├── pages/candidate/    → Trang ứng viên (Hồ sơ, đơn ứng tuyển, lịch phỏng vấn)
              ├── pages/interview/    → Phòng phỏng vấn AI
              ├── pages/job-board/    → Chi tiết việc làm công khai
              ├── pages/kiosk/        → Chế độ kiosk
              └── pages/NotFoundPage  → Trang 404
```

---

## 2. Cấu Trúc Thư Mục

```
frontend/src/
│
├── main.tsx                      # Entry point - Render App component
├── App.tsx                       # Router chính - Định nghĩa tất cả routes
├── vite-env.d.ts                # Type declarations cho Vite
│
├── config/
│   └── constants.ts             # Hằng số: API URL, ROLES, INTERVIEW_MODES
│
├── routes/
│   └── index.ts                # Định nghĩa route paths dạng constant
│
├── contexts/
│   └── AuthContext.tsx         # Auth context (đang phát triển)
│
├── components/                  # UI Components có thể tái sử dụng
│   │
│   ├── ui/                     # Components cơ bản
│   │   ├── Button.tsx          # Button với variants (primary, secondary, ghost)
│   │   ├── GlassCard.tsx      # Card với hiệu ứng glass morphism
│   │   └── Container.tsx       # Layout container (Container, ContainerItem)
│   │
│   ├── layout/                 # Layout wrapper cho từng nhóm trang
│   │   ├── AdminLayout.tsx     # Sidebar navigation cho admin
│   │   ├── AuthLayout.tsx     # Wrapper đơn giản cho trang auth
│   │   ├── CandidateLayout.tsx # Layout cho trang ứng viên
│   │   ├── InterviewLayout.tsx # Layout cho phòng phỏng vấn (MUI)
│   │   ├── Navigation.tsx      # Navigation công khai
│   │   ├── PublicNav.tsx       # Public navigation cố định
│   │   ├── CandidateNav.tsx    # Navigation cho ứng viên
│   │   ├── CandidateFooter.tsx # Footer cho ứng viên
│   │   └── Footer.tsx         # Footer công khai
│   │
│   ├── common/                 # Shared components
│   │   ├── LoadingButton.tsx   # MUI Button với trạng thái loading
│   │   ├── LoadingSpinner.tsx  # Spinner hiển thị loading
│   │   └── ErrorAlert.tsx     # Alert thông báo lỗi
│   │
│   ├── sections/               # Section components cho landing page
│   │   ├── Hero.tsx            # Hero section với video background
│   │   ├── Demo.tsx           # Demo/features section
│   │   ├── CTA.tsx            # Call to Action section
│   │   ├── Stats.tsx          # Statistics section với animation
│   │   └── ScrollStorytelling.tsx # Scroll-driven animation
│   │
│   └── three/                  # Three.js components
│       ├── AISphereDemo.tsx   # AI Sphere demo visualization
│       └── ThreeBackground.tsx # 3D background cho landing page
│
├── pages/                       # Page components - Mỗi route có 1 page
│   │
│   ├── landing/                # Trang công khai
│   │   ├── index.ts           # Barrel exports
│   │   ├── HomePage.tsx       # Trang chủ nhà tuyển dụng (/nha-tuyen-dung)
│   │   └── FindJobPage.tsx    # Trang tìm kiếm việc làm (/)
│   │
│   ├── auth/                   # Trang xác thực
│   │   ├── index.ts
│   │   ├── LoginPage.tsx      # Đăng nhập nhà tuyển dụng (/dang-nhap)
│   │   ├── RegisterPage.tsx    # Đăng ký nhà tuyển dụng (/dang-ky)
│   │   ├── CandidateLoginPage.tsx  # Đăng nhập ứng viên (/dang-nhap-ung-vien)
│   │   └── CandidateRegisterPage.tsx # Đăng ký ứng viên (/dang-ky-ung-vien)
│   │
│   ├── admin/                  # Dashboard quản lý (wrapped by AdminLayout)
│   │   ├── index.ts
│   │   ├── DashboardPage.tsx   # Tổng quan dashboard
│   │   ├── JobPostingsPage.tsx # Danh sách tin tuyển dụng
│   │   ├── JobPostingDetailPage.tsx # Chi tiết tin tuyển dụng
│   │   ├── CreateJobPostingPage.tsx # Tạo mới tin tuyển dụng
│   │   ├── CandidatesPage.tsx  # Danh sách ứng viên
│   │   ├── CandidateDetailPage.tsx # Chi tiết hồ sơ ứng viên
│   │   ├── InterviewSessionsPage.tsx # Các phiên phỏng vấn
│   │   ├── EvaluationReviewPage.tsx # Xem và duyệt đánh giá
│   │   ├── ReportsPage.tsx    # Báo cáo thống kê
│   │   ├── PlaybooksPage.tsx  # Quản lý kịch bản phỏng vấn
│   │   ├── SettingsPage.tsx   # Cài đặt tài khoản
│   │   └── TeamPage.tsx       # Quản lý nhóm
│   │
│   ├── candidate/              # Trang dành cho ứng viên
│   │   ├── index.ts
│   │   ├── CandidateHome.tsx   # Trang chủ ứng viên (/ung-vien)
│   │   ├── DashboardPage.tsx   # Dashboard ứng viên
│   │   ├── MyApplicationsPage.tsx # Danh sách đơn ứng tuyển
│   │   ├── InterviewSchedulePage.tsx # Lịch phỏng vấn
│   │   ├── PortalPage.tsx      # Cổng thông tin ứng viên (/ung-vien/cong-cua)
│   │   └── FeedbackPage.tsx    # Kết quả phỏng vấn (/ung-vien/ket-qua)
│   │
│   ├── interview/              # Phòng phỏng vấn AI (wrapped by InterviewLayout)
│   │   ├── index.ts
│   │   ├── InterviewRoomPage.tsx # Phòng phỏng vấn thực tế
│   │   └── PracticeSessionPage.tsx # Phiên luyện tập
│   │
│   ├── job-board/              # Trang công khai về việc làm
│   │   └── JobDetailPage.tsx  # Chi tiết việc làm (/jobs/:id)
│   │
│   ├── kiosk/                  # Chế độ kiosk
│   │   └── KioskPage.tsx      # Trang kiosk (/kiosk)
│   │
│   ├── NotFoundPage.tsx        # Trang 404
│   └── index.ts               # Barrel exports
│
├── services/                   # Giao tiếp API
│   ├── index.ts              # Barrel exports
│   ├── apiClient.ts          # Axios instance - interceptors, auth headers
│   │
│   ├── auth/                 # Authentication
│   │   ├── index.ts
│   │   └── authService.ts   # Login, register, refresh token, logout
│   │
│   ├── job/                  # Tin tuyển dụng
│   │   ├── index.ts
│   │   └── jobService.ts    # CRUD tin tuyển dụng
│   │
│   ├── application/          # Đơn ứng tuyển
│   │   ├── index.ts
│   │   └── applicationService.ts # CRUD đơn ứng tuyển
│   │
│   ├── interview/            # Phiên phỏng vấn
│   │   ├── index.ts
│   │   └── interviewService.ts # Tạo/get phiên phỏng vấn, transcription
│   │
│   ├── evaluation/           # Đánh giá
│   │   ├── index.ts
│   │   └── evaluationService.ts # Lấy báo cáo đánh giá
│   │
│   └── schedule/             # Lịch phỏng vấn
│       ├── index.ts
│       └── scheduleService.ts # Availability slots
│
├── types/                     # TypeScript type definitions
│   ├── index.ts             # Barrel exports
│   │
│   ├── auth/
│   │   ├── index.ts
│   │   └── auth.types.ts   # User, AuthTokens, LoginRequest, RegisterRequest
│   │
│   ├── job/
│   │   ├── index.ts
│   │   └── job.types.ts    # JobPosting, CreateJobPostingRequest, SalaryRange
│   │
│   ├── application/
│   │   ├── index.ts
│   │   └── application.types.ts # Application, ApplicationDetail
│   │
│   ├── interview/
│   │   ├── index.ts
│   │   └── interview.types.ts # InterviewRoomState, Signal, Candidate
│   │
│   ├── evaluation/
│   │   ├── index.ts
│   │   └── evaluation.types.ts # EvaluationReport, CriterionScore
│   │
│   └── organization/
│       ├── index.ts
│       └── organization.types.ts # Organization, Subscription
│
├── store/                     # Zustand state stores
│   ├── index.ts
│   │
│   ├── auth/                # Auth state
│   │   ├── index.ts
│   │   └── authStore.ts    # User, tokens, login, logout, isAuthenticated
│   │
│   ├── application/         # Application state
│   │   ├── index.ts
│   │   └── applicationStore.ts # Applications list
│   │
│   └── interview/           # Interview room state
│       ├── index.ts
│       └── interviewStore.ts # Interview room signals, state
│
├── hooks/                    # Custom React hooks
│   ├── index.ts
│   │
│   ├── interview/           # Interview room hooks
│   │   ├── index.ts
│   │   └── useInterviewRoom.ts # Business logic cho phòng phỏng vấn
│   │
│   └── cheat-detection/     # Anti-cheat hooks
│       ├── index.ts
│       └── useCheatDetection.ts # Phát hiện tab switch, eye tracking
│
└── utils/                    # Tiện ích
    ├── index.ts
    ├── devAuth.ts          # Dev mode authentication helpers
    └── format/
        ├── index.ts
        └── format.ts       # Format ngày tháng, thời gian
```

---

## 3. Routing - Điều Hướng

### 3.1 Route Paths

Định nghĩa tại `routes/index.ts`:

```typescript
export const routes = {
  // Public
  home: '/',
  login: '/dang-nhap',
  register: '/dang-ky',

  // HR Admin
  dashboard: '/quan-ly',
  jobPostings: '/quan-ly/tin-tuyen-dung',
  candidates: '/quan-ly/ung-vien',
  evaluations: '/quan-ly/danh-gia',
  reports: '/quan-ly/bao-cao',

  // Candidate
  candidateApply: '/ung-vien/ung-tuyen',
  candidateInterview: '/ung-vien/phong-van',
  candidatePortal: '/ung-vien/cong-cua',

  // Interview
  interviewRoom: '/phong-van/:sessionId',
  interviewKiosk: '/kiosk',
} as const;
```

### 3.2 Tất Cả Routes trong App.tsx

| Route | Component | Layout | Mô tả |
|---|---|---|---|
| `/` | `FindJobPage` | - | Trang tìm việc |
| `/nha-tuyen-dung` | `HomePage` | - | Landing page nhà tuyển dụng |
| `/dang-nhap` | `LoginPage` | - | Đăng nhập nhà tuyển dụng |
| `/dang-ky` | `RegisterPage` | - | Đăng ký nhà tuyển dụng |
| `/dang-nhap-ung-vien` | `CandidateLoginPage` | - | Đăng nhập ứng viên |
| `/dang-ky-ung-vien` | `CandidateRegisterPage` | - | Đăng ký ứng viên |
| `/ung-vien` | `CandidateHome` | `CandidateLayout` | Trang chủ ứng viên |
| `/ung-vien/ung-tuyen` | `CandidateApply` | - | Trang ứng tuyển |
| `/ung-vien/phong-van` | `InterviewSchedulePage` | - | Lịch phỏng vấn |
| `/ung-vien/cong-cua` | `PortalPage` | - | Cổng thông tin |
| `/ung-vien/ket-qua` | `FeedbackPage` | - | Kết quả đánh giá |
| `/quan-ly` | `DashboardPage` | `AdminLayout` | Dashboard admin |
| `/quan-ly/tin-tuyen-dung` | `JobPostingsPage` | `AdminLayout` | Danh sách tin tuyển dụng |
| `/quan-ly/tin-tuyen-dung/tao-moi` | `CreateJobPostingPage` | `AdminLayout` | Tạo tin tuyển dụng |
| `/quan-ly/tin-tuyen-dung/:id` | `JobPostingDetailPage` | `AdminLayout` | Chi tiết tin |
| `/quan-ly/ung-vien` | `CandidatesPage` | `AdminLayout` | Danh sách ứng viên |
| `/quan-ly/ung-vien/:id` | `CandidateDetailPage` | `AdminLayout` | Chi tiết ứng viên |
| `/quan-ly/danh-gia` | `EvaluationReviewPage` | `AdminLayout` | Duyệt đánh giá |
| `/quan-ly/bao-cao` | `ReportsPage` | `AdminLayout` | Báo cáo |
| `/quan-ly/cai-dat` | `SettingsPage` | `AdminLayout` | Cài đặt |
| `/quan-ly/playbooks` | `PlaybooksPage` | `AdminLayout` | Kịch bản phỏng vấn |
| `/quan-ly/nhom` | `TeamPage` | `AdminLayout` | Quản lý nhóm |
| `/quan-ly/phong-van` | `InterviewSessionsPage` | `AdminLayout` | Phiên phỏng vấn |
| `/interview/room/:sessionId` | `InterviewRoomPage` | `InterviewLayout` | Phòng phỏng vấn |
| `/interview/practice/:applicationId` | `PracticeSessionPage` | `InterviewLayout` | Luyện tập |
| `/kiosk` | `KioskPage` | `InterviewLayout` | Chế độ kiosk |
| `/jobs/:id` | `JobDetailPage` | - | Chi tiết việc làm |
| `*` | `NotFoundPage` | - | Trang 404 |

---

## 4. Quản Lý Trạng Thái

### 4.1 Auth Store (Zustand)

```typescript
// store/auth/authStore.ts
interface AuthState {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  setAuth: (user: User, tokens: AuthTokens) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  tokens: null,
  isAuthenticated: false,
  setAuth: (user, tokens) => set({ user, tokens, isAuthenticated: true }),
  clearAuth: () => set({ user: null, tokens: null, isAuthenticated: false }),
}));
```

Sử dụng: `const { user, isAuthenticated, setAuth, clearAuth } = useAuthStore()`

### 4.2 Interview Store

```typescript
// store/interview/interviewStore.ts
// Quản lý signals, state của phòng phỏng vấn real-time
```

---

## 5. Services - Giao Tiếp API

### 5.1 API Client

```typescript
// services/apiClient.ts
// Axios instance với:
// - Base URL từ constants.ts
// - Request interceptor: gắn auth token
// - Response interceptor: xử lý 401, refresh token
```

### 5.2 Các Service Chính

| Service | File | Các hàm chính |
|---|---|---|
| `authService` | `auth/authService.ts` | `login`, `register`, `refreshToken`, `logout` |
| `jobService` | `job/jobService.ts` | `getJobPostings`, `createJobPosting`, `getPublicJobPostings` |
| `applicationService` | `application/applicationService.ts` | `createApplication`, `getApplications` |
| `interviewService` | `interview/interviewService.ts` | `createSession`, `getSession`, `transcribe` |
| `evaluationService` | `evaluation/evaluationService.ts` | `getEvaluations`, `getMyEvaluations` |
| `scheduleService` | `schedule/scheduleService.ts` | `getAvailabilitySlots` |

---

## 6. Types - Kiểu Dữ Liệu

| Module | File | Các interface chính |
|---|---|---|
| Auth | `auth/auth.types.ts` | `User`, `AuthTokens`, `LoginRequest`, `RegisterRequest` |
| Job | `job/job.types.ts` | `JobPosting`, `CreateJobPostingRequest`, `SalaryRange` |
| Application | `application/application.types.ts` | `Application`, `ApplicationDetail`, `ApplicationStatus` |
| Interview | `interview/interview.types.ts` | `InterviewRoomState`, `Signal`, `Candidate`, `WebRTCSignal` |
| Evaluation | `evaluation/evaluation.types.ts` | `EvaluationReport`, `CriterionScore` |
| Organization | `organization/organization.types.ts` | `Organization`, `Subscription` |

---

## 7. Hooks Tùy Chỉnh

### 7.1 `useInterviewRoom(sessionId: string)`

Logic business cho phòng phỏng vấn: kết nối WebRTC, quản lý signals, transcription.

### 7.2 `useCheatDetection(config: CheatDetectionConfig)`

Phát hiện gian lận:
- Theo dõi tab switch / window blur
- Eye tracking (nếu có camera permission)
- Cảnh báo khi phát hiện hành vi bất thường

---

## 8. Utils - Tiện Ích

| File | Mô tả |
|---|---|
| `utils/devAuth.ts` | Helpers cho dev mode: `getDevAuth()`, `isDevMode` - tự động đăng nhập khi `VITE_DEV_AUTH=true` |
| `utils/format/format.ts` | Format ngày tháng: `formatDate()`, `formatTime()`, `formatDateTime()` |
| `config/constants.ts` | Hằng số: `API_BASE_URL`, `ROLES`, `INTERVIEW_MODES`, `DEFAULT_PAGINATION` |

---

## 9. Cài Đặt và Chạy

### 9.1 Yêu Cầu

- Node.js 18+
- npm 9+

### 9.2 Cài Đặt

```bash
cd frontend
npm install
```

### 9.3 Chạy Development Server

```bash
npm run dev
```

Ứng dụng sẽ chạy tại `http://localhost:5173`

### 9.4 Build Production

```bash
npm run build
```

Output sẽ nằm trong thư mục `dist/`

### 9.5 Biến Môi Trường

Tạo file `.env` trong thư mục `frontend/`:

```env
VITE_API_URL=http://localhost:8080/api
VITE_WS_URL=ws://localhost:8080/ws
VITE_DEV_AUTH=false
```

### 9.6 Dev Mode Auth

Để tự động đăng nhập dev, set trong `.env`:

```env
VITE_DEV_AUTH=true
```

Điều này sẽ tự động load user và tokens từ `devAuth.ts` khi khởi động app.

---

## 10. Ghi Chú Quan Trọng

### 10.1 MUI vs Tailwind

Dự án sử dụng **chủ yếu Tailwind CSS** cho styling. MUI (Material UI) chỉ được dùng cho một số components cụ thể trong `interview/` và `components/common/`. Cần thống nhất để tránh conflict CSS.

### 10.2 AuthLayout

`AuthLayout` được định nghĩa trong codebase nhưng hiện tại **không được sử dụng** trong routes. Các trang auth (`LoginPage`, `RegisterPage`) được render trực tiếp mà không qua layout wrapper.

### 10.3 API Base URL

Backend API được cấu hình trong `config/constants.ts`. Đảm bảo backend chạy và URL chính xác trong file `.env`.

### 10.4 WebSocket

Tính năng real-time (phỏng vấn AI) sử dụng WebSocket cho transcription và signals. Cần cấu hình `VITE_WS_URL` đúng.
