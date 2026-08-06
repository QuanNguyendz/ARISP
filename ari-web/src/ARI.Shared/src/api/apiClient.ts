import axios, { AxiosInstance, AxiosError, AxiosRequestHeaders } from 'axios';
import { API_BASE_URL } from '@ari/shared/config/constants';
import { useAuthStore } from '@ari/shared/store/auth';

/**
 * Endpoint refresh token — cấu hình theo site.
 * Staff dùng `/auth/refresh`, Candidate dùng `/auth/candidate/refresh` (xem ADR-046).
 * Mỗi site gọi `configureApiClient({ refreshPath })` một lần lúc bootstrap (main.tsx).
 */
let refreshPath = '/auth/refresh';

export function configureApiClient(options: { refreshPath?: string }): void {
  if (options.refreshPath) refreshPath = options.refreshPath;
}

/**
 * Token phạm vi MỘT phiên phỏng vấn (Kiosk — ADR-052). Máy Kiosk dùng chung, không ai đăng nhập:
 * mã phỏng vấn hợp lệ → BE trả token gắn đúng phiên, FE nạp vào đây cho mọi request/SignalR.
 * Được ưu tiên hơn token người dùng; gọi `setInterviewSessionToken(null)` khi rời phòng.
 */
let interviewSessionToken: string | null = null;

export function setInterviewSessionToken(token: string | null): void {
  interviewSessionToken = token;
}

export function getInterviewSessionToken(): string | null {
  return interviewSessionToken;
}

export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(
  (config) => {
    const tokens = useAuthStore.getState().tokens;
    const user = useAuthStore.getState().user;
    const headers = (config.headers ?? {}) as AxiosRequestHeaders;

    // Kiosk: token phiên thắng token người dùng (máy dùng chung, không đăng nhập).
    if (interviewSessionToken) {
      headers.Authorization = `Bearer ${interviewSessionToken}`;
    } else if (tokens?.accessToken) {
      headers.Authorization = `Bearer ${tokens.accessToken}`;
    }

    if (!interviewSessionToken && user?.id) {
      headers['X-User-Id'] = user.id;
    }

    config.headers = headers;
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as any;
    if (error.response?.status === 401 && originalRequest && !originalRequest._retry) {
      originalRequest._retry = true;
      const refreshToken = useAuthStore.getState().tokens?.refreshToken;
      if (refreshToken) {
        try {
          const response = await axios.post(`${API_BASE_URL}${refreshPath}`, {
            refreshToken,
          });
          const { accessToken } = response.data;
          const currentTokens = useAuthStore.getState().tokens!;
          useAuthStore.getState().setAuth(useAuthStore.getState().user!, {
            ...currentTokens,
            accessToken,
          });
          originalRequest.headers.Authorization = `Bearer ${accessToken}`;
          return apiClient(originalRequest);
        } catch {
          useAuthStore.getState().logout();
        }
      }
    }
    return Promise.reject(error);
  }
);
