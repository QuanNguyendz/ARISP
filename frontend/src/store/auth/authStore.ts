import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, AuthTokens, AuthResponse } from '../../types/auth';

interface JwtPayload {
  sub?: string;
  email?: string;
  name?: string;
  unique_name?: string;
  role?: string;
}

interface AuthState {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  setAuth: (user: User, tokens: AuthTokens) => void;
  setAuthFromResponse: (response: AuthResponse) => User;
  updateUser: (user: Partial<User>) => void;
  logout: () => void;
  setLoading: (loading: boolean) => void;
  login: (user: User, tokens: AuthTokens) => void;
}

function parseJwtPayload(token: string): JwtPayload | null {
  try {
    const [, payload] = token.split('.');
    if (!payload) {
      return null;
    }

    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
    const decoded = JSON.parse(window.atob(padded)) as JwtPayload;
    return decoded;
  } catch {
    return null;
  }
}

function authResponseToUser(response: AuthResponse): User {
  const payload = parseJwtPayload(response.accessToken);

  return {
    id: payload?.sub || 'unknown',
    email: payload?.email || '',
    name: response.fullName || payload?.name || payload?.unique_name || payload?.email || 'Unknown User',
    role: response.role || payload?.role || '',
  };
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      tokens: null,
      isAuthenticated: false,
      isLoading: false,

      setAuth: (user, tokens) =>
        set({
          user,
          tokens,
          isAuthenticated: true,
          isLoading: false,
        }),

      setAuthFromResponse: (response) => {
        const user = authResponseToUser(response);
        const tokens: AuthTokens = {
          accessToken: response.accessToken,
          refreshToken: response.refreshToken,
          expiresAt: Date.now() + 7 * 24 * 60 * 60 * 1000,
        };
        set({ user, tokens, isAuthenticated: true, isLoading: false });
        return user;
      },

      updateUser: (partialUser) =>
        set((state) => ({
          user: state.user ? { ...state.user, ...partialUser } : null,
        })),

      logout: () =>
        set({
          user: null,
          tokens: null,
          isAuthenticated: false,
          isLoading: false,
        }),

      setLoading: (loading) => set({ isLoading: loading }),

      login: (user, tokens) => set({ user, tokens, isAuthenticated: true, isLoading: false }),
    }),
    {
      name: 'arisp-auth',
      partialize: (state) => ({
        user: state.user,
        tokens: state.tokens,
        isAuthenticated: state.isAuthenticated,
      }),
      onRehydrateStorage: () => (state) => {
        state?.setLoading(false);
      },
    }
  )
);
