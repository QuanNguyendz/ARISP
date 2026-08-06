import { apiClient, getInterviewSessionToken } from '@ari/shared/api/apiClient';
import { API_BASE_URL } from '@ari/shared/config/constants';
import { useAuthStore } from '@ari/shared/store/auth';
import type {
  InterviewSession,
  ScheduleInterviewRequest,
  InterviewCodeResponse,
  StartSessionRequest,
  StartSessionResponse,
} from '@ari/shared/types/interview';
import type { MyPracticeReview, MyPracticeSessionItem } from '@ari/shared/types/application';

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

/** Kết quả nhập Interview Code tại Kiosk (ADR-052). */
export interface KioskSessionInfo {
  valid: boolean;
  /** Lý do khi mã không dùng được: not_found | used | expired. */
  reason?: string | null;
  sessionId?: string | null;
  token?: string | null;
  tokenExpiresAt?: string | null;
  candidateName?: string | null;
  jobTitle?: string | null;
  roundNumber: number;
  roundType?: string | null;
  language: string;
}

export interface PracticeMediaConfig {
  sessionId: string;
  language: string;
  sessionType: string;
  /** Trần thời lượng phiên (giây) để vẽ đếm ngược; 0 = không giới hạn (ADR-050). */
  maxDurationSeconds?: number;
  /** Mốc bắt đầu phiên (ISO UTC) để tính thời gian còn lại khớp giờ server. */
  startedAtUtc?: string | null;
  deepgram?: { token: string; expiresInSeconds: number; model: string } | null;
  heyGen?: { token: string; serverUrl: string; avatarId?: string | null; voiceId?: string | null } | null;
}

export const interviewService = {
  // Token Deepgram (STT) + LiveAvatar (avatar) cho FE vào phòng phỏng vấn.
  async getMediaConfig(sessionId: string): Promise<PracticeMediaConfig> {
    const { data } = await apiClient.get<PracticeMediaConfig>(`/interview/session/${sessionId}/media-config`);
    return data;
  },

  // TTS câu hỏi → base64 PCM 24k (LiveAvatar repeatAudio / WebAudio). Chỉ là FALLBACK —
  // đường chính là BE tự đẩy ReceiveQuestionAudio qua SignalR ngay sau ReceiveQuestion.
  async getTtsAudio(sessionId: string, text: string): Promise<string> {
    const { data } = await apiClient.post<{ audio: string }>(`/interview/session/${sessionId}/tts`, { text });
    return data?.audio ?? '';
  },

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

  // Kiosk: nhập mã 6 ký tự → BE tạo phiên phỏng vấn THẬT + trả token phạm vi phiên (ADR-052).
  async validateInterviewCode(code: string): Promise<KioskSessionInfo> {
    const { data } = await apiClient.post<KioskSessionInfo>('/interview/validate-code', { code });
    return data;
  },

  // Kiosk: tải video buổi phỏng vấn thật lên storage (tự xoá theo hạn lưu phía BE).
  async uploadRecording(
    sessionId: string,
    blob: Blob,
    fileName = 'interview.webm'
  ): Promise<{ saved: boolean; sizeBytes: number; expiresAt?: string | null }> {
    const form = new FormData();
    form.append('file', blob, fileName);
    const { data } = await apiClient.post(`/interview/session/${sessionId}/recording`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120000, // video vài chục MB — vượt timeout mặc định 30s
    });
    return data;
  },

  // Tín hiệu nghi vấn trong phòng phỏng vấn (Kiosk thoát toàn màn hình, chuyển tab… — ADR-054).
  async reportSessionSignal(
    sessionId: string,
    signalType: string,
    payload?: Record<string, unknown>
  ): Promise<{ count: number }> {
    const { data } = await apiClient.post<{ count: number }>(
      `/interview/session/${sessionId}/signals`,
      { signalType, payload: payload ? JSON.stringify(payload) : undefined }
    );
    return data;
  },

  /**
   * Bản "gửi lúc trang đang đóng": request thường bị huỷ khi unload nên dùng fetch keepalive
   * (sendBeacon không đặt được header Authorization).
   */
  reportSignalBeacon(sessionId: string, signalType: string): void {
    const token = getInterviewSessionToken() ?? useAuthStore.getState().tokens?.accessToken;
    if (!token) return;
    try {
      void fetch(`${API_BASE_URL}/interview/session/${sessionId}/signals`, {
        method: 'POST',
        keepalive: true,
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({ signalType, payload: JSON.stringify({ at: new Date().toISOString() }) }),
      });
    } catch {
      /* trang đang đóng — không còn gì để xử lý */
    }
  },

  async getUpcomingSessions(): Promise<InterviewSession[]> {
    const { data } = await apiClient.get<InterviewSession[]>('/candidate/upcoming-sessions');
    return data;
  },

  // ===== Xem lại buổi phỏng vấn THỬ (riêng tư của ứng viên — ADR-051) =====

  // Candidate: các buổi thử đã làm; truyền applicationId để lọc theo 1 hồ sơ.
  async getMyPracticeSessions(applicationId?: string): Promise<MyPracticeSessionItem[]> {
    const { data } = await apiClient.get<MyPracticeSessionItem[]>('/portal/practice/sessions', {
      params: applicationId ? { applicationId } : undefined,
    });
    return data;
  },

  // Candidate: transcript đầy đủ + nhận xét AI của một buổi thử (không có verdict Pass/Not Pass).
  async getMyPracticeReview(sessionId: string): Promise<MyPracticeReview> {
    const { data } = await apiClient.get<MyPracticeReview>(`/portal/practice/sessions/${sessionId}`);
    return data;
  },
};
