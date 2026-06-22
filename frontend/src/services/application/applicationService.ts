import { apiClient } from '../apiClient'
import type {
  Application,
  ApplicationDetail,
  CreateApplicationRequest,
  HrApplicationItem,
  MyApplicationItem,
  MyApplicationDetail,
} from '../../types/application'

export const applicationService = {
  // HR/Recruiter: lấy hồ sơ ứng tuyển (GET /applications trả về mảng phẳng).
  // mine=true: chỉ ứng viên thuộc các tin do người đang đăng nhập tạo (Recruiter).
  async getApplications(mine = false): Promise<HrApplicationItem[]> {
    const { data } = await apiClient.get<HrApplicationItem[]>('/applications', {
      params: mine ? { mine: true } : undefined,
    })
    return data
  },

  async getApplicationById(id: string): Promise<ApplicationDetail> {
    const { data } = await apiClient.get<ApplicationDetail>(`/applications/${id}`)
    return data
  },

  // Staff: chi tiết hồ sơ theo shape ApplicationResponse thật của backend (HrApplicationItem)
  async getHrApplicationById(id: string): Promise<HrApplicationItem> {
    const { data } = await apiClient.get<HrApplicationItem>(`/applications/${id}`)
    return data
  },

  async createApplication(request: CreateApplicationRequest): Promise<Application> {
    const formData = new FormData()
    formData.append('jobPostingId', request.jobPostingId)
    if (request.cvFile) {
      formData.append('cvFile', request.cvFile)
    }

    const { data } = await apiClient.post<Application>('/applications', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async updateApplicationStatus(id: string, status: Application['status']): Promise<Application> {
    const { data } = await apiClient.patch<Application>(`/applications/${id}/status`, { status })
    return data
  },

  async sendInvite(applicationId: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/send-invite`)
  },

  async hasPracticeSession(applicationId: string): Promise<{ available: boolean }> {
    const { data } = await apiClient.get<{ available: boolean }>(
      `/applications/${applicationId}/practice`
    )
    return data
  },

  // Candidate: danh sách hồ sơ ứng tuyển của chính mình (GET /api/portal/applications)
  async getMyApplications(): Promise<MyApplicationItem[]> {
    const { data } = await apiClient.get<MyApplicationItem[]>('/portal/applications')
    return data
  },

  // Candidate: chi tiết một hồ sơ ứng tuyển của chính mình (GET /api/portal/applications/{id})
  async getMyApplicationDetail(id: string): Promise<MyApplicationDetail> {
    const { data } = await apiClient.get<MyApplicationDetail>(`/portal/applications/${id}`)
    return data
  },

  // Candidate: nộp hồ sơ ứng tuyển qua Job Board → gửi về bộ phận nhân sự.
  // CV để trống = dùng CV hồ sơ; truyền cvFile để nộp CV khác cho riêng tin này.
  async applyToJob(
    jobPostingId: string,
    payload: {
      candidateName: string
      candidatePhone: string
      desiredLocation: string
      coverLetter: string
      noticePeriod: string
      cvFile?: File | null
    }
  ): Promise<{ id: string; status: string }> {
    const formData = new FormData()
    formData.append('candidateName', payload.candidateName)
    formData.append('candidatePhone', payload.candidatePhone)
    formData.append('desiredLocation', payload.desiredLocation)
    formData.append('coverLetter', payload.coverLetter)
    formData.append('noticePeriod', payload.noticePeriod)
    if (payload.cvFile) formData.append('cvFile', payload.cvFile)

    const { data } = await apiClient.post<{ id: string; status: string }>(
      `/portal/applications/${jobPostingId}/apply`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    return data
  },
}
