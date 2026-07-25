# Kế Hoạch Sửa Frontend — Responsive Design

> **Ngày lập:** 2026-07-24 (cập nhật 2026-07-25: thêm mục 6 — Bản đồ theo Role để dễ test)
> **Dựa trên:** `.ai/responsive-audit-2026-07-24.md`
> **Nguyên tắc:** Chia 4 Phase — fix theo thứ tự ưu tiên, không sửa code trong giai đoạn lập kế hoạch.

---

## 0. Tổng quan kế hoạch

### 0.1. Effort tổng thể

| Phase | Mô tả | Số file | Effort | Thời gian |
|---|---|---|---|---|
| **Phase 1** | Lỗi nghiêm trọng nhất (P0 + P1) | **10 file** | ~28 giờ | 3-4 ngày |
| **Phase 2** | Màn hình chính (P2 + P1 còn lại) | **16 file** | ~40 giờ | 5-6 ngày |
| **Phase 3** | Component dùng chung (P3) | **11 file** | ~14 giờ | 2 ngày |
| **Phase 4** | Tinh chỉnh UI + Tailwind config | **5 file** | ~8 giờ | 1 ngày |
| **TỔNG** | | **~30 file** | **~90 giờ** | **~11-13 ngày** |

### 0.2. Thứ tự ưu tiên tổng

```
P0 (1) → P1 (11) → P2 (34) → P3 (22)  =  68 issues
       └─ Phase 1 ─┘
                      └────── Phase 2 ──────┘
                                          └── Phase 3 ──┘
                                                    └ Phase 4 ┘
```

### 0.3. Mức độ khó & rủi ro định nghĩa

| Mức | Định nghĩa | Ví dụ |
|---|---|---|
| **Dễ** | Đổi 1-2 class, không ảnh hưởng logic | `p-6` → `p-4 sm:p-6` |
| **Trung bình** | Đổi layout có thể cần test lại ở nhiều breakpoint | Table grid + overflow wrapper |
| **Khó** | Refactor component lớn hoặc thêm state/logic mới | Sidebar collapsible với drawer |
| **Rủi ro cao** | Đụng design system, ảnh hưởng nhiều page | Refactor InterviewSchedulePage |

### 0.4. Trạng thái thực thi (cập nhật 2026-07-25)

| Phase / Nhóm | Trạng thái | Ghi chú |
|---|---|---|
| Phase 1 | ✅ Đã commit (16 commits fix) | Hoàn thành |
| Phase 2 Nhóm A — Sidebar & Settings | ✅ Đã commit (5 files) | Hoàn thành |
| Phase 2 Nhóm A bổ sung — ProfilePage iOS auto-shrink | ✅ Đã commit | Fix text thu nhỏ 320px |
| **Phase 2 Nhóm B — Form & Input** | 🔜 **Đang thực thi** | Tiếp theo |
| Phase 2 Nhóm C — Table | ⏳ Backlog | |
| Phase 2 Nhóm D — Layout phức tạp | ⏳ Backlog | |
| Phase 2 Nhóm E — Chart & Modal nhỏ | ⏳ Backlog | |
| Phase 3 | ⏳ Backlog | |
| Phase 4 | ⏳ Backlog | |

---

## 1. Gom lỗi theo nhóm

> Tổng hợp từ báo cáo audit, dùng làm input cho Phase.

### 1.1. Layout (22 issues)

| Severity | Số | Files |
|---|---|---|
| P0 | 0 | — |
| P1 | 5 | EvaluationReviewPage, DashboardPage×2, ApplicationsPage, FindJobPage, HomePage, InterviewSchedulePage |
| P2 | 12 | ApplicationDetailPage, ProfilePage, InterviewRoomPage×2, JobDetailPage(Candidate), CreateJobPostingPage, ScrollStorytelling×2, JobDetailPage(Recruiter)×2, Footer, JobPostingDetailPage×2 |
| P3 | 5 | JD grid, tabs overflow×3, padding tweaks |

### 1.2. Header (7 issues)

| Severity | Số | Files |
|---|---|---|
| P0/P1 | 0 | — |
| P2 | 3 | JobPostingDetailPage title, Recruiter JobDetailPage buttons, Recruiter JobDetailPage title |
| P3 | 4 | Avatars×2, button padding, sticky header, breadcrumb |

### 1.3. Sidebar (6 issues)

| Severity | Số | Files |
|---|---|---|
| P1 | 2 | FindJobPage FilterSidebar, ApplicationsPage stats sidebar |
| P2 | 3 | ProfilePage section nav, Recruiter SettingsPage, Super Admin SettingsPage |
| P3 | 1 | HR SettingsPage |

### 1.4. Navbar (0 issues) ✅
Tất cả layouts đã có mobile drawer đầy đủ.

### 1.5. Dashboard (5 issues)

| Severity | Số | Files |
|---|---|---|
| P1 | 1 | HrDashboardPage priority cards |
| P2 | 3 | KpiCard grid, stats grid, ResponsiveGridLayout breakpoints |
| P3 | 1 | Priority count font |

### 1.6. Form (8 issues)

| Severity | Số | Files |
|---|---|---|
| P2 | 5 | ProfilePage×2, CreateJobPostingPage, InterviewCodePage, JobPostingDetailPage filter |
| P3 | 3 | Language grid, search input, leading-7 |

### 1.7. Table (5 issues)

| Severity | Số | Files |
|---|---|---|
| P1 | 4 | HrJobPostingDetailPage table + actions, RecruiterJobDetailPage table + actions |
| P2 | 3 | HrDashboardPage candidates table, RecruiterCandidatesPage, SuperAdminUsersPage |

### 1.8. Modal (5 issues)

| Severity | Số | Files |
|---|---|---|
| **P0** | **1** | **SchedulePage** (Candidate) |
| P1 | 2 | Hr/Recruiter JobDetailPage cover letter modals |
| P2 | 1 | HrJobPostingDetailPage reject modal |
| P3 | 1 | ChangePasswordModal |

### 1.9. Card (1 issue)
| P3 | 1 | EvaluationReviewPage score font |

### 1.10. Chart (2 issues)
| P2 | 1 | RecruiterBarChart YAxis |
| P3 | 1 | KpiCard SVG decoration |

### 1.11. Responsive Utility (1 issue)
| P1 | 1 | EvaluationReviewPage main layout breakpoint |

### 1.12. Shared Component (5 issues)
| P2 | 1 | Footer brand paragraph |
| P3 | 4 | NotFoundPage text-7xl/9xl, SearchableSelect, DeviceCheck, DocumentViewer (cosmetic) |

---

## 2. Phân loại mức độ ưu tiên

### 2.1. P0 — Không sử dụng được trên Mobile (1 issue)

| # | File | Issue |
|---|---|---|
| 1 | `pages/candidate/SchedulePage.tsx` | Modal `max-w-2xl` thiếu `w-[90%]` → tràn 320px |

### 2.2. P1 — Layout bị vỡ (11 issues)

| # | File | Issue |
|---|---|---|
| 1 | `pages/hr/EvaluationReviewPage.tsx` | `xl:grid-cols-[1fr_360px]` không có tablet fallback |
| 2 | `pages/hr/EvaluationReviewPage.tsx` | Score `text-5xl` mobile-killer |
| 3 | `pages/hr/JobPostingDetailPage.tsx` | Application table thiếu wrapper |
| 4 | `pages/recruiter/JobDetailPage.tsx` | Application table thiếu wrapper |
| 5 | `pages/hr/JobPostingDetailPage.tsx` | `w-36` actions column |
| 6 | `pages/recruiter/JobDetailPage.tsx` | `w-36` actions column |
| 7 | `pages/hr/DashboardPage.tsx` | `p-6` padding cố định |
| 8 | `pages/hr/DashboardPage.tsx` | Priority cards grid thiếu mobile |
| 9 | `pages/candidate/ApplicationsPage.tsx` | Main grid không collapse |
| 10 | `pages/landing/FindJobPage.tsx` | FilterSidebar + main grid |
| 11 | `pages/landing/HomePage.tsx` | InterviewKioskSection input+button row |
| 12 | `pages/candidate/InterviewSchedulePage.tsx` | Toàn page dark-glass lệch design system |
| 13 | `pages/hr/JobPostingDetailPage.tsx` | Cover letter modal `grid-cols-2` |
| 14 | `pages/recruiter/JobDetailPage.tsx` | Cover letter modal `grid-cols-2` |

### 2.3. P2 — UI chưa đẹp (34 issues)
_(Xem bảng chi tiết trong báo cáo audit mục 4 — bao gồm widths cố định, font lớn, settings sidebar không collapsible, salary grid 3-col, chart YAxis, table thiếu min-w, etc.)_

### 2.4. P3 — Chỉ cần tối ưu thêm (22 issues)
_(Xem báo cáo audit mục 4 — padding `py-40`, decorative SVG, leading-7, avatar sizes, NotFoundPage cosmetic.)_

---

## 3. Kế hoạch theo Phase

---

### 🔴 PHASE 1 — Lỗi nghiêm trọng nhất (P0 + P1)

> **Mục tiêu:** Fix toàn bộ P0 (1) + P1 (11) → đảm bảo layout không bị vỡ và ứng dụng dùng được trên mobile.
> **Số file:** 10 file (5 page có thể chia PR riêng)
> **Ước lượng:** ~28 giờ (~3-4 ngày)
> **PR đề xuất:** 2 PR

#### 3.1.1. File cần sửa

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 1 | `pages/candidate/SchedulePage.tsx` | Modal `max-w-2xl` thiếu `w-[90%]` (P0) | `CandidateSchedulePage` modal container | **Dễ** | **Thấp** — chỉ thêm 1 class |
| 2 | `pages/hr/JobPostingDetailPage.tsx` | Table thiếu wrapper + `w-36` actions + cover letter modal grid | `JobPostingDetailPage` table + cover letter modal | **Trung bình** | **Trung bình** — ảnh hưởng hiển thị ứng viên |
| 3 | `pages/recruiter/JobDetailPage.tsx` | Table thiếu wrapper + `w-36` actions + cover letter modal grid | `JobDetailPage` table + cover letter modal | **Trung bình** | **Trung bình** — tương tự HR |
| 4 | `pages/hr/EvaluationReviewPage.tsx` | `xl:` → `lg:` + `text-5xl` → `text-4xl sm:text-5xl` | `EvaluationReviewPage` sidebar + score card | **Dễ** | **Trung bình** — chạm score lớn, dễ regression |
| 5 | `pages/hr/DashboardPage.tsx` | `p-6` → `p-4 sm:p-6 lg:p-8` + priority grid thêm `md:` | `HrDashboardPage` root + priority section | **Dễ** | **Thấp** |
| 6 | `pages/candidate/ApplicationsPage.tsx` | Main grid collapse + sidebar ẩn mobile | `ApplicationsPage` main grid + sidebar | **Khó** | **Cao** — cần thêm state/toggle để collapse sidebar |
| 7 | `pages/landing/FindJobPage.tsx` | Work modes grid + main layout sidebar | `FindJobPage` FilterSidebar + main grid | **Khó** | **Cao** — sidebar là tính năng chính của trang tìm việc |
| 8 | `pages/landing/HomePage.tsx` | InterviewKioskSection input+button row wrap | `InterviewKioskSection` flex container | **Dễ** | **Thấp** |
| 9 | `pages/candidate/InterviewSchedulePage.tsx` | Refactor toàn page từ dark-glass → design system ink/brand/ai | `InterviewSchedulePage` toàn bộ | **Khó** | **Rất cao** — chạm design system, có thể ảnh hưởng branding |

#### 3.1.2. Component liên quan (cross-cutting)

| Component | Vai trò | Phase 1 đụng tới |
|---|---|---|
| `StatCard` / `StatsGrid` (shared) | KPI cards trên Dashboard | DashboardPage priority cards |
| `PageHeader` (shared) | Page title + meta | HR JobPostingDetailPage title |
| Modal pattern (`fixed inset-0` + content) | Dùng ở nhiều page | SchedulePage, JobPostingDetailPage×2 |
| Sidebar pattern (desktop) | FilterSidebar | FindJobPage, ApplicationsPage |

#### 3.1.3. Rủi ro tổng hợp

| Rủi ro | Mức | Biện pháp giảm thiểu |
|---|---|---|
| Refactor InterviewSchedulePage phá design | Rất cao | Snapshot trước khi sửa, QA trên 4 breakpoint 320/768/1024/1440 |
| Sidebar collapse trên FindJobPage mất chức năng filter | Cao | Implement dưới dạng drawer có thể đóng/mở, giữ nguyên state |
| Bảng 7-cột scroll ngang khó dùng mobile | Trung bình | Cân nhắc hiển thị dạng card list trên mobile (ngoài phạm vi Phase 1, xếp Phase 2) |
| Test trên iOS Safari chưa có thiết bị thật | Trung bình | Dùng Chrome DevTools mobile + BrowserStack |

#### 3.1.4. Checklist Phase 1

**PR #1.1 — Fix nhanh P0 + P1 đơn giản (~8 giờ)**
- [x] `ari-web/src/ARI.CandidateSite/src/pages/candidate/SchedulePage.tsx` :: Thêm `w-[90%]` cho modal container (Dễ, 5 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Đổi `xl:grid-cols-[1fr_360px]` → `lg:grid-cols-[1fr_360px]` (Dễ, 10 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Đổi `text-5xl` score → `text-4xl sm:text-5xl` (Dễ, 5 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Đổi page padding `p-6` → `p-4 sm:p-6 lg:p-8` (Dễ, 5 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Priority cards grid thêm `md:grid-cols-2` (Dễ, 10 phút)
- [x] `ari-web/src/ARI.CandidateSite/src/pages/landing/HomePage.tsx` :: InterviewKioskSection input+button row → `flex-col sm:flex-row` (Dễ, 15 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Application table thêm wrapper `overflow-x-auto min-w-[700px]` (Trung bình, 30 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Actions column `w-36` → `w-auto min-w-[144px]` (Dễ, 10 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Cover letter modal inner grid `grid-cols-2` → `grid-cols-1 sm:grid-cols-2` (Dễ, 10 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Application table thêm wrapper tương tự HR (Trung bình, 30 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Actions column `w-36` (Dễ, 10 phút)
- [x] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Cover letter modal inner grid (Dễ, 10 phút)

**PR #1.2 — Refactor layout phức tạp P1 (~20 giờ)**
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ApplicationsPage.tsx` :: Collapse sidebar trên mobile (Khó, 6 giờ — cần thêm state + drawer)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/landing/FindJobPage.tsx` :: FilterSidebar collapse + work modes grid (Khó, 8 giờ — sidebar là tính năng chính)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/InterviewSchedulePage.tsx` :: Refactor toàn page sang design system ink/brand/ai (Khó, 6 giờ — chạm design system)

---

### 🟠 PHASE 2 — Các màn hình chính (P2 + P1 còn lại)

> **Mục tiêu:** Hoàn thiện responsive cho các trang chính của ứng viên & staff (Detail pages, Settings, Form phức tạp, Dashboard polish, Tables).
> **Số file:** 16 file
> **Ước lượng:** ~40 giờ (~5-6 ngày)
> **PR đề xuất:** 3 PR theo nhóm (Sidebar/Form/Table | Layout/Chart/Shared | Modal/Dashboard)

#### 3.2.1. File cần sửa (theo nhóm con)

**Nhóm A — Sidebar & Settings (~12 giờ)** ✅ Đã xong

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 1 | `pages/recruiter/SettingsPage.tsx` | Sidebar `lg:w-64` cố định không collapsible | SettingsPage tab sidebar | **Khó** | Trung bình |
| 2 | `pages/super-admin/SettingsPage.tsx` | Sidebar `lg:w-64` cố định không collapsible | SettingsPage tab sidebar | **Khó** | Trung bình |
| 3 | `pages/hr/SettingsPage.tsx` | Sidebar `lg:w-64` (settings ít dùng) | SettingsPage tab sidebar | Trung bình | Thấp |
| 4 | `pages/candidate/ProfilePage.tsx` | Section nav sidebar không collapse | ProfilePage nav | Khó | Trung bình |
| 5 | `pages/candidate/ApplicationDetailPage.tsx` | Round buttons sidebar không collapse | ApplicationDetailPage round sidebar | Trung bình | Trung bình |
| 6 | `pages/candidate/InterviewRoomPage.tsx` | Transcript panel không collapse (chuyển thành floating drawer) | InterviewRoomPage layout | Khó | Cao — phải test với WebRTC |

**Nhóm B — Form & Input (~10 giờ)** 🔜 Đang thực thi

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 7 | `pages/candidate/ProfilePage.tsx` | Experience form grid `sm:grid-cols-2` thiếu mobile | ProfilePage Experience form | Dễ | Thấp |
| 8 | `pages/candidate/ProfilePage.tsx` | Education form grid tương tự | ProfilePage Education form | Dễ | Thấp |
| 9 | `pages/recruiter/CreateJobPostingPage.tsx` | Salary `grid-cols-3` không responsive | CreateJobPosting salary grid | Dễ | Thấp |
| 10 | `pages/recruiter/InterviewCodePage.tsx` | Search input `max-w-md` cố định | InterviewCodePage search bar | Dễ | Thấp |
| 11 | `pages/hr/JobPostingDetailPage.tsx` | Filter bar `flex flex-wrap gap-4` overflow | JobPostingDetailPage filter bar | Trung bình | Thấp |

**Nhóm C — Table (~6 giờ)**

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 12 | `pages/hr/DashboardPage.tsx` | Candidates table thiếu `min-w` | DashboardPage table wrapper | Dễ | Thấp |
| 13 | `pages/recruiter/CandidatesPage.tsx` | Table cell text overflow | CandidatesPage table | Trung bình | Thấp |
| 14 | `pages/super-admin/UsersPage.tsx` | Table thiếu `min-w` | UsersPage table | Dễ | Thấp |

**Nhóm D — Layout phức tạp (~8 giờ)**

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 15 | `pages/candidate/ApplicationDetailPage.tsx` | Round sidebar (đã liệt kê ở nhóm A) | — | — | — |
| 16 | `pages/job-board/JobDetailPage.tsx` | Sticky aside không collapse (chuyển thành bottom fixed bar) | JobDetailPage sticky card | Khó | Trung bình |
| 17 | `pages/interview/InterviewRoomPage.tsx` | Avatar `h-44 w-44` cố định (đã liệt kê) | — | — | — |
| 18 | `components/sections/ScrollStorytelling.tsx` | Visual `aspect-square max-w-sm` + `gap-20` | ScrollStorytelling sections | Dễ | Thấp |

**Nhóm E — Chart & Modal nhỏ (~4 giờ)**

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 19 | `pages/hr/DashboardPage.tsx` | RecruiterBarChart YAxis `width={96}` | DashboardPage chart | Dễ | Thấp |
| 20 | `pages/hr/JobPostingDetailPage.tsx` | Reject modal thiếu `max-h overflow-y-auto` | JobPostingDetailPage reject modal | Dễ | Thấp |

#### 3.2.2. Component liên quan (cross-cutting)

| Component | Vai trò | Phase 2 đụng tới |
|---|---|---|
| `SearchableSelect` (shared) | Dropdown search | CreateJobPostingPage, InterviewCodePage |
| `StatsGrid` (shared) | Dashboard stats | SettingsPage sidebars (nếu refactor) |
| Form input pattern | Tất cả forms | ProfilePage, CreateJobPostingPage |
| Table pattern (`overflow-x-auto`) | Tất cả tables | CandidatesPage, UsersPage |
| Sidebar pattern | Settings pages | SettingsPage×3, ProfilePage, ApplicationDetailPage, InterviewRoomPage |
| WebRTC layout | Interview | InterviewRoomPage (rủi ro cao) |

#### 3.2.3. Rủi ro tổng hợp

| Rủi ro | Mức | Biện pháp giảm thiểu |
|---|---|---|
| Settings sidebar collapse → mất UX quen thuộc | Trung bình | A/B test hoặc giữ desktop mode mặc định |
| ApplicationDetailPage round sidebar → mất navigation nhanh | Trung bình | Dùng horizontal scroll tabs thay vì collapse hẳn |
| InterviewRoomPage WebRTC bị ảnh hưởng | Cao | Test với thiết bị thật + Camera/Mic permission flow |
| JobDetailPage (Candidate) sticky card → bottom bar che footer | Trung bình | Giữ khoảng cách an toàn + dismiss button |

#### 3.2.4. Checklist Phase 2

**PR #2.1 — Sidebar & Settings (~12 giờ)** ✅ Đã xong
- [x] `ari-web/src/ARI.StaffSite/src/pages/recruiter/SettingsPage.tsx` :: Sidebar collapse với drawer hoặc `xl:w-64` (Khó, 4 giờ)
- [x] `ari-web/src/ARI.StaffSite/src/pages/super-admin/SettingsPage.tsx` :: Sidebar tương tự (Khó, 4 giờ)
- [x] `ari-web/src/ARI.StaffSite/src/pages/hr/SettingsPage.tsx` :: Sidebar `xl:w-64` đơn giản (Trung bình, 1 giờ)
- [x] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Section nav → horizontal scroll (Khó, 2 giờ)
- [x] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ApplicationDetailPage.tsx` :: Round sidebar → horizontal scroll tabs (Trung bình, 1 giờ)
- [x] `ari-web/src/ARI.CandidateSite/src/pages/interview/InterviewRoomPage.tsx` :: Transcript panel → floating drawer (Khó, 4 giờ — test với WebRTC)

**PR #2.1 bổ sung — iOS Safari auto-shrink fix** ✅ Đã xong
- [x] `ari-web/src/ARI.Shared/src/styles/index.css` :: Thêm `-webkit-text-size-adjust: 100%` cho `html`/`body`
- [x] `ari-web/src/ARI.CandidateSite/index.html` :: Thêm `viewport-fit=cover, maximum-scale=5.0`
- [x] `ari-web/src/ARI.StaffSite/index.html` :: Thêm `viewport-fit=cover, maximum-scale=5.0`
- [x] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Nav pills `w-max min-w-full` + ẩn scrollbar + `shrink-0` cho mỗi pill

**PR #2.2 — Form & Input (~10 giờ)** 🔜 Đang thực thi
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Experience form `grid-cols-1 sm:grid-cols-2` (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/candidate/ProfilePage.tsx` :: Education form tương tự (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/CreateJobPostingPage.tsx` :: Salary grid `grid-cols-1 sm:grid-cols-3` (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/InterviewCodePage.tsx` :: Search input responsive (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Filter bar overflow-x-auto + responsive (Trung bình, 2 giờ)

**PR #2.3 — Table, Layout & Chart (~18 giờ)**
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Candidates table `min-w-[600px]` (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/CandidatesPage.tsx` :: Table cell text overflow + `min-w-[600px]` (Trung bình, 2 giờ)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/super-admin/UsersPage.tsx` :: Table `min-w-[700px]` (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/pages/job-board/JobDetailPage.tsx` :: Sticky aside → bottom fixed bar mobile (Khó, 4 giờ)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/ScrollStorytelling.tsx` :: Visual size + gap responsive (Dễ, 1 giờ)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: RecruiterBarChart YAxis `width={60}` (Dễ, 30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Reject modal `max-h-[90vh] overflow-y-auto` (Dễ, 30 phút)

---

### 🟡 PHASE 3 — Component dùng chung (P3)

> **Mục tiêu:** Polish các component Header, Avatar, padding, decorative elements. Cải thiện thẩm mỹ mà không phá logic.
> **Số file:** 11 file
> **Ước lượng:** ~14 giờ (~2 ngày)
> **PR đề xuất:** 2 PR (HR pages polish | Candidate pages polish)

#### 3.3.1. File cần sửa

**Nhóm A — HR/Recruiter pages polish (~7 giờ)**

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 1 | `pages/hr/JobPostingDetailPage.tsx` | Avatar `w-20 h-20`, button `px-6 py-3`, JD grid `sm:`, tabs overflow | Header + tabs | Dễ | Thấp |
| 2 | `pages/hr/JobPostingDetailPage.tsx` | Funnel tabs overflow | Tabs row | Dễ | Thấp |
| 3 | `pages/hr/EvaluationReviewPage.tsx` | Sticky header `h-14 sm:h-16`, breadcrumb `truncate` | Header | Dễ | Thấp |
| 4 | `pages/hr/EvaluationReviewPage.tsx` | Language grid `grid-cols-3`, stats value font | Dashboard | Dễ | Thấp |
| 5 | `pages/recruiter/JobDetailPage.tsx` | Page padding, avatar, title, stats value | Header + Dashboard | Dễ | Thấp |
| 6 | `pages/recruiter/JobDetailPage.tsx` | Stats grid `md:`, tabs overflow, modal truncate | Layout + Modal | Dễ | Thấp |
| 7 | `pages/recruiter/EvaluationReviewPage.tsx` | Search `sm:max-w-sm`, modal header grid, leading-7 | Form + Modal | Dễ | Thấp |
| 8 | `pages/hr/DashboardPage.tsx` | Funnel conv `w-12 sm:w-16`, SVG `h-5 w-14 sm:`, priority count | Dashboard | Dễ | Thấp |

**Nhóm B — Candidate pages polish (~7 giờ)**

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 9 | `components/sections/CTA.tsx` | `py-40` → `py-20 sm:py-32 lg:py-40` | Section | Dễ | Thấp |
| 10 | `components/sections/Demo.tsx` | `h-[400px]` + `lg:grid-cols-2` không collapse | Section | Dễ | Thấp |
| 11 | `components/sections/Hero.tsx` | `min-h-screen` + safe area overlay | Section | Dễ | Thấp |
| 12 | `components/three/AISphereDemo.tsx` | `w-64 h-64` cố định | 3D component | Dễ | Thấp |
| 13 | `components/profile/ChangePasswordModal.tsx` | Modal `w-[90%]` | Modal | Dễ | Thấp |
| 14 | `app/layouts/Footer.tsx` | Brand paragraph `min-w-0 break-words` | Footer (Candidate) | Dễ | Thấp |
| 15 | `ui/NotFoundPage.tsx` | `text-7xl md:text-9xl` jump (cosmetic) | NotFoundPage | Dễ | Thấp |

#### 3.3.2. Component liên quan

| Component | Vai trò | Phase 3 đụng tới |
|---|---|---|
| `Header pattern` (HR pages) | Page header | JobPostingDetailPage, EvaluationReviewPage |
| `Avatar pattern` | Avatar/icon box | JobPostingDetailPage, EvaluationReviewPage, JobDetailPage |
| Landing sections | Hero/Demo/CTA/Scroll | CTA, Demo, Hero, ScrollStorytelling, AISphereDemo |
| Modal pattern | Modal container | ChangePasswordModal |

#### 3.3.3. Rủi ro tổng hợp

| Rủi ro | Mức | Biện pháp giảm thiểu |
|---|---|---|
| Hero `min-h-screen` cản trở iOS Safari | Thấp | Thêm `pb-safe` hoặc margin-bottom đủ lớn |
| 3D component resize làm re-render Three.js | Thấp | Wrap trong memo hoặc chỉ resize khi cần |

#### 3.3.4. Checklist Phase 3

**PR #3.1 — HR/Recruiter pages polish (~7 giờ)**
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Avatar `w-16 h-16 sm:w-20 sm:h-20` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Edit button `px-4 py-2 sm:px-6 sm:py-3` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: JD grid `sm:grid-cols-2 md:grid-cols-2` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Funnel tabs overflow-x-auto (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/JobPostingDetailPage.tsx` :: Tabs overflow-x-auto (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Sticky header `h-14 sm:h-16` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Breadcrumb truncate + min-w-0 (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Language grid text-xs guard (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/EvaluationReviewPage.tsx` :: Stats value `text-xl sm:text-2xl` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Funnel conv `w-12 sm:w-16` (10 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: SVG `h-5 w-14 sm:h-7 sm:w-20` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/DashboardPage.tsx` :: Priority count `text-2xl sm:text-3xl` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Page padding `p-4 sm:p-6 lg:p-8` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Avatar `h-10 w-10 sm:h-12 sm:w-12` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Title `text-lg sm:text-xl` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Stats value `text-xl sm:text-2xl` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Stats grid `md:grid-cols-3 lg:grid-cols-5` (10 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Tab strip overflow-x-auto (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/JobDetailPage.tsx` :: Modal email `sm:truncate` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Search `sm:max-w-sm sm:flex-1` (5 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Modal header `grid-cols-1 sm:grid-cols-3` (10 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/EvaluationReviewPage.tsx` :: Leading-7 → leading-6 (5 phút)

**PR #3.2 — Candidate pages polish (~7 giờ)**
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/CTA.tsx` :: `py-20 sm:py-32 lg:py-40` (10 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Demo.tsx` :: `h-[250px] sm:h-[350px] md:h-[400px]` (10 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Demo.tsx` :: Grid `grid-cols-1 lg:grid-cols-2 gap-8 lg:gap-12` (10 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/sections/Hero.tsx` :: Safe area + bottom margin (30 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/three/AISphereDemo.tsx` :: `w-40 h-40 sm:w-64 sm:h-64` (10 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/components/profile/ChangePasswordModal.tsx` :: `w-[90%] max-w-md` (5 phút)
- [ ] `ari-web/src/ARI.CandidateSite/src/app/layouts/Footer.tsx` :: Brand `min-w-0 break-words` (10 phút)
- [ ] `ari-web/src/ARI.Shared/src/ui/NotFoundPage.tsx` :: Smooth font scale (15 phút)

---

### 🟢 PHASE 4 — Tinh chỉnh UI & Tailwind Config

> **Mục tiêu:** Hoàn thiện design system responsive, thêm custom breakpoint `xs` cho 320-425, polish design tokens.
> **Số file:** 5 file (config + shared + test setup)
> **Ước lượng:** ~8 giờ (~1 ngày)
> **PR đề xuất:** 1 PR

#### 3.4.1. File cần sửa

| # | File | Issue chính | Component | Độ khó | Rủi ro |
|---|---|---|---|---|---|
| 1 | `tailwind-preset.cjs` | Thêm custom `xs: '480px'` breakpoint | Config | Dễ | Thấp |
| 2 | `tailwind-preset.cjs` | Audit lại colors ink/brand/ai (loại bỏ `bg-bg-secondary` etc.) | Config | Dễ | Thấp |
| 3 | `shared` components (Container, Button) | Polish responsive defaults | Shared UI | Trung bình | Thấp |
| 4 | `_skeletons.tsx` (HR + Recruiter + SA) | Đồng bộ breakpoints với page chính | Skeletons | Trung bình | Thấp |
| 5 | Test setup (DevTools profile, BrowserStack) | Tạo test matrix 8 breakpoints × critical pages | QA | Trung bình | Thấp |

#### 3.4.2. Component liên quan

| Component | Vai trò | Phase 4 đụng tới |
|---|---|---|
| `tailwind-preset.cjs` | Theme chung | Cả 3 site |
| `_skeletons.tsx` (×3) | Loading state | HR, Recruiter, Super Admin |
| `Container` (shared) | Layout wrapper | Có thể thêm padding responsive mặc định |

#### 3.4.3. Rủi ro tổng hợp

| Rủi ro | Mức | Biện pháp giảm thiểu |
|---|---|---|
| Thêm `xs` breakpoint làm vỡ utility khác | Thấp | Đặt dưới `sm` (480 < 640), test các page dùng `xs:` |
| Skeletons khác page gây layout shift | Thấp | Đồng bộ grid breakpoints |

#### 3.4.4. Checklist Phase 4

- [ ] `ari-web/src/ARI.Shared/tailwind-preset.cjs` :: Thêm `xs: '480px'` vào `theme.extend.screens` (10 phút)
- [ ] `ari-web/src/ARI.Shared/tailwind-preset.cjs` :: Kiểm tra colors theme có khớp design tokens (30 phút)
- [ ] `ari-web/src/ARI.Shared/src/ui/Container.tsx` :: Verify padding responsive `px-6 sm:px-8 lg:px-12` (15 phút)
- [ ] `ari-web/src/ARI.Shared/src/ui/Button.tsx` :: Verify size variants không quá lớn mobile (15 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/hr/_skeletons.tsx` :: Đồng bộ breakpoints với `HrDashboardPage` (30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/recruiter/_skeletons.tsx` :: Đồng bộ với `RecruiterDashboardPage` (30 phút)
- [ ] `ari-web/src/ARI.StaffSite/src/pages/super-admin/_skeletons.tsx` :: Đồng bộ với `SuperAdminDashboardPage` (30 phút)
- [ ] Tạo test matrix responsive cho 8 breakpoints × critical pages (2 giờ)
- [ ] Verify trên iOS Safari + Android Chrome (qua BrowserStack hoặc thiết bị thật) (2 giờ)
- [ ] Verify dark/light mode không vỡ ở các breakpoint mới (30 phút)

---

## 4. Tổng kết effort

### 4.1. Theo Phase

| Phase | File | Effort | Ngày | PR |
|---|---|---|---|---|
| Phase 1 | 10 | 28h | 3-4 | 2 |
| Phase 2 | 16 | 40h | 5-6 | 3 |
| Phase 3 | 11 | 14h | 2 | 2 |
| Phase 4 | 5 | 8h | 1 | 1 |
| **TỔNG** | **~30 (unique)** | **~90h** | **11-13** | **8** |

> **Lưu ý:** Một số file xuất hiện ở nhiều Phase (vd: `pages/hr/DashboardPage.tsx` ở Phase 1+2+3), nhưng đếm unique thì ~30 file.

### 4.2. Theo độ khó

| Độ khó | Số issues | % |
|---|---|---|
| Dễ | ~50 | 73% |
| Trung bình | ~12 | 18% |
| Khó | ~6 | 9% |

### 4.3. Theo rủi ro

| Rủi ro | Số issues | Ghi chú |
|---|---|---|
| Thấp | ~58 | Class swap đơn giản |
| Trung bình | ~7 | Cần test kỹ |
| Cao | ~3 | WebRTC, design system, sidebar collapse |
| Rất cao | ~1 | InterviewSchedulePage refactor |

### 4.4. Theo component

| Component | Issues | Phase chính |
|---|---|---|
| `pages/candidate/*` | ~13 | 1, 2, 3 |
| `pages/hr/*` | ~18 | 1, 2, 3 |
| `pages/recruiter/*` | ~14 | 1, 2, 3 |
| `pages/super-admin/*` | ~3 | 2 |
| `pages/landing/*` | ~4 | 1, 2 |
| `pages/interview/*` | ~2 | 2 |
| `pages/job-board/*` | ~1 | 2 |
| `components/sections/*` | ~3 | 3 |
| `components/three/*` | ~1 | 3 |
| `components/profile/*` | ~1 | 3 |
| `layouts/Footer.tsx` | ~1 | 3 |
| `tailwind-preset.cjs` | ~1 | 4 |
| `_skeletons.tsx` | ~3 | 4 |

---

## 5. Khuyến nghị thứ tự thực thi

```
Tuần 1:
  Day 1-2  →  Phase 1 PR #1.1 (P0 + P1 đơn giản, ~8h) ✅
  Day 2-4  →  Phase 1 PR #1.2 (P1 layout phức tạp, ~20h) — Remaining: ApplicationsPage, FindJobPage, InterviewSchedulePage

Tuần 2:
  Day 5-6  →  Phase 2 PR #2.1 (Sidebar & Settings, ~12h) ✅
  Day 7-8  →  Phase 2 PR #2.2 (Form & Input, ~10h) 🔜
  Day 9-10 →  Phase 2 PR #2.3 (Table, Layout, Chart, ~18h)

Tuần 3:
  Day 11   →  Phase 3 PR #3.1 (HR/Recruiter polish, ~7h)
  Day 12   →  Phase 3 PR #3.2 (Candidate polish, ~7h)
  Day 13   →  Phase 4 (Config + Skeletons + QA, ~8h)
```

### 5.1. Lưu ý quan trọng

1. **Snapshot trước mỗi PR** — git tag hoặc screenshot test key breakpoint.
2. **Test trên thiết bị thật** cho InterviewRoomPage (Phase 2) và InterviewSchedulePage (Phase 1.2).
3. **Không merge khi chưa QA trên ít nhất 4 breakpoint**: 320, 768, 1024, 1440.
4. **Update `.ai/tasks.md`** sau mỗi PR theo quy tắc workspace.
5. **Báo cáo audit này là input** — nếu phát sinh vấn đề mới khi fix, bổ sung vào checklist phase tương ứng.

---

## 6. Bản đồ theo Role (Test matrix)

> **Mục đích:** Mục này được tạo 2026-07-25 theo yêu cầu — tổ chức lại toàn bộ file cần test theo **role người dùng** thay vì theo Phase, để dễ dàng chạy test matrix thực tế. Cấu trúc Phase ở mục 3 vẫn giữ nguyên — mục 6 chỉ là **view khác** để hỗ trợ test.

### 6.1. Cấu trúc Role → Site → Page

```
HR            →  StaffSite (port 3001) →  routes /hr/*
Recruiter     →  StaffSite (port 3001) →  routes /recruiter/*
Super Admin   →  StaffSite (port 3001) →  routes /super-admin/*
Candidate     →  CandidateSite (port 3000) →  routes /candidate/*, /interview/*, /kiosk, /schedule/*, /practice/*
Landing       →  CandidateSite (port 3000) →  routes /, /jobs/*, /login, /register
Shared        →  Cả 2 site →  components trong ARI.Shared
```

### 6.2. Test paths theo Role

#### 👑 ROLE: HR (StaffSite)

| Route | Page | Issues sửa | Phase | Trạng thái |
|---|---|---|---|---|
| `/hr` | `DashboardPage` | P1: padding, priority grid P3: funnel, svg, count | 1, 2, 3 | ✅ Phase 1 xong |
| `/hr/jobs` | `JobsPage` | (chưa list) | — | — |
| `/hr/jobs/pending` | `PendingJobsPage` | (chưa list) | — | — |
| `/hr/jobs/:id` | `JobPostingDetailPage` | P1: table wrapper, w-36, modal grid P2: filter bar P3: avatar, button, JD grid, tabs overflow | 1, 2, 3 | ✅ Phase 1 xong |
| `/hr/candidates` | `CandidatesPage` | (chưa list) | — | — |
| `/hr/candidates/:id` | `CandidateDetailPage` | Mobile 320px text tràn | 2 bổ sung | ✅ Phase 2 nhóm A xong |
| `/hr/evaluations/:id` | `EvaluationReviewPage` | P1: `xl:` → `lg:`, text-5xl P3: sticky header, breadcrumb, language grid, stats | 1, 3 | ✅ Phase 1 xong |
| `/hr/interview-sessions` | `InterviewSessionsPage` | (chưa list) | — | — |
| `/hr/playbooks` | `PlaybooksPage` | (chưa list) | — | — |
| `/hr/reports` | `ReportsPage` | (chưa list) | — | — |
| `/hr/team` | `TeamPage` | (chưa list) | — | — |
| `/hr/notifications` | `NotificationsPage` | (chưa list) | — | — |
| `/hr/settings` | `SettingsPage` | P3: sidebar `lg:w-64` | 2 | ✅ Đã xong |

**Test thực tế HR:**
```bash
# Mobile viewport checklist HR (320, 375, 768, 1024, 1440)
1. /hr → DashboardPage xem priority cards, funnel
2. /hr/jobs/:id → table ứng viên, modal cover letter
3. /hr/evaluations/:id → score 96px, sidebar 360px
4. /hr/candidates/:id → text tràn box mobile
5. /hr/settings → pills horizontal scroll
```

---

#### 🎯 ROLE: Recruiter (StaffSite)

| Route | Page | Issues sửa | Phase | Trạng thái |
|---|---|---|---|---|
| `/recruiter` | `DashboardPage` | (chưa list) | — | — |
| `/recruiter/my-jobs` | `MyJobsPage` | Mobile 375px lệch trái (padding thừa) | 2 bổ sung | ✅ Phase 2 nhóm A xong |
| `/recruiter/jobs/new` | `CreateJobPostingPage` | P2: salary grid 3-col Mobile 375px lệch trái (padding thừa) | 2 | ✅ Phase 2 nhóm A xong |
| `/recruiter/jobs/:id` | `JobDetailPage` | P1: table wrapper, w-36, modal grid P3: padding, avatar, title, stats grid, tabs overflow, modal truncate | 1, 3 | ✅ Phase 1 xong |
| `/recruiter/candidates` | `CandidatesPage` | P2: table cell overflow | 2 | ⏳ Phase 2 Nhóm C |
| `/recruiter/candidates/:id` | `CandidateDetailPage` | (chưa list) | — | — |
| `/recruiter/interview-codes` | `InterviewCodePage` | P2: search input responsive | 2 | ⏳ Phase 2 Nhóm B |
| `/recruiter/interview-sessions` | `InterviewSessionsPage` | (chưa list) | — | — |
| `/recruiter/evaluations/:id` | `EvaluationReviewPage` | P3: search, modal header, leading-7 | 3 | ⏳ Phase 3 |
| `/recruiter/job-schedule/:id` | `JobScheduleConfigPage` | (chưa list) | — | — |
| `/recruiter/notifications` | `NotificationsPage` | (chưa list) | — | — |
| `/recruiter/settings` | `SettingsPage` | P2: sidebar `lg:w-64` | 2 | ✅ Đã xong |

**Test thực tế Recruiter:**
```bash
# Mobile viewport checklist Recruiter (320, 375, 768, 1024, 1440)
1. /recruiter/my-jobs → list job, padding đúng
2. /recruiter/jobs/new → form salary 3-col collapse thành 1-col
3. /recruiter/jobs/:id → table ứng viên, modal cover letter, tabs overflow
4. /recruiter/interview-codes → search bar full-width mobile
5. /recruiter/settings → pills horizontal scroll
```

---

#### 🛡️ ROLE: Super Admin (StaffSite)

| Route | Page | Issues sửa | Phase | Trạng thái |
|---|---|---|---|---|
| `/super-admin` | `DashboardPage` | (chưa list) | — | — |
| `/super-admin/users` | `UsersPage` | P2: table `min-w-[700px]` | 2 | ⏳ Phase 2 Nhóm C |
| `/super-admin/users/pending` | `PendingUsersPage` | (chưa list) | — | — |
| `/super-admin/audit-logs` | `AuditLogsPage` | (chưa list) | — | — |
| `/super-admin/settings` | `SettingsPage` | P2: sidebar `lg:w-64` | 2 | ✅ Đã xong |

**Test thực tế Super Admin:**
```bash
# Mobile viewport checklist Super Admin (320, 375, 768, 1024, 1440)
1. /super-admin/users → table scroll ngang với min-w 700px
2. /super-admin/settings → pills horizontal scroll
```

---

#### 👤 ROLE: Candidate (CandidateSite)

| Route | Page | Issues sửa | Phase | Trạng thái |
|---|---|---|---|---|
| `/` | `HomePage` (Landing) | (chuyển xuống Landing) | — | — |
| `/jobs` | `FindJobPage` (Landing) | (chuyển xuống Landing) | — | — |
| `/jobs/:id` | `JobDetailPage` (job-board) | P2: sticky aside → bottom fixed bar | 2 | ⏳ Phase 2 Nhóm D |
| `/jobs/:id/apply` | `ApplyPage` | Text tràn box validation | 2 bổ sung | ✅ Phase 2 nhóm A xong |
| `/jobs/login` | `CandidateLoginPage` | (chưa list) | — | — |
| `/jobs/register` | `CandidateRegisterPage` | (chưa list) | — | — |
| `/candidate` | `ApplicationsPage` | P1: main grid collapse | 1 | ⏳ Phase 1 PR #1.2 |
| `/candidate/applications/:id` | `ApplicationDetailPage` | P2: round sidebar → horizontal scroll | 2 | ✅ Phase 2 nhóm A xong |
| `/candidate/profile` | `ProfilePage` | P2: section nav → horizontal scroll P2: Experience/Education form grid P2 bổ sung: iOS auto-shrink fix | 2 | ✅ Phase 2 nhóm A xong |
| `/candidate/saved-jobs` | `SavedJobsPage` | (chưa list) | — | — |
| `/candidate/schedule` | `SchedulePage` | **P0**: modal thiếu `w-[90%]` | 1 | ✅ Phase 1 xong |
| `/candidate/schedule/:appId` | `InterviewSchedulePage` | P1: refactor dark-glass → design system | 1 | ⏳ Phase 1 PR #1.2 |
| `/candidate/notifications` | `NotificationsPage` | (chưa list) | — | — |
| `/candidate/settings` | `SettingsPage` | (chưa list) | — | — |
| `/interview/practice/:appId` | `PracticeSessionPage` | (chưa list) | — | — |
| `/interview/room/:id` | `InterviewRoomPage` | P2: transcript panel → floating drawer P2: avatar responsive | 2 | ✅ Phase 2 nhóm A xong |
| `/kiosk` | `KioskPage` | (chưa list) | — | — |
| `/verify-email` | `VerifyEmailPage` | (chưa list) | — | — |
| `/privacy`, `/terms` | `PrivacyPolicyPage`, `TermsPage` | (chưa list) | — | — |

**Test thực tế Candidate:**
```bash
# Mobile viewport checklist Candidate (320, 375, 768, 1024, 1440)
1. /candidate/applications → sidebar collapse thành drawer
2. /candidate/applications/:id → horizontal scroll tabs cho rounds
3. /candidate/profile → nav pills horizontal scroll, experience form 1-col
4. /candidate/schedule → modal full-width 90% với max-w-2xl
5. /candidate/schedule/:appId → redesigned sang ink/brand/ai theme
6. /interview/room/:id → transcript drawer, avatar responsive
7. /jobs/:id → bottom fixed bar CTA thay sticky aside
```

---

#### 🌐 ROLE: Landing (CandidateSite, public)

| Route | Page | Issues sửa | Phase | Trạng thái |
|---|---|---|---|---|
| `/` | `HomePage` | P1: InterviewKioskSection input+button row | 1 | ✅ Phase 1 xong |
| `/jobs` | `FindJobPage` | P1: FilterSidebar collapse + work modes grid | 1 | ⏳ Phase 1 PR #1.2 |
| `/jobs/:id` | `JobDetailPage` (job-board) | (xem Candidate) | — | — |

**Test thực tế Landing:**
```bash
# Mobile viewport checklist Landing (320, 375, 768, 1024, 1440)
1. / → Kiosk section input full-width mobile
2. /jobs → Sidebar filter collapse thành drawer, work modes grid responsive
```

---

#### 🔧 SHARED (cả 2 site)

| File | Issues sửa | Phase | Trạng thái |
|---|---|---|---|
| `tailwind-preset.cjs` | Thêm `xs: '480px'` breakpoint | 4 | ⏳ Phase 4 |
| `index.css` (Shared) | Thêm `-webkit-text-size-adjust: 100%` | 2 bổ sung | ✅ Phase 2 nhóm A xong |
| `Container.tsx` (Shared) | Verify padding responsive | 4 | ⏳ Phase 4 |
| `Button.tsx` (Shared) | Size variants mobile | 4 | ⏳ Phase 4 |
| `NotFoundPage.tsx` (Shared) | Smooth font scale | 3 | ⏳ Phase 3 |
| `_skeletons.tsx` (×3) | Đồng bộ breakpoints | 4 | ⏳ Phase 4 |
| `Footer.tsx` (Candidate) | Brand paragraph `min-w-0 break-words` | 3 | ⏳ Phase 3 |
| `CTA.tsx` (Candidate) | `py-20 sm:py-32 lg:py-40` | 3 | ⏳ Phase 3 |
| `Demo.tsx` (Candidate) | `h-[400px]` responsive, grid collapse | 3 | ⏳ Phase 3 |
| `Hero.tsx` (Candidate) | Safe area overlay | 3 | ⏳ Phase 3 |
| `AISphereDemo.tsx` (Candidate) | `w-40 h-40 sm:w-64 sm:h-64` | 3 | ⏳ Phase 3 |
| `ScrollStorytelling.tsx` (Candidate) | Visual + gap responsive | 2 | ⏳ Phase 2 Nhóm D |
| `ChangePasswordModal.tsx` (Candidate) | `w-[90%] max-w-md` | 3 | ⏳ Phase 3 |

---

### 6.3. Test matrix tổng hợp theo Role

> **Cách dùng:** Mỗi lần chạy test, vào `cd ari-web && npm run dev:staff` (port 3001) hoặc `npm run dev:candidate` (port 3000), mở DevTools → toggle device toolbar → chọn iPhone SE (320), iPhone 12 (390), iPad (768), iPad Pro (1024), Desktop (1440). Lần lượt đi theo route của role.

#### Checklist test 320px (iPhone SE) — Critical
```
[ ] HR       /hr                          → 2 priority cards, không tràn
[ ] HR       /hr/jobs/:id                 → table scroll ngang, modal cover letter
[ ] HR       /hr/evaluations/:id          → score nhỏ gọn, sidebar collapse
[ ] HR       /hr/candidates/:id           → text không tràn box
[ ] HR       /hr/settings                 → pills horizontal scroll
[ ] Recruiter /recruiter/my-jobs          → list job, padding đúng
[ ] Recruiter /recruiter/jobs/new         → salary grid 1-col
[ ] Recruiter /recruiter/jobs/:id         → table scroll, modal overflow
[ ] Recruiter /recruiter/interview-codes  → search full-width
[ ] Recruiter /recruiter/settings         → pills horizontal scroll
[ ] SA       /super-admin/users           → table scroll ngang
[ ] SA       /super-admin/settings        → pills horizontal scroll
[ ] Candidate /candidate/profile          → nav pills cuộn ngang, text size chuẩn
[ ] Candidate /candidate/applications/:id → horizontal scroll tabs cho rounds
[ ] Candidate /candidate/schedule         → modal w-[90%]
[ ] Candidate /candidate/schedule/:appId  → design system mới
[ ] Candidate /interview/room/:id         → transcript drawer, avatar nhỏ
[ ] Candidate /jobs/:id                   → bottom fixed bar CTA
[ ] Landing  /                             → Kiosk input full-width
[ ] Landing  /jobs                         → sidebar filter drawer
```

#### Checklist test 768px (iPad) — Tablet
```
[ ] HR       /hr                          → 2 priority cards layout ngang
[ ] HR       /hr/jobs/:id                 → table hiển thị 1 phần
[ ] HR       /hr/settings                 → nav vẫn vertical hoặc horizontal scroll
[ ] Recruiter /recruiter/jobs/:id         → table 7-col hiển thị đầy đủ
[ ] Candidate /candidate/profile          → pills wrap thành 2 hàng
[ ] Candidate /interview/room/:id         → transcript panel inline
[ ] Candidate /jobs/:id                   → sticky aside visible
```

#### Checklist test 1024px (laptop nhỏ)
```
[ ] Tất cả route: kiểm tra sidebar/layout desktop, KHÔNG dùng `xl:` thuần
```

#### Checklist test 1440px+ (desktop)
```
[ ] Container max-w-6xl / max-w-7xl center
[ ] KHÔNG tràn viền 2 bên
[ ] Grid 3-col / 4-col hoạt động đúng
```

---

### 6.4. Mapping Phase ↔ Role (dễ đối chiếu)

| Phase | HR | Recruiter | Super Admin | Candidate | Landing | Shared |
|---|---|---|---|---|---|---|
| **Phase 1** | Dashboard, JobPostingDetail, EvaluationReview | JobDetail, JobDetail, JobDetail | — | Schedule, Applications, InterviewSchedule | HomePage, FindJob | — |
| **Phase 2 Nhóm A** ✅ | SettingsPage | SettingsPage, CreateJobPosting, MyJobs | SettingsPage | ProfilePage, ApplicationDetail, InterviewRoom | — | index.css, 2× index.html |
| **Phase 2 Nhóm B** 🔜 | JobPostingDetail (filter bar) | CreateJobPosting (salary), InterviewCodePage | — | ProfilePage (Experience form, Education form) | — | — |
| **Phase 2 Nhóm C** | Dashboard (candidates table) | CandidatesPage | UsersPage | — | — | — |
| **Phase 2 Nhóm D** | — | — | — | JobDetailPage (job-board), InterviewRoom | — | ScrollStorytelling |
| **Phase 2 Nhóm E** | Dashboard (chart), JobPostingDetail (reject modal) | — | — | — | — | — |
| **Phase 3** | JobPostingDetail (avatar, tabs), EvaluationReview, Dashboard (funnel) | JobDetail (padding, avatar, title, stats, tabs), EvaluationReview | — | — | — | Footer, CTA, Demo, Hero, AISphereDemo, ChangePasswordModal, NotFoundPage |
| **Phase 4** | — | — | — | — | — | tailwind-preset, Container, Button, _skeletons×3 |

---

### 6.5. Quy ước Test theo Role

Khi đến bước "xong Phase X", tôi sẽ:
1. **Liệt kê đúng các file thuộc role đó** trong Phase đó (theo mục 6.2)
2. **Ghi hướng dẫn test theo route + breakpoint** (theo mục 6.3)
3. **Đánh dấu trạng thái** vào bảng mục 6.2 (✅ / ⏳)
4. **Update section 4.4** (theo component) nếu phát sinh file mới

Bạn chỉ cần click vào role cần test, mở theo route, test theo breakpoint — không cần đọc lại toàn bộ Phase.

---

**Người lập:** Cursor Agent
**File nguồn:** `.ai/responsive-audit-2026-07-24.md`
**Trạng thái:** Kế hoạch — đang thực thi Phase 2 Nhóm B
