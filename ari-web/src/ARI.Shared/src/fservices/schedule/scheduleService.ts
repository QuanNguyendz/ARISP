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

  // ===== Candidate: xem lịch đã được nhân sự xếp (read-only) =====
  async getMySchedule(): Promise<{
    upcomingSlots: AvailabilitySlot[]
    pastSlots: AvailabilitySlot[]
  }> {
    const { data } = await apiClient.get('/candidate/schedule')
    return data
  },
}

export default scheduleService
