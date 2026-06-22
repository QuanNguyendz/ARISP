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

export interface HrInterviewSessionItem {
  id: string;
  applicationId: string;
  candidateName: string;
  jobTitle?: string | null;
  roundNumber: number;
  roundType: string;
  sessionType: string; // practice | real
  status: string; // pending | active | completed | aborted | error
  interviewLanguage: string;
  durationSeconds?: number | null;
  hasRecording: boolean;
  startedAt?: string | null;
  endedAt?: string | null;
  createdAt: string;
  evaluationId?: string | null;
  verdict?: string | null;
}

export interface InterviewCodeSummary {
  code: string;
  roundNumber: number;
  expiresAt: string;
  usedAt?: string | null;
  status: string; // Active | Used | Expired
  candidateName: string;
}

export const interviewService = {
  async getHrSessions(): Promise<HrInterviewSessionItem[]> {
    const { data } = await apiClient.get<HrInterviewSessionItem[]>('/interview/sessions');
    return data;
  },

  // Staff: sinh Interview Code cho 1 hồ sơ (round tự suy ra nếu không truyền)
  async generateCode(applicationId: string, roundNumber?: number): Promise<{ code: string; expiresAt: string; applicationId: string }> {
    const { data } = await apiClient.post('/interview/generate-code', { applicationId, roundNumber });
    return data;
  },

  // Staff: danh sách mã đã cấp cho 1 tin tuyển dụng
  async getCodesByJob(jobPostingId: string): Promise<InterviewCodeSummary[]> {
    const { data } = await apiClient.get<InterviewCodeSummary[]>('/interview/codes', { params: { jobPostingId } });
    return data;
  },

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
