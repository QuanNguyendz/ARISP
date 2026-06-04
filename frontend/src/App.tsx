import { Routes, Route } from 'react-router-dom';
import { useEffect } from 'react';
import { useAuthStore } from '@store/auth/authStore';
import { getDevAuth, isDevMode } from '@utils/devAuth';

import AdminLayout from '@components/layout/AdminLayout';
import CandidateLayout from '@components/layout/CandidateLayout';
import InterviewLayout from '@components/layout/InterviewLayout';

// Auth Pages
import LoginPage from '@pages/auth/LoginPage';
import RegisterPage from '@pages/auth/RegisterPage';
import CandidateLoginPage from '@pages/auth/CandidateLoginPage';
import CandidateRegisterPage from '@pages/auth/CandidateRegisterPage';
import ForgotPasswordPage from '@pages/auth/ForgotPasswordPage';
import ResetPasswordPage from '@pages/auth/ResetPasswordPage';

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
  }, [isDevMode, isAuthenticated, setAuth]);

  return (
    <Routes>
      {/* ==================== PUBLIC ROUTES ==================== */}

      {/* Root - Job Board */}
      <Route path="/" element={<FindJobPage />} />

      {/* Employer Landing Page */}
      <Route path="/employer" element={<HomePage />} />

      {/* employer Login/Register */}
      <Route path="/auth/login" element={<LoginPage />} />
      <Route path="/auth/register" element={<RegisterPage />} />

      {/* Candidate Login/Register */}
      <Route path="/auth/candidate-login" element={<CandidateLoginPage />} />
      <Route path="/auth/candidate-register" element={<CandidateRegisterPage />} />

      {/* Reset Password Route */}
      <Route path="/auth/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/auth/reset-password" element={<ResetPasswordPage />} />

      {/* ==================== CANDIDATE PROTECTED ROUTES ==================== */}

      <Route element={<CandidateLayout />}>
        <Route path="/candidate/dashboard" element={<CandidateHome />} />
        <Route path="/candidate/profile" element={<CandidateHome />} />
        <Route path="/candidate/jobs" element={<CandidateHome />} />
      </Route>

      {/* Candidate Pages (standalone) */}
      <Route path="/candidate/applications" element={<CandidateApply />} />
      <Route path="/candidate/applications/:id" element={<CandidateApply />} />
      <Route path="/candidate/interviews" element={<InterviewSchedulePage />} />
      <Route path="/candidate/portal" element={<PortalPage />} />
      <Route path="/candidate/results" element={<FeedbackPage />} />
      <Route path="/candidate/results/:id" element={<FeedbackPage />} />

      {/* ==================== HR ADMIN ROUTES ==================== */}

      <Route element={<AdminLayout />}>
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
      <Route path="/interview" element={<InterviewLayout />}>
        <Route path="room/:sessionId" element={<InterviewRoomPage />} />
        <Route path="practice/:applicationId" element={<PracticeSessionPage />} />
      </Route>

      {/* Kiosk */}
      <Route path="/kiosk" element={<InterviewLayout />}>
        <Route index element={<KioskPage />} />
      </Route>

      {/* Public Pages */}
      <Route path="/jobs" element={<FindJobPage />} />
      <Route path="/jobs/:id" element={<JobDetailPage />} />

      {/* 404 */}
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}

export default App;
