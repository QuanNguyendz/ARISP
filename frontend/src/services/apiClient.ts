import axios, { AxiosInstance, AxiosError } from 'axios';
import { API_BASE_URL } from '@config/constants';
import { useAuthStore } from '@store/auth';

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
    if (tokens?.accessToken) {
      config.headers.Authorization = `Bearer ${tokens.accessToken}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    if (error.response?.status === 401) {
      const refreshToken = useAuthStore.getState().tokens?.refreshToken;
      if (refreshToken) {
        try {
          const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
            refreshToken,
          });
          const { accessToken } = response.data;
          useAuthStore.getState().setAuth(useAuthStore.getState().user!, {
            ...useAuthStore.getState().tokens!,
            accessToken,
          });
          if (error.config) {
            error.config.headers.Authorization = `Bearer ${accessToken}`;
            return apiClient(error.config);
          }
        } catch {
          useAuthStore.getState().logout();
        }
      }
    }
    return Promise.reject(error);
  }
);
