import { apiClient } from '../apiClient';
import type { AvailabilitySlot } from '../../types/job';

interface SlotFilters {
  jobPostingId: string;
  date?: string;
}

export const scheduleService = {
  async getAvailableSlots(filters: SlotFilters): Promise<AvailabilitySlot[]> {
    const { data } = await apiClient.get<AvailabilitySlot[]>('/schedules/slots', {
      params: filters,
    });
    return data;
  },

  async createSlot(slot: Omit<AvailabilitySlot, 'id' | 'bookedCount'>): Promise<AvailabilitySlot> {
    const { data } = await apiClient.post<AvailabilitySlot>('/schedules/slots', slot);
    return data;
  },

  async deleteSlot(slotId: string): Promise<void> {
    await apiClient.delete(`/schedules/slots/${slotId}`);
  },

  async updateSlotCapacity(slotId: string, capacity: number): Promise<AvailabilitySlot> {
    const { data } = await apiClient.patch<AvailabilitySlot>(`/schedules/slots/${slotId}/capacity`, {
      capacity,
    });
    return data;
  },

  async getMySchedule(): Promise<{ upcomingSlots: AvailabilitySlot[]; pastSlots: AvailabilitySlot[] }> {
    const { data } = await apiClient.get('/candidate/schedule');
    return data;
  },
};
