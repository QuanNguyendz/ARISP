export interface EvaluationReport {
  id: string;
  sessionId: string;
  verdict: 'Pass' | 'NotPass';
  overallScore: number;
  criteria: CriterionScore[];
  languageAssessment?: LanguageAssessment;
  cheatScore?: number;
  cheatSignals?: CheatSignal[];
  perQuestionAnalysis: QuestionAnalysis[];
  recommendedNextStep?: string;
  generatedAt: Date;
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
  type: 'eye_tracking' | 'response_timing' | 'speech_pattern' | 'tab_switch';
  severity: 'low' | 'medium' | 'high';
  description: string;
  timestamp?: Date;
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
  hrDecision: 'confirmed' | 'overridden';
  overrideReason?: string;
  reviewedBy: string;
  reviewedAt: Date;
}

export interface EvaluationFilter {
  jobPostingId?: string;
  applicationId?: string;
  verdict?: 'Pass' | 'NotPass' | 'Pending';
  dateFrom?: Date;
  dateTo?: Date;
}
