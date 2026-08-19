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
  /** Tổng số vòng theo cấu hình job — "Đạt" chỉ khi qua hết (ADR-053). */
  totalRounds?: number
  /** Số vòng THẬT đã được HR xác nhận Đạt — dựng nhãn "Qua vòng N/M". */
  passedRounds?: number
  pendingHrReview: boolean
  /** Lịch phỏng vấn thật đã qua giờ mà ứng viên chưa vào phòng — cần liên hệ nhân sự xếp lại. */
  missedInterview?: MyApplicationUpcoming | null
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
  /**
   * Tên tiêu chí do doanh nghiệp đặt, chụp lại lúc chấm (ADR-060). Null với bản đánh giá cũ —
   * lúc đó mới rơi về từ điển nhãn i18n.
   */
  label?: string | null
  /** Trọng số (%) của tiêu chí lúc chấm. Null với bản đánh giá cũ. */
  weight?: number | null
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
  scheduledAt?: string | null
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

/**
 * Đánh giá năng lực ngôn ngữ của buổi thử — AI chấm dựa trên chính câu trả lời của ứng viên,
 * kèm bậc CEFR và dẫn chứng (ADR-051).
 */
export interface MyPracticeLanguage extends MyEvalLanguage {
  cefrLevel?: string | null
  languageAdherence?: string | null
  evidence?: string | null
}

/** Buổi phỏng vấn thử đã làm — lối vào trang xem lại (ADR-051). */
export interface MyPracticeSessionItem {
  id: string
  applicationId: string
  jobTitle?: string | null
  roundNumber: number
  roundType: string
  status: string
  startedAt?: string | null
  endedAt?: string | null
  durationSeconds?: number | null
  hasEvaluation: boolean
  overallScore?: number | null
  turnCount: number
}

/**
 * Một lượt hỏi–đáp trong transcript buổi thử. `score`/`analysis`/`feedback` là nhận xét AI
 * của đúng lượt này (backend đã ghép theo `sequenceNumber` — ADR-051).
 */
export interface MyPracticeTurn {
  sequenceNumber: number
  question: string
  questionType?: string | null
  answer?: string | null
  askedAt: string
  answeredAt?: string | null
  responseTimeMs?: number | null
  score?: number | null
  analysis?: string | null
  feedback?: string | null
}

/**
 * Nhận xét AI của buổi thử — CỐ Ý không có verdict Pass/Not Pass: buổi thử chỉ để luyện tập,
 * không phải kết quả tuyển dụng (ADR-051).
 */
export interface MyPracticeEvaluation {
  id: string
  overallScore?: number | null
  reasoning?: string | null
  recommendedNextStep?: string | null
  criterionScores: MyEvalCriterion[]
  languageAssessment?: MyPracticeLanguage | null
  /** Số lượt hỏi–đáp có nhận xét AI kèm theo (phân tích nằm trong `turns`). */
  analyzedTurnCount: number
  createdAt: string
}

/** Chi tiết một buổi thử để xem lại: transcript đầy đủ + nhận xét AI. */
export interface MyPracticeReview {
  id: string
  applicationId: string
  jobTitle?: string | null
  roundNumber: number
  roundType: string
  status: string
  interviewLanguage: string
  /** Ngôn ngữ AI viết nhận xét (theo ngôn ngữ giao diện lúc bắt đầu phiên). */
  reportLanguage?: string | null
  startedAt?: string | null
  endedAt?: string | null
  durationSeconds?: number | null
  closingText?: string | null
  turns: MyPracticeTurn[]
  evaluation?: MyPracticeEvaluation | null
  /** Phiên đã đóng nhưng AI chưa chấm xong. */
  evaluationPending: boolean
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
  totalRounds?: number
  passedRounds?: number
  interviewCode?: MyApplicationCode | null
  upcomingInterview?: MyApplicationUpcoming | null
  sessions: MyApplicationSession[]
  /** Các buổi thử của hồ sơ (tách khỏi tiến trình vòng thật) — ADR-051. */
  practiceSessions: MyPracticeSessionItem[]
}

/**
 * Item hồ sơ ứng tuyển cho danh sách HR/Recruiter — khớp đúng JSON từ GET /applications
 * (ApplicationResponse ở backend). Status là chuỗi thô từ backend, map nhãn ở UI.
 */
/** Một dòng điểm tiêu chí chấm CV (ADR-060). */
export interface CvCriterionScore {
  key: string
  score: number
  /** Tên hiển thị doanh nghiệp đặt — null với bản phân tích cũ. */
  label?: string | null
  /** Trọng số (%) — null với bản phân tích cũ. */
  weight?: number | null
}

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
  cvJdSummary?: string
  /**
   * Điểm từng tiêu chí chấm CV kèm nhãn + trọng số doanh nghiệp khai (ADR-060).
   * Rỗng khi tin chưa khai bộ tiêu chí chấm CV — khi đó chỉ hiện điểm tổng như trước.
   */
  cvCriterionScores?: CvCriterionScore[]
  /** Ứng viên đã đặt lịch phỏng vấn thật → đủ điều kiện cấp Interview Code On-site (ADR-015/016). */
  hasScheduledInterview?: boolean
  currentRound?: number | null
  coverLetter?: string
  noticePeriod?: string
  interviewScore?: number | null
  interviewDate?: string
  /** Phản hồi của ứng viên với lịch vòng hiện tại: pending | confirmed (null nếu chưa có lịch). */
  scheduleConfirmationStatus?: string | null
  /** Lý do ứng viên báo bận lần xếp lịch gần nhất (khi đang chờ nhân sự xếp lại). */
  scheduleDeclineReason?: string | null

  // Candidate Profile fields (Online Profile)
  candidateHeadline?: string | null
  candidateAbout?: string | null
  candidateLocation?: string | null
  candidateDateOfBirth?: string | null
  candidateLinkedinUrl?: string | null
  candidateGithubUrl?: string | null
  candidatePortfolioUrl?: string | null
  allowHrViewProfile?: boolean
  candidateSkills?: string[]
  candidateExperience?: Array<{ title: string; organization: string; period: string; description?: string }>
  candidateEducation?: Array<{ school: string; degree: string; period: string; note?: string }>
}
