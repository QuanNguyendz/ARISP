# ARISP Frontend - Tài Liệu Dự Án

> **ARISP** - AI-Powered Recruitment and Interview Support Platform
> Nền tảng hỗ trợ tuyển dụng và phỏng vấn bằng AI

## Mục Lục

1. [Giới Thiệu](#giới-thiệu)
2. [Cấu Trúc Thư Mục](#cấu-trúc-thư-mục)
3. [Chi Tiết Từng Thành Phần](#chi-tiết-từng-thành-phần)
4. [Routing - Điều Hướng](#routing---điều-hướng)
5. [State Management - Quản Lý Trạng Thái](#state-management---quản-lý-trạng-thái)
6. [API Services - Dịch Vụ API](#api-services---dịch-vụ-api)
7. [Types - Kiểu Dữ Liệu](#types---kiểu-dữ-liệu)
8. [Hooks Tùy Chỉnh](#hooks-tùy-chỉnh)
9. [Utils - Tiện Ích](#utils---tiện-ích)
10. [Cài Đặt và Chạy](#cài-đặt-và-chạy)

---

## Giới Thiệu

### Công Nghệ Sử Dụng

| Công Nghệ | Phiên Bản | Mô Tả |
|-----------|-----------|--------|
| React | 18.x | Thư viện UI chính |
| TypeScript | 5.x | Ngôn ngữ lập trình |
| Vite | 5.x | Build tool |
| MUI (Material UI) | 5.x | Component library |
| Tailwind CSS | 3.x | Utility-first CSS |
| Zustand | 4.x | State management |
| React Router | 6.x | Routing |
| Axios | 1.x | HTTP client |
| Zod | 3.x | Schema validation |

### Kiến Trúc Ứng Dụng

```
┌─────────────────────────────────────────────────────────────┐
│                         App.tsx                             │
│                   (Router Configuration)                     │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   ┌─────────┐          ┌─────────┐          ┌─────────┐
   │  Pages  │          │  Pages  │          │  Pages  │
   │ (Admin) │          │(Candidate)         │(Interview)       │
   └─────────┘          └─────────┘          └─────────┘
        │                     │                     │
        ▼                     ▼                     ▼
   ┌─────────────────────────────────────────────────────────┐
   │                    Services Layer                       │
   │   (API calls - jobService, authService, etc.)          │
   └─────────────────────────────────────────────────────────┘
        │                     │                     │
        ▼                     ▼                     ▼
   ┌─────────────────────────────────────────────────────────┐
   │                    Store Layer (Zustand)                │
   │         (authStore, applicationStore, interviewStore)   │
   └─────────────────────────────────────────────────────────┘
```

---

## Cấu Trúc Thư Mục

```
frontend/
├── .env.example                    # Template biến môi trường
├── index.html                      # HTML entry point
├── package.json                    # Dependencies
├── vite.config.ts                  # Cấu hình Vite
├── tsconfig.json                    # Cấu hình TypeScript
├── tailwind.config.js               # Cấu hình Tailwind CSS
├── postcss.config.js                # Cấu hình PostCSS
│
└── src/
    ├── App.tsx                      # Root component + Routing
    ├── main.tsx                     # Entry point (ReactDOM)
    ├── index.css                     # Global styles
    │
    ├── assets/                      # Static assets
    │   ├── icons/
    │   └── images/
    │
    ├── components/                  # Reusable components
    │   ├── common/                  # Components chung (ErrorAlert, Loading...)
    │   ├── layout/                  # Layout wrappers
    │   ├── interview/               # Interview-specific components
    │   ├── job-board/               # Job board components
    │   └── portal/                  # Portal components
    │
    ├── config/                      # Configuration
    │   ├── constants.ts              # Hằng số ứng dụng
    │   └── env.ts                    # Environment variables
    │
    ├── contexts/                    # React Contexts (Provider)
    │
    ├── hooks/                       # Custom React Hooks
    │   ├── cheat-detection/          # Hook phát hiện gian lận
    │   ├── interview/               # Hook phòng phỏng vấn
    │   └── recording/               # Hook ghi âm
    │
    ├── pages/                       # Page components (Route-level)
    │   ├── admin/                    # Trang quản trị HR
    │   ├── auth/                     # Trang đăng nhập/đăng ký
    │   ├── candidate/                # Trang ứng viên
    │   ├── interview/                # Trang phỏng vấn
    │   ├── job-board/                # Trang việc làm công khai
    │   └── kiosk/                    # Trang kiosk tại quầy
    │
    ├── services/                    # API Services
    │   ├── apiClient.ts              # Axios instance + interceptors
    │   ├── application/              # Service quản lý đơn ứng tuyển
    │   ├── auth/                     # Service xác thực
    │   ├── evaluation/               # Service đánh giá
    │   ├── interview/                # Service phỏng vấn
    │   ├── job/                      # Service tin tuyển dụng
    │   └── schedule/                 # Service lịch phỏng vấn
    │
    ├── store/                       # Zustand Stores
    │   ├── auth/                     # Auth state
    │   ├── application/             # Application state
    │   └── interview/               # Interview state
    │
    ├── types/                       # TypeScript Interfaces
    │   ├── application/
    │   ├── auth/
    │   ├── evaluation/
    │   ├── interview/
    │   ├── job/
    │   └── organization/
    │
    └── utils/                       # Utility Functions
        ├── audio/                    # Xử lý âm thanh
        ├── format/                   # Định dạng (date, currency...)
        └── validation/               # Zod validation schemas
```

---

## Chi Tiết Từng Thành Phần

### 1. Components (`src/components/`)

#### Components Chung (`components/common/`)

| Component | Mô Tả | Props |
|-----------|-------|-------|
| `ErrorAlert` | Hiển thị thông báo lỗi | `message`, `severity`, `onClose` |
| `LoadingButton` | Button với trạng thái loading | `loading`, `children`, `...ButtonProps` |
| `LoadingSpinner` | Spinner loading | `size`, `color` |

#### Layouts (`components/layout/`)

| Layout | Mô Tả | Sử Dụng Cho |
|--------|-------|-------------|
| `AuthLayout` | Layout cho trang đăng nhập | Trang auth |
| `MainLayout` | Layout chính với sidebar | Trang dashboard, admin |
| `InterviewLayout` | Layout cho phòng phỏng vấn | Trang phỏng vấn |

### 2. Pages (`src/pages/`)

#### Trang Admin (`pages/admin/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `DashboardPage` | `/admin` | Tổng quan dashboard HR |
| `JobPostingsPage` | `/admin/jobs` | Danh sách tin tuyển dụng |
| `CreateJobPostingPage` | `/admin/jobs/create` | Tạo tin tuyển dụng |
| `JobPostingDetailPage` | `/admin/jobs/:id` | Chi tiết tin tuyển dụng |
| `CandidatesPage` | `/admin/candidates` | Danh sách ứng viên |
| `EvaluationReviewPage` | `/admin/evaluations` | Xem lại đánh giá |
| `InterviewSessionsPage` | `/admin/interviews` | Quản lý buổi phỏng vấn |
| `TeamPage` | `/admin/team` | Quản lý nhóm |
| `PlaybooksPage` | `/admin/playbooks` | Quản lý playbook |
| `SettingsPage` | `/admin/settings` | Cài đặt |

#### Trang Candidate (`pages/candidate/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `DashboardPage` | `/candidate` | Dashboard ứng viên |
| `MyApplicationsPage` | `/candidate/applications` | Đơn ứng tuyển của tôi |
| `InterviewSchedulePage` | `/candidate/schedule` | Lịch phỏng vấn |
| `FeedbackPage` | `/candidate/feedback` | Xem phản hồi |
| `RecordingPage` | `/candidate/recordings` | Bản ghi phỏng vấn |

#### Trang Interview (`pages/interview/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `InterviewRoomPage` | `/interview/:id` | Phòng phỏng vấn chính |
| `PracticeSessionPage` | `/interview/practice` | Phiên luyện tập |

#### Trang Job Board (`pages/job-board/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `JobBoardPage` | `/jobs` | Danh sách việc làm công khai |
| `JobDetailPage` | `/jobs/:id` | Chi tiết việc làm |

#### Trang Auth (`pages/auth/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `LoginPage` | `/login` | Trang đăng nhập |
| `MagicLinkCallbackPage` | `/auth/callback` | Callback từ magic link |

#### Trang Kiosk (`pages/kiosk/`)

| Page | Route | Mô Tả |
|------|-------|-------|
| `KioskPage` | `/kiosk` | Giao diện kiosk tại quầy |

---

## Routing - Điều Hướng

### Cấu Hình Routes (trong `App.tsx`)

```typescript
// Public Routes
/                     → Trang chủ
/login                → LoginPage
/jobs                 → JobBoardPage
/jobs/:id             → JobDetailPage
/kiosk                → KioskPage

// Protected Routes (Candidate)
/candidate            → DashboardPage
/candidate/applications → MyApplicationsPage
/candidate/schedule   → InterviewSchedulePage
/candidate/feedback    → FeedbackPage
/candidate/recordings   → RecordingPage

// Protected Routes (Interview Room)
/interview/:id         → InterviewRoomPage
/interview/practice    → PracticeSessionPage

// Protected Routes (Admin - HR Role)
/admin                → DashboardPage
/admin/jobs           → JobPostingsPage
/admin/jobs/create    → CreateJobPostingPage
/admin/jobs/:id       → JobPostingDetailPage
/admin/candidates      → CandidatesPage
/admin/evaluations     → EvaluationReviewPage
/admin/interviews      → InterviewSessionsPage
/admin/team           → TeamPage
/admin/playbooks       → PlaybooksPage
/admin/settings        → SettingsPage
```

### Protected Routes

Routes được bảo vệ bằng `ProtectedRoute` component:
- Kiểm tra authentication (auth state)
- Kiểm tra authorization (role-based access)
- Redirect về login nếu chưa đăng nhập

---

## State Management - Quản Lý Trạng Thái

Sử dụng **Zustand** để quản lý state.

### Auth Store (`store/auth/authStore.ts`)

```typescript
interface AuthState {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;

  // Actions
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  setAuth: (user: User, tokens: AuthTokens) => void;
  refreshToken: () => Promise<void>;
}
```

### Application Store (`store/application/applicationStore.ts`)

```typescript
interface ApplicationState {
  applications: Application[];
  currentApplication: Application | null;
  filters: ApplicationFilters;

  // Actions
  fetchApplications: (filters?: ApplicationFilters) => Promise<void>;
  getApplicationById: (id: string) => Promise<Application>;
  createApplication: (data: CreateApplicationRequest) => Promise<Application>;
  updateApplicationStatus: (id: string, status: ApplicationStatus) => Promise<void>;
}
```

### Interview Store (`store/interview/interviewStore.ts`)

```typescript
interface InterviewState {
  currentSession: InterviewSession | null;
  recordingBlob: Blob | null;
  cheatAlerts: CheatAlert[];

  // Actions
  startSession: (config: SessionConfig) => Promise<void>;
  endSession: () => Promise<void>;
  sendMessage: (message: string) => Promise<void>;
  startRecording: () => Promise<void>;
  stopRecording: () => Promise<void>;
}
```

---

## API Services - Dịch Vụ API

### API Client (`services/apiClient.ts`)

Axios instance với các tính năng:
- Base URL từ environment
- Request interceptor: Thêm JWT token
- Response interceptor: Xử lý 401 (refresh token)

```typescript
// Usage
import { apiClient } from '@services/apiClient';

const response = await apiClient.get('/users');
```

### Services

| Service | Mô Tả | Endpoints |
|---------|-------|-----------|
| `authService` | Xác thực người dùng | login, logout, refresh, magic-link |
| `jobService` | Quản lý tin tuyển dụng | CRUD, publish, pause |
| `applicationService` | Quản lý đơn ứng tuyển | CRUD, update status |
| `interviewService` | Quản lý phỏng vấn | Sessions, messages, transcripts |
| `evaluationService` | Đánh giá ứng viên | Create, get evaluations |
| `scheduleService` | Quản lý lịch phỏng vấn | Slots, bookings |

### Ví Dụ Sử Dụng Service

```typescript
import { jobService } from '@services/job';

// Get all job postings
const { items, total, page, pageSize, totalPages } = await jobService.getJobPostings({
  status: 'active',
  page: 1,
  pageSize: 10
});

// Create new job
const newJob = await jobService.createJobPosting({
  title: 'Senior Developer',
  description: '...',
  requirements: ['5+ years experience'],
  skills: ['React', 'TypeScript'],
  location: 'Ho Chi Minh City',
  interviewConfig: { ... }
});
```

---

## Types - Kiểu Dữ Liệu

### Job Types (`types/job/`)

```typescript
interface JobPosting {
  id: string;
  organizationId: string;
  title: string;
  description: string;
  requirements: string[];
  skills: string[];
  location: string;
  salaryRange?: SalaryRange;
  interviewConfig: InterviewConfig;
  status: 'draft' | 'active' | 'paused' | 'closed';
  createdAt: Date;
  updatedAt: Date;
  applicationCount: number;
}

interface InterviewConfig {
  rounds: RoundConfig[];
  interviewModes: ('Remote' | 'OnSite')[];
  availabilitySlots?: AvailabilitySlot[];
  languageRequirement?: LanguageRequirement;
  scoringRubric?: ScoringRubric;
  persona?: InterviewPersona;
}

interface InterviewPersona {
  name: string;
  gender: 'male' | 'female';
  style: 'friendly' | 'professional' | 'strict';
  voiceId?: string;
}
```

### Auth Types (`types/auth/`)

```typescript
interface User {
  id: string;
  email: string;
  name: string;
  role: 'admin' | 'hr' | 'candidate';
  organizationId?: string;
  avatar?: string;
}

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}
```

### Application Types (`types/application/`)

```typescript
interface Application {
  id: string;
  jobId: string;
  candidateId: string;
  status: 'pending' | 'screening' | 'interview' | 'offer' | 'rejected' | 'accepted';
  resume: Resume;
  coverLetter?: string;
  appliedAt: Date;
  updatedAt: Date;
}
```

### Interview Types (`types/interview/`)

```typescript
interface InterviewSession {
  id: string;
  jobPostingId: string;
  applicationId: string;
  candidateId: string;
  interviewerId?: string;
  status: 'scheduled' | 'in_progress' | 'completed' | 'cancelled';
  currentRound: number;
  messages: ChatMessage[];
  startedAt?: Date;
  endedAt?: Date;
  evaluation?: Evaluation;
}
```

---

## Hooks Tùy Chỉnh

### 1. useInterviewRoom (`hooks/interview/useInterviewRoom.ts`)

Hook quản lý phòng phỏng vấn:

```typescript
const {
  // State
  isConnected,
  messages,
  currentRound,
  isRecording,
  error,

  // Actions
  connect,
  disconnect,
  sendMessage,
  startRecording,
  stopRecording,
  nextRound,
} = useInterviewRoom(sessionId);
```

### 2. useRecording (`hooks/recording/useRecording.ts`)

Hook ghi âm:

```typescript
const {
  isRecording,
  duration,
  audioLevel,
  startRecording,
  stopRecording,
  pauseRecording,
  resumeRecording,
  getAudioBlob,
} = useRecording();
```

### 3. useCheatDetection (`hooks/cheat-detection/useCheatDetection.ts`)

Hook phát hiện gian lận:

```typescript
const {
  alerts,
  isMonitoring,
  startMonitoring,
  stopMonitoring,
  clearAlerts,
} = useCheatDetection({
  onCheatDetected: (alert) => {
    console.log('Cheat detected:', alert.type);
  },
});
```

---

## Utils - Tiện Ích

### Validation Schemas (`utils/validation/schemas.ts`)

Sử dụng **Zod** để validate dữ liệu:

```typescript
import { loginSchema, jobPostingSchema, applicationSchema } from '@utils/validation';

// Validate login form
const result = loginSchema.safeParse(formData);
if (!result.success) {
  // Handle validation error
}

// Validate job posting
const jobResult = jobPostingSchema.safeParse(jobData);
```

### Format Utils (`utils/format/format.ts`)

```typescript
import { formatDate, formatCurrency, formatDuration } from '@utils/format';

formatDate(new Date(), 'DD/MM/YYYY');     // "26/05/2026"
formatCurrency(1000000, 'VND');           // "1.000.000 VND"
formatDuration(3665);                      // "1h 1m 5s"
```

### Audio Utils (`utils/audio/audio.ts`)

```typescript
import { AudioProcessor } from '@utils/audio';

// Process audio from microphone
const processor = new AudioProcessor();
processor.start();
processor.getVolumeLevel();  // 0-1
processor.stop();
```

---

## Cài Đặt và Chạy

### Yêu Cầu

- Node.js >= 18.x
- npm >= 9.x hoặc yarn >= 1.x

### Các Bước

```bash
# 1. Di chuyển vào thư mục frontend
cd frontend

# 2. Cài đặt dependencies
npm install

# 3. Tạo file .env từ .env.example
cp .env.example .env

# 4. Chỉnh sửa .env với API URL của bạn
# VITE_API_BASE_URL=http://localhost:8080/api

# 5. Chạy development server
npm run dev

# 6. Build cho production
npm run build

# 7. Preview production build
npm run preview
```

### Scripts Có Sẵn

| Script | Mô Tả |
|--------|-------|
| `npm run dev` | Chạy development server |
| `npm run build` | Build production |
| `npm run preview` | Preview production build |
| `npm run lint` | Chạy ESLint |
| `npm run lint:fix` | Tự động fix lint errors |
| `npm run format` | Format code với Prettier |

### Environment Variables

```env
# .env
VITE_API_BASE_URL=http://localhost:8080/api
VITE_APP_NAME=ARISP
VITE_APP_ENV=development
```

---

## Quy Ước Đặt Tên

### Files

- Components: `PascalCase.tsx` (VD: `JobPostingCard.tsx`)
- Services: `camelCase.ts` (VD: `jobService.ts`)
- Hooks: `camelCase.ts` (VD: `useInterviewRoom.ts`)
- Utils: `camelCase.ts` (VD: `formatDate.ts`)
- Types: `kebab-case.types.ts` (VD: `job.types.ts`)

### Folders

- Tất cả folders sử dụng `kebab-case`: `job-postings`, `my-applications`
- Index files cho exports: `index.ts`

### Imports

- Sử dụng alias path: `@/components/...`, `@/services/...`, `@/types/...`
- Relative imports cho cùng folder: `./Button`

---

## Next Steps - Bước Tiếp Theo

1. **Cài backend** - Triển khai backend API tương ứng
2. **Hoàn thiện components** - Các folders trống cần được implement
3. **Contexts** - Thêm AuthProvider, ThemeProvider
4. **Testing** - Thêm unit tests và integration tests
5. **CI/CD** - Cấu hình GitHub Actions

---

## Liên Hệ / Hỗ Trợ

Nếu có câu hỏi về dự án, vui lòng refer documentation hoặc liên hệ team development.
