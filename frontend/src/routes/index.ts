export const routes = {
  // Public
  home: '/',
  login: '/auth/login',
  register: '/auth/register',

  // HR Admin
  dashboard: '/admin/dashboard',
  jobPostings: '/quan-ly/tin-tuyen-dung',
  jobPostingCreate: '/quan-ly/tin-tuyen-dung/tao-moi',
  jobPostingDetail: (id: string) => `/quan-ly/tin-tuyen-dung/${id}`,
  candidates: '/quan-ly/ung-vien',
  candidateDetail: (id: string) => `/quan-ly/ung-vien/${id}`,
  evaluations: '/quan-ly/danh-gia',
  reports: '/admin/reports',
  settings: '/quan-ly/cai-dat',

  // Candidate
  candidateApply: '/candidate/applications',
  candidateInterview: '/candidate/interviews',
  candidatePortal: '/candidate/portal',
  candidateResult: (id: string) => `/candidate/results/${id}`,

  // Interview
  interviewRoom: '/interview/room/:sessionId',
  interviewKiosk: '/kiosk',
} as const;

export type Routes = typeof routes;
