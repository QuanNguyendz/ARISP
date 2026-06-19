export interface RoundConfig {
  roundNumber: number;
  roundType: string; // 'screening' | 'technical'
  interviewLanguage?: string;
  interviewCodeTtlHours: number;
  maxDurationMinutes: number;
}

export interface AvailabilitySlot {
  id: string;
  startTime: Date | string;
  endTime: Date | string;
  timezone: string;
  capacity: number;
  bookedCount: number;
}

export interface JobPosting {
  id: string;
  createdByUserId: string;
  title: string;
  department?: string;
  jobDescription: string;
  interviewMode: 'remote' | 'onsite' | 'both';
  status: 'draft' | 'pending' | 'active' | 'paused' | 'rejected' | 'closed' | 'archived';
  isPublicListing: boolean;
  detectedLanguage?: string;
  languageRequirement?: string;
  languageConfirmed: boolean;
  roundConfigs: RoundConfig[];
  createdAt: string;
  location?: string;
  workMode?: string;
  salaryMin?: number;
  salaryMax?: number;
  salaryCurrency?: string;
  salaryIsNegotiable: boolean;
  employmentType?: string;
  experienceLevel?: string;
  skills: string[];
  jobCategory?: string;
  applicationDeadline?: string;
  isUrgent: boolean;
  scoringRubric?: unknown;
  rescheduleDeadlineHours?: number;
  inviteTokenTtlHours?: number;
  /** Số ứng viên đã ứng tuyển — trả về từ GET /jobs/admin */
  applicantCount?: number;
  publishedAt?: string;
  /** Tên người tạo tin (Recruiter/HR) — trả về từ GET /jobs/admin */
  createdByName?: string;
  /** Lý do từ chối (khi status = 'rejected') */
  rejectionReason?: string;
}

export interface CreateJobPostingRequest {
  title: string;
  department?: string;
  jobDescription: string;
  interviewMode: 'remote' | 'onsite' | 'both';
  isPublicListing: boolean;
  languageRequirement?: string;
  rescheduleDeadlineHours?: number;
  inviteTokenTtlHours?: number;
  roundConfigs: RoundConfig[];
  location?: string;
  workMode?: string;
  salaryMin?: number;
  salaryMax?: number;
  salaryCurrency?: string;
  salaryIsNegotiable: boolean;
  employmentType?: string;
  experienceLevel?: string;
  skills: string[];
  jobCategory?: string;
  applicationDeadline?: string;
  isUrgent: boolean;
  personaName?: string;
  personaVoiceId?: string;
  personaStyle?: string;
}
