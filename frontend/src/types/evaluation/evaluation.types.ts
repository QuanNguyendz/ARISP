export interface EvaluationReport {
  id: string;
  sessionId: string;
  applicationId: string;
  roundNumber: number;
  sessionType: string;
  aiVerdict: 'pass' | 'not_pass' | string;
  overallScore?: number | null;
  criterionScores?: Record<string, number> | null;
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
}

export interface CriterionScore {
  name: string;
  score: number;
  maxScore: number;
  reasoning: string;
}

export interface LanguageAssessment {
  language: string;
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
}
