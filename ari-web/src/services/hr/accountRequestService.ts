import { apiClient } from '../apiClient'

export interface MyAccountRequest {
  id: string
  batchId?: string | null
  email: string
  fullName: string
  role: string // hr_admin | recruiter
  department?: string | null
  status: string // pending | approved | rejected
  reviewReason?: string | null
  createdAt: string
  reviewedAt?: string | null
}

export interface NewAccountRequestItem {
  email: string
  fullName: string
  role: string
  department?: string
}

export const accountRequestService = {
  // HR Leader: danh sách yêu cầu tạo tài khoản mình đã gửi
  async getMine(): Promise<MyAccountRequest[]> {
    const { data } = await apiClient.get<MyAccountRequest[]>('/hr/account-requests')
    return data
  },

  // HR Leader: gửi 1 hoặc nhiều yêu cầu (nhiều mục = cùng batch)
  async create(items: NewAccountRequestItem[]): Promise<{ message: string; count: number; batchId?: string | null }> {
    const { data } = await apiClient.post('/hr/account-requests', items)
    return data
  },
}

export default accountRequestService
