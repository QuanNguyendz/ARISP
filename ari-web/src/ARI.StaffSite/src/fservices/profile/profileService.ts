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

/**
 * Tải tuyển dụng của một Recruiter — màn "Phân công & tải tuyển dụng" của HR Lead.
 * Cấu trúc theo nghiệp vụ ATS: req load + nút thắt theo giai đoạn + tuổi chờ (SLA).
 */
export interface RecruiterOverview {
  id: string
  fullName: string
  email: string
  department?: string | null
  isActive: boolean
  lockReason?: string | null
  lastLoginAt?: string | null

  jobsTotal: number
  jobsActive: number

  draftsAwaitingApproval: number
  draftsOldestDays: number
  applicationsUnscreened: number
  unscreenedOldestDays: number
  awaitingScheduling: number
  awaitingSchedulingOldestDays: number
  declinedNeedRebooking: number
  pendingReviews: number
  pendingReviewsOldestDays: number

  activePipeline: number
  hired: number
  oldestBottleneckDays: number
}

export interface RecruiterJobBrief {
  id: string
  title: string
  status: string
  candidates: number
  createdAt: string
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
  getRecruiterJobs: async (recruiterId: string): Promise<RecruiterJobBrief[]> => {
    const res = await apiClient.get(`/staff/recruiters/${recruiterId}/jobs`)
    return res.data
  },
  /** Chuyển giao tin sang recruiter khác — thao tác cân tải của HR Lead. */
  reassignJob: async (
    jobId: string,
    payload: { toRecruiterId: string; reason?: string }
  ): Promise<{ message: string }> => {
    const res = await apiClient.post(`/staff/jobs/${jobId}/reassign`, payload)
    return res.data
  },
}
