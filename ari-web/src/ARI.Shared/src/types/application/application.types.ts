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
  /** Loại vòng — vòng trắc nghiệm là "bài trắc nghiệm", không phải "phỏng vấn". */
  roundType?: string | null
}

export interface MyApplicationItem {
  id: string
  jobPostingId: string
  jobTitle?: string | null
  /** Tin đã kết thúc tuyển (đóng VÀ qua ngày đi làm dự kiến) — xem `JobClosure` phía server. */
  jobClosed?: boolean
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
/**
 * Nhận xét AI của buổi thử đang ở đâu (ADR-073) — server suy ra: `done` có nhận xét · `pending` đang chấm (kể cả
 * đang tự thử lại) · `needs_rubric` vị trí chưa có bộ tiêu chí · `no_answers` không có câu trả lời · `failed` hỏng
 * sau nhiều lần thử. `null` với phiên chưa kết thúc.
 */
export type PracticeEvaluationState = 'done' | 'pending' | 'needs_rubric' | 'no_answers' | 'failed'

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
  evaluationState?: PracticeEvaluationState | null
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
  evaluationState?: PracticeEvaluationState | null
}

/** Chi tiết một hồ sơ ứng tuyển của chính ứng viên. */
export interface MyApplicationDetail {
  id: string
  jobPostingId: string
  jobTitle?: string | null
  /** Tin đã đóng hoặc lưu trữ — vị trí này không còn tuyển. */
  jobClosed?: boolean
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
/**
 * Trạng thái điểm CV của hồ sơ (ADR-070) — do server suy ra:
 * - `scored`: đã chấm theo bộ tiêu chí hiện hành
 * - `rescoring`: có điểm theo bộ tiêu chí cũ, đang chấm lại theo bộ mới
 * - `queued`: chưa có điểm, đang/sắp chấm
 * - `scoring_failed`: lượt chấm gần nhất hỏng, hệ thống tự thử lại (xem `retryAt`)
 * - `pending_rubric`: tin chưa có bộ tiêu chí — chờ HM khai
 * - `invalid_cv`: file không phải CV
 * - `no_cv`: hồ sơ không có file CV
 */
export type CvScoreState =
  | 'scored'
  | 'rescoring'
  | 'queued'
  | 'scoring_failed'
  | 'pending_rubric'
  | 'invalid_cv'
  | 'no_cv'

/** Lý do một lượt chấm CV hỏng (server phân loại, không in nguyên lỗi của nhà cung cấp AI). */
export type CvScoreFailureReason = 'ai_unavailable' | 'cv_unreadable' | 'no_criterion_scored'

export type CvScoreBand = 'excellent' | 'good' | 'fair' | 'poor'

export interface CvScoreCriterion {
  key: string
  label: string
  weight?: number | null
  score?: number | null
  /** Số điểm tiêu chí góp vào điểm cuối = điểm × trọng số ÷ Σ trọng số. */
  contribution?: number | null
  band?: CvScoreBand | null
  /** Trích nguyên văn từ CV làm căn cứ. */
  evidence?: string | null
  reasoning?: string | null
  description?: string | null
  levels?: {
    excellent?: string | null
    good?: string | null
    fair?: string | null
    poor?: string | null
  } | null
  /** Khoảng điểm của dải (vd 90–100). */
  bandMin?: number | null
  bandMax?: number | null
  /** `checklist` = vị trí trong dải tính từ ý kiểm · `ai` = AI ước lượng (tiêu chí chưa có ý kiểm). */
  scoreSource?: 'checklist' | 'ai' | string | null
  /** Số ý đạt / số ý AI đã trả lời. */
  checksMet?: number | null
  checksAnswered?: number | null
  /** Σ trọng số ý đạt / Σ trọng số ý đã trả lời (ADR-075) — bằng hai số trên khi mọi ý ×1. */
  checksMetWeight?: number | null
  checksAnsweredWeight?: number | null
  checks?: CvScoreCheck[]
  /** Vị trí 0..1 AI cho trong dải (tiêu chí không có ý kiểm, ADR-075). */
  position?: number | null
  /** Điểm tối thiểu của tiêu chí + kết quả cổng (ADR-075). */
  minScore?: number | null
  gate?: CvGateOutcome | null
}

/** Kết quả một cổng của công thức (ADR-075). `unknown` = chưa xác minh được — cần người kiểm, không ép khuyến nghị. */
export type CvGateOutcome = 'pass' | 'fail' | 'unknown'

/** Kết quả gộp các cổng của một bản chấm; `null` = bộ tiêu chí không có cổng. */
export type CvGateStatus = 'pass' | 'fail' | 'review'

/** Một cổng: điều kiện bắt buộc, hoặc điểm tối thiểu của một tiêu chí (ADR-075). */
export interface CvScoreGate {
  key: string
  label: string
  type: 'knockout' | 'min_score'
  outcome: CvGateOutcome
  /** `knockout_failed` | `knockout_unverified` | `below_min_score` | `min_score_unscored`. */
  reason?: string | null
  score?: number | null
  minScore?: number | null
  /** Điều kiện bị AI đánh "đạt" mà không trích được bằng chứng. */
  unsupported?: boolean
  evidence?: string | null
  reasoning?: string | null
  description?: string | null
}

/** Công thức cấp tin đã áp cho một bản chấm (ảnh chụp). */
export interface CvScorePolicy {
  bands: { excellentFrom: number; goodFrom: number; fairFrom: number }
  tiers: { strongHireFrom: number; hireFrom: number; cautionFrom: number }
}

/** Một ý kiểm đã được AI trả lời. */
export interface CvScoreCheck {
  key: string
  text: string
  /** true = đạt · false = không đạt · null = AI không trả lời (không tính vào mẫu số). */
  met?: boolean | null
  evidence?: string | null
  /** AI đánh đạt nhưng không trích được bằng chứng — không tính. */
  unsupported?: boolean
  /** ×1 / ×2 / ×3 (ADR-075). */
  weight?: number
}

/** Cách ra điểm CV — chỉ có ở màn chi tiết hồ sơ (ADR-070). */
export interface CvScoreBreakdown {
  state: CvScoreState
  total?: number | null
  /** Kết quả phép chia trước khi làm tròn. */
  exactTotal?: number | null
  weightedSum: number
  totalWeight: number
  criteria: CvScoreCriterion[]
  /** Tiêu chí AI không chấm được — bị loại khỏi CẢ tử lẫn mẫu. */
  excluded: CvScoreCriterion[]
  scoredAt?: string | null
  model?: string | null
  isCurrentRubric: boolean
  rubricSavedAt?: string | null
  rubricSavedBy?: string | null
  invalidReason?: string | null
  /** Khi `state = scoring_failed`. */
  failureReason?: CvScoreFailureReason | string | null
  failedAttempts?: number | null
  /** Lúc hệ thống tự chấm lại — đã qua nghĩa là đang chờ lượt quét kế tiếp. */
  retryAt?: string | null
  summary?: string | null
  skillsMatched: string[]
  skillsGaps: string[]
  redFlags: string[]
  seniorityAlignment?: string | null
  experienceRelevance?: string | null
  /**
   * Nhãn cuối: Strong Hire | Hire | Proceed with caution | Reject — theo ngưỡng khuyến nghị của tin, bị ép "Reject" khi
   * trượt cổng (ADR-075).
   */
  recommendation?: string | null
  /** Nhãn theo riêng điểm tổng (chưa xét cổng). */
  scoreRecommendation?: string | null
  gateStatus?: CvGateStatus | null
  gates?: CvScoreGate[]
  /** Công thức đã áp cho bản chấm này. */
  policy?: CvScorePolicy | null
  /** Bản chấm được tính lại theo công thức mới từ câu trả lời cũ của AI — không có lời gọi AI mới. */
  derived?: boolean
  /** Lúc AI thật sự đọc CV. */
  observedAt?: string | null
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
  /** Điểm CV theo bộ tiêu chí (ADR-070). Null khi chưa có điểm hợp lệ — xem `cvScoreStatus`. */
  matchScore?: number | null
  cvScoreStatus?: CvScoreState | null
  /** Khi `cvScoreStatus = scoring_failed`: lúc hệ thống tự chấm lại. */
  cvScoreRetryAt?: string | null
  /** Nhãn khuyến nghị của điểm CV (ADR-075) — theo công thức của tin; tô màu theo nhãn này, không theo số viết cứng. */
  cvRecommendation?: string | null
  /** Kết quả cổng của công thức — chỉ khi có điểm. */
  cvGateStatus?: CvGateStatus | null
  cvJdSummary?: string
  /** Cách ra điểm CV — chỉ có ở màn chi tiết hồ sơ. */
  cvScore?: CvScoreBreakdown | null
  /** Ứng viên đã đặt lịch phỏng vấn thật → đủ điều kiện cấp Interview Code On-site (ADR-015/016). */
  hasScheduledInterview?: boolean
  currentRound?: number | null
  coverLetter?: string
  noticePeriod?: string
  /**
   * Bước "Xác thực thông tin" lúc ứng tuyển: `match` | `mismatch`. Trống = hồ sơ nộp trước khi có
   * tính năng này, hoặc lần đó không đối chiếu được (mất mạng, CV không đọc ra chữ).
   */
  contactVerificationStatus?: string | null
  /** Chi tiết chỗ lệch — chỉ có nghĩa khi `contactVerificationStatus = mismatch`. */
  contactVerificationDetails?: string | null
  /** Cổng duyệt của Hiring Manager (ADR-061): pending | approved | rejected | bypassed. */
  hmDecision?: string | null
  hmDecisionNote?: string | null
  /**
   * Việc đang thật sự diễn ra ở vòng hiện tại (ADR-067) — `status` chỉ nói hồ sơ ở KHÚC nào của
   * phễu, nên nó đứng yên ở "interview" suốt cả một vòng. Rỗng = dùng lại `status` như trước.
   */
  stageStatus?: string | null
  hmDecidedAt?: string | null
  interviewScore?: number | null
  interviewDate?: string
  /**
   * Điểm bài trắc nghiệm của VÒNG HIỆN TẠI (0–100) — null khi vòng này không phải vòng trắc nghiệm
   * hoặc ứng viên chưa nộp bài. Vòng trắc nghiệm không có `Evaluation` nên `interviewScore` luôn
   * trống ở đó; đây là con số duy nhất nói được ứng viên làm bài ra sao.
   */
  onlineTestScore?: number | null
  /**
   * Bài trắc nghiệm có đạt điểm sàn không (null khi chưa nộp). **Không tự đổi trạng thái hồ sơ** —
   * dưới sàn thì ứng viên vẫn ở nguyên vòng trắc nghiệm cho tới khi Recruiter quyết định loại.
   */
  onlineTestPassed?: boolean | null
  /**
   * Bài trắc nghiệm do hệ thống nộp thay khi hết hạn — ứng viên được hẹn giờ nhưng không vào làm.
   * Điểm vẫn là điểm thật (thường là 0); cờ này để bảng nói "không làm bài" thay vì trông như
   * "làm sai hết". Null khi chưa có bài.
   */
  onlineTestExpired?: boolean | null
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
