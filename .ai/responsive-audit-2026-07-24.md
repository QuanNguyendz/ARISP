# Báo Cáo Audit Responsive Design — Frontend ARISP

> **Ngày audit:** 2026-07-24
> **Phạm vi:** Toàn bộ frontend (`ari-web/src/`)
> **Stack:** React + TypeScript + TailwindCSS (npm workspaces: ARI.Shared, ARI.CandidateSite, ARI.StaffSite)
> **Breakpoints Tailwind (mặc định):** sm=640, md=768, lg=1024, xl=1280, 2xl=1536
> **Breakpoints audit thêm:** 320, 375, 425, 768, 1024, 1280, 1440, >1440

---

## 1. Tổng quan

### 1.1. Thống kê mã nguồn

| Hạng mục | Số lượng |
|---|---|
| Tổng file `.tsx` / `.ts` | **168** |
| Pages (CandidateSite) | **30** |
| Pages (StaffSite) | **34** |
| Layouts | **14** |
| Shared UI components | **15** |
| Site-specific components | **12** |
| Services / Stores / Hooks / Guards / Utils / Types | **63** |
| Tailwind preset | 1 (không có custom `screens`) |

### 1.2. Tổng số vấn đề phát hiện

| Mức độ | Mô tả | Số lượng |
|---|---|---|
| **P0** | Không sử dụng được trên Mobile | **1** |
| **P1** | Layout bị vỡ | **11** |
| **P2** | UI chưa đẹp | **34** |
| **P3** | Chỉ cần tối ưu thêm | **22** |
| **TỔNG** | | **68** |

### 1.3. Phân bố theo nhóm

| Nhóm | P0 | P1 | P2 | P3 | Tổng |
|---|---|---|---|---|---|
| Layout | 0 | 5 | 12 | 5 | **22** |
| Header | 0 | 0 | 3 | 4 | **7** |
| Sidebar | 0 | 2 | 3 | 1 | **6** |
| Navbar | 0 | 0 | 0 | 0 | **0** ✅ |
| Dashboard | 0 | 1 | 3 | 1 | **5** |
| Form | 0 | 0 | 5 | 3 | **8** |
| Table | 0 | 2 | 3 | 0 | **5** |
| Modal | 1 | 0 | 3 | 1 | **5** |
| Card | 0 | 0 | 0 | 1 | **1** |
| Chart | 0 | 0 | 1 | 1 | **2** |
| Responsive Utility | 0 | 0 | 0 | 1 | **1** |
| Shared Component | 0 | 0 | 1 | 4 | **5** |
| Toast / Skeleton / 3D / Chart | 0 | 1 | 0 | 0 | **1** |

### 1.4. Đánh giá tổng thể

| Phạm vi | Điểm mạnh | Điểm yếu chính |
|---|---|---|
| **Layouts / Shared UI** | Hamburger menu đầy đủ, dropdown mobile-safe, grids responsive | Footer paragraph 1 chỗ thiếu `break-words` |
| **CandidateSite** | Pages auth + landing + legal tốt | **InterviewSchedulePage** lạc design system (dark-glass); nhiều sidebar grid không collapse; section quá lớn (`py-40`) |
| **StaffSite HR** | Tables có `overflow-x-auto` + `min-w-[700px]` chuẩn | **DashboardPage** nhiều P1; **EvaluationReviewPage** có `text-5xl` mobile-killer; **JobPostingDetailPage** `w-36` actions column |
| **StaffSite Recruiter / SA** | Pages auth + landing tốt, dùng `StatsGrid` shared | **JobDetailPage** table 7-cột thiếu wrapper; **CreateJobPosting** salary grid `grid-cols-3` không responsive; Settings sidebar `lg:w-64` không collapsible |

---

## 2. Danh sách màn hình

### 2.1. CandidateSite (Public + Candidate + Interview)

| # | Page | Path | Status | Top issue |
|---|---|---|---|---|
| 1 | CandidateLoginPage | `/auth/candidate-login` | ✅ | — |
| 2 | CandidateRegisterPage | `/auth/candidate-register` | ✅ | — |
| 3 | VerifyEmailPage | `/auth/verify-email` | ✅ | — |
| 4 | ForgotPasswordPage | `/auth/forgot-password` | ✅ | Shared UI |
| 5 | ResetPasswordPage | `/auth/reset-password` | ✅ | Shared UI |
| 6 | OAuthCallbackPage | `/auth/callback` | ✅ | Shared UI |
| 7 | HomePage | `/employer` | ⚠️ | InterviewKioskSection input+button row không wrap |
| 8 | FindJobPage | `/` hoặc `/jobs` | ⚠️ | FilterSidebar `grid-cols-3` cố định + main grid không collapse |
| 9 | JobDetailPage (Candidate) | `/jobs/:id` | ⚠️ | `lg:grid-cols-[1fr_360px]` không collapse |
| 10 | ApplyPage | `/jobs/:id/apply` | ⚠️ | Form grid 2-col nhưng header OK |
| 11 | TermsPage | `/terms` | ✅ | Dùng LegalPageShell |
| 12 | PrivacyPolicyPage | `/privacy` | ✅ | Dùng LegalPageShell |
| 13 | SchedulePage | `/portal/schedule/:applicationId` | ⚠️ | **Modal `max-w-2xl` không có `w-[90%]`** (P0) |
| 14 | InterviewSchedulePage | `/candidate/interviews` | ❌ | **Dark-glass styling không đồng bộ design system** |
| 15 | ApplicationsPage | `/candidate/applications` | ⚠️ | Sidebar layout không collapse |
| 16 | ApplicationDetailPage | `/candidate/applications/:id` | ⚠️ | Round selector sidebar không collapse |
| 17 | ProfilePage | `/candidate/profile` | ⚠️ | Section nav sidebar + forms không responsive |
| 18 | SavedJobsPage | `/candidate/saved-jobs` | ✅ | — |
| 19 | NotificationsPage | `/candidate/notifications` | ✅ | — |
| 20 | SettingsPage | `/candidate/settings` | ✅ | — |
| 21 | InterviewRoomPage | `/interview/room/:sessionId` | ⚠️ | Transcript panel `lg:grid-cols-[1fr_380px]` không collapse; avatar `h-44 w-44` |
| 22 | PracticeSessionPage | `/interview/practice/:applicationId` | ✅ | Full-bleed, đúng pattern |
| 23 | KioskPage | `/kiosk` | ✅ | Màn hình cố định, đúng pattern |

### 2.2. StaffSite (Super Admin)

| # | Page | Path | Status | Top issue |
|---|---|---|---|---|
| 24 | LoginPage | `/auth/login` | ✅ | — |
| 25 | RegisterPage | `/auth/register` | ✅ | — |
| 26 | SuperAdminDashboardPage | `/super-admin/dashboard` | ✅ | — |
| 27 | UsersPage | `/super-admin/users` | ⚠️ | Table overflow-x nhưng thiếu `min-w` |
| 28 | PendingUsersPage | `/super-admin/users/pending` | ✅ | — |
| 29 | AuditLogsPage | `/super-admin/audit-logs` | ✅ | — |
| 30 | SettingsPage (SA) | `/super-admin/settings` | ⚠️ | Sidebar `lg:w-64` cố định |

### 2.3. StaffSite (HR Admin)

| # | Page | Path | Status | Top issue |
|---|---|---|---|---|
| 31 | HrDashboardPage | `/hr/dashboard` | ⚠️ | `p-6` không responsive; priority cards grid; chart YAxis `width={96}` |
| 32 | PendingJobsPage | `/hr/jobs/pending` | ✅ | OK |
| 33 | JobsPage | `/hr/jobs` | ✅ | OK |
| 34 | JobPostingDetailPage | `/hr/jobs/:id` | ⚠️ | `w-36` actions cell; cover letter modal `grid-cols-2` |
| 35 | CandidatesPage | `/hr/candidates` | ✅ | OK |
| 36 | CandidateDetailPage | `/hr/candidates/:id` | ✅ | OK |
| 37 | EvaluationReviewPage | `/hr/evaluations` | ⚠️ | **`text-5xl` score**, `xl:grid-cols-[1fr_360px]` không có tablet fallback |
| 38 | ReportsPage | `/hr/reports` | ✅ | OK |
| 39 | PlaybooksPage | `/hr/playbooks` | ✅ | OK |
| 40 | TeamPage | `/hr/team` | ✅ | OK |
| 41 | InterviewSessionsPage | `/hr/interviews` | ✅ | OK |
| 42 | NotificationsPage | `/hr/notifications` | ✅ | Delegate StaffNotificationsView |
| 43 | SettingsPage (HR) | `/hr/settings` | ⚠️ | Sidebar `lg:w-64` cố định |

### 2.4. StaffSite (Recruiter)

| # | Page | Path | Status | Top issue |
|---|---|---|---|---|
| 44 | RecruiterDashboardPage | `/recruiter/dashboard` | ✅ | — |
| 45 | MyJobsPage | `/recruiter/my-jobs` | ✅ | — |
| 46 | JobDetailPage (Recruiter) | `/recruiter/my-jobs/:id` | ⚠️ | CSS grid table 7-cột thiếu wrapper, `w-36` actions |
| 47 | CreateJobPostingPage | `/recruiter/jobs/create` | ⚠️ | **Salary `grid-cols-3`** không responsive |
| 48 | JobScheduleConfigPage | `/recruiter/my-jobs/:id/schedule` | ✅ | — |
| 49 | InterviewCodePage | `/recruiter/code` | ⚠️ | Search input `max-w-md` cố định |
| 50 | CandidatesPage (Recruiter) | `/recruiter/candidates` | ⚠️ | Table thiếu `min-w` |
| 51 | CandidateDetailPage (Recruiter) | `/recruiter/candidates/:id` | ✅ | OK |
| 52 | EvaluationReviewPage (Recruiter) | `/recruiter/evaluations` | ⚠️ | Modal `grid-cols-3` header cards |
| 53 | InterviewSessionsPage (Recruiter) | `/recruiter/interviews` | ⚠️ | Session row items overflow |
| 54 | NotificationsPage (Recruiter) | `/recruiter/notifications` | ✅ | — |
| 55 | SettingsPage (Recruiter) | `/recruiter/settings` | ⚠️ | Sidebar `lg:w-64` cố định |

### 2.5. Layouts (đã audit)

| Layout | Mobile hamburger | Sidebar collapse | Status |
|---|---|---|---|
| `HrLayout` | ✅ | ✅ (`hidden lg:flex`) | ✅ |
| `RecruiterLayout` | ✅ (kế thừa) | ✅ | ✅ |
| `SuperAdminLayout` | ✅ (kế thừa) | ✅ | ✅ |
| `WorkspaceLayout` | ✅ | ✅ | ✅ |
| `CandidateLayout` | ✅ | ✅ | ✅ |
| `CandidateAppLayout` | N/A (full-width) | N/A | ✅ |
| `CandidateHeader` | ✅ | ✅ | ✅ |
| `CandidateNav` | ✅ | ✅ | ✅ |
| `CandidateFooter` | N/A | N/A | ✅ |
| `Navigation` | ✅ | ✅ | ✅ |
| `PublicNav` | ✅ | ✅ | ✅ |
| `InterviewLayout` | N/A (full-screen) | N/A | ✅ |
| `Footer` (CandidateSite) | N/A | N/A | ⚠️ P2 (1 issue) |

---

## 3. Component cần sửa

### 3.1. Theo nhóm

#### Layout (22 issues)
- `pages/candidate/InterviewSchedulePage.tsx` — Dark-glass styling (P1)
- `pages/candidate/ApplicationsPage.tsx` — `lg:grid-cols-[1fr_320px]` không collapse (P1)
- `pages/candidate/ApplicationDetailPage.tsx` — `lg:grid-cols-[320px_1fr]` (P2)
- `pages/candidate/ProfilePage.tsx` — `lg:grid-cols-[240px_1fr]` (P2)
- `pages/candidate/InterviewRoomPage.tsx` — `lg:grid-cols-[1fr_380px]` (P2)
- `pages/candidate/ApplicationDetailPage.tsx` — Round buttons (P2)
- `pages/job-board/JobDetailPage.tsx` — `lg:grid-cols-[1fr_360px]` (P2)
- `pages/landing/FindJobPage.tsx` — `lg:grid-cols-[260px_1fr]` (P1)
- `pages/hr/DashboardPage.tsx` — `p-6` cố định (P1)
- `pages/hr/DashboardPage.tsx` — Priority cards grid `lg:grid-cols-2` (P1)
- `pages/hr/EvaluationReviewPage.tsx` — `xl:grid-cols-[1fr_360px]` (P1)
- `pages/hr/JobPostingDetailPage.tsx` — `text-3xl` job title (P2)
- `pages/hr/JobPostingDetailPage.tsx` — `text-xl` header (P3)
- `pages/recruiter/JobDetailPage.tsx` — Header `text-xl` (P3)
- `pages/recruiter/JobDetailPage.tsx` — `p-6 lg:p-8` (P3)
- `pages/recruiter/JobDetailPage.tsx` — Stats `grid grid-cols-2 lg:grid-cols-5` (P3)
- `components/sections/CTA.tsx` — `py-40` (P3)
- `components/sections/Demo.tsx` — `lg:grid-cols-2` + `h-[400px]` (P3)
- `components/sections/ScrollStorytelling.tsx` — `gap-20`, `aspect-square max-w-sm` (P2)
- `components/three/AISphereDemo.tsx` — `w-64 h-64` (P3)
- `pages/landing/HomePage.tsx` — InterviewKioskSection input+button (P1)
- `app/layouts/Footer.tsx` — Brand paragraph overflow (P2)

#### Header (7 issues)
- `pages/hr/JobPostingDetailPage.tsx` — `text-3xl` job title (P2)
- `pages/hr/JobPostingDetailPage.tsx` — Edit button `px-6 py-3` (P3)
- `pages/hr/JobPostingDetailPage.tsx` — Avatar `w-20 h-20` (P3)
- `pages/recruiter/JobDetailPage.tsx` — 5 buttons in row (P1)
- `pages/recruiter/JobDetailPage.tsx` — Avatar `h-12 w-12` (P3)
- `pages/hr/EvaluationReviewPage.tsx` — Sticky `h-16` (P3)
- `pages/hr/EvaluationReviewPage.tsx` — Breadcrumb overflow (P3)

#### Sidebar (6 issues)
- `pages/landing/FindJobPage.tsx` — FilterSidebar không collapse (P1)
- `pages/candidate/ApplicationsPage.tsx` — Stats sidebar không collapse (P1)
- `pages/candidate/ProfilePage.tsx` — Section nav (P2)
- `pages/recruiter/SettingsPage.tsx` — `lg:w-64` cố định (P2)
- `pages/super-admin/SettingsPage.tsx` — `lg:w-64` cố định (P2)
- `pages/hr/SettingsPage.tsx` — `lg:w-64` (P3)

#### Navbar (0 issues) ✅

#### Dashboard (5 issues)
- `pages/hr/DashboardPage.tsx` — Priority cards `lg:grid-cols-2` (P1)
- `pages/hr/DashboardPage.tsx` — KpiCard `sm:grid-cols-2 xl:grid-cols-4` (P2)
- `pages/hr/DashboardPage.tsx` — Stats `grid-cols-2 lg:grid-cols-4` (P2)
- `pages/hr/DashboardPage.tsx` — ResponsiveGridLayout breakpoints (P2)
- `pages/hr/DashboardPage.tsx` — Priority card `text-3xl` (P3)

#### Form (8 issues)
- `pages/candidate/ProfilePage.tsx` — Experience `sm:grid-cols-2` thiếu mobile fallback (P2)
- `pages/candidate/ProfilePage.tsx` — Education `sm:grid-cols-2` thiếu mobile fallback (P2)
- `pages/recruiter/CreateJobPostingPage.tsx` — Salary `grid-cols-3` (P2)
- `pages/recruiter/InterviewCodePage.tsx` — Search `max-w-md` cố định (P2)
- `pages/hr/JobPostingDetailPage.tsx` — Filter bar `flex flex-wrap gap-4` (P2)
- `pages/hr/JobPostingDetailPage.tsx` — JD grid `md:grid-cols-2` thiếu sm (P3)
- `pages/hr/JobPostingDetailPage.tsx` — Funnel tabs thiếu overflow-x-auto (P3)
- `pages/candidate/SettingsPage.tsx` / `pages/candidate/SavedJobsPage.tsx` — Form fields (P3)

#### Table (5 issues)
- `pages/hr/JobPostingDetailPage.tsx` — Application grid table thiếu wrapper hoặc wrapper không đủ (P1)
- `pages/recruiter/JobDetailPage.tsx` — Application grid table thiếu wrapper đúng (P1)
- `pages/hr/JobPostingDetailPage.tsx` — `w-36` actions column (P1)
- `pages/recruiter/JobDetailPage.tsx` — `w-36` actions column (P1)
- `pages/recruiter/CandidatesPage.tsx` — Table thiếu `min-w` (P2)
- `pages/super-admin/UsersPage.tsx` — Table thiếu `min-w` (P2)

#### Modal (5 issues)
- `pages/candidate/SchedulePage.tsx` — Modal `max-w-2xl` không `w-[90%]` (**P0**)
- `pages/hr/JobPostingDetailPage.tsx` — Reject modal thiếu `max-h` (P2)
- `pages/hr/JobPostingDetailPage.tsx` — Cover letter modal `grid-cols-2` (P1)
- `pages/recruiter/JobDetailPage.tsx` — Cover letter modal `grid-cols-2` (P1)
- `components/profile/ChangePasswordModal.tsx` — `max-w-md` không `w-[90%]` (P3)

#### Card (1 issue)
- `pages/hr/EvaluationReviewPage.tsx` — Score `text-3xl` mobile-killer (P3)

#### Chart (2 issues)
- `pages/hr/DashboardPage.tsx` — RecruiterBarChart YAxis `width={96}` (P2)
- `pages/hr/DashboardPage.tsx` — KpiCard SVG `h-7 w-20` (P3)

#### Responsive Utility (1 issue)
- `pages/hr/EvaluationReviewPage.tsx` — Main layout không có `lg:grid-cols` mà chỉ `xl:` (P1)

#### Shared Component (5 issues)
- `app/layouts/Footer.tsx` — Brand paragraph overflow (P2)
- `ui/NotFoundPage.tsx` — `text-7xl md:text-9xl` jump (P3, cosmetic)
- `media/DeviceCheck.tsx` — OK nhưng có thể polish
- `ui/SearchableSelect.tsx` — OK
- `document/DocumentViewer.tsx` — OK

### 3.2. Theo pattern (lỗi phổ biến nhất)

| Pattern | Số file | Số lần xuất hiện | Severity cao nhất |
|---|---|---|---|
| `lg:grid-cols-[sidebar_content]` không collapse | 8 | 8 | P1 |
| `grid-cols-3` không có mobile fallback | 5 | 5 | P2 |
| Table `grid-cols-[...]` thiếu `overflow-x-auto` wrapper | 4 | 4 | P1 |
| Modal `max-w-X` không có `w-[90%]` | 3 | 3 | P0 (1) / P3 (2) |
| Avatar / Box `w-[N] h-[N]` cố định | 6 | 6 | P2 |
| `p-6` không có `sm:p-X` | 3 | 3 | P1 |
| Font `text-3xl/4xl/5xl` không responsive | 5 | 5 | P2 |
| Sidebar `lg:w-64` không collapsible | 3 | 3 | P2 |
| `py-20/32/40` quá lớn trên mobile | 2 | 2 | P3 |

---

## 4. Lỗi Responsive (chi tiết)

### Bảng tổng hợp 68 lỗi

| # | File | Component | Severity | Category | Issue Type | Root Cause (class cụ thể) | Cách khắc phục đề xuất |
|---|---|---|---|---|---|---|---|
| 1 | `pages/candidate/SchedulePage.tsx` | Modal | **P0** | Modal | Modal tràn màn hình | `max-w-2xl` không có `w-[90%]` | Thêm `w-[90%]` → `max-w-2xl w-[90%]` |
| 2 | `pages/hr/EvaluationReviewPage.tsx` | Sidebar grid | **P1** | Layout | Sidebar/drawer lỗi | `xl:grid-cols-[1fr_360px]` (xl=1280, không có lg fallback) | Đổi thành `lg:grid-cols-[1fr_360px]` |
| 3 | `pages/hr/EvaluationReviewPage.tsx` | Score card | **P1** | Card | Font-size quá nhỏ/lớn | `font-display text-5xl font-extrabold` (line ~500) | `text-4xl sm:text-5xl` |
| 4 | `pages/hr/JobPostingDetailPage.tsx` | Application table | **P1** | Table | Bảng không responsive | `grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px]` thiếu wrapper `overflow-x-auto` | Bọc trong `<div className="overflow-x-auto">` |
| 5 | `pages/recruiter/JobDetailPage.tsx` | Application table | **P1** | Table | Bảng không responsive | `grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px]` | Tương tự #4 |
| 6 | `pages/hr/JobPostingDetailPage.tsx` | Actions cell | **P1** | Table | Width cố định | `w-36 flex gap-2 shrink-0 justify-center` | `w-auto min-w-[144px]` |
| 7 | `pages/recruiter/JobDetailPage.tsx` | Actions cell | **P1** | Table | Width cố định | `w-36` | Tương tự #6 |
| 8 | `pages/hr/DashboardPage.tsx` | Page padding | **P1** | Layout | Margin/padding cố định | `min-h-screen space-y-6 bg-ink-50 p-6 dark:bg-ink-950` | `p-4 sm:p-6 lg:p-8` |
| 9 | `pages/hr/DashboardPage.tsx` | Priority cards | **P1** | Dashboard | Grid chưa responsive | `grid gap-4 lg:grid-cols-2` | `grid gap-4 md:grid-cols-2` |
| 10 | `pages/candidate/ApplicationsPage.tsx` | Main grid | **P1** | Layout | Grid không collapse | `lg:grid-cols-[1fr_320px]` không có mobile fallback | Đổi thành `flex flex-col lg:grid lg:grid-cols-[1fr_320px]` hoặc ẩn sidebar trên mobile |
| 11 | `pages/landing/FindJobPage.tsx` | Work modes | **P1** | Sidebar | Grid không responsive | `grid grid-cols-3 gap-1.5` | `grid-cols-2 sm:grid-cols-3` |
| 12 | `pages/landing/FindJobPage.tsx` | Main layout | **P1** | Sidebar | Grid không collapse | `lg:grid-cols-[260px_1fr]` không mobile fallback | Sidebar ẩn mobile + toggle button |
| 13 | `pages/landing/HomePage.tsx` | InterviewKioskSection | **P1** | Layout | Flex không wrap | `flex gap-3` input+button | `flex-col sm:flex-row gap-3` |
| 14 | `pages/candidate/InterviewSchedulePage.tsx` | Toàn page | **P1** | Layout | Design system lệch | `bg-bg-secondary text-text-primary text-accent-primary` (không tồn tại trong theme) | Refactor toàn bộ về `ink-*` `brand-*` `ai-*` |
| 15 | `pages/hr/JobPostingDetailPage.tsx` | Cover letter modal | **P1** | Modal | Grid không responsive | `grid grid-cols-2 gap-3` bên trong modal | `grid-cols-1 sm:grid-cols-2` |
| 16 | `pages/recruiter/JobDetailPage.tsx` | Cover letter modal | **P1** | Modal | Grid không responsive | `grid grid-cols-2 gap-3` | Tương tự #15 |
| 17 | `pages/hr/DashboardPage.tsx` | FunnelWidget label | P2 | Layout | Width cố định | `w-32 shrink-0` cho label | `w-20 sm:w-24 lg:w-32` |
| 18 | `pages/hr/DashboardPage.tsx` | FunnelWidget conv | P2 | Layout | Width cố định | `w-16` | `w-12 sm:w-16` |
| 19 | `pages/hr/DashboardPage.tsx` | RecruiterBarChart | P2 | Chart | Chart không co giãn | YAxis `width={96}` | `width={60}` mobile |
| 20 | `pages/hr/DashboardPage.tsx` | Candidates table | P2 | Table | Thiếu min-width | `div className="overflow-x-auto"` (không min-w) | `overflow-x-auto min-w-[600px]` |
| 21 | `pages/hr/JobPostingDetailPage.tsx` | Title | P2 | Header | Font quá lớn | `text-3xl font-semibold` | `text-2xl sm:text-3xl` |
| 22 | `pages/hr/JobPostingDetailPage.tsx` | Application grid min-w | P2 | Table | min-width cố định | `min-w-[700px]` quá rộng cho tablet | `min-w-[640px]` |
| 23 | `pages/hr/JobPostingDetailPage.tsx` | Cover letter modal grid | P2 | Modal | Grid không responsive | `grid grid-cols-2 gap-3 text-xs` bên trong modal | `grid-cols-1 sm:grid-cols-2` |
| 24 | `pages/hr/EvaluationReviewPage.tsx` | Stats grid | P2 | Dashboard | Grid thiếu sm | `grid grid-cols-2 lg:grid-cols-4` | `grid-cols-2 sm:grid-cols-2 lg:grid-cols-4` (explicit) |
| 25 | `pages/candidate/ApplicationsPage.tsx` | Stats | P2 | Dashboard | Grid thiếu variant | `grid grid-cols-2 gap-4 sm:grid-cols-4` | OK nhưng có thể polish |
| 26 | `pages/candidate/ProfilePage.tsx` | Section nav | P2 | Sidebar | Grid không collapse | `lg:grid-cols-[240px_1fr]` | Horizontal scroll nav trên mobile |
| 27 | `pages/candidate/ProfilePage.tsx` | Experience form | P2 | Form | Grid không mobile | `grid gap-3 sm:grid-cols-2` | `grid-cols-1 sm:grid-cols-2` |
| 28 | `pages/candidate/ProfilePage.tsx` | Education form | P2 | Form | Grid không mobile | `grid gap-3 rounded-xl border ... sm:grid-cols-2` | `grid-cols-1 sm:grid-cols-2` |
| 29 | `pages/candidate/ApplicationDetailPage.tsx` | Round sidebar | P2 | Layout | Grid không collapse | `lg:grid-cols-[320px_1fr]` | Horizontal tabs trên mobile |
| 30 | `pages/job-board/JobDetailPage.tsx` | Main grid | P2 | Layout | Grid không collapse | `lg:grid-cols-[1fr_360px]` | Bottom fixed bar trên mobile |
| 31 | `pages/interview/InterviewRoomPage.tsx` | Transcript grid | P2 | Layout | Grid không collapse | `flex-1 grid lg:grid-cols-[1fr_380px]` | Floating drawer mobile |
| 32 | `pages/interview/InterviewRoomPage.tsx` | Avatar | P2 | Card | Width/height cố định | `grid h-44 w-44 place-items-center rounded-full` | `h-32 w-32 sm:h-44 sm:w-44` |
| 33 | `pages/recruiter/CreateJobPostingPage.tsx` | Salary grid | P2 | Form | Grid không responsive | `grid grid-cols-3 gap-2` | `grid-cols-1 sm:grid-cols-3` |
| 34 | `pages/recruiter/InterviewCodePage.tsx` | Search | P2 | Form | Width cố định | `max-w-md` | `sm:max-w-md w-full` |
| 35 | `pages/recruiter/CandidatesPage.tsx` | Table | P2 | Table | Thiếu min-w | `<table className="w-full">` không `min-w` | `min-w-[600px]` wrapper |
| 36 | `pages/super-admin/UsersPage.tsx` | Table | P2 | Table | Thiếu min-w | `overflow-x-auto` không `min-w` | `min-w-[700px]` |
| 37 | `pages/recruiter/SettingsPage.tsx` | Sidebar | P2 | Sidebar | Width cố định | `lg:w-64 shrink-0` | `xl:w-64 xl:shrink-0` hoặc collapsible |
| 38 | `pages/super-admin/SettingsPage.tsx` | Sidebar | P2 | Sidebar | Width cố định | `lg:w-64` | Tương tự #37 |
| 39 | `components/sections/ScrollStorytelling.tsx` | Visual | P2 | Layout | Width cố định | `aspect-square max-w-sm` | `w-full max-w-xs sm:max-w-sm` |
| 40 | `components/sections/ScrollStorytelling.tsx` | Gap | P2 | Layout | Gap quá lớn | `grid lg:grid-cols-2 gap-20` | `gap-8 lg:gap-20` |
| 41 | `app/layouts/Footer.tsx` | Brand paragraph | P2 | Shared Component | Text overflow | `col-span-2 md:col-span-1` paragraph | Add `min-w-0 break-words` |
| 42 | `pages/hr/JobPostingDetailPage.tsx` | Avatar | P3 | Header | Size lớn | `w-20 h-20` | `w-16 h-16 sm:w-20 sm:h-20` |
| 43 | `pages/hr/JobPostingDetailPage.tsx` | Edit button | P3 | Header | Button padding | `px-6 py-3` | `px-4 py-2 sm:px-6 sm:py-3` |
| 44 | `pages/hr/JobPostingDetailPage.tsx` | JD grid | P3 | Layout | Thiếu sm | `grid md:grid-cols-2 gap-6` | `sm:grid-cols-2 md:grid-cols-2` |
| 45 | `pages/hr/JobPostingDetailPage.tsx` | Tabs | P3 | Layout | Thiếu overflow | `flex flex-wrap gap-2 mb-6` tabs | Wrap `overflow-x-auto` |
| 46 | `pages/hr/JobPostingDetailPage.tsx` | Funnel tabs | P3 | Layout | Thiếu overflow | tabs row | Tương tự #45 |
| 47 | `pages/hr/EvaluationReviewPage.tsx` | Sticky header | P3 | Header | Height lớn | `h-16` | `h-14 sm:h-16` |
| 48 | `pages/hr/EvaluationReviewPage.tsx` | Breadcrumb | P3 | Header | Text overflow | inline text `·` separator | `truncate` + `min-w-0` |
| 49 | `pages/hr/EvaluationReviewPage.tsx` | Language grid | P3 | Layout | 3 col chật | `grid grid-cols-3 gap-3` | `text-xs` overflow guard |
| 50 | `pages/hr/EvaluationReviewPage.tsx` | Stats value | P3 | Dashboard | Font lớn | `text-2xl font-extrabold` | `text-xl sm:text-2xl` |
| 51 | `pages/hr/DashboardPage.tsx` | SVG decoration | P3 | Chart | Size lớn | `h-7 w-20` | `h-5 w-14 sm:h-7 sm:w-20` |
| 52 | `pages/hr/DashboardPage.tsx` | Priority count | P3 | Card | Font lớn | `text-3xl font-extrabold` | `text-2xl sm:text-3xl` |
| 53 | `pages/recruiter/JobDetailPage.tsx` | Page padding | P3 | Layout | Padding hơi lớn | `p-6 lg:p-8` | `p-4 sm:p-6 lg:p-8` |
| 54 | `pages/recruiter/JobDetailPage.tsx` | Avatar | P3 | Header | Size lớn | `h-12 w-12` | `h-10 w-10 sm:h-12 sm:w-12` |
| 55 | `pages/recruiter/JobDetailPage.tsx` | Title | P3 | Header | Font lớn | `text-xl font-bold` | `text-lg sm:text-xl` |
| 56 | `pages/recruiter/JobDetailPage.tsx` | Stats value | P3 | Dashboard | Font lớn | `text-2xl font-bold` | `text-xl sm:text-2xl` |
| 57 | `pages/recruiter/JobDetailPage.tsx` | Stats grid | P3 | Dashboard | Grid thiếu sm | `grid grid-cols-2 lg:grid-cols-5` | `md:grid-cols-3 lg:grid-cols-5` |
| 58 | `pages/recruiter/JobDetailPage.tsx` | Tab strip | P3 | Layout | Thiếu overflow | tabs row | Wrap `overflow-x-auto` |
| 59 | `pages/recruiter/JobDetailPage.tsx` | Modal truncate | P3 | Modal | Truncate aggressive | `truncate block` email | `sm:truncate` |
| 60 | `pages/recruiter/EvaluationReviewPage.tsx` | Search | P3 | Form | Width cố định | `sm:max-w-xs sm:flex-1` | `sm:max-w-sm sm:flex-1` |
| 61 | `pages/recruiter/EvaluationReviewPage.tsx` | Modal header grid | P3 | Layout | 3 col chật | `grid grid-cols-3 gap-3` | `grid-cols-1 sm:grid-cols-3` |
| 62 | `pages/recruiter/EvaluationReviewPage.tsx` | Leading | P3 | Form | Line-height | `leading-7` trên `text-sm` | `leading-6` |
| 63 | `components/sections/CTA.tsx` | Padding | P3 | Layout | Padding quá lớn | `py-40` | `py-20 sm:py-32 lg:py-40` |
| 64 | `components/sections/Demo.tsx` | Height | P3 | Layout | Height cố định | `h-[400px]` | `h-[250px] sm:h-[350px] md:h-[400px]` |
| 65 | `components/sections/Demo.tsx` | Grid | P3 | Layout | Không collapse | `lg:grid-cols-2 gap-12` | `grid-cols-1 lg:grid-cols-2 gap-8 lg:gap-12` |
| 66 | `components/sections/Hero.tsx` | Min-h-screen | P3 | Layout | Video bg overlay | `min-h-screen` + absolute | `pb-safe` hoặc bottom margin |
| 67 | `components/three/AISphereDemo.tsx` | Avatar | P3 | Layout | Size cố định | `w-64 h-64` | `w-40 h-40 sm:w-64 sm:h-64` |
| 68 | `components/profile/ChangePasswordModal.tsx` | Modal | P3 | Modal | Tràn màn hình | `w-full max-w-md` | `w-[90%] max-w-md` |

---

## 5. Mức độ ưu tiên

### 5.1. P0 — Không sử dụng được trên Mobile (1 issue)

| # | File | Mô tả |
|---|---|---|
| 1 | `pages/candidate/SchedulePage.tsx` | Modal container `max-w-2xl` không có `w-[90%]` — có thể tràn ngang trên iPhone SE/màn hình 320px khi mở modal chọn lịch |

### 5.2. P1 — Layout bị vỡ (11 issues)

| # | File | Mô tả |
|---|---|---|
| 1 | `pages/hr/EvaluationReviewPage.tsx` (line 325) | `xl:grid-cols-[1fr_360px]` không có tablet fallback — sidebar mất tích trên 1024-1280px |
| 2 | `pages/hr/EvaluationReviewPage.tsx` (line ~500) | `text-5xl` trên score — quá lớn, vỡ layout mobile |
| 3 | `pages/hr/JobPostingDetailPage.tsx` | Table ứng viên thiếu `overflow-x-auto` wrapper |
| 4 | `pages/recruiter/JobDetailPage.tsx` | Table ứng viên thiếu wrapper đúng |
| 5 | `pages/hr/JobPostingDetailPage.tsx` | `w-36` actions column ép layout |
| 6 | `pages/recruiter/JobDetailPage.tsx` | `w-36` actions column ép layout |
| 7 | `pages/hr/DashboardPage.tsx` | `p-6` không responsive |
| 8 | `pages/hr/DashboardPage.tsx` | Priority cards grid thiếu mobile |
| 9 | `pages/candidate/ApplicationsPage.tsx` | `lg:grid-cols-[1fr_320px]` không collapse |
| 10 | `pages/landing/FindJobPage.tsx` | `grid-cols-3` cố định + main layout không collapse |
| 11 | `pages/landing/HomePage.tsx` | InterviewKioskSection input+button row không wrap |
| 12 | `pages/candidate/InterviewSchedulePage.tsx` | Toàn page dùng dark-glass không tồn tại trong theme |
| 13 | `pages/hr/JobPostingDetailPage.tsx` | Cover letter modal `grid-cols-2` bên trong |
| 14 | `pages/recruiter/JobDetailPage.tsx` | Cover letter modal `grid-cols-2` bên trong |

### 5.3. P2 — UI chưa đẹp (34 issues)

_(Xem bảng tổng hợp mục 4 để biết chi tiết. Tóm tắt: width cố định ở funnel/chart/avatar, grids 2-col/3-col chưa mobile-fallback, modals padding không có `w-[90%]`, sidebar settings không collapsible, font lớn ở title.)_

### 5.4. P3 — Chỉ cần tối ưu thêm (22 issues)

_(Xem bảng tổng hợp mục 4. Tóm tắt: padding `py-40/32`, leading-7 trên text-sm, font `text-2xl/3xl` chưa responsive, decorative SVG, `text-7xl md:text-9xl` jump ở NotFoundPage.)_

---

## 6. Checklist (Markdown Checklist)

> Định dạng: `- [ ] file_path :: short_description`

### Phase 1 — Lỗi nghiêm trọng (P0 + P1)

#### P0 (1)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/SchedulePage.tsx` :: Modal container thiếu `w-[90%]` → có thể tràn màn hình 320px

#### P1 Layout (5)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: `xl:grid-cols-[1fr_360px]` không có tablet fallback (line ~325)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Score `text-5xl` mobile-killer (line ~500)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Page padding `p-6` không responsive
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Priority cards grid `lg:grid-cols-2` thiếu mobile fallback
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ApplicationsPage.tsx` :: Main grid `lg:grid-cols-[1fr_320px]` không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/landing/FindJobPage.tsx` :: FilterSidebar + work modes grid không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/landing/HomePage.tsx` :: InterviewKioskSection input+button row không wrap
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/InterviewSchedulePage.tsx` :: Toàn page dark-glass không đồng bộ design system

#### P1 Table (4)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Application table thiếu `overflow-x-auto` wrapper
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Application table thiếu wrapper đúng
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Actions column `w-36` ép layout
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Actions column `w-36` ép layout

#### P1 Modal (2)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Cover letter modal `grid-cols-2` bên trong
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Cover letter modal `grid-cols-2` bên trong

### Phase 2 — Các màn hình chính (P2 + phần còn lại của P1 ở trên)

#### Sidebar (P2 — 3 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/SettingsPage.tsx` :: Sidebar `lg:w-64` không collapsible
- [ ] `ari-web/src/ARI.StaffSite/src/pages/super-admin/SettingsPage.tsx` :: Sidebar `lg:w-64` không collapsible
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Section nav sidebar không collapse

#### Form (P2 — 5 issues)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Experience form grid thiếu mobile
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Education form grid thiếu mobile
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/CreateJobPostingPage.tsx` :: Salary grid `grid-cols-3` không responsive
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/InterviewCodePage.tsx` :: Search input `max-w-md` cố định
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Filter bar overflow ngang

#### Table (P2 — 2 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/CandidatesPage.tsx` :: Table thiếu `min-w`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/super-admin/UsersPage.tsx` :: Table thiếu `min-w`

#### Modal (P2 — 1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Reject modal thiếu `max-h` + `overflow-y-auto`

#### Dashboard (P2 — 3 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: KpiCard grid thiếu variant
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Stats `grid-cols-2 lg:grid-cols-4` thiếu sm
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: ResponsiveGridLayout breakpoints không có layouts `md/sm`

#### Layout (P2 — 7 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Title `text-3xl` quá lớn
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Cover letter modal inner grid
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ApplicationDetailPage.tsx` :: Round sidebar không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/job-board/JobDetailPage.tsx` :: Sticky aside không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/interview/InterviewRoomPage.tsx` :: Transcript panel không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/interview/InterviewRoomPage.tsx` :: Avatar `h-44 w-44` cố định
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/ScrollStorytelling.tsx` :: Visual `aspect-square max-w-sm` quá nhỏ + `gap-20` lớn

#### Chart (P2 — 1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: RecruiterBarChart YAxis `width={96}`

#### Shared Component (P2 — 1 issue)
- [ ] `ari-web/src/ARI.CandidateSite/src/app/layouts/Footer.tsx` :: Brand paragraph overflow ngang 320px

### Phase 3 — Component dùng chung & Polish (P3)

#### Header (4 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Avatar `w-20 h-20` lớn
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Edit button `px-6 py-3` lớn
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Sticky header `h-16` lớn
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Breadcrumb overflow
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Avatar `h-12 w-12` lớn
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Title `text-xl`

#### Layout (5 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: JD grid `md:grid-cols-2` thiếu sm
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Tabs thiếu `overflow-x-auto`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Funnel tabs overflow
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Tab strip thiếu `overflow-x-auto`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Page padding `p-6 lg:p-8`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Stats grid `grid-cols-2 lg:grid-cols-5`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Modal header `grid-cols-3`
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/CTA.tsx` :: `py-40` quá lớn
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Demo.tsx` :: `h-[400px]` cố định
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Demo.tsx` :: Grid `lg:grid-cols-2` không collapse
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Hero.tsx` :: `min-h-screen` + absolute overlay mobile
- [ ] `ari-web/src/ARI.CandidateSite/src/components/three/AISphereDemo.tsx` :: `w-64 h-64` cố định

#### Dashboard (1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Priority count `text-3xl`

#### Chart (1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: KpiCard SVG `h-7 w-20`

#### Card (1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Priority count `text-3xl`

#### Modal (1 issue)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/profile/ChangePasswordModal.tsx` :: Modal thiếu `w-[90%]`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Email `truncate block` aggressive

#### Form (3 issues)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Language grid `grid-cols-3`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Search `sm:max-w-xs`
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Leading-7 trên text-sm

#### Shared Component (1 issue)
- [ ] `ari-web/src/ARI.Shared/src/ui/NotFoundPage.tsx` :: `text-7xl md:text-9xl` jump (cosmetic)

#### Sidebar (1 issue)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/SettingsPage.tsx` :: Sidebar `lg:w-64` (settings — ít dùng)

### Phase 4 — Tinh chỉnh UI (cải thiện thẩm mỹ chung, ngoài bug list)

- [ ] Review breakpoint Tailwind — thêm custom `xs: 480` nếu muốn optimize cho 320-425
- [ ] Refactor các page dùng `_skeletons.tsx` để đồng bộ breakpoints với page chính
- [ ] Thêm `useMediaQuery` hook shared nếu cần JS-driven responsive
- [ ] Test với Chrome DevTools mobile emulator (iPhone SE, Pixel 5, iPad)
- [ ] Test với Safari iOS (đặc biệt `100vh` issue ở InterviewRoomPage / Kiosk)
- [ ] Kiểm tra `text-text-secondary`, `text-accent-primary`, `bg-bg-secondary` còn sót ở các page khác (chỉ InterviewSchedulePage confirm lệch theme)

---

## 7. Ghi chú kỹ thuật

### 7.1. Tailwind config hiện tại

```js
// ari-web/src/ARI.Shared/tailwind-preset.cjs
theme: { extend: { ... } }  // KHÔNG có custom screens
// → Dùng defaults: sm=640, md=768, lg=1024, xl=1280, 2xl=1536
```

**Đề xuất**: Thêm `xs: '480px'` vào preset để cover 320-425px tốt hơn.

### 7.2. Pattern phổ biến cần fix

```tsx
// ❌ Sai: width cố định gây tràn
<div className="max-w-2xl">...</div>

// ✅ Đúng: thêm w-[90%] fallback
<div className="w-[90%] max-w-2xl">...</div>

// ❌ Sai: grid không có mobile fallback
<div className="grid lg:grid-cols-[1fr_320px]">...</div>

// ✅ Đúng: thêm flex-col mobile-first
<div className="flex flex-col gap-4 lg:grid lg:grid-cols-[1fr_320px] lg:gap-6">...</div>

// ❌ Sai: Table grid không có wrapper
<div className="grid-cols-[40px_1fr_110px_...]">...</div>

// ✅ Đúng: bọc overflow-x-auto + min-w
<div className="overflow-x-auto">
  <div className="min-w-[700px] grid grid-cols-[40px_1fr_110px_...]">...</div>
</div>
```

### 7.3. Files KHÔNG có vấn đề (audit clean)

| File | Ghi chú |
|---|---|
| Tất cả Layouts (Hr, Recruiter, SA, Workspace, Candidate*) | Mobile drawer đầy đủ |
| Tất cả Auth pages (Candidate + Staff) | Centered card pattern OK |
| Legal pages (Terms, Privacy) | Qua LegalPageShell |
| KioskPage | Màn hình cố định, đúng pattern |
| PracticeSessionPage | Full-bleed, đúng pattern |
| Charts trong CandidateSite sections | OK |
| PendingJobsPage, JobsPage (HR) | OK |
| CandidateDetailPage (HR + Recruiter) | OK |
| PlaybooksPage | OK |
| TeamPage | OK |
| ReportsPage | OK |
| SettingsPage (HR) | Chỉ 1 issue nhỏ |
| NotificationsPage (tất cả) | OK |
| ForgotPasswordPage, ResetPasswordPage | OK |
| OAuthCallbackPage | OK |
| Container, Button, LoadingButton, GlassCard | OK |
| DeviceCheck, DocumentViewer | OK |
| NotFoundPage (chỉ cosmetic) | OK |

---

## 8. Khuyến nghị thứ tự thực hiện

1. **Ngay lập tức (1 giờ)**: Fix P0 SchedulePage modal — 1 dòng class.
2. **PR #1 — Phase 1 P1 (1-2 ngày)**: Fix 13 issue P1 (đặc biệt EvaluationReviewPage `text-5xl`, 2 JobDetailPage tables + actions, DashboardPage padding/grid, InterviewSchedulePage refactor, FindJobPage sidebar).
3. **PR #2 — Phase 2 P2 Sidebar/Form/Table (2-3 ngày)**: Settings sidebar collapsible, salary grid, search inputs, table min-w, modals inner grids.
4. **PR #3 — Phase 2 P2 Layout/Chart (2-3 ngày)**: Title fonts, avatar sizes, sticky header heights, YAxis width.
5. **PR #4 — Phase 3 P3 (1-2 ngày)**: Polish — padding, leading, decorative SVG, search input width.
6. **PR #5 — Phase 4 (1 ngày)**: Custom `xs` breakpoint vào tailwind-preset, refactor shared `_skeletons.tsx`.

**Tổng effort ước tính**: ~8-10 ngày làm việc cho 1 người.

---

**Báo cáo được tạo bởi:** Cursor Agent
**Workspace:** `/Users/nguyenminhanh/Do An/B/ARISP/`
**Branch:** Hiện tại
**Số file đã đọc:** 168
**Số file có vấn đề:** 28
**Tổng issues:** 68 (1 P0, 13 P1, 34 P2, 22 P3 — một số issue P1 được tính gộp từ 2 file JobDetailPage có cùng pattern)