import { apiClient } from '@ari/shared/api/apiClient'
import type { AvailabilitySlot } from '@ari/shared/types/job'

export interface CreateSlotRequest {
  jobPostingId: string
  roundNumber: number
  startTime: string
  endTime: string
  timezone?: string
  capacity?: number
}

/** Một mục lịch phỏng vấn của ứng viên (kèm booking để xác nhận/từ chối) — ADR-048. */
export interface CandidateScheduleItem {
  bookingId: string
  applicationId: string
  jobTitle?: string | null
  roundNumber: number
  startTime: string
  endTime: string
  timezone: string
  /** pending | confirmed | declined */
  confirmationStatus: string
  declineReason?: string | null
}

export interface CandidateSchedule {
  upcoming: CandidateScheduleItem[]
  past: CandidateScheduleItem[]
  awaitingReschedule: CandidateScheduleItem[]
}

export const scheduleService = {
  // ===== Recruiter/HR: quản lý khung giờ phỏng vấn của job =====
  async getSlots(jobPostingId: string, round?: number): Promise<AvailabilitySlot[]> {
    const { data } = await apiClient.get<AvailabilitySlot[]>('/schedules/slots', {
      params: { jobPostingId, round },
    })
    return data
  },

  async createSlot(req: CreateSlotRequest): Promise<AvailabilitySlot> {
    const { data } = await apiClient.post<AvailabilitySlot>('/schedules/slots', req)
    return data
  },

  async deleteSlot(slotId: string): Promise<void> {
    await apiClient.delete(`/schedules/slots/${slotId}`)
  },

  async updateSlotCapacity(slotId: string, capacity: number): Promise<AvailabilitySlot> {
    const { data } = await apiClient.patch<AvailabilitySlot>(
      `/schedules/slots/${slotId}/capacity`,
      {
        capacity,
      }
    )
    return data
  },

  // ===== HR: gán cứng 1 khung giờ trong kho cho 1 ứng viên (ADR-048) =====
  async assign(payload: {
    applicationId: string
    slotId: string
    round: number
  }): Promise<void> {
    await apiClient.post('/schedules/assign', payload)
  },

  // ===== Candidate: xem lịch đã được nhân sự xếp + xác nhận/từ chối (ADR-048) =====
  async getMySchedule(): Promise<CandidateSchedule> {
    const { data } = await apiClient.get<CandidateSchedule>('/candidate/schedule')
    return data
  },

  /** Ứng viên xác nhận sẽ tham dự khung giờ đã xếp. */
  async confirmSchedule(bookingId: string): Promise<void> {
    await apiClient.post(`/candidate/schedule/${bookingId}/confirm`)
  },

  /** Ứng viên bận → từ chối kèm lý do để nhân sự xếp lịch khác. */
  async declineSchedule(bookingId: string, reason: string): Promise<void> {
    await apiClient.post(`/candidate/schedule/${bookingId}/decline`, { reason })
  },

  /** Ứng viên ẩn (xoá khỏi danh sách) một lịch đã bị huỷ/từ chối. */
  async dismissSchedule(bookingId: string): Promise<void> {
    await apiClient.delete(`/candidate/schedule/${bookingId}`)
  },
}

export default scheduleService
