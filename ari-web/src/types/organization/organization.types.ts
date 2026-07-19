export interface Organization {
  id: string;
  name: string;
  logoUrl?: string;
  subscription: Subscription;
}

export interface Subscription {
  plan: 'starter' | 'professional' | 'enterprise';
  maxJobPostings: number;
  maxInterviewSessions: number;
  maxTeamMembers: number;
  expiresAt: Date;
}

export interface TeamMember {
  id: string;
  name: string;
  email: string;
  role: 'HRAdmin' | 'Recruiter';
  department?: string;
  isActive: boolean;
  createdAt: Date;
}
