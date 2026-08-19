import { apiClient } from '@ari/shared/api/apiClient'
import type { JobPosting, CreateJobPostingRequest, AnalyzeJdResult } from '@ari/shared/types/job'
import type { HrApplicationItem } from '@ari/shared/types/application'

export interface JobFacetItem {
  value: string
  label: string
  count: number
}

export interface JobFacets {
  categories: JobFacetItem[]
  employmentTypes: JobFacetItem[]
  experienceLevels: JobFacetItem[]
  workModes: JobFacetItem[]
  locations: JobFacetItem[]
  skills: JobFacetItem[]
  languages: JobFacetItem[]
  totalJobs: number
}

export interface GetPublicJobsParams {
  search?: string
  categories?: string // comma-separated values
  employmentTypes?: string // comma-separated values
  experienceLevels?: string // comma-separated values
  workModes?: string // comma-separated values
  locations?: string // comma-separated values
  skills?: string // comma-separated values
  languages?: string // comma-separated values
  sortBy?: string
  minSalary?: number
  maxSalary?: number
  salaryIsNegotiable?: boolean
  page?: number
  pageSize?: number
}

export interface PaginatedJobs {
  items: JobPosting[]
  totalCount: number
}

export const jobService = {
  // Public: Bộ lọc khả dụng (chỉ những giá trị có trong DB) + số lượng cho Job Board
  async getJobFacets(): Promise<JobFacets> {
    const { data } = await apiClient.get<JobFacets>('/jobs/facets')
    return data
  },

  // HR Admin: Get all jobs (draft, active, paused, closed)
  async getAdminJobPostings(): Promise<JobPosting[]> {
    const { data } = await apiClient.get<JobPosting[]>('/jobs/admin')
    return data
  },

  // Recruiter: chỉ lấy tin do chính mình tạo
  async getMyJobPostings(): Promise<JobPosting[]> {
    const { data } = await apiClient.get<JobPosting[]>('/jobs/admin', { params: { mine: true } })
    return data
  },

  // Staff: danh sách ứng viên của MỘT job (kiểm soát ứng viên theo job)
  async getJobApplications(jobId: string): Promise<HrApplicationItem[]> {
    const { data } = await apiClient.get<HrApplicationItem[]>(`/jobs/${jobId}/applications`)
    return data
  },

  // Staff: upload + phân tích JD (PDF/DOCX) → auto-fill form tạo tin.
  // Timeout riêng: Gemini đọc PDF nhiều trang (nhất là PDF toàn ảnh phải OCR) mất vài chục giây,
  // vượt mức mặc định 30s của apiClient → request bị HUỶ giữa chừng, server ghi 499 và người dùng
  // chỉ thấy "không phân tích được" dù AI vẫn đang chạy. Backend tự bó thời gian ngắn hơn số này.
  async analyzeJd(file: File): Promise<AnalyzeJdResult> {
    const formData = new FormData()
    formData.append('file', file)
    const { data } = await apiClient.post<AnalyzeJdResult>('/jobs/analyze-jd', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 150000,
    })
    return data
  },

  // Public: Get active public jobs with filtering and pagination
  async getPublicJobPostings(params?: GetPublicJobsParams): Promise<PaginatedJobs> {
    const { data } = await apiClient.get<PaginatedJobs>('/jobs', { params })
    return data
  },

  // Get job detail by ID (Supports both public candidates and HR staff)
  async getJobPostingById(id: string): Promise<JobPosting> {
    const { data } = await apiClient.get<JobPosting>(`/jobs/${id}`)
    return data
  },

  // HR Admin: Create a new job posting
  async createJobPosting(request: CreateJobPostingRequest): Promise<JobPosting> {
    const { data } = await apiClient.post<JobPosting>('/jobs', request)
    return data
  },

  // HR Admin: Update an existing job posting
  async updateJob(id: string, request: CreateJobPostingRequest): Promise<JobPosting> {
    const { data } = await apiClient.put<JobPosting>(`/jobs/${id}`, request)
    return data
  },

  // HR Admin: Add availability slots for a job posting
  async addAvailabilitySlots(id: string, slots: any[]): Promise<any> {
    const { data } = await apiClient.post(`/jobs/${id}/slots`, slots)
    return data
  },

  // HR Admin: Approve / reject / change a job posting status.
  // status: 'active' (duyệt) | 'rejected' (từ chối, cần rejectionReason) | 'closed' | 'archived' | 'pending'
  async updateJobStatus(
    id: string,
    status: JobPosting['status'],
    rejectionReason?: string
  ): Promise<void> {
    await apiClient.patch(`/jobs/${id}/status`, { status, rejectionReason })
  },

  // Update display settings without changing status or full job content
  async updateJobDisplay(
    id: string,
    payload: { isUrgent?: boolean; isPublicListing?: boolean }
  ): Promise<JobPosting> {
    const { data } = await apiClient.patch<JobPosting>(`/jobs/${id}/display`, payload)
    return data
  },
}
export default jobService
