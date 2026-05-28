export interface InterviewRoomState {
  sessionId: string;
  status: 'idle' | 'connecting' | 'waiting_for_candidate' | 'in_progress' | 'paused' | 'completed';
  currentRound: number;
  currentQuestionIndex: number;
  totalQuestions: number;
  currentQuestion?: string;
  candidateSpeaking: boolean;
  aiSpeaking: boolean;
  elapsedTime: number;
  remainingTime: number;
  signals: {
    eyeTracking?: EyeTrackingData;
    responseTiming?: ResponseTimingData;
    speechPattern?: SpeechPatternData;
    tabSwitch?: TabSwitchData;
  };
}

export interface InterviewSession {
  id: string;
  applicationId: string;
  round: number;
  type: 'Screening' | 'Technical';
  sessionType: 'real' | 'practice';
  status: 'scheduled' | 'in_progress' | 'completed' | 'cancelled';
  scheduledAt?: Date;
  startedAt?: Date;
  endedAt?: Date;
  recordingUrl?: string;
  verdict?: 'Pass' | 'NotPass';
  overallScore?: number;
}

export interface EyeTrackingData {
  gazeX: number;
  gazeY: number;
  offScreen: boolean;
  timestamp: number;
}

export interface ResponseTimingData {
  questionAskedAt: number;
  answerStartedAt: number;
  responseTimeMs: number;
}

export interface SpeechPatternData {
  cadenceScore: number;
  isReading: boolean;
  confidence: number;
}

export interface TabSwitchData {
  timestamp: number;
  duration: number;
  count: number;
}

export interface InterviewMessage {
  type: 'question' | 'answer' | 'status' | 'error' | 'evaluation' | 'signal';
  payload: unknown;
  timestamp: number;
}

export interface QuestionContext {
  round: number;
  type: 'Screening' | 'Technical';
  questionIndex: number;
  previousQuestions: string[];
  previousAnswers: string[];
  jdSummary: string;
  cvSummary: string;
  retrievedContext: string[];
}

export interface ScheduleInterviewRequest {
  applicationId: string;
  slotId: string;
  type: 'Remote' | 'OnSite';
}

export interface InterviewCodeRequest {
  applicationId: string;
}

export interface InterviewCodeResponse {
  code: string;
  expiresAt: Date;
  applicationId: string;
}
