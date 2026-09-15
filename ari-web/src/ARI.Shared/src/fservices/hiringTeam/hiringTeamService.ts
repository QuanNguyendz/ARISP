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

/**
 * Thêm một thành viên PHỤ vào đội (người phỏng vấn / người theo dõi / HM phụ chỉ đọc). Hiring Manager chính
 * KHÔNG đặt ở đây (ADR-068) — xem `setPrimaryHiringManager`.
 */
export interface AddHiringTeamMemberRequest {
  userId: string
  /** interviewer | observer | hiring_manager — bỏ trống thì server mặc định interviewer. */
  roleOnJob?: string
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

  /** Gỡ thành viên PHỤ. Server từ chối gỡ Hiring Manager chính — chỉ chuyển được (ADR-068). */
  async removeMember(jobPostingId: string, memberId: string): Promise<void> {
    await apiClient.delete(`/jobs/${jobPostingId}/hiring-team/${memberId}`)
  },

  /**
   * Gán hoặc chuyển Hiring Manager chính của tin (ADR-068). Chỉ HR Leader / Super Admin, lý do tối thiểu
   * 10 ký tự — server báo cho cả HM cũ lẫn HM mới.
   */
  async setPrimaryHiringManager(jobPostingId: string, userId: string, reason: string): Promise<HiringTeamMember> {
    const { data } = await apiClient.put<HiringTeamMember>(`/jobs/${jobPostingId}/hiring-manager`, { userId, reason })
    return data
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
   * Chỉ là quyết định chuyên môn — lịch có mặt của HM KHÔNG đi kèm lệnh này mà khai riêng qua
   * `scheduleService.setHmAvailability` (mục "Lịch tôi có mặt được" ở màn tin).
   */
  async submitHmDecision(
    applicationId: string,
    decision: 'approved' | 'rejected',
    note?: string
  ): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/hm-decision`, { decision, note })
  },

  /** Quản trị viên vượt cổng duyệt — lý do bắt buộc, tối thiểu 10 ký tự. */
  async bypassHmApproval(applicationId: string, reason: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/hm-bypass`, { reason })
  },

  // ===== Cổng ký duyệt JD (ADR-061) =====

  /**
   * Hiring Manager ký duyệt (tin lên `active`) / yêu cầu sửa (tin về `rejected` cho Recruiter sửa) mô tả công
   * việc. `reason` bắt buộc khi yêu cầu sửa, tối thiểu 10 ký tự.
   */
  async submitJobSignOff(
    jobPostingId: string,
    decision: 'approved' | 'rejected',
    reason?: string
  ): Promise<void> {
    await apiClient.post(`/jobs/${jobPostingId}/hm-signoff`, { decision, reason })
  },
}
