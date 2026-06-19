import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect } from 'react'
import { useAuthStore } from '@store/auth/authStore'
import { getDevAuth, isDevMode } from '@utils/devAuth'

import CandidateLayout from '@components/layout/CandidateLayout'
import InterviewLayout from '@components/layout/InterviewLayout'
import ProtectedRoute from '@components/auth/ProtectedRoute'
import GuestRoute from '@components/auth/GuestRoute'

// Layouts for each role
import SuperAdminLayout from '@components/layout/SuperAdminLayout'
import HrLayout from '@components/layout/HrLayout'
import RecruiterLayout from '@components/layout/RecruiterLayout'

// Auth Pages
import LoginPage from '@pages/auth/LoginPage'
import RegisterPage from '@pages/auth/RegisterPage'
import CandidateLoginPage from '@pages/auth/CandidateLoginPage'
import CandidateRegisterPage from '@pages/auth/CandidateRegisterPage'
import ForgotPasswordPage from '@pages/auth/ForgotPasswordPage'
import ResetPasswordPage from '@pages/auth/ResetPasswordPage'
import OAuthCallbackPage from '@pages/auth/OAuthCallbackPage'
import VerifyEmailPage from '@pages/auth/VerifyEmailPage'

// Legal Pages
import TermsPage from '@pages/legal/TermsPage'
import PrivacyPolicyPage from '@pages/legal/PrivacyPolicyPage'

// Super Admin Pages
import SuperAdminDashboardPage from '@pages/super-admin/DashboardPage'
import SuperAdminUsersPage from '@pages/super-admin/UsersPage'
import SuperAdminPendingUsersPage from '@pages/super-admin/PendingUsersPage'
import SuperAdminAuditLogsPage from '@pages/super-admin/AuditLogsPage'
import SuperAdminSettingsPage from '@pages/super-admin/SettingsPage'

// HR Admin Pages (from hr/ folder)
import HrDashboardPage from '@pages/hr/DashboardPage'
import HrPendingJobsPage from '@pages/hr/PendingJobsPage'
import HrJobsPage from '@pages/hr/JobsPage'
import HrCandidatesPage from '@pages/hr/CandidatesPage'
import HrCandidateDetailPage from '@pages/hr/CandidateDetailPage'
import HrEvaluationsPage from '@pages/hr/EvaluationReviewPage'
import HrReportsPage from '@pages/hr/ReportsPage'
import HrPlaybooksPage from '@pages/hr/PlaybooksPage'
import HrTeamPage from '@pages/hr/TeamPage'
import HrInterviewsPage from '@pages/hr/InterviewSessionsPage'
import HrJobDetailPage from '@pages/hr/JobPostingDetailPage'
import HrSettingsPage from '@pages/hr/SettingsPage'

// Recruiter Pages (from recruiter/ folder)
import RecruiterDashboardPage from '@pages/recruiter/DashboardPage'
import RecruiterMyJobsPage from '@pages/recruiter/MyJobsPage'
import RecruiterCreateJobPage from '@pages/recruiter/CreateJobPostingPage'
import RecruiterCandidatesPage from '@pages/recruiter/CandidatesPage'
import RecruiterCandidateDetailPage from '@pages/recruiter/CandidateDetailPage'
import RecruiterEvaluationsPage from '@pages/recruiter/EvaluationReviewPage'
import RecruiterInterviewsPage from '@pages/recruiter/InterviewSessionsPage'
import RecruiterSettingsPage from '@pages/recruiter/SettingsPage'

// Candidate Pages
import CandidateAppLayout from '@components/layout/CandidateAppLayout'
import CandidateApplicationsPage from '@pages/candidate/ApplicationsPage'
import CandidateProfilePage from '@pages/candidate/ProfilePage'
import CandidateHome from '@pages/candidate/CandidateHome'
import InterviewSchedulePage from '@pages/candidate/InterviewSchedulePage'
import PortalPage from '@pages/candidate/PortalPage'
import FeedbackPage from '@pages/candidate/FeedbackPage'

// Interview Pages
import InterviewRoomPage from '@pages/interview/InterviewRoomPage'
import PracticeSessionPage from '@pages/interview/PracticeSessionPage'

// Landing Pages
import HomePage from '@pages/landing/HomePage'
import FindJobPage from '@pages/landing/FindJobPage'
import JobDetailPage from '@pages/job-board/JobDetailPage'
import CandidateApply from '@pages/CandidateApply'
import KioskPage from '@pages/kiosk/KioskPage'
import NotFoundPage from '@pages/NotFoundPage'

function App() {
  const setAuth = useAuthStore((state) => state.setAuth)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)

  useEffect(() => {
    if (isDevMode && !isAuthenticated) {
      const devAuth = getDevAuth()
      if (devAuth) {
        setAuth(devAuth.user, devAuth.tokens)
      }
    }
  }, [isAuthenticated, setAuth])

  return (
    <Routes>
      <Route path="/403" element={<NotFoundPage />} />

      {/* ==================== PUBLIC ROUTES ==================== */}
      <Route path="/" element={<FindJobPage />} />
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
      <Route path="/jobs" element={<FindJobPage />} />
      <Route path="/jobs/:id" element={<JobDetailPage />} />

      {/* ========== CANDIDATE ROUTES (redesign — light theme, mockup) ========== */}
      <Route
        element={
          <ProtectedRoute allowedRoles={['Candidate']}>
            <CandidateAppLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/candidate/applications" element={<CandidateApplicationsPage />} />
        <Route path="/candidate/profile" element={<CandidateProfilePage />} />
      </Route>

      {/* ========== CANDIDATE ROUTES (chưa redesign — sẽ migrate dần) ========== */}
      <Route
        element={
          <ProtectedRoute allowedRoles={['Candidate']}>
            <CandidateLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/candidate/dashboard" element={<CandidateHome />} />
        <Route path="/candidate/jobs" element={<CandidateHome />} />
        <Route path="/candidate/applications/:id" element={<CandidateApply />} />
        <Route path="/candidate/interviews" element={<InterviewSchedulePage />} />
        <Route path="/candidate/portal" element={<PortalPage />} />
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
        <Route path="/recruiter/jobs/create" element={<RecruiterCreateJobPage mode="create" />} />
        <Route path="/recruiter/my-jobs" element={<RecruiterMyJobsPage />} />
        <Route path="/recruiter/my-jobs/:id" element={<RecruiterCreateJobPage mode="edit" />} />
        <Route path="/recruiter/candidates" element={<RecruiterCandidatesPage />} />
        <Route path="/recruiter/candidates/:id" element={<RecruiterCandidateDetailPage />} />
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
        <Route path="/interview/practice/:applicationId" element={<PracticeSessionPage />} />
      </Route>

      {/* ==================== KIOSK ROUTE ==================== */}
      <Route path="/kiosk" element={<InterviewLayout />}>
        <Route index element={<KioskPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/404" replace />} />
      <Route path="/404" element={<NotFoundPage />} />
    </Routes>
  )
}

export default App
