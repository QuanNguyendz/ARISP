import { Routes, Route, Navigate } from 'react-router-dom';
import { useEffect } from 'react';
import { useAuthStore } from '@store/auth/authStore';
import { getDevAuth, isDevMode } from '@utils/devAuth';

import AdminLayout from '@components/layout/AdminLayout';
import CandidateLayout from '@components/layout/CandidateLayout';
import InterviewLayout from '@components/layout/InterviewLayout';
import ProtectedRoute from '@components/auth/ProtectedRoute';

// Auth Pages
import LoginPage from '@pages/auth/LoginPage';
import RegisterPage from '@pages/auth/RegisterPage';
import CandidateLoginPage from '@pages/auth/CandidateLoginPage';
import CandidateRegisterPage from '@pages/auth/CandidateRegisterPage';
import ForgotPasswordPage from '@pages/auth/ForgotPasswordPage';
import ResetPasswordPage from '@pages/auth/ResetPasswordPage';
import OAuthCallbackPage from '@pages/auth/OAuthCallbackPage';

// Admin Pages
import DashboardPage from '@pages/admin/DashboardPage';
import JobPostingsPage from '@pages/admin/JobPostingsPage';
import JobPostingDetailPage from '@pages/admin/JobPostingDetailPage';
import CreateJobPostingPage from '@pages/admin/CreateJobPostingPage';
import CandidatesPage from '@pages/admin/CandidatesPage';
import CandidateDetailPage from '@pages/admin/CandidateDetailPage';
import InterviewSessionsPage from '@pages/admin/InterviewSessionsPage';
import EvaluationReviewPage from '@pages/admin/EvaluationReviewPage';
import ReportsPage from '@pages/admin/ReportsPage';
import PlaybooksPage from '@pages/admin/PlaybooksPage';
import SettingsPage from '@pages/admin/SettingsPage';
import TeamPage from '@pages/admin/TeamPage';

// Candidate Pages
import CandidateHome from '@pages/candidate/CandidateHome';
import InterviewSchedulePage from '@pages/candidate/InterviewSchedulePage';
import PortalPage from '@pages/candidate/PortalPage';
import FeedbackPage from '@pages/candidate/FeedbackPage';

// Interview Pages
import InterviewRoomPage from '@pages/interview/InterviewRoomPage';
import PracticeSessionPage from '@pages/interview/PracticeSessionPage';

// Landing Pages
import HomePage from '@pages/landing/HomePage';
import FindJobPage from '@pages/landing/FindJobPage';
import JobDetailPage from '@pages/job-board/JobDetailPage';
import CandidateApply from '@pages/CandidateApply';
import KioskPage from '@pages/kiosk/KioskPage';
import NotFoundPage from '@pages/NotFoundPage';

function App() {
  const setAuth = useAuthStore((state) => state.setAuth);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

  useEffect(() => {
    if (isDevMode && !isAuthenticated) {
      const devAuth = getDevAuth();
      if (devAuth) {
        setAuth(devAuth.user, devAuth.tokens);
      }
    }
  }, [isAuthenticated, setAuth]);

  return (
    <Routes>
      <Route path="/403" element={<NotFoundPage />} />

      {/* ==================== PUBLIC ROUTES ==================== */}
      <Route path="/" element={<FindJobPage />} />
      <Route path="/employer" element={<HomePage />} />
      <Route path="/auth/login" element={<LoginPage />} />
      <Route path="/auth/register" element={<RegisterPage />} />
      <Route path="/auth/callback" element={<OAuthCallbackPage />} />
      <Route path="/auth/candidate-login" element={<CandidateLoginPage />} />
      <Route path="/auth/candidate-register" element={<CandidateRegisterPage />} />
      <Route path="/auth/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/auth/reset-password" element={<ResetPasswordPage />} />
      <Route path="/jobs" element={<FindJobPage />} />
      <Route path="/jobs/:id" element={<JobDetailPage />} />

      {/* ==================== CANDIDATE PROTECTED ROUTES ==================== */}
      <Route
        element={
          <ProtectedRoute allowedRoles={['Candidate']}>
            <CandidateLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/candidate/dashboard" element={<CandidateHome />} />
        <Route path="/candidate/profile" element={<CandidateHome />} />
        <Route path="/candidate/jobs" element={<CandidateHome />} />
        <Route path="/candidate/applications" element={<CandidateApply />} />
        <Route path="/candidate/applications/:id" element={<CandidateApply />} />
        <Route path="/candidate/interviews" element={<InterviewSchedulePage />} />
        <Route path="/candidate/portal" element={<PortalPage />} />
        <Route path="/candidate/results" element={<FeedbackPage />} />
        <Route path="/candidate/results/:id" element={<FeedbackPage />} />
      </Route>

      {/* ==================== HR ADMIN ROUTES ==================== */}
      <Route
        element={
          <ProtectedRoute allowedRoles={['Super_admin', 'Hr_admin', 'Recruiter']}>
            <AdminLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/admin/dashboard" element={<DashboardPage />} />
        <Route path="/admin/jobs" element={<JobPostingsPage />} />
        <Route path="/admin/jobs/create" element={<CreateJobPostingPage />} />
        <Route path="/admin/jobs/:id" element={<JobPostingDetailPage />} />
        <Route path="/admin/candidates" element={<CandidatesPage />} />
        <Route path="/admin/candidates/:id" element={<CandidateDetailPage />} />
        <Route path="/admin/evaluations" element={<EvaluationReviewPage />} />
        <Route path="/admin/reports" element={<ReportsPage />} />
        <Route path="/admin/settings" element={<SettingsPage />} />
        <Route path="/admin/playbooks" element={<PlaybooksPage />} />
        <Route path="/admin/team" element={<TeamPage />} />
        <Route path="/admin/interviews" element={<InterviewSessionsPage />} />
      </Route>

      {/* Interview Routes */}
      <Route
        path="/interview"
        element={
          <ProtectedRoute allowedRoles={['Candidate']}>
            <InterviewLayout />
          </ProtectedRoute>
        }
      >
        <Route path="room/:sessionId" element={<InterviewRoomPage />} />
        <Route path="practice/:applicationId" element={<PracticeSessionPage />} />
      </Route>

      {/* Kiosk */}
      <Route path="/kiosk" element={<InterviewLayout />}>
        <Route index element={<KioskPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/404" replace />} />
      <Route path="/404" element={<NotFoundPage />} />
    </Routes>
  );
}

export default App;
