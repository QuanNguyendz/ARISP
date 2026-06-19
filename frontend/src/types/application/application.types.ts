export interface Application {
  id: string;
  jobPostingId: string;
  jobTitle: string;
  organizationId: string;
  organizationName: string;
  candidateId: string;
  candidateName: string;
  candidateEmail: string;
  cvUrl?: string;
  status: ApplicationStatus;
  hasPracticeSession: boolean;
  appliedAt: Date;
  updatedAt: Date;
}

export type ApplicationStatus =
  | 'pending_review'
  | 'approved'
  | 'interview_scheduled'
  | 'interview_completed'
  | 'offer_extended'
  | 'rejected';

export interface ApplicationDetail extends Application {
  interviews: ApplicationInterviewSession[];
  notes?: string;
}

export interface ApplicationInterviewSession {
  id: string;
  applicationId: string;
  round: number;
  type: 'Screening' | 'Technical';
  sessionType: 'real' | 'practice';
  status: SessionStatus;
  scheduledAt?: Date;
  startedAt?: Date;
  endedAt?: Date;
  recordingUrl?: string;
  verdict?: 'Pass' | 'NotPass';
  overallScore?: number;
}

export type SessionStatus =
  | 'scheduled'
  | 'in_progress'
  | 'completed'
  | 'cancelled';

export interface CreateApplicationRequest {
  jobPostingId: string;
  cvFile?: File;
}

/** Một vòng phỏng vấn trong tiến trình của ứng viên (GET /portal/applications). */
export interface MyApplicationRound {
  roundNumber: number;
  roundType: string;
  sessionType: string;
  status: string;
  verdict?: string | null;
  overallScore?: number | null;
}

/**
 * Item hồ sơ ứng tuyển của chính ứng viên — khớp JSON từ GET /api/portal/applications.
 * Status là chuỗi thô từ backend, map nhãn ở UI.
 */
export interface MyApplicationItem {
  id: string;
  jobPostingId: string;
  jobTitle?: string | null;
  location?: string | null;
  department?: string | null;
  candidateEmail: string;
  candidateName: string;
  status: string;
  matchScore?: number | null;
  cvFileUrl?: string | null;
  practiceSessionUsed: boolean;
  source: string;
  createdAt: string;
  updatedAt: string;
  rounds: MyApplicationRound[];
}

/**
 * Item hồ sơ ứng tuyển cho danh sách HR/Recruiter — khớp đúng JSON từ GET /applications
 * (ApplicationResponse ở backend). Status là chuỗi thô từ backend, map nhãn ở UI.
 */
export interface HrApplicationItem {
  id: string;
  jobPostingId: string;
  jobTitle?: string;
  candidateEmail: string;
  candidateName: string;
  candidatePhone?: string;
  cvFileUrl?: string;
  source: string;
  status: string;
  practiceSessionUsed: boolean;
  createdAt: string;
  cvJdAnalysisId?: string;
  matchScore?: number | null;
}
