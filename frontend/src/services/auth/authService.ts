import type {
  LoginRequest,
  AuthResponse,
  CandidateRegisterRequest,
  User,
} from '../../types/auth';
import { API_BASE_URL } from '@config/constants';
import { firebaseAuth, googleAuthProvider, isFirebaseConfigured } from '@config/firebase';
import {
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut as firebaseSignOut,
  updateProfile,
} from 'firebase/auth';

export interface RegisterRequest {
  email: string;
  password: string;
  name: string;
  company?: string;
  phone?: string;
  role?: string;
}

const USE_FAKE_AUTH = import.meta.env.VITE_ENABLE_FAKE_AUTH === 'true';
const USE_FIREBASE_AUTH = isFirebaseConfigured && Boolean(firebaseAuth);

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

    if (USE_FIREBASE_AUTH && firebaseAuth) {
      const credential = await signInWithEmailAndPassword(firebaseAuth, credentials.email, credentials.password);
      const idToken = await credential.user.getIdToken();
      return this.exchangeFirebaseCandidateToken(idToken);
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

    if (USE_FIREBASE_AUTH && firebaseAuth) {
      const credential = await createUserWithEmailAndPassword(firebaseAuth, request.email, request.password);
      await updateProfile(credential.user, { displayName: request.fullName });
      const idToken = await credential.user.getIdToken(true);
      await this.exchangeFirebaseCandidateToken(idToken);
      return { message: 'Candidate registered successfully.' };
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    return parseResponse<{ message: string }>(response);
  },

  async candidateGoogleLogin(): Promise<AuthResponse> {
    if (!USE_FIREBASE_AUTH || !firebaseAuth) {
      throw new Error('Firebase is not configured.');
    }

    const credential = await signInWithPopup(firebaseAuth, googleAuthProvider);
    const idToken = await credential.user.getIdToken();
    return this.exchangeFirebaseCandidateToken(idToken);
  },

  async exchangeFirebaseCandidateToken(idToken: string): Promise<AuthResponse> {
    const response = await fetch(`${API_BASE_URL}/auth/firebase/candidate/login`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${idToken}`,
      },
    });

    return parseResponse<AuthResponse>(response);
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
    if (firebaseAuth) {
      await firebaseSignOut(firebaseAuth).catch(() => {});
    }
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
