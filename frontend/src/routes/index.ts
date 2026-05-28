export const routes = {
  // Public
  home: '/',
  login: '/dang-nhap',
  register: '/dang-ky',
  
  // HR Admin
  dashboard: '/quan-ly',
  jobPostings: '/quan-ly/tin-tuyen-dung',
  jobPostingCreate: '/quan-ly/tin-tuyen-dung/tao-moi',
  jobPostingDetail: (id: string) => `/quan-ly/tin-tuyen-dung/${id}`,
  candidates: '/quan-ly/ung-vien',
  candidateDetail: (id: string) => `/quan-ly/ung-vien/${id}`,
  evaluations: '/quan-ly/danh-gia',
  reports: '/quan-ly/bao-cao',
  settings: '/quan-ly/cai-dat',
  
  // Candidate
  candidateApply: '/ung-vien/ung-tuyen',
  candidateInterview: '/ung-vien/phong-van',
  candidatePortal: '/ung-vien/cong-cua',
  candidateResult: (id: string) => `/ung-vien/ket-qua/${id}`,
  
  // Interview
  interviewRoom: '/phong-van/:sessionId',
  interviewKiosk: '/kiosk',
} as const;

export type Routes = typeof routes;
