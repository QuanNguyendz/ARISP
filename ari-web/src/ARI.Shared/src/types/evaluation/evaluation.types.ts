export interface EvaluationReport {
  id: string;
  sessionId: string;
  applicationId: string;
  roundNumber: number;
  sessionType: string;
  aiVerdict: 'pass' | 'not_pass' | string;
  overallScore?: number | null;
  criterionScores?: Record<string, number> | null;
  /**
   * Điểm từng tiêu chí kèm nhãn + trọng số doanh nghiệp khai, chụp lại lúc chấm (ADR-060).
   * Null với bản đánh giá chấm trước khi tin khai bộ tiêu chí — khi đó chỉ có `criterionScores`.
   */
  criterionDetails?: CriterionDetail[] | null;
  reasoning?: string | null;
  recommendedNextStep?: string | null;
  questionAnalyses?: QuestionAnalysis[] | null;
  cheatScore?: number | null;
  cheatSignals?: CheatSignal[] | null;
  languageAssessment?: LanguageAssessment | null;
  createdAt: string;
  updatedAt?: string;
  candidateName?: string;
  candidateEmail?: string;
  jobTitle?: string;
  status?: 'pending' | 'completed';
  finalVerdict?: 'pass' | 'not_pass' | string;
  hrReview?: HRReview | null;
  /** Video buổi phỏng vấn thật (ADR-052) — null nếu chưa quay hoặc đã quá hạn lưu. */
  recordingUrl?: string | null;
  recordingExpiresAt?: string | null;
  recordingDeletedAt?: string | null;
  /** Điểm khớp CV-JD do Gemini chấm lúc ứng tuyển (ADR-030). Null = hồ sơ chưa có phân tích. */
  cvMatchScore?: number | null;
  cvMatchSummary?: string | null;

  /** ===== Người có thẩm quyền chốt kết quả này (ADR-061) ===== */
  jobPostingId?: string;
  /** Tin có Hiring Manager chính không. False = mọi thứ như trước ADR-061. */
  requiresHmApproval?: boolean;
  hiringManagerUserId?: string | null;
  hiringManagerName?: string | null;
}

export interface CriterionScore {
  name: string;
  score: number;
  maxScore: number;
  reasoning: string;
}

/** Một dòng điểm tiêu chí theo rubric doanh nghiệp (ADR-060). */
export interface CriterionDetail {
  key: string;
  score: number;
  /** Tên hiển thị doanh nghiệp đặt — null với bản đánh giá cũ. */
  label?: string | null;
  /** Trọng số (%) lúc chấm — null với bản đánh giá cũ. */
  weight?: number | null;
}

export interface LanguageAssessment {
  language: string;
  cefrLevel?: string | null;
  fluency: number;
  grammar: number;
  vocabulary: number;
  comprehension: number;
  overallScore: number;
}

export interface CheatSignal {
  type: 'eye_tracking' | 'response_timing' | 'speech_pattern' | 'tab_switch' | string;
  severity: 'low' | 'medium' | 'high' | string;
  description: string;
  timestamp?: string;
}

export interface QuestionAnalysis {
  question: string;
  answer: string;
  score: number;
  analysis: string;
  feedback?: string;
}

export interface HRReview {
  id: string;
  evaluationId: string;
  reviewedByUserId: string;
  finalVerdict: 'pass' | 'not_pass' | string;
  isOverride: boolean;
  overrideReason?: string;
  shareRecording: boolean;
  shareTranscript: boolean;
  shareEvaluation: boolean;
  shareFeedback: boolean;
  candidateFeedback?: string;
  createdAt: string;
  updatedAt: string;

  /** ===== ADR-061 ===== */
  /** Ảnh chụp vai trò người chốt lúc chốt: hiring_manager | hr_admin | super_admin. */
  reviewerRole?: string | null;
  /** Quản trị viên đã chốt THAY Hiring Manager của tin. */
  isHrFallback?: boolean;
  fallbackReason?: string | null;
  /** Đề xuất cấp bậc + dải lương — nguồn điền sẵn cho thư mời nhận việc. */
  suggestedLevel?: string | null;
  suggestedSalaryMin?: number | null;
  suggestedSalaryMax?: number | null;
  suggestedSalaryCurrency?: string | null;
  strengths?: string | null;
  concerns?: string | null;
}

export interface EvaluationFilter {
  jobPostingId?: string;
  status?: 'completed' | 'pending' | 'pass' | 'not_pass';
  page?: number;
  pageSize?: number;
}

export interface SubmitEvaluationReviewPayload {
  evaluationId: string;
  finalVerdict: 'pass' | 'not_pass';
  overrideReason?: string;
  shareRecording?: boolean;
  shareTranscript?: boolean;
  shareEvaluation?: boolean;
  shareFeedback?: boolean;
  candidateFeedback?: string;

  /** ===== ADR-061 ===== */
  /**
   * Lý do quản trị viên chốt THAY Hiring Manager. Server bắt buộc tối thiểu 10 ký tự khi tin có
   * Hiring Manager mà người chốt không phải người đó; bỏ qua ở mọi trường hợp khác.
   */
  fallbackReason?: string;
  /** Đề xuất của người chốt, dùng để điền sẵn thư mời nhận việc. */
  suggestedLevel?: string;
  suggestedSalaryMin?: number;
  suggestedSalaryMax?: number;
  suggestedSalaryCurrency?: string;
  strengths?: string;
  concerns?: string;
}
