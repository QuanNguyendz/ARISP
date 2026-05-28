import type { User, AuthTokens } from '../types/auth';

// Mock user for development
export const DEV_CANDIDATE_USER: User = {
  id: 'dev-candidate-001',
  email: 'candidate@dev.com',
  name: 'Nguyễn Văn Dev',
  role: 'Candidate',
  avatarUrl: undefined,
  organizationId: undefined,
};

export const DEV_ADMIN_USER: User = {
  id: 'dev-admin-001',
  email: 'admin@dev.com',
  name: 'Admin Dev',
  role: 'SuperAdmin',
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
  const devMode = params.get('dev');

  if (devMode === 'candidate') {
    return { user: DEV_CANDIDATE_USER, tokens: DEV_TOKENS };
  }
  if (devMode === 'admin') {
    return { user: DEV_ADMIN_USER, tokens: DEV_TOKENS };
  }

  return null;
}
