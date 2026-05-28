import type { LoginRequest, LoginResponse, MagicLinkRequest, User, AuthTokens } from '../../types/auth';

export interface RegisterRequest {
  email: string;
  password: string;
  name: string;
  company?: string;
  phone?: string;
  role?: string;
}

// Mock users for fake authentication
const MOCK_USERS = {
  admin: {
    id: 'dev-admin-001',
    email: 'admin@arisp.com',
    name: 'Admin User',
    role: 'SuperAdmin' as const,
    avatarUrl: undefined,
    organizationId: 'dev-org-001',
  },
  candidate: {
    id: 'dev-candidate-001',
    email: 'candidate@arisp.com',
    name: 'Nguyễn Văn An',
    role: 'Candidate' as const,
    avatarUrl: undefined,
    organizationId: undefined,
  },
};

const MOCK_TOKENS: AuthTokens = {
  accessToken: 'mock-access-token-' + Date.now(),
  refreshToken: 'mock-refresh-token-' + Date.now(),
  expiresAt: Date.now() + 3600000,
};

// Check if we should use fake auth (development mode)
const USE_FAKE_AUTH = import.meta.env.DEV || !import.meta.env.VITE_API_URL;

export const authService = {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    // Fake login for development - accept any credentials
    if (USE_FAKE_AUTH) {
      // Simulate API delay
      await new Promise(resolve => setTimeout(resolve, 800));

      // Determine role from email or default to candidate
      const email = credentials.email.toLowerCase();
      const isAdmin = email.includes('admin') || email.includes('hr') || email.includes('recruiter');
      
      const mockUser = isAdmin ? MOCK_USERS.admin : MOCK_USERS.candidate;
      const user: User = {
        ...mockUser,
        id: 'mock-' + Date.now(),
        email: credentials.email,
        name: credentials.email.split('@')[0].replace(/[._]/g, ' ').replace(/\b\w/g, l => l.toUpperCase()),
      };

      return {
        user,
        tokens: MOCK_TOKENS,
      };
    }

    // Real API call for production
    const response = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(credentials),
    });

    if (!response.ok) {
      throw new Error('Invalid credentials');
    }

    return response.json();
  },

  async register(request: RegisterRequest): Promise<LoginResponse> {
    // Fake register for development
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 800));

      const user: User = {
        id: 'mock-' + Date.now(),
        email: request.email,
        name: request.name,
        role: (request.role as User['role']) || 'Candidate',
        avatarUrl: undefined,
        organizationId: request.company ? 'mock-org-' + Date.now() : undefined,
      };

      return {
        user,
        tokens: MOCK_TOKENS,
      };
    }

    const response = await fetch('/api/auth/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      throw new Error('Registration failed');
    }

    return response.json();
  },

  async logout(): Promise<void> {
    // No-op for fake auth
    if (USE_FAKE_AUTH) {
      return;
    }
    await fetch('/api/auth/logout', { method: 'POST' });
  },

  async requestMagicLink(request: MagicLinkRequest): Promise<void> {
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 500));
      return;
    }
    await fetch('/api/auth/magic-link', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
  },

  async verifyMagicLink(token: string): Promise<LoginResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 500));
      return {
        user: MOCK_USERS.candidate,
        tokens: MOCK_TOKENS,
      };
    }
    const response = await fetch('/api/auth/magic-link/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token }),
    });
    return response.json();
  },

  async refreshToken(refreshToken: string): Promise<{ accessToken: string }> {
    if (USE_FAKE_AUTH) {
      return { accessToken: 'mock-refreshed-token-' + Date.now() };
    }
    const response = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    return response.json();
  },

  async getCurrentUser() {
    if (USE_FAKE_AUTH) {
      return MOCK_USERS.admin;
    }
    const response = await fetch('/api/auth/me');
    return response.json();
  },
};
