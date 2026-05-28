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
      <Route path="/nha-tuyen-dung" element={<HomePage />} />

      {/* employer Login/Register */}
      <Route path="/dang-nhap" element={<LoginPage />} />
      <Route path="/dang-ky" element={<RegisterPage />} />

      {/* Candidate Login/Register */}
      <Route path="/dang-nhap-ung-vien" element={<CandidateLoginPage />} />
      <Route path="/dang-ky-ung-vien" element={<CandidateRegisterPage />} />

      {/* ==================== CANDIDATE PROTECTED ROUTES ==================== */}

      <Route element={<CandidateLayout />}>
        <Route path="/ung-vien" element={<CandidateHome />} />
        <Route path="/ung-vien/ho-so" element={<CandidateHome />} />
        <Route path="/ung-vien/cong-viec" element={<CandidateHome />} />
      </Route>

      {/* Candidate Pages (standalone) */}
      <Route path="/ung-vien/ung-tuyen" element={<CandidateApply />} />
      <Route path="/ung-vien/ung-tuyen/:id" element={<CandidateApply />} />
      <Route path="/ung-vien/phong-van" element={<InterviewSchedulePage />} />
      <Route path="/ung-vien/cong-cua" element={<PortalPage />} />
      <Route path="/ung-vien/ket-qua" element={<FeedbackPage />} />
      <Route path="/ung-vien/ket-qua/:id" element={<FeedbackPage />} />

      {/* ==================== HR ADMIN ROUTES ==================== */}

      <Route element={<AdminLayout />}>
        <Route path="/quan-ly" element={<DashboardPage />} />
        <Route path="/quan-ly/tin-tuyen-dung" element={<JobPostingsPage />} />
        <Route path="/quan-ly/tin-tuyen-dung/tao-moi" element={<CreateJobPostingPage />} />
        <Route path="/quan-ly/tin-tuyen-dung/:id" element={<JobPostingDetailPage />} />
        <Route path="/quan-ly/ung-vien" element={<CandidatesPage />} />
        <Route path="/quan-ly/ung-vien/:id" element={<CandidateDetailPage />} />
        <Route path="/quan-ly/danh-gia" element={<EvaluationReviewPage />} />
        <Route path="/quan-ly/bao-cao" element={<ReportsPage />} />
        <Route path="/quan-ly/cai-dat" element={<SettingsPage />} />
        <Route path="/quan-ly/playbooks" element={<PlaybooksPage />} />
        <Route path="/quan-ly/nhom" element={<TeamPage />} />
        <Route path="/quan-ly/phong-van" element={<InterviewSessionsPage />} />
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
