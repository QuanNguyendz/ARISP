import { apiClient } from '../apiClient';
import type { InterviewSession, ScheduleInterviewRequest, InterviewCodeResponse } from '@types/interview';

interface SessionFilters {
  applicationId?: string;
  status?: InterviewSession['status'];
  round?: number;
  page?: number;
  pageSize?: number;
}

interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const interviewService = {
  async getSessions(filters?: SessionFilters): Promise<PaginatedResponse<InterviewSession>> {
    const { data } = await apiClient.get<PaginatedResponse<InterviewSession>>('/interviews', {
      params: filters,
    });
    return data;
  },

  async getSessionById(id: string): Promise<InterviewSession> {
    const { data } = await apiClient.get<InterviewSession>(`/interviews/${id}`);
    return data;
  },

  async startSession(id: string): Promise<{ sessionToken: string }> {
    const { data } = await apiClient.post<{ sessionToken: string }>(`/interviews/${id}/start`);
    return data;
  },

  async endSession(id: string): Promise<void> {
    await apiClient.post(`/interviews/${id}/end`);
  },

  async pauseSession(id: string): Promise<void> {
    await apiClient.post(`/interviews/${id}/pause`);
  },

  async resumeSession(id: string): Promise<void> {
    await apiClient.post(`/interviews/${id}/resume`);
  },

  async scheduleInterview(request: ScheduleInterviewRequest): Promise<InterviewSession> {
    const { data } = await apiClient.post<InterviewSession>('/interviews/schedule', request);
    return data;
  },

  async getInterviewCode(applicationId: string): Promise<InterviewCodeResponse> {
    const { data } = await apiClient.post<InterviewCodeResponse>(`/interviews/generate-code`, {
      applicationId,
    });
    return data;
  },

  async validateInterviewCode(code: string): Promise<{ valid: boolean; sessionId?: string }> {
    const { data } = await apiClient.post<{ valid: boolean; sessionId?: string }>('/interviews/validate-code', {
      code,
    });
    return data;
  },

  async submitSignals(sessionId: string, signals: unknown): Promise<void> {
    await apiClient.post(`/interviews/${sessionId}/signals`, signals);
  },

  async getUpcomingSessions(): Promise<InterviewSession[]> {
    const { data } = await apiClient.get<InterviewSession[]>('/candidate/upcoming-sessions');
    return data;
  },
};
