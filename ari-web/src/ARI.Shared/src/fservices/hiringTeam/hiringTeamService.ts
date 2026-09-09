import { apiClient } from '@ari/shared/api/apiClient'

/** Một thành viên trong đội tuyển dụng của tin (ADR-061). */
export interface HiringTeamMember {
  id: string
  jobPostingId: string
  userId: string
  fullName?: string | null
  email: string
  department?: string | null
  /** hiring_manager | interviewer | observer */
  roleOnJob: string
  /** Hiring Manager chính — người mà chữ ký duyệt chặn phễu. */
  isPrimary: boolean
  addedByName?: string | null
  createdAt: string
}

/** Tài khoản Hiring Manager để chọn khi gán vào tin. */
export interface HiringManagerOption {
  id: string
  fullName?: string | null
  email: string
  department?: string | null
  /** Cùng phòng ban với tin — chỉ để xếp gợi ý lên đầu, KHÔNG phải điều kiện quyền. */
  matchesJobDepartment: boolean
}

export interface AddHiringTeamMemberRequest {
  userId: string
  roleOnJob?: string
  isPrimary?: boolean
}

export const hiringTeamService = {
  async getTeam(jobPostingId: string): Promise<HiringTeamMember[]> {
    const { data } = await apiClient.get<HiringTeamMember[]>(`/jobs/${jobPostingId}/hiring-team`)
    return data
  },

  async addMember(jobPostingId: string, request: AddHiringTeamMemberRequest): Promise<HiringTeamMember> {
    const { data } = await apiClient.post<HiringTeamMember>(`/jobs/${jobPostingId}/hiring-team`, request)
    return data
  },

  async removeMember(jobPostingId: string, memberId: string): Promise<void> {
    await apiClient.delete(`/jobs/${jobPostingId}/hiring-team/${memberId}`)
  },

  /**
   * Danh sách Hiring Manager để gán. Truyền `jobPostingId` thì người cùng phòng ban với tin
   * được server xếp lên đầu — gợi ý, không lọc.
   */
  async getHiringManagerOptions(jobPostingId?: string): Promise<HiringManagerOption[]> {
    const { data } = await apiClient.get<HiringManagerOption[]>('/staff/hiring-managers', {
      params: jobPostingId ? { jobPostingId } : undefined,
    })
    return data
  },

  // ===== Cổng duyệt shortlist (ADR-061) =====

  /** Chủ tin gửi hồ sơ cho Hiring Manager duyệt — hồ sơ chuyển sang `hm_review`. */
  async requestHmApproval(applicationId: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/request-hm-approval`)
  },

  /**
   * Hiring Manager duyệt hoặc từ chối. `note` bắt buộc khi từ chối.
   *
   * `availabilities` (ADR-067): khung giờ HM có mặt được cho vòng 1, gửi KÈM lệnh duyệt. Recruiter
   * chỉ xếp được ca nằm trong các khung này, nên duyệt mà không có khung nào (và chưa khai lần
   * trước) sẽ bị server từ chối — hồ sơ về hàng chờ mà không có giờ nào để xếp thì nằm im ở đó.
   */
  async submitHmDecision(
    applicationId: string,
    decision: 'approved' | 'rejected',
    note?: string,
    availabilities?: { startTime: string; endTime: string; note?: string | null }[]
  ): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/hm-decision`, {
      decision,
      note,
      availabilities,
    })
  },

  /** Quản trị viên vượt cổng duyệt — lý do bắt buộc, tối thiểu 10 ký tự. */
  async bypassHmApproval(applicationId: string, reason: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/hm-bypass`, { reason })
  },

  // ===== Cổng ký duyệt JD (ADR-061) =====

  /** Hiring Manager ký duyệt / yêu cầu sửa mô tả công việc. `reason` bắt buộc khi từ chối. */
  async submitJobSignOff(
    jobPostingId: string,
    decision: 'approved' | 'rejected',
    reason?: string
  ): Promise<void> {
    await apiClient.post(`/jobs/${jobPostingId}/hm-signoff`, { decision, reason })
  },
}
