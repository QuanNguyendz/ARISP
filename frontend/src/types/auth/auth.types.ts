export interface User {
  id: string;
  email: string;
  name: string;
  role: 'SuperAdmin' | 'HRAdmin' | 'Recruiter' | 'Candidate';
  organizationId?: string;
  avatarUrl?: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  user: User;
  tokens: AuthTokens;
}

export interface CandidateRegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phone?: string;
}

export interface MagicLinkRequest {
  email: string;
}

export interface MagicLinkCallback {
  token: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  fullName: string;
  role: 'SuperAdmin' | 'HRAdmin' | 'Recruiter' | 'Candidate';
}
