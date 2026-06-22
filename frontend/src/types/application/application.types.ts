export interface Application {
  id: string
  jobPostingId: string
  jobTitle: string
  organizationId: string
  organizationName: string
  candidateId: string
  candidateName: string
  candidateEmail: string
  cvUrl?: string
  status: ApplicationStatus
  hasPracticeSession: boolean
  appliedAt: Date
  updatedAt: Date
}

export type ApplicationStatus =
  | 'pending_review'
  | 'approved'
  | 'interview_scheduled'
  | 'interview_completed'
  | 'offer_extended'
  | 'rejected'

export interface ApplicationDetail extends Application {
  interviews: ApplicationInterviewSession[]
  notes?: string
}

export interface ApplicationInterviewSession {
  id: string
  applicationId: string
  round: number
  type: 'Screening' | 'Technical'
  sessionType: 'real' | 'practice'
  status: SessionStatus
  scheduledAt?: Date
  startedAt?: Date
  endedAt?: Date
  recordingUrl?: string
  verdict?: 'Pass' | 'NotPass'
  overallScore?: number
}

export type SessionStatus = 'scheduled' | 'in_progress' | 'completed' | 'cancelled'

export interface CreateApplicationRequest {
  jobPostingId: string
  cvFile?: File
}

/** Một vòng phỏng vấn trong tiến trình của ứng viên (GET /portal/applications). */
export interface MyApplicationRound {
  roundNumber: number
  roundType: string
  sessionType: string
  status: string
  verdict?: string | null
  overallScore?: number | null
}

/**
 * Item hồ sơ ứng tuyển của chính ứng viên — khớp JSON từ GET /api/portal/applications.
 * Status là chuỗi thô từ backend, map nhãn ở UI.
 */
/** Mã phỏng vấn On-site còn hiệu lực (nhập tại Kiosk). */
export interface MyApplicationCode {
  code: string
  expiresAt: string
  roundNumber: number
}

/** Lịch phỏng vấn sắp tới (booking đã đặt). */
export interface MyApplicationUpcoming {
  startTime: string
  timezone?: string | null
  roundNumber: number
}

export interface MyApplicationItem {
  id: string
  jobPostingId: string
  jobTitle?: string | null
  location?: string | null
  department?: string | null
  interviewMode?: string | null
  candidateEmail: string
  candidateName: string
  status: string
  matchScore?: number | null
  cvFileUrl?: string | null
  practiceSessionUsed: boolean
  practiceAvailable: boolean
  /** Vòng đang hoạt động (vòng được mời mới nhất) — dùng cho phỏng vấn thử theo vòng */
  activeRound?: number
  pendingHrReview: boolean
  hrFeedback?: string | null
  source: string
  createdAt: string
  updatedAt: string
  rounds: MyApplicationRound[]
  interviewCode?: MyApplicationCode | null
  upcomingInterview?: MyApplicationUpcoming | null
}

// ===== Chi tiết hồ sơ ứng tuyển của ứng viên (GET /api/portal/applications/{id}) =====

export interface MyEvalCriterion {
  name: string
  score: number
}

export interface MyEvalQuestion {
  question: string
  answer: string
  score: number
  analysis: string
  feedback?: string | null
}

export interface MyEvalLanguage {
  language: string
  fluency: number
  grammar: number
  vocabulary: number
  comprehension: number
  overallScore: number
}

/** Báo cáo đánh giá đã được HR chia sẻ cho ứng viên (chỉ có khi ShareEvaluation). */
export interface MySharedEvaluation {
  id: string
  roundNumber: number
  aiVerdict: string
  overallScore?: number | null
  reasoning?: string | null
  recommendedNextStep?: string | null
  criterionScores: MyEvalCriterion[]
  questionAnalyses: MyEvalQuestion[]
  languageAssessment?: MyEvalLanguage | null
}

/** Một vòng phỏng vấn trong chi tiết hồ sơ (kèm bản ghi/feedback/đánh giá nếu được chia sẻ). */
export interface MyApplicationSession {
  id: string
  roundNumber: number
  roundType: string
  sessionType: string
  status: string
  startedAt?: string | null
  endedAt?: string | null
  durationSeconds?: number | null
  recordingUrl?: string | null
  transcriptShared: boolean
  pendingHrReview: boolean
  hrFeedback?: string | null
  hrFinalVerdict?: string | null
  evaluation?: MySharedEvaluation | null
}

/** Chi tiết một hồ sơ ứng tuyển của chính ứng viên. */
export interface MyApplicationDetail {
  id: string
  jobPostingId: string
  jobTitle?: string | null
  jobDescription?: string | null
  location?: string | null
  department?: string | null
  interviewMode?: string | null
  detectedLanguage?: string | null
  candidateEmail: string
  candidateName: string
  candidatePhone?: string | null
  cvFileUrl?: string | null
  status: string
  createdAt: string
  updatedAt: string
  interviewCode?: MyApplicationCode | null
  upcomingInterview?: MyApplicationUpcoming | null
  sessions: MyApplicationSession[]
}

/**
 * Item hồ sơ ứng tuyển cho danh sách HR/Recruiter — khớp đúng JSON từ GET /applications
 * (ApplicationResponse ở backend). Status là chuỗi thô từ backend, map nhãn ở UI.
 */
export interface HrApplicationItem {
  id: string
  jobPostingId: string
  jobTitle?: string
  candidateEmail: string
  candidateName: string
  candidatePhone?: string
  cvFileUrl?: string
  source: string
  status: string
  practiceSessionUsed: boolean
  createdAt: string
  cvJdAnalysisId?: string
  matchScore?: number | null
}
