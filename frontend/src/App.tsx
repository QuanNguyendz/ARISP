import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuthStore } from '@store/auth/authStore';

import AuthLayout from '@components/layout/AuthLayout';
import MainLayout from '@components/layout/MainLayout';
import InterviewLayout from '@components/layout/InterviewLayout';

import LoginPage from '@pages/auth/LoginPage';
import MagicLinkCallbackPage from '@pages/auth/MagicLinkCallbackPage';

import DashboardPage from '@pages/admin/DashboardPage';
import JobPostingsPage from '@pages/admin/JobPostingsPage';
import JobPostingDetailPage from '@pages/admin/JobPostingDetailPage';
import CreateJobPostingPage from '@pages/admin/CreateJobPostingPage';
import CandidatesPage from '@pages/admin/CandidatesPage';
import InterviewSessionsPage from '@pages/admin/InterviewSessionsPage';
import EvaluationReviewPage from '@pages/admin/EvaluationReviewPage';
import PlaybooksPage from '@pages/admin/PlaybooksPage';
import SettingsPage from '@pages/admin/SettingsPage';
import TeamPage from '@pages/admin/TeamPage';

import CandidateDashboardPage from '@pages/candidate/DashboardPage';
import MyApplicationsPage from '@pages/candidate/MyApplicationsPage';
import InterviewSchedulePage from '@pages/candidate/InterviewSchedulePage';
import RecordingPage from '@pages/candidate/RecordingPage';
import FeedbackPage from '@pages/candidate/FeedbackPage';

import InterviewRoomPage from '@pages/interview/InterviewRoomPage';
import PracticeSessionPage from '@pages/interview/PracticeSessionPage';

import JobBoardPage from '@pages/job-board/JobBoardPage';
import JobDetailPage from '@pages/job-board/JobDetailPage';

import KioskPage from '@pages/kiosk/KioskPage';

import NotFoundPage from '@pages/NotFoundPage';

const ProtectedRoute = ({ children, allowedRoles }: { children: React.ReactNode; allowedRoles?: string[] }) => {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/auth/login" replace />;
  }

  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <>{children}</>;
};

function App() {
  return (
    <Routes>
      <Route path="/auth">
        <Route element={<AuthLayout />}>
          <Route path="login" element={<LoginPage />} />
          <Route path="magic-callback" element={<MagicLinkCallbackPage />} />
        </Route>
      </Route>

      <Route path="/admin">
        <Route element={<ProtectedRoute allowedRoles={['SuperAdmin', 'HRAdmin', 'Recruiter']} />}>
          <Route element={<MainLayout />}>
            <Route index element={<Navigate to="/admin/dashboard" replace />} />
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="job-postings">
              <Route index element={<JobPostingsPage />} />
              <Route path="create" element={<CreateJobPostingPage />} />
              <Route path=":id" element={<JobPostingDetailPage />} />
              <Route path=":id/edit" element={<CreateJobPostingPage />} />
            </Route>
            <Route path="candidates" element={<CandidatesPage />} />
            <Route path="interviews">
              <Route index element={<InterviewSessionsPage />} />
              <Route path=":sessionId/review" element={<EvaluationReviewPage />} />
            </Route>
            <Route path="playbooks" element={<PlaybooksPage />} />
            <Route path="team" element={<TeamPage />} />
            <Route path="settings" element={<SettingsPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/candidate">
        <Route element={<ProtectedRoute allowedRoles={['Candidate']} />}>
          <Route element={<MainLayout />}>
            <Route index element={<Navigate to="/candidate/dashboard" replace />} />
            <Route path="dashboard" element={<CandidateDashboardPage />} />
            <Route path="applications" element={<MyApplicationsPage />} />
            <Route path="schedule" element={<InterviewSchedulePage />} />
            <Route path="recordings" element={<RecordingPage />} />
            <Route path="feedback" element={<FeedbackPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/interview">
        <Route element={<InterviewLayout />}>
          <Route path="room/:sessionId" element={<InterviewRoomPage />} />
          <Route path="practice/:applicationId" element={<PracticeSessionPage />} />
        </Route>
      </Route>

      <Route path="/jobs">
        <Route element={<MainLayout />}>
          <Route index element={<JobBoardPage />} />
          <Route path=":id" element={<JobDetailPage />} />
        </Route>
      </Route>

      <Route path="/kiosk">
        <Route element={<InterviewLayout />}>
          <Route index element={<KioskPage />} />
        </Route>
      </Route>

      <Route path="/" element={<Navigate to="/jobs" replace />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}

export default App;
