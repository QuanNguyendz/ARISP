import type { User, AuthTokens } from '../types/auth';

// Mock user for development - sử dụng đúng format role với underscore
export const DEV_CANDIDATE_USER: User = {
  id: 'dev-candidate-001',
  email: 'candidate@dev.com',
  name: 'Nguyễn Văn Dev',
  role: 'Candidate',
  avatarUrl: undefined,
  organizationId: undefined,
};

export const DEV_HR_ADMIN_USER: User = {
  id: 'dev-hr-admin-001',
  email: 'hr@dev.com',
  name: 'HR Admin Dev',
  role: 'Hr_admin',
  avatarUrl: undefined,
  organizationId: 'dev-org-001',
};

export const DEV_RECRUITER_USER: User = {
  id: 'dev-recruiter-001',
  email: 'recruiter@dev.com',
  name: 'Recruiter Dev',
  role: 'Recruiter',
  avatarUrl: undefined,
  organizationId: 'dev-org-001',
};

export const DEV_SUPER_ADMIN_USER: User = {
  id: 'dev-super-admin-001',
  email: 'admin@dev.com',
  name: 'Super Admin Dev',
  role: 'Super_admin',
  avatarUrl: undefined,
  organizationId: 'dev-org-001',
};

export const DEV_TOKENS: AuthTokens = {
  accessToken: 'dev-access-token-12345',
  refreshToken: 'dev-refresh-token-67890',
  expiresAt: Date.now() + 3600000,
};

// Check if we're in development mode
export const isDevMode = import.meta.env.DEV;

// Auto-login helper - call this in App.tsx to auto-authenticate in dev
export function getDevAuth(): { user: User; tokens: AuthTokens } | null {
  if (!isDevMode) return null;

  // Check URL for dev mode
  const params = new URLSearchParams(window.location.search);
  const devRole = params.get('dev');

  if (devRole === 'candidate') {
    return { user: DEV_CANDIDATE_USER, tokens: DEV_TOKENS };
  }
  if (devRole === 'hr') {
    return { user: DEV_HR_ADMIN_USER, tokens: DEV_TOKENS };
  }
  if (devRole === 'recruiter') {
    return { user: DEV_RECRUITER_USER, tokens: DEV_TOKENS };
  }
  if (devRole === 'super-admin') {
    return { user: DEV_SUPER_ADMIN_USER, tokens: DEV_TOKENS };
  }
  // Default to HR admin for testing
  if (devRole === 'admin') {
    return { user: DEV_HR_ADMIN_USER, tokens: DEV_TOKENS };
  }

  return null;
}
