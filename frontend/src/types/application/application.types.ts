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
  interviews: InterviewSession[];
  notes?: string;
}

export interface InterviewSession {
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
