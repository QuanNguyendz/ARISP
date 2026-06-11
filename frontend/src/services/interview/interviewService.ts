import { apiClient } from '../apiClient';
import type {
  InterviewSession,
  ScheduleInterviewRequest,
  InterviewCodeResponse,
  StartSessionRequest,
  StartSessionResponse,
} from '../../types/interview';

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
    const { data } = await apiClient.get<PaginatedResponse<InterviewSession>>('/interview', {
      params: filters,
    });
    return data;
  },

  async getSessionById(id: string): Promise<InterviewSession> {
    const { data } = await apiClient.get<InterviewSession>(`/interview/${id}`);
    return data;
  },

  async startSession(request: StartSessionRequest): Promise<StartSessionResponse> {
    const { data } = await apiClient.post<StartSessionResponse>('/interview/session/start', request);
    return data;
  },

  async endSession(id: string): Promise<void> {
    await apiClient.post(`/interview/session/${id}/end`);
  },

  async pauseSession(id: string): Promise<void> {
    await apiClient.post(`/interview/${id}/pause`);
  },

  async resumeSession(id: string): Promise<void> {
    await apiClient.post(`/interview/${id}/resume`);
  },

  async scheduleInterview(request: ScheduleInterviewRequest): Promise<InterviewSession> {
    const { data } = await apiClient.post<InterviewSession>('/interview/schedule', request);
    return data;
  },

  async getInterviewCode(applicationId: string): Promise<InterviewCodeResponse> {
    const { data } = await apiClient.post<InterviewCodeResponse>(`/interview/generate-code`, {
      applicationId,
    });
    return data;
  },

  async validateInterviewCode(code: string): Promise<{ valid: boolean; sessionId?: string }> {
    const { data } = await apiClient.post<{ valid: boolean; sessionId?: string }>('/interview/validate-code', {
      code,
    });
    return data;
  },

  async submitSignals(sessionId: string, signals: unknown): Promise<void> {
    await apiClient.post(`/interview/${sessionId}/signals`, signals);
  },

  async getUpcomingSessions(): Promise<InterviewSession[]> {
    const { data } = await apiClient.get<InterviewSession[]>('/candidate/upcoming-sessions');
    return data;
  },
};
