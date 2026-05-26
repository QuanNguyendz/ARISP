export interface JobPosting {
  id: string;
  organizationId: string;
  title: string;
  description: string;
  requirements: string[];
  skills: string[];
  location: string;
  salaryRange?: SalaryRange;
  interviewConfig: InterviewConfig;
  status: 'draft' | 'active' | 'paused' | 'closed';
  createdAt: Date;
  updatedAt: Date;
  applicationCount: number;
}

export interface SalaryRange {
  min: number;
  max: number;
  currency: string;
}

export interface InterviewConfig {
  rounds: RoundConfig[];
  interviewModes: ('Remote' | 'OnSite')[];
  availabilitySlots?: AvailabilitySlot[];
  languageRequirement?: LanguageRequirement;
  scoringRubric?: ScoringRubric;
  persona?: InterviewPersona;
}

export interface RoundConfig {
  round: number;
  type: 'Screening' | 'Technical';
  duration: number;
}

export interface AvailabilitySlot {
  id: string;
  startTime: Date;
  endTime: Date;
  timezone: string;
  capacity: number;
  bookedCount: number;
}

export interface LanguageRequirement {
  language: string;
  requirement: string;
  isConfirmed: boolean;
}

export interface ScoringRubric {
  criteria: ScoringCriterion[];
}

export interface ScoringCriterion {
  name: string;
  weight: number;
  description: string;
}

export interface InterviewPersona {
  name: string;
  gender: 'male' | 'female';
  style: 'friendly' | 'professional' | 'strict';
  voiceId?: string;
}

export interface CreateJobPostingRequest {
  title: string;
  description: string;
  requirements: string[];
  skills: string[];
  location: string;
  salaryRange?: SalaryRange;
  interviewConfig: InterviewConfig;
}
