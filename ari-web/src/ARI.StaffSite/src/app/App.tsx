import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, lazy, Suspense } from 'react'
import { useAuthStore } from '@ari/shared/store/auth'
import { getDevAuth, isDevMode } from '@ari/shared/utils/devAuth'

// Layouts + route guards: giữ eager vì nhỏ và dùng ở mọi route (bọc các page).
import ProtectedRoute from '@ari/shared/guards/ProtectedRoute'
import GuestRoute from '@ari/shared/guards/GuestRoute'
import SuperAdminLayout from '@/app/layouts/SuperAdminLayout'
import HrLayout from '@/app/layouts/HrLayout'
import RecruiterLayout from '@/app/layouts/RecruiterLayout'
import { DocumentViewerProvider } from '@ari/shared/document/DocumentViewer'
import { useAppNotifications } from '@ari/shared/realtime/useAppNotifications'

// Trang đích của staff theo role — cùng bảng với ProtectedRoute (backend snake_case).
import StaffHomeRedirect from './StaffHomeRedirect'

// Pages: lazy-load → mỗi page thành 1 chunk riêng, chỉ tải khi vào route đó.
// Auth
const LoginPage = lazy(() => import('@/pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('@/pages/auth/RegisterPage'))
const ForgotPasswordPage = lazy(() => import('@ari/shared/authflows/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('@ari/shared/authflows/ResetPasswordPage'))
const OAuthCallbackPage = lazy(() => import('@ari/shared/authflows/OAuthCallbackPage'))

// Super Admin
const SuperAdminDashboardPage = lazy(() => import('@/pages/super-admin/DashboardPage'))
const SuperAdminUsersPage = lazy(() => import('@/pages/super-admin/UsersPage'))
const SuperAdminPendingUsersPage = lazy(() => import('@/pages/super-admin/PendingUsersPage'))
const SuperAdminAuditLogsPage = lazy(() => import('@/pages/super-admin/AuditLogsPage'))
const SuperAdminSettingsPage = lazy(() => import('@/pages/super-admin/SettingsPage'))

// HR Admin
const HrDashboardPage = lazy(() => import('@/pages/hr/DashboardPage'))
const HrPendingJobsPage = lazy(() => import('@/pages/hr/PendingJobsPage'))
const HrJobsPage = lazy(() => import('@/pages/hr/JobsPage'))
const HrCandidatesPage = lazy(() => import('@/pages/hr/CandidatesPage'))
const HrCandidateDetailPage = lazy(() => import('@/pages/hr/CandidateDetailPage'))
const HrEvaluationsPage = lazy(() => import('@/pages/hr/EvaluationReviewPage'))
const HrReportsPage = lazy(() => import('@/pages/hr/ReportsPage'))
const HrPlaybooksPage = lazy(() => import('@/pages/hr/PlaybooksPage'))
const HrTeamPage = lazy(() => import('@/pages/hr/TeamPage'))
const HrInterviewsPage = lazy(() => import('@/pages/hr/InterviewSessionsPage'))
const HrJobDetailPage = lazy(() => import('@/pages/hr/JobPostingDetailPage'))
const HrSettingsPage = lazy(() => import('@/pages/hr/SettingsPage'))
const HrNotificationsPage = lazy(() => import('@/pages/hr/NotificationsPage'))

// Recruiter
const RecruiterDashboardPage = lazy(() => import('@/pages/recruiter/DashboardPage'))
const RecruiterMyJobsPage = lazy(() => import('@/pages/recruiter/MyJobsPage'))
const RecruiterJobDetailPage = lazy(() => import('@/pages/recruiter/JobDetailPage'))
const RecruiterCreateJobPage = lazy(() => import('@/pages/recruiter/CreateJobPostingPage'))
const RecruiterJobSchedulePage = lazy(() => import('@/pages/recruiter/JobScheduleConfigPage'))
const JobOnlineTestPage = lazy(() => import('@/pages/recruiter/JobOnlineTestPage'))
const JobOnlineTestResultsPage = lazy(() => import('@/pages/recruiter/JobOnlineTestResultsPage'))
const RecruiterInterviewCodePage = lazy(() => import('@/pages/recruiter/InterviewCodePage'))
const RecruiterCandidatesPage = lazy(() => import('@/pages/recruiter/CandidatesPage'))
const RecruiterCandidateDetailPage = lazy(() => import('@/pages/recruiter/CandidateDetailPage'))
const RecruiterEvaluationsPage = lazy(() => import('@/pages/recruiter/EvaluationReviewPage'))
const RecruiterInterviewsPage = lazy(() => import('@/pages/recruiter/InterviewSessionsPage'))
const RecruiterSettingsPage = lazy(() => import('@/pages/recruiter/SettingsPage'))
const RecruiterNotificationsPage = lazy(() => import('@/pages/recruiter/NotificationsPage'))

const NotFoundPage = lazy(() => import('@ari/shared/ui/NotFoundPage'))

/** Fallback nhẹ khi đang tải chunk của page. */
function RouteFallback() {
  return (
    <div className="grid min-h-screen place-items-center bg-ink-50 dark:bg-ink-950">
      <div className="h-10 w-10 animate-spin rounded-full border-2 border-brand-300 border-t-brand-600 dark:border-brand-500/30 dark:border-t-brand-400" />
    </div>
  )
}

function App() {
  const setAuth = useAuthStore((state) => state.setAuth)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)

  useAppNotifications()

  useEffect(() => {
    if (isDevMode && !isAuthenticated) {
      const devAuth = getDevAuth()
      if (devAuth) {
        setAuth(devAuth.user, devAuth.tokens)
      }
    }
  }, [isAuthenticated, setAuth])

  return (
    <DocumentViewerProvider>
      <Suspense fallback={<RouteFallback />}>
        <Routes>
          <Route path="/403" element={<NotFoundPage />} />

          {/* Trang gốc staff: đã đăng nhập → workspace theo role; chưa → trang đăng nhập. */}
          <Route path="/" element={<StaffHomeRedirect />} />

          {/* ==================== AUTH ROUTES ==================== */}
          <Route
            path="/auth/login"
            element={
              <GuestRoute>
                <LoginPage />
              </GuestRoute>
            }
          />
          <Route
            path="/auth/register"
            element={
              <GuestRoute>
                <RegisterPage />
              </GuestRoute>
            }
          />
          <Route path="/auth/callback" element={<OAuthCallbackPage />} />
          <Route path="/auth/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/auth/reset-password" element={<ResetPasswordPage />} />

          {/* ==================== SUPER ADMIN ROUTES ==================== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Super_admin']}>
                <SuperAdminLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/super-admin/dashboard" element={<SuperAdminDashboardPage />} />
            <Route path="/super-admin/users" element={<SuperAdminUsersPage />} />
            <Route path="/super-admin/users/pending" element={<SuperAdminPendingUsersPage />} />
            <Route path="/super-admin/audit-logs" element={<SuperAdminAuditLogsPage />} />
            <Route path="/super-admin/settings" element={<SuperAdminSettingsPage />} />
          </Route>

          {/* ==================== HR ADMIN ROUTES ==================== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Hr_admin']}>
                <HrLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/hr/dashboard" element={<HrDashboardPage />} />
            <Route path="/hr/jobs/pending" element={<HrPendingJobsPage />} />
            <Route path="/hr/jobs" element={<HrJobsPage />} />
            <Route path="/hr/jobs/create" element={<RecruiterCreateJobPage mode="create" />} />
            <Route path="/hr/jobs/:id/edit" element={<RecruiterCreateJobPage mode="edit" />} />
            <Route path="/hr/jobs/:id" element={<HrJobDetailPage />} />
            <Route path="/hr/jobs/:id/online-test" element={<JobOnlineTestPage />} />
            <Route path="/hr/jobs/:id/online-test/results" element={<JobOnlineTestResultsPage />} />
            <Route path="/hr/candidates" element={<HrCandidatesPage />} />
            <Route path="/hr/candidates/:id" element={<HrCandidateDetailPage />} />
            <Route path="/hr/evaluations" element={<HrEvaluationsPage />} />
            <Route path="/hr/reports" element={<HrReportsPage />} />
            <Route path="/hr/playbooks" element={<HrPlaybooksPage />} />
            <Route path="/hr/team" element={<HrTeamPage />} />
            <Route path="/hr/interviews" element={<HrInterviewsPage />} />
            <Route path="/hr/notifications" element={<HrNotificationsPage />} />
            <Route path="/hr/settings" element={<HrSettingsPage />} />
          </Route>

          {/* ==================== RECRUITER ROUTES ==================== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Recruiter']}>
                <RecruiterLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/recruiter/dashboard" element={<RecruiterDashboardPage />} />
            <Route path="/recruiter/jobs/create" element={<RecruiterCreateJobPage mode="create" />} />
            <Route path="/recruiter/my-jobs" element={<RecruiterMyJobsPage />} />
            <Route path="/recruiter/my-jobs/:id" element={<RecruiterJobDetailPage />} />
            <Route path="/recruiter/my-jobs/:id/edit" element={<RecruiterCreateJobPage mode="edit" />} />
            <Route path="/recruiter/my-jobs/:id/schedule" element={<RecruiterJobSchedulePage />} />
            <Route path="/recruiter/my-jobs/:id/online-test" element={<JobOnlineTestPage />} />
            <Route
              path="/recruiter/my-jobs/:id/online-test/results"
              element={<JobOnlineTestResultsPage />}
            />
            <Route path="/recruiter/candidates" element={<RecruiterCandidatesPage />} />
            <Route path="/recruiter/candidates/:id" element={<RecruiterCandidateDetailPage />} />
            <Route path="/recruiter/code" element={<RecruiterInterviewCodePage />} />
            <Route path="/recruiter/evaluations" element={<RecruiterEvaluationsPage />} />
            <Route path="/recruiter/interviews" element={<RecruiterInterviewsPage />} />
            <Route path="/recruiter/notifications" element={<RecruiterNotificationsPage />} />
            <Route path="/recruiter/settings" element={<RecruiterSettingsPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/404" replace />} />
          <Route path="/404" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </DocumentViewerProvider>
  )
}

export default App
