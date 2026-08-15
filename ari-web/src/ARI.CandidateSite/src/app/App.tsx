import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, lazy, Suspense } from 'react'
import { useAuthStore } from '@ari/shared/store/auth'
import { getDevAuth, isDevMode } from '@ari/shared/utils/devAuth'

// Layouts + route guards: giữ eager vì nhỏ và dùng ở mọi route (bọc các page).
import CandidateLayout from '@/app/layouts/CandidateLayout'
import InterviewLayout from '@/app/layouts/InterviewLayout'
import ProtectedRoute from '@ari/shared/guards/ProtectedRoute'
import GuestRoute from '@ari/shared/guards/GuestRoute'
import CandidateAppLayout from '@/app/layouts/CandidateAppLayout'
import { DocumentViewerProvider } from '@ari/shared/document/DocumentViewer'
import { useAppNotifications } from '@ari/shared/realtime/useAppNotifications'

// Pages: lazy-load → mỗi page thành 1 chunk riêng, chỉ tải khi vào route đó
// (tách cả dep nặng như react-grid-layout ra khỏi bundle chính).
// Auth
const CandidateLoginPage = lazy(() => import('@pages/auth/CandidateLoginPage'))
const CandidateRegisterPage = lazy(() => import('@pages/auth/CandidateRegisterPage'))
const ForgotPasswordPage = lazy(() => import('@ari/shared/authflows/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('@ari/shared/authflows/ResetPasswordPage'))
const OAuthCallbackPage = lazy(() => import('@ari/shared/authflows/OAuthCallbackPage'))
const VerifyEmailPage = lazy(() => import('@pages/auth/VerifyEmailPage'))

// Legal
const TermsPage = lazy(() => import('@pages/legal/TermsPage'))
const PrivacyPolicyPage = lazy(() => import('@pages/legal/PrivacyPolicyPage'))

// Candidate
const CandidateApplicationsPage = lazy(() => import('@pages/candidate/ApplicationsPage'))
const CandidateApplicationDetailPage = lazy(() => import('@pages/candidate/ApplicationDetailPage'))
const CandidateProfilePage = lazy(() => import('@pages/candidate/ProfilePage'))
const SavedJobsPage = lazy(() => import('@pages/candidate/SavedJobsPage'))
const CandidateNotificationsPage = lazy(() => import('@pages/candidate/NotificationsPage'))
const CandidateSettingsPage = lazy(() => import('@pages/candidate/SettingsPage'))
const InterviewSchedulePage = lazy(() => import('@pages/candidate/InterviewSchedulePage'))
const CandidateSchedulePage = lazy(() => import('@pages/candidate/SchedulePage'))
const CandidateOnlineTestPage = lazy(() => import('@pages/candidate/OnlineTestPage'))
const PracticeReviewPage = lazy(() => import('@pages/candidate/PracticeReviewPage'))

// Interview
const InterviewRoomPage = lazy(() => import('@pages/interview/InterviewRoomPage'))
const PracticeSessionPage = lazy(() => import('@pages/interview/PracticeSessionPage'))

// Landing / Job board
const FindJobPage = lazy(() => import('@pages/landing/FindJobPage'))
const JobDetailPage = lazy(() => import('@pages/job-board/JobDetailPage'))
const JobApplyPage = lazy(() => import('@pages/job-board/ApplyPage'))
const KioskPage = lazy(() => import('@pages/kiosk/KioskPage'))
const KioskInterviewPage = lazy(() => import('@pages/kiosk/KioskInterviewPage'))
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

          {/* ==================== PUBLIC ROUTES ==================== */}
          {/* Job board công khai cho khách + ứng viên (site này chỉ phục vụ ứng viên — ADR-046). */}
          <Route path="/" element={<FindJobPage />} />
          {/* Site này không có `/auth/login` — nút quay lại phải trỏ màn đăng nhập ứng viên. */}
          <Route path="/auth/callback" element={<OAuthCallbackPage loginPath="/auth/candidate-login" />} />
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
          {/* Link sâu từ email mời. `standalone` vì route này KHÔNG bọc layout nào nên trang
              phải tự dựng nền + lối quay lại. Tham số `:applicationId` chỉ để giữ nguyên dạng
              link đã gửi trong email — trang trả TOÀN BỘ lịch của ứng viên, không lọc theo hồ sơ. */}
          <Route
            path="/portal/schedule/:applicationId"
            element={<CandidateSchedulePage standalone />}
          />
          <Route path="/jobs" element={<FindJobPage />} />
          <Route path="/jobs/:id" element={<JobDetailPage />} />
          <Route path="/jobs/:id/apply" element={<JobApplyPage />} />

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
            <Route path="/candidate/schedule" element={<CandidateSchedulePage />} />
            <Route path="/candidate/saved-jobs" element={<SavedJobsPage />} />
            <Route path="/candidate/notifications" element={<CandidateNotificationsPage />} />
            <Route path="/candidate/settings" element={<CandidateSettingsPage />} />
            <Route
              path="/candidate/online-test/:applicationId"
              element={<CandidateOnlineTestPage />}
            />
            {/* Xem lại buổi phỏng vấn thử: transcript + nhận xét AI (ADR-051) */}
            <Route path="/candidate/practice/:sessionId" element={<PracticeReviewPage />} />
          </Route>

          {/* Redirect các route ứng viên cũ (đã bị thay bằng job board / hồ sơ ứng tuyển mới) */}
          <Route path="/candidate/dashboard" element={<Navigate to="/" replace />} />
          <Route path="/candidate/jobs" element={<Navigate to="/jobs" replace />} />
          <Route
            path="/candidate/portal"
            element={<Navigate to="/candidate/applications" replace />}
          />

          {/* ========== CANDIDATE ROUTES (Lịch phỏng vấn — chờ redesign) ========== */}
          <Route
            element={
              <ProtectedRoute allowedRoles={['Candidate']}>
                <CandidateLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/candidate/interviews" element={<InterviewSchedulePage />} />
          </Route>

          {/* Màn "Kết quả" cũ (FeedbackPage, mock) đã xoá — kết quả phỏng vấn nằm trong
              chi tiết hồ sơ ứng tuyển (/candidate/applications/:id). Redirect link cũ. */}
          <Route
            path="/candidate/results"
            element={<Navigate to="/candidate/applications" replace />}
          />
          <Route
            path="/candidate/results/:id"
            element={<Navigate to="/candidate/applications" replace />}
          />

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
          {/* Kiosk chạy TOÀN MÀN HÌNH: 2 trang tự dựng shell min-h-screen riêng nên KHÔNG bọc
              InterviewLayout — layout đó là flex container khiến trang co lại vào góc trái,
              lại thêm thanh tiêu đề không thuộc chế độ Kiosk. */}
          <Route path="/kiosk" element={<KioskPage />} />
          {/* Phòng phỏng vấn THẬT tại Kiosk — vào bằng token phiên sau khi nhập mã (ADR-052) */}
          <Route path="/kiosk/interview" element={<KioskInterviewPage />} />

          <Route path="*" element={<Navigate to="/404" replace />} />
          <Route path="/404" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </DocumentViewerProvider>
  )
}

export default App
