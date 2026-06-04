// 🟢 ĐÃ XÓA: Bỏ dòng import ResetPasswordPage thừa thãi ở đây đi

export const routes = {
  // Public
  home: '/',
  login: '/auth/login',
  register: '/auth/register',
  forgotPassword: '/auth/forgot-password',
  resetPassword: '/auth/reset-password', // 🟢 Đug chuẩn: Chỉ khai báo chuỗi đường dẫn (string url) giống các route khác

  // HR Admin
  dashboard: '/admin/dashboard',
  jobPostings: '/admin/jobs',
  jobPostingCreate: '/admin/jobs/create',
  jobPostingDetail: (id: string) => `/admin/jobs/${id}`,
  candidates: '/admin/candidates',
  candidateDetail: (id: string) => `/admin/candidates/${id}`,
  evaluations: '/admin/evaluations',
  reports: '/admin/reports',
  settings: '/admin/settings',

  // Candidate
  candidateApply: '/candidate/applications',
  candidateInterview: '/candidate/interviews',
  candidatePortal: '/candidate/portal',
  candidateResult: (id: string) => `/candidate/results/${id}`,

  // Interview
  interviewRoom: '/interview/room/:sessionId',
  interviewKiosk: '/kiosk',
} as const;

// Đổi tên type để không trùng với thư viện react-router-dom
export type AppRoutesType = typeof routes;