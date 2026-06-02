import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, AuthTokens, AuthResponse } from '../../types/auth';

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

function authResponseToUser(response: AuthResponse, id?: string): User {
  return {
    id: id || 'unknown',
    email: '',
    name: response.fullName,
    role: response.role,
  };
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      tokens: null,
      isAuthenticated: false,
      isLoading: true,

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
    }
  )
);
