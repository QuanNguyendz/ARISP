import { apiClient } from '@ari/shared/api/apiClient'
import type {
  Application,
  ApplicationDetail,
  CreateApplicationRequest,
  HrApplicationItem,
  MyApplicationItem,
  MyApplicationDetail,
} from '@ari/shared/types/application'

let pendingGetApplicationsPromise: Promise<HrApplicationItem[]> | null = null

export const applicationService = {
  // HR/Recruiter: lấy hồ sơ ứng tuyển (GET /applications trả về mảng phẳng).
  // mine=true: chỉ ứng viên thuộc các tin do người đang đăng nhập tạo (Recruiter).
  async getApplications(mine = false): Promise<HrApplicationItem[]> {
    if (!mine && pendingGetApplicationsPromise) {
      return pendingGetApplicationsPromise
    }

    const promise = (async () => {
      try {
        const { data } = await apiClient.get<HrApplicationItem[]>('/applications', {
          params: mine ? { mine: true } : undefined,
        })
        return data
      } finally {
        if (!mine) {
          setTimeout(() => {
            if (pendingGetApplicationsPromise === promise) {
              pendingGetApplicationsPromise = null
            }
          }, 300)
        }
      }
    })()

    if (!mine) {
      pendingGetApplicationsPromise = promise
    }

    return promise
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

  /**
   * Duyệt CV KÈM khung giờ vòng 1 — backend chốt chỗ rồi gửi thư mời phỏng vấn có giờ hẹn.
   * `slotId` là bắt buộc: duyệt suông sẽ để ứng viên không nhận được thư nào.
   */
  /**
   * Duyệt CV + xếp lịch vòng 1 trong MỘT thao tác (ADR-059).
   * `emailOverride`: nội dung thư mời do nhân sự sửa ở trình soạn thảo (ADR-061) — gửi KÈM lệnh
   * này chứ không phải một lời gọi riêng, để huỷ trình soạn = không chốt chỗ, không gửi thư.
   */
  async rejectApplication(applicationId: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/reject`)
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
      coverLetter: string
      noticePeriod: string
      cvFile?: File | null
    }
  ): Promise<{ id: string; status: string }> {
    const formData = new FormData()
    formData.append('candidateName', payload.candidateName)
    formData.append('candidatePhone', payload.candidatePhone)
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

  // Candidate: So sánh thông tin liên hệ và nội dung trong CV bằng AI.
  async verifyCvContactInfo(payload: {
    candidateName: string
    candidatePhone: string
    cvFile?: File | null
  }): Promise<{ isMatch: boolean; mismatchDetails: string | null }> {
    const formData = new FormData()
    formData.append('candidateName', payload.candidateName)
    formData.append('candidatePhone', payload.candidatePhone)
    if (payload.cvFile) formData.append('cvFile', payload.cvFile)

    const { data } = await apiClient.post<{ is_match: boolean; mismatch_details: string | null }>(
      '/portal/applications/verify-cv-info',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    return {
      isMatch: data.is_match,
      mismatchDetails: data.mismatch_details
    }
  },
}
