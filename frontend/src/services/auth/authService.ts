import { apiClient } from '../apiClient';
import type { LoginRequest, LoginResponse, MagicLinkRequest } from '@types/auth';

export const authService = {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const { data } = await apiClient.post<LoginResponse>('/auth/login', credentials);
    return data;
  },

  async logout(): Promise<void> {
    await apiClient.post('/auth/logout');
  },

  async requestMagicLink(request: MagicLinkRequest): Promise<void> {
    await apiClient.post('/auth/magic-link', request);
  },

  async verifyMagicLink(token: string): Promise<LoginResponse> {
    const { data } = await apiClient.post<LoginResponse>('/auth/magic-link/verify', { token });
    return data;
  },

  async refreshToken(refreshToken: string): Promise<{ accessToken: string }> {
    const { data } = await apiClient.post<{ accessToken: string }>('/auth/refresh', {
      refreshToken,
    });
    return data;
  },

  async getCurrentUser() {
    const { data } = await apiClient.get('/auth/me');
    return data;
  },
};
