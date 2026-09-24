import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import type { User, AuthTokens, AuthResponse } from '@ari/shared/types/auth';

/**
 * Phạm vi lưu phiên đăng nhập:
 * - 'local'   → localStorage: chia sẻ mọi tab cùng origin (mặc định — dùng cho Candidate site).
 * - 'session' → sessionStorage: MỖI TAB một phiên riêng (Staff site) nên 2 tài khoản staff mở ở
 *               2 tab của cùng trình duyệt KHÔNG ghi đè token của nhau; refresh (F5) vẫn giữ đúng
 *               tài khoản của tab đó (trước đây token dùng chung → F5 tab này nhảy sang tài khoản
 *               tab kia rồi văng ra /403 → trang 404).
 * Mỗi site chọn bằng `configureAuthStorage(kind)` lúc bootstrap (giống `configureApiClient`).
 */
type AuthStorageKind = 'local' | 'session';
let authStorageKind: AuthStorageKind = 'local';

// StateStorage ổn định — chọn session/local ở TỪNG lần đọc/ghi, không chốt cứng lúc import, nên
// đổi `authStorageKind` trước khi rehydrate là có hiệu lực ngay.
const dynamicAuthStorage = {
  getItem: (name: string): string | null => {
    try {
      return (authStorageKind === 'session' ? sessionStorage : localStorage).getItem(name);
    } catch {
      return null;
    }
  },
  setItem: (name: string, value: string): void => {
    try {
      (authStorageKind === 'session' ? sessionStorage : localStorage).setItem(name, value);
    } catch {
      /* private mode / storage bị chặn — phiên vẫn sống trong RAM của tab, chỉ không lưu lại */
    }
  },
  removeItem: (name: string): void => {
    try {
      (authStorageKind === 'session' ? sessionStorage : localStorage).removeItem(name);
    } catch {
      /* ignore */
    }
  },
};

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
      // Storage do site chọn qua `configureAuthStorage` lúc bootstrap. `skipHydration` để KHÔNG tự
      // nạp lúc import (khi đó `authStorageKind` còn là mặc định) — site set kind xong mới rehydrate
      // đúng storage của mình.
      storage: createJSONStorage(() => dynamicAuthStorage),
      skipHydration: true,
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

/**
 * Chọn phạm vi lưu phiên cho site rồi NẠP LẠI state ngay (sync-storage nên có hiệu lực đồng bộ,
 * lần render đầu đã thấy đúng phiên). Gọi đúng một lần lúc bootstrap, TRƯỚC khi render:
 *   - Staff:     configureAuthStorage('session')  // mỗi tab một tài khoản, F5 giữ nguyên
 *   - Candidate: configureAuthStorage('local')    // giữ đăng nhập chung mọi tab như trước
 */
export function configureAuthStorage(kind: AuthStorageKind): void {
  authStorageKind = kind;
  void useAuthStore.persist.rehydrate();
}
