export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';
export const WS_BASE_URL = import.meta.env.VITE_WS_BASE_URL || 'ws://localhost:5000';

export const ROLES = {
  SuperAdmin: 'SuperAdmin',
  HRAdmin: 'HRAdmin',
  Recruiter: 'Recruiter',
  Candidate: 'Candidate',
} as const;

export const INTERVIEW_MODES = {
  Remote: 'Remote',
  OnSite: 'OnSite',
  Both: 'Both',
} as const;

export const SESSION_TYPES = {
  Real: 'real',
  Practice: 'practice',
} as const;

export const ROUND_TYPES = {
  Screening: 'Screening',
  Technical: 'Technical',
} as const;

export const VERDICT = {
  Pass: 'Pass',
  NotPass: 'NotPass',
  Pending: 'Pending',
} as const;

export const TIMEOUTS = {
  interviewCodeTTL: 2 * 60 * 60 * 1000,
  magicLinkTTL: 15 * 60 * 1000,
  inviteLinkTTL: 72 * 60 * 60 * 1000,
} as const;
