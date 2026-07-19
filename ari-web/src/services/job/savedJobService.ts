import { apiClient } from '../apiClient'

/** Một việc làm đã lưu (bookmark) — đủ thông tin để render thẻ job. */
export interface SavedJobItem {
  id: string
  title: string
  department?: string
  location?: string
  workMode?: string
  employmentType?: string
  experienceLevel?: string
  jobCategory?: string
  skills: string[]
  isUrgent: boolean
  salaryMin?: number
  salaryMax?: number
  salaryCurrency?: string
  salaryIsNegotiable: boolean
  createdAt: string
  publishedAt?: string
  savedAt: string
}

export const savedJobService = {
  // Danh sách việc đã lưu (đầy đủ thông tin job)
  async getSavedJobs(): Promise<SavedJobItem[]> {
    const { data } = await apiClient.get<SavedJobItem[]>('/portal/saved-jobs')
    return data
  },

  // Chỉ lấy Id các job đã lưu — dùng để tô đậm nút bookmark trên Job Board
  async getSavedJobIds(): Promise<string[]> {
    const { data } = await apiClient.get<string[]>('/portal/saved-jobs/ids')
    return data
  },

  // Lưu một tin (idempotent)
  async saveJob(jobId: string): Promise<void> {
    await apiClient.post(`/portal/saved-jobs/${jobId}`)
  },

  // Bỏ lưu một tin (idempotent)
  async unsaveJob(jobId: string): Promise<void> {
    await apiClient.delete(`/portal/saved-jobs/${jobId}`)
  },
}

export default savedJobService
