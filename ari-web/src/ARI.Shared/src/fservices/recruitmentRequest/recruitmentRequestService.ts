import { apiClient } from '@ari/shared/api/apiClient'

/**
 * Phiếu yêu cầu tuyển dụng (ADR-063) — điểm bắt đầu bắt buộc của mọi tin tuyển dụng.
 *
 * Hiring Manager lập phiếu → HR Leader duyệt kèm phân công Recruiter → Recruiter dựng tin.
 *
 * PHẠM VI DO SERVER QUYẾT ĐỊNH: `list()` không có tham số kiểu `?mine` — server tự lọc theo vai trò
 * người gọi (HM thấy phiếu mình, Recruiter thấy phiếu được giao, HR Leader thấy tất cả). Đừng thêm
 * cờ phạm vi vào đây; ADR-061 đã phải đi vá đúng lỗ "client tự khai phạm vi của chính mình".
 */

export type RecruitmentRequestStatus = 'pending' | 'approved' | 'rejected' | 'cancelled'

/** Nội dung do Hiring Manager nhập — dùng chung cho tạo mới và sửa. */
/** high | medium | low — quyết định thứ tự hàng chờ duyệt của HR Leader. */
export type RecruitmentPriority = 'high' | 'medium' | 'low'

export interface RecruitmentRequestInput {
  title: string

  // CỐ Ý không có `departmentId`: đội của phiếu luôn lấy từ tài khoản người lập (ADR-065),
  // và chỉ Hiring Manager lập được phiếu nên không còn ai cần chọn đội nữa.

  headcount: number
  priority: RecruitmentPriority

  /**
   * Người lập tích "Thoả thuận". KHÔNG có cột tương ứng trong DB — trạng thái này suy ra từ việc cả
   * hai ô lương trống. Cờ chỉ để server phân biệt "cố ý thoả thuận" với "quên điền".
   */
  salaryNegotiable?: boolean

  reason?: string
  description?: string

  /** Kỹ năng và tiêu chí ứng viên phải đáp ứng — ghép vào bản nháp JD ở bước dựng tin. */
  requirements?: string
  employmentType?: string
  workMode?: string
  location?: string
  experienceLevel?: string
  expectedStartDate?: string
  salaryMin?: number
  salaryMax?: number
  salaryCurrency?: string
}

export interface RecruitmentRequestListItem {
  id: string
  title: string
  departmentId?: string | null
  department?: string | null
  headcount: number
  priority: RecruitmentPriority
  status: RecruitmentRequestStatus
  salaryMin?: number | null
  salaryMax?: number | null
  salaryCurrency?: string | null
  requestedByName: string
  requestedByUserId: string
  assignedRecruiterName?: string | null
  assignedRecruiterId?: string | null
  reviewReason?: string | null
  submissionCount: number
  jobPostingId?: string | null
  createdAt: string
  reviewedAt?: string | null
}

export interface RecruitmentRequestDetail extends RecruitmentRequestInput {
  id: string

  /** Tên đội, server tra bằng join — phiếu chỉ lưu khoá. */
  department?: string | null

  status: RecruitmentRequestStatus
  reviewReason?: string | null
  requestedByUserId: string
  requestedByName: string
  reviewedByUserId?: string | null
  reviewedByName?: string | null
  reviewedAt?: string | null
  assignedRecruiterId?: string | null
  assignedRecruiterName?: string | null
  jobPostingId?: string | null
  submissionCount: number

  /** Lý do thu hồi phê duyệt (ADR-066) — có trên cả phiếu đã đóng lẫn phiếu vừa được mở lại. */
  revokedReason?: string | null
  revokedByName?: string | null

  createdAt: string
  updatedAt: string

  /**
   * Ba cờ quyền do SERVER tính. Màn hình không được tự suy ra từ vai trò: cùng một vai trò nhưng
   * khác chủ phiếu thì khác quyền, và người không được duyệt phiếu của chính mình.
   */
  canEdit: boolean
  canReview: boolean
  canCreateJob: boolean

  /**
   * Còn thu hồi được phê duyệt không (ADR-066) — một cờ cho cả hai nút "Mở lại để sửa" và
   * "Đóng phiếu", vì chúng cùng một điều kiện. Server tắt cờ này khi phiếu đã dựng thành tin.
   */
  canRevoke: boolean
}

export const recruitmentRequestService = {
  async list(params: { status?: string; search?: string; priority?: string } = {}): Promise<RecruitmentRequestListItem[]> {
    const { data } = await apiClient.get<RecruitmentRequestListItem[]>('/recruitment-requests', { params })
    return data
  },

  async getById(id: string): Promise<RecruitmentRequestDetail> {
    const { data } = await apiClient.get<RecruitmentRequestDetail>(`/recruitment-requests/${id}`)
    return data
  },

  async create(input: RecruitmentRequestInput): Promise<string> {
    const { data } = await apiClient.post<{ id: string }>('/recruitment-requests', input)
    return data.id
  },

  async update(id: string, input: RecruitmentRequestInput): Promise<void> {
    await apiClient.put(`/recruitment-requests/${id}`, input)
  },

  async resubmit(id: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/resubmit`)
  },

  async cancel(id: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/cancel`)
  },

  /** Duyệt LUÔN kèm phân công Recruiter — server từ chối nếu thiếu người phụ trách. */
  async approve(id: string, assignedRecruiterId: string, note?: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/approve`, { assignedRecruiterId, note })
  },

  /** Trả phiếu về cho HM. Lý do bắt buộc, tối thiểu 10 ký tự (server cũng kiểm lại). */
  async reject(id: string, reason: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/reject`, { reason })
  },

  /**
   * Mở lại phiếu ĐÃ DUYỆT để sửa (ADR-066) — quay về chờ duyệt, phân công Recruiter bị gỡ theo
   * chữ ký bị thu hồi. Khác `resubmit` (đó là gửi lại phiếu đã bị từ chối).
   */
  async reopen(id: string, reason: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/reopen`, { reason })
  },

  /**
   * Đóng phiếu ĐÃ DUYỆT vì hết nhu cầu (ADR-066). Khác `cancel` ở chỗ bắt buộc lý do và báo cho
   * Recruiter đang cầm việc — `cancel` chỉ dùng cho phiếu chưa ai duyệt.
   */
  async close(id: string, reason: string): Promise<void> {
    await apiClient.post(`/recruitment-requests/${id}/close`, { reason })
  },
}
