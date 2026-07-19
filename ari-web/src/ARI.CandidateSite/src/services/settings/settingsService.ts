import { apiClient } from '../apiClient'

export interface NotificationChannelPref {
  email: boolean
  push: boolean
}

export interface CandidateSettings {
  language: string // "vi" | "en"
  interviewInvite: NotificationChannelPref
  result: NotificationChannelPref
  applicationUpdate: NotificationChannelPref
  jobSuggestion: NotificationChannelPref
  allowHrViewProfile: boolean
  allowRecording: boolean
  marketingEmail: boolean
}

export const settingsService = {
  async get(): Promise<CandidateSettings> {
    const { data } = await apiClient.get<CandidateSettings>('/portal/settings')
    return data
  },

  async update(settings: CandidateSettings): Promise<CandidateSettings> {
    const { data } = await apiClient.put<CandidateSettings>('/portal/settings', settings)
    return data
  },

  /** Tải toàn bộ dữ liệu (hồ sơ + đơn ứng tuyển) dưới dạng file JSON. */
  async exportData(): Promise<void> {
    const res = await apiClient.get('/portal/settings/export', { responseType: 'blob' })
    const blob = new Blob([res.data], { type: 'application/json' })
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, '')
    a.download = `arisp-data-${stamp}.json`
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  },

  /** Thu hồi toàn bộ refresh token → đăng xuất khỏi mọi thiết bị. */
  async logoutAllDevices(): Promise<{ revoked: number }> {
    const { data } = await apiClient.post<{ revoked: number }>('/portal/settings/logout-all')
    return data
  },
}

export default settingsService
