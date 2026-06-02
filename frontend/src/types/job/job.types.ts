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
  status: 'draft' | 'active' | 'paused' | 'closed';
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
  scoringRubric?: any;
  rescheduleDeadlineHours?: number;
  inviteTokenTtlHours?: number;
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
