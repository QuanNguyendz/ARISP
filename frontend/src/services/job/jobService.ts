import { apiClient } from '../apiClient'
import type { JobPosting, CreateJobPostingRequest, AnalyzeJdResult } from '@/types/job'
import type { HrApplicationItem } from '@/types/application'

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

  // Staff: upload + phân tích JD (PDF/DOCX) → auto-fill form tạo tin
  async analyzeJd(file: File): Promise<AnalyzeJdResult> {
    const formData = new FormData()
    formData.append('file', file)
    const { data } = await apiClient.post<AnalyzeJdResult>('/jobs/analyze-jd', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  // Public: Get active public jobs
  async getPublicJobPostings(): Promise<JobPosting[]> {
    const { data } = await apiClient.get<JobPosting[]>('/jobs')
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
}
export default jobService
