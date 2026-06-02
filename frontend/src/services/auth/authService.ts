import type {
  LoginRequest,
  AuthResponse,
  CandidateRegisterRequest,
  User,
} from '../../types/auth';
import { API_BASE_URL } from '@config/constants';

export interface RegisterRequest {
  email: string;
  password: string;
  name: string;
  company?: string;
  phone?: string;
  role?: string;
}

const USE_FAKE_AUTH = !import.meta.env.VITE_API_URL;

async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'An error occurred' }));
    throw new Error(error.message || `HTTP ${response.status}`);
  }
  return response.json();
}

export const authService = {

  async candidateLogin(credentials: LoginRequest): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 800));
      return {
        accessToken: 'mock-candidate-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'Nguyễn Văn An',
        role: 'Candidate',
      };
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(credentials),
    });

    return parseResponse<AuthResponse>(response);
  },

  async candidateRegister(request: CandidateRegisterRequest): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 800));
      return { message: 'Candidate registered successfully.' };
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    return parseResponse<{ message: string }>(response);
  },

  async verifyMagicLink(email: string, token: string): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise(resolve => setTimeout(resolve, 500));
      return {
        accessToken: 'mock-magic-link-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'Nguyễn Văn An',
        role: 'Candidate',
      };
    }

    const response = await fetch(
      `${API_BASE_URL}/auth/magic-link/verify?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`,
      { method: 'GET' }
    );

    const data = await parseResponse<{ message: string; token: string }>(response);
    return {
      accessToken: data.token,
      refreshToken: '',
      fullName: '',
      role: 'Candidate',
    };
  },

  async logout(): Promise<void> {
    if (USE_FAKE_AUTH) return;
    await fetch(`${API_BASE_URL}/auth/logout`, { method: 'POST' }).catch(() => {});
  },

  async refreshToken(refreshToken: string): Promise<{ accessToken: string }> {
    if (USE_FAKE_AUTH) {
      return { accessToken: 'mock-refreshed-token-' + Date.now() };
    }
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    return parseResponse<{ accessToken: string }>(response);
  },

  async getCurrentUser(accessToken: string): Promise<User> {
    if (USE_FAKE_AUTH) {
      return {
        id: 'mock-user',
        email: 'mock@arisp.com',
        name: 'Mock User',
        role: 'Candidate',
      };
    }
    const response = await fetch(`${API_BASE_URL}/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    return parseResponse<User>(response);
  },

  buildOAuthRedirectUrl(provider: string = 'Google', returnUrl: string = '/'): string {
    const encodedReturnUrl = encodeURIComponent(returnUrl);
    return `${API_BASE_URL}/auth/external/signin?provider=${provider}&returnUrl=${encodedReturnUrl}`;
  },

  parseOAuthCallback(url: string): { accessToken?: string; role?: string; status?: string; message?: string } {
    const hash = url.split('#')[1] || '';
    const params = new URLSearchParams(hash);
    return {
      accessToken: params.get('access_token') || undefined,
      role: params.get('role') || undefined,
      status: params.get('status') || undefined,
      message: params.get('message') || undefined,
    };
  },
};
