export const routes = {
  // Public
  home: '/',
  login: '/auth/login',
  register: '/auth/register',

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

export type Routes = typeof routes;
