import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { User, AuthTokens, AuthResponse } from '@ari/shared/types/auth';

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

    // `atob` trả về "binary string": mỗi ký tự = 1 BYTE (0–255). Payload JWT là JSON mã hoá
    // UTF-8, nên ký tự tiếng Việt (nhiều byte) bị đọc thành từng byte rời → "Quân Nguyễn"
    // hiện thành "QuÃ¢n Nguyá»…n" (â = C3 A2, ễ = E1 BB 85). Phải gom byte lại rồi giải mã UTF-8.
    const binary = window.atob(padded);
    const bytes = Uint8Array.from(binary, (ch) => ch.charCodeAt(0));
    const decoded = JSON.parse(new TextDecoder('utf-8').decode(bytes)) as JwtPayload;
    return decoded;
  } catch {
    return null;
  }
}

function authResponseToUser(response: AuthResponse): User {
  const payload = parseJwtPayload(response.accessToken);

  return {
    id: response.userId || payload?.sub || 'unknown',
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
      // v1: tên lưu ở v0 được giải mã bằng `atob` nên tiếng Việt bị hỏng và nằm lì trong
      // localStorage tới khi hết hạn đăng nhập (7 ngày) — refresh token KHÔNG cập nhật lại
      // `user`. Migrate giải lại tên từ chính access token đang lưu bằng bộ giải mã đã sửa,
      // nên người đang đăng nhập tự khỏi ngay lần tải trang kế tiếp, không phải đăng nhập lại.
      version: 1,
      migrate: (persisted, fromVersion) => {
        const state = persisted as Partial<AuthState> | undefined;
        if (state && fromVersion < 1 && state.user && state.tokens?.accessToken) {
          const name = parseJwtPayload(state.tokens.accessToken)?.name;
          if (name) {
            state.user = { ...state.user, name };
          }
        }
        return state as AuthState;
      },
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
