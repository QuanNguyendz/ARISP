import { apiClient } from '@ari/shared/api/apiClient'

export interface StaffSettings {
  receiveEmail: boolean
  receivePush: boolean
}

export interface StaffProfile {
  id: string
  fullName: string
  email: string
  role: string
  department?: string | null
  lastLoginAt?: string | null
  createdAt: string
  /** false = tài khoản chỉ đăng nhập Google, chưa từng đặt mật khẩu. */
  hasPassword: boolean
}

/** Recruiter kèm khối lượng công việc — màn Quản lý Recruiter của HR Lead (chỉ đọc). */
export interface RecruiterOverview {
  id: string
  fullName: string
  email: string
  department?: string | null
  isActive: boolean
  lockReason?: string | null
  lastLoginAt?: string | null
  createdAt: string
  jobsTotal: number
  jobsActive: number
  jobsDraft: number
  candidatesTotal: number
  pendingReviews: number
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
  getProfile: async (): Promise<StaffProfile> => {
    const res = await apiClient.get('/staff/profile')
    return res.data
  },
  updateProfile: async (payload: { fullName: string; department?: string | null }): Promise<StaffProfile> => {
    const res = await apiClient.put('/staff/profile', payload)
    return res.data
  },
  changePassword: async (payload: {
    currentPassword?: string
    newPassword: string
  }): Promise<{ message: string }> => {
    const res = await apiClient.post('/staff/profile/change-password', payload)
    return res.data
  },
  getRecruiters: async (): Promise<RecruiterOverview[]> => {
    const res = await apiClient.get('/staff/recruiters')
    return res.data
  },
}
