import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, lazy, Suspense } from 'react'
import { useAuthStore } from '@store/auth/authStore'
import { getDevAuth, isDevMode } from '@utils/devAuth'

// Layouts + route guards: giữ eager vì nhỏ và dùng ở mọi route (bọc các page).
import CandidateLayout from '@components/layout/CandidateLayout'
import InterviewLayout from '@components/layout/InterviewLayout'
import ProtectedRoute from '@components/auth/ProtectedRoute'
import GuestRoute from '@components/auth/GuestRoute'
import StaffRedirect from '@components/auth/StaffRedirect'
import SuperAdminLayout from '@components/layout/SuperAdminLayout'
import HrLayout from '@components/layout/HrLayout'
import RecruiterLayout from '@components/layout/RecruiterLayout'
import CandidateAppLayout from '@components/layout/CandidateAppLayout'
import { DocumentViewerProvider } from '@components/document/DocumentViewer'
import { useAppNotifications } from '@hooks/useAppNotifications'

// Pages: lazy-load → mỗi page thành 1 chunk riêng, chỉ tải khi vào route đó
// (tách cả dep nặng như react-grid-layout ra khỏi bundle chính).
// Auth
const LoginPage = lazy(() => import('@pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('@pages/auth/RegisterPage'))
const CandidateLoginPage = lazy(() => import('@pages/auth/CandidateLoginPage'))
const CandidateRegisterPage = lazy(() => import('@pages/auth/CandidateRegisterPage'))
const ForgotPasswordPage = lazy(() => import('@pages/auth/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('@pages/auth/ResetPasswordPage'))
const OAuthCallbackPage = lazy(() => import('@pages/auth/OAuthCallbackPage'))
const VerifyEmailPage = lazy(() => import('@pages/auth/VerifyEmailPage'))

// Legal
const TermsPage = lazy(() => import('@pages/legal/TermsPage'))
const PrivacyPolicyPage = lazy(() => import('@pages/legal/PrivacyPolicyPage'))

// Super Admin
const SuperAdminDashboardPage = lazy(() => import('@pages/super-admin/DashboardPage'))
const SuperAdminUsersPage = lazy(() => import('@pages/super-admin/UsersPage'))
const SuperAdminPendingUsersPage = lazy(() => import('@pages/super-admin/PendingUsersPage'))
const SuperAdminAuditLogsPage = lazy(() => import('@pages/super-admin/AuditLogsPage'))
const SuperAdminSettingsPage = lazy(() => import('@pages/super-admin/SettingsPage'))

// HR Admin
const HrDashboardPage = lazy(() => import('@pages/hr/DashboardPage'))
const HrPendingJobsPage = lazy(() => import('@pages/hr/PendingJobsPage'))
const HrJobsPage = lazy(() => import('@pages/hr/JobsPage'))
const HrCandidatesPage = lazy(() => import('@pages/hr/CandidatesPage'))
const HrCandidateDetailPage = lazy(() => import('@pages/hr/CandidateDetailPage'))
const HrEvaluationsPage = lazy(() => import('@pages/hr/EvaluationReviewPage'))
const HrReportsPage = lazy(() => import('@pages/hr/ReportsPage'))
const HrPlaybooksPage = lazy(() => import('@pages/hr/PlaybooksPage'))
const HrTeamPage = lazy(() => import('@pages/hr/TeamPage'))
const HrInterviewsPage = lazy(() => import('@pages/hr/InterviewSessionsPage'))
const HrJobDetailPage = lazy(() => import('@pages/hr/JobPostingDetailPage'))
const HrSettingsPage = lazy(() => import('@pages/hr/SettingsPage'))

// Recruiter
const RecruiterDashboardPage = lazy(() => import('@pages/recruiter/DashboardPage'))
const RecruiterMyJobsPage = lazy(() => import('@pages/recruiter/MyJobsPage'))
const RecruiterJobDetailPage = lazy(() => import('@pages/recruiter/JobDetailPage'))
const RecruiterCreateJobPage = lazy(() => import('@pages/recruiter/CreateJobPostingPage'))
const RecruiterJobSchedulePage = lazy(() => import('@pages/recruiter/JobScheduleConfigPage'))
const RecruiterInterviewCodePage = lazy(() => import('@pages/recruiter/InterviewCodePage'))
const RecruiterCandidatesPage = lazy(() => import('@pages/recruiter/CandidatesPage'))
const RecruiterCandidateDetailPage = lazy(() => import('@pages/recruiter/CandidateDetailPage'))
const RecruiterEvaluationsPage = lazy(() => import('@pages/recruiter/EvaluationReviewPage'))
const RecruiterInterviewsPage = lazy(() => import('@pages/recruiter/InterviewSessionsPage'))
const RecruiterSettingsPage = lazy(() => import('@pages/recruiter/SettingsPage'))

// Candidate
const CandidateApplicationsPage = lazy(() => import('@pages/candidate/ApplicationsPage'))
const CandidateApplicationDetailPage = lazy(() => import('@pages/candidate/ApplicationDetailPage'))
const CandidateProfilePage = lazy(() => import('@pages/candidate/ProfilePage'))
const SavedJobsPage = lazy(() => import('@pages/candidate/SavedJobsPage'))
const CandidateNotificationsPage = lazy(() => import('@pages/candidate/NotificationsPage'))
const CandidateSettingsPage = lazy(() => import('@pages/candidate/SettingsPage'))
const InterviewSchedulePage = lazy(() => import('@pages/candidate/InterviewSchedulePage'))
const CandidateSchedulePage = lazy(() => import('@pages/candidate/SchedulePage'))
const FeedbackPage = lazy(() => import('@pages/candidate/FeedbackPage'))

// Interview
const InterviewRoomPage = lazy(() => import('@pages/interview/InterviewRoomPage'))
const PracticeSessionPage = lazy(() => import('@pages/interview/PracticeSessionPage'))

// Landing / Job board
const HomePage = lazy(() => import('@pages/landing/HomePage'))
const FindJobPage = lazy(() => import('@pages/landing/FindJobPage'))
const JobDetailPage = lazy(() => import('@pages/job-board/JobDetailPage'))
const JobApplyPage = lazy(() => import('@pages/job-board/ApplyPage'))
const KioskPage = lazy(() => import('@pages/kiosk/KioskPage'))
const NotFoundPage = lazy(() => import('@pages/NotFoundPage'))

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

          {/* ==================== PUBLIC ROUTES ==================== */}
          {/* Job board công khai cho khách + ứng viên; staff (admin/hr/recruiter) bị đưa về workspace. */}
          <Route
            path="/"
            element={
              <StaffRedirect>
                <FindJobPage />
              </StaffRedirect>
            }
          />
          <Route path="/employer" element={<HomePage />} />
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
          <Route
            path="/auth/candidate-login"
            element={
              <GuestRoute>
                <CandidateLoginPage />
              </GuestRoute>
            }
          />
          <Route
            path="/auth/candidate-register"
            element={
              <GuestRoute>
                <CandidateRegisterPage />
              </GuestRoute>
            }
          />
          <Route path="/auth/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/auth/reset-password" element={<ResetPasswordPage />} />
          <Route path="/auth/verify-email" element={<VerifyEmailPage />} />
          <Route path="/terms" element={<TermsPage />} />
          <Route path="/privacy" element={<PrivacyPolicyPage />} />
          {/* Chọn lịch phỏng vấn từ email mời (token) — không cần đăng nhập đầy đủ */}
          <Route path="/portal/schedule/:applicationId" element={<CandidateSchedulePage />} />
          <Route
            path="/jobs"
            element={
              <StaffRedirect>
                <FindJobPage />
              </StaffRedirect>
            }
          />
          <Route
            path="/jobs/:id"
            element={
              <StaffRedirect>
                <JobDetailPage />
              </StaffRedirect>
            }
          />
          <Route
            path="/jobs/:id/apply"
            element={
              <StaffRedirect>
                <JobApplyPage />
              </StaffRedirect>
            }
          />

          {/* ========== CANDIDATE ROUTES (redesign — light theme, mockup) ========== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Candidate']}>
                <CandidateAppLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/candidate/applications" element={<CandidateApplicationsPage />} />
            <Route
              path="/candidate/applications/:id"
              element={<CandidateApplicationDetailPage />}
            />
            <Route path="/candidate/profile" element={<CandidateProfilePage />} />
            <Route path="/candidate/saved-jobs" element={<SavedJobsPage />} />
            <Route path="/candidate/notifications" element={<CandidateNotificationsPage />} />
            <Route path="/candidate/settings" element={<CandidateSettingsPage />} />
          </Route>

          {/* Redirect các route ứng viên cũ (đã bị thay bằng job board / hồ sơ ứng tuyển mới) */}
          <Route path="/candidate/dashboard" element={<Navigate to="/" replace />} />
          <Route path="/candidate/jobs" element={<Navigate to="/jobs" replace />} />
          <Route
            path="/candidate/portal"
            element={<Navigate to="/candidate/applications" replace />}
          />

          {/* ========== CANDIDATE ROUTES (Kết quả & Lịch phỏng vấn — chờ redesign) ========== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Candidate']}>
                <CandidateLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/candidate/interviews" element={<InterviewSchedulePage />} />
            <Route path="/candidate/results" element={<FeedbackPage />} />
            <Route path="/candidate/results/:id" element={<FeedbackPage />} />
          </Route>

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
            <Route path="/hr/jobs/:id" element={<HrJobDetailPage />} />
            <Route path="/hr/candidates" element={<HrCandidatesPage />} />
            <Route path="/hr/candidates/:id" element={<HrCandidateDetailPage />} />
            <Route path="/hr/evaluations" element={<HrEvaluationsPage />} />
            <Route path="/hr/reports" element={<HrReportsPage />} />
            <Route path="/hr/playbooks" element={<HrPlaybooksPage />} />
            <Route path="/hr/team" element={<HrTeamPage />} />
            <Route path="/hr/interviews" element={<HrInterviewsPage />} />
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
            <Route
              path="/recruiter/jobs/create"
              element={<RecruiterCreateJobPage mode="create" />}
            />
            <Route path="/recruiter/my-jobs" element={<RecruiterMyJobsPage />} />
            <Route path="/recruiter/my-jobs/:id" element={<RecruiterJobDetailPage />} />
            <Route
              path="/recruiter/my-jobs/:id/edit"
              element={<RecruiterCreateJobPage mode="edit" />}
            />
            <Route path="/recruiter/my-jobs/:id/schedule" element={<RecruiterJobSchedulePage />} />
            <Route path="/recruiter/candidates" element={<RecruiterCandidatesPage />} />
            <Route path="/recruiter/candidates/:id" element={<RecruiterCandidateDetailPage />} />
            <Route path="/recruiter/code" element={<RecruiterInterviewCodePage />} />
            <Route path="/recruiter/evaluations" element={<RecruiterEvaluationsPage />} />
            <Route path="/recruiter/interviews" element={<RecruiterInterviewsPage />} />
            <Route path="/recruiter/settings" element={<RecruiterSettingsPage />} />
          </Route>

          {/* ==================== INTERVIEW ROUTES ==================== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Candidate']}>
                <InterviewLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/interview/room/:sessionId" element={<InterviewRoomPage />} />
          </Route>

          {/* Practice session: full-bleed (tự quản layout, có cổng kiểm tra mic/cam) */}
          <Route
            path="/interview/practice/:applicationId"
            element={
              <ProtectedRoute allowedRoles={['Candidate']}>
                <PracticeSessionPage />
              </ProtectedRoute>
            }
          />

          {/* ==================== KIOSK ROUTE ==================== */}
          <Route path="/kiosk" element={<InterviewLayout />}>
            <Route index element={<KioskPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/404" replace />} />
          <Route path="/404" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </DocumentViewerProvider>
  )
}

export default App
