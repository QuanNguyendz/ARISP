import { apiClient } from '../apiClient'
import type { AvailabilitySlot } from '../../types/job'

export interface CreateSlotRequest {
  jobPostingId: string
  roundNumber: number
  startTime: string
  endTime: string
  timezone?: string
  capacity?: number
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

  // ===== Candidate: chọn lịch / xem lịch (Phase B2) =====
  async getOpenSlots(
    applicationId: string,
    round: number,
    token?: string
  ): Promise<AvailabilitySlot[]> {
    const { data } = await apiClient.get<AvailabilitySlot[]>(`/schedule/${applicationId}/slots`, {
      params: { round, token },
    })
    return data
  },

  async book(
    applicationId: string,
    payload: { slotId: string; round: number; token?: string }
  ): Promise<void> {
    await apiClient.post(`/schedule/${applicationId}/book`, payload)
  },

  async getMySchedule(): Promise<{
    upcomingSlots: AvailabilitySlot[]
    pastSlots: AvailabilitySlot[]
  }> {
    const { data } = await apiClient.get('/candidate/schedule')
    return data
  },
}

export default scheduleService
