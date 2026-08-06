import { apiClient } from '@ari/shared/api/apiClient'

export interface StaffSettings {
  receiveEmail: boolean
  receivePush: boolean
}

export const profileService = {
  getSettings: async (): Promise<StaffSettings> => {
    const res = await apiClient.get('/staff/profile/settings')
    return res.data
  },
  updateSettings: async (settings: StaffSettings): Promise<StaffSettings> => {
    const res = await apiClient.put('/staff/profile/settings', settings)
    return res.data
  },
}
