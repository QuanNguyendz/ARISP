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
  /** waiting | active | completed | … — phòng chờ buổi thật (ADR-067). */
  status?: string;
  /** Hiring Manager đã vào phòng chưa — màn chờ đổi chữ theo mốc này. */
  hiringManagerPresent?: boolean;
}

/** Một phòng phỏng vấn thật đang mở (ADR-067). */
export interface WaitingRoom {
  sessionId: string;
  applicationId: string;
  jobPostingId: string;
  candidateName?: string | null;
  jobTitle?: string | null;
  roundNumber: number;
  roundType?: string | null;
  /** waiting = ứng viên đang chờ được cho vào · active = đang phỏng vấn. */
  status: string;
  createdAt: string;
  hmJoinedAt?: string | null;
  admittedAt?: string | null;
}

export const interviewService = {
  // ===== Phòng chờ buổi phỏng vấn thật (ADR-067) =====

  /** Buổi phỏng vấn thật đang mở trong phạm vi người gọi (đang chờ, hoặc đang diễn ra). */
  async getWaitingRooms(): Promise<WaitingRoom[]> {
    const { data } = await apiClient.get<WaitingRoom[]>('/interview/waiting-rooms');
    return data;
  },

  /** Hiring Manager vào phòng cùng AI — điều kiện để buổi phỏng vấn bắt đầu được. */
  async joinInterviewRoom(sessionId: string): Promise<WaitingRoom> {
    const { data } = await apiClient.post<WaitingRoom>(`/interview/session/${sessionId}/hm-join`);
    return data;
  },

  /** Cho ứng viên vào phòng — phiên chuyển sang đang diễn ra và AI bắt đầu hỏi. */
  async admitCandidate(sessionId: string): Promise<WaitingRoom> {
    const { data } = await apiClient.post<WaitingRoom>(`/interview/session/${sessionId}/admit`);
    return data;
  },

  // Token Deepgram (STT) + trần thời lượng cho FE vào phòng phỏng vấn.
  async getMediaConfig(sessionId: string): Promise<PracticeMediaConfig> {
    const { data } = await apiClient.get<PracticeMediaConfig>(`/interview/session/${sessionId}/media-config`);
    return data;
  },

  // TTS câu hỏi → base64 PCM 24k (phát qua WebAudio). Chỉ là FALLBACK —
  // đường chính là BE tự đẩy ReceiveQuestionAudio qua SignalR ngay sau ReceiveQuestion.
  async getTtsAudio(sessionId: string, text: string): Promise<string> {
    const { data } = await apiClient.post<{ audio: string }>(`/interview/session/${sessionId}/tts`, { text });
    return data?.audio ?? '';
  },

  async getHrSessions(applicationId?: string): Promise<HrInterviewSessionItem[]> {
    const { data } = await apiClient.get<HrInterviewSessionItem[]>('/interview/sessions', {
      params: applicationId ? { applicationId } : undefined,
    });
    return data;
  },

  // Staff: sinh Interview Code cho 1 hồ sơ (round tự suy ra nếu không truyền)
  async generateCode(applicationId: string, roundNumber?: number): Promise<{ code: string; expiresAt: string; applicationId: string }> {
    const { data } = await apiClient.post('/interview/generate-code', { applicationId, roundNumber });
    return data;
  },

  // Staff: sinh Interview Code hàng loạt cho danh sách hồ sơ
  async generateCodeBatch(applicationIds: string[], roundNumber?: number): Promise<Array<{ code: string; applicationId: string }>> {
    const { data } = await apiClient.post('/interview/generate-code-batch', { applicationIds, roundNumber });
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

  // ─── Interview Management: Job → Slot → Candidate ───

  async getInterviewJobs(): Promise<InterviewJobSummary[]> {
    const { data } = await apiClient.get<InterviewJobSummary[]>('/interview/management/jobs');
    return data;
  },

  async getSlotsForJob(jobId: string): Promise<InterviewSlotDetail[]> {
    const { data } = await apiClient.get<InterviewSlotDetail[]>(`/interview/management/jobs/${jobId}/slots`);
    return data;
  },

  async getCandidatesInSlot(slotId: string): Promise<SlotCandidate[]> {
    const { data } = await apiClient.get<SlotCandidate[]>(`/interview/management/slots/${slotId}/candidates`);
    return data;
  },

  async sendBookingReminder(bookingId: string): Promise<{ success: boolean; message: string }> {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(`/interview/management/booking/${bookingId}/remind`);
    return data;
  },

  async rescheduleBooking(bookingId: string, targetSlotId: string): Promise<{ success: boolean; message: string }> {
    const { data } = await apiClient.post<{ success: boolean; message: string }>(`/interview/management/booking/${bookingId}/reschedule`, { targetSlotId });
    return data;
  },

  /**
   * Dời NHIỀU ứng viên sang cùng một ca trong MỘT request — được ăn cả ngã về không.
   * Thay cho vòng lặp gửi N request tuần tự: cách cũ với ca còn 1 chỗ và 3 người thì người đầu
   * lọt, hai người sau thất bại lần lượt và không có gì hoàn tác.
   */
  async rescheduleBookings(bookingIds: string[], targetSlotId: string): Promise<RescheduleResult> {
    const { data } = await apiClient.post<RescheduleResult>('/interview/management/bookings/reschedule', {
      bookingIds,
      targetSlotId,
    });
    return data;
  },
};

// ─── Interview Management DTOs ───────────────────────────────────────────────

export interface InterviewJobSummary {
  jobId: string;
  jobTitle: string;
  jobStatus: string;
  totalSlots: number;
  totalBooked: number;
  totalConfirmed: number;
  maxRound: number;
  totalSessions: number;
  completedSessions: number;
  nextSlotTime?: string | null;
}

export interface InterviewSlotDetail {
  slotId: string;
  jobPostingId: string;
  roundNumber: number;
  startTime: string;
  endTime: string;
  timezone: string;
  capacity: number;
  /**
   * SỐ CHỖ ĐANG BỊ CHIẾM (booking `status = 'scheduled'`), không phải tổng số dòng booking.
   * Người báo bận / quá hạn / bị loại đều đã trả chỗ nên không tính vào đây — đúng bằng con số
   * server dùng để chặn khi gán và khi dời lịch. Muốn tổng số dòng thì dùng `totalBookingRows`.
   */
  bookedCount: number;
  /** Số chỗ còn nhận thêm được, đã kẹp không âm. Dùng thẳng, đừng tự trừ capacity - bookedCount. */
  seatsAvailable: number;
  confirmedCount: number;
  pendingCount: number;
  /** Đã báo bận / quá hạn xác nhận — ĐÃ trả chỗ. */
  declinedCount: number;
  /** Đã bị loại khỏi quy trình — ĐÃ trả chỗ. */
  cancelledCount: number;
  totalBookingRows: number;
  /** Số chỗ bị chiếm vượt quá sức chứa (dữ liệu cũ bị lệch, hoặc nhân sự hạ sức chứa). */
  isOverCapacity: boolean;
  isPast: boolean;
}

/** Trạng thái ứng viên trong ca — server tính sẵn, giao diện KHÔNG tự suy luận. */
export type CandidateState =
  | 'pending'
  | 'confirmed'
  | 'declined_by_candidate'
  | 'expired_no_response'
  | 'no_show'
  | 'rejected_by_staff'
  | 'cancelled';

export interface SlotCandidate {
  applicationId: string;
  bookingId: string;
  roundNumber: number;
  candidateName: string;
  candidateEmail: string;
  confirmationStatus: string; // pending | confirmed | declined
  declineReason?: string | null;
  bookingStatus: string;       // scheduled | declined | cancelled
  /**
   * Nguồn sự thật duy nhất cho nhãn + việc bật/tắt nút. Trước đây giao diện tự suy ra bằng cách
   * dò chuỗi tiếng Việt trong `declineReason` nên gắn nhãn "Từ chối (báo bận)" cho cả người bị
   * hệ thống huỷ vì quá hạn xác nhận.
   */
  candidateState: CandidateState;
  /** Ứng viên này có đang chiếm một chỗ của ca không. */
  occupiesSeat: boolean;
  /** Trạng thái HỒ SƠ (khác trạng thái lịch) — để phân biệt "lịch đóng" với "hồ sơ bị loại". */
  applicationStatus?: string | null;
  sessionId?: string | null;
  sessionStatus?: string | null;
  durationSeconds?: number | null;
  evaluationId?: string | null;
  verdict?: string | null;
  overallScore?: number | null;
  interviewCode?: string | null;
  codeExpiresAt?: string | null;
}

export interface RescheduleFailure {
  bookingId: string;
  message: string;
}

export interface RescheduleResult {
  movedCount: number;
  failed: RescheduleFailure[];
}
