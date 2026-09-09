import { apiClient } from '@ari/shared/api/apiClient'

/** Thư mời nhận việc, góc nhìn ỨNG VIÊN — không có ghi chú nội bộ. */
export interface CandidateOffer {
  id: string
  applicationId: string
  status: string
  jobTitle?: string | null
  position?: string | null
  salaryAmount?: number | null
  salaryCurrency?: string | null
  salaryPeriod?: string | null
  bonus?: string | null
  benefits?: string | null
  employmentType?: string | null
  workLocation?: string | null
  startDate?: string | null
  expiresAt?: string | null
  sentAt?: string | null
  respondedAt?: string | null
  candidateResponseNote?: string | null
  offerLetterFileUrl?: string | null
}

/** Thư mời nhận việc, góc nhìn NHÂN SỰ. */
export interface StaffOffer extends CandidateOffer {
  jobPostingId: string
  candidateName?: string | null
  candidateEmail?: string | null
  notes?: string | null
  approvalNote?: string | null
  rejectedReason?: string | null
  withdrawnReason?: string | null
  createdAt: string
}

export const OFFER_STATUS = {
  Draft: 'draft',
  PendingApproval: 'pending_approval',
  Approved: 'approved',
  Sent: 'sent',
  Accepted: 'accepted',
  Declined: 'declined',
  Withdrawn: 'withdrawn',
  Expired: 'expired',
} as const

export const offerService = {
  // ===== Ứng viên =====

  /** Thư mời của hồ sơ này. Server chỉ trả về khi thư đã được GỬI trở đi. */
  async getMyOffer(applicationId: string): Promise<CandidateOffer> {
    const { data } = await apiClient.get<CandidateOffer>(`/portal/applications/${applicationId}/offer`)
    return data
  },

  async respond(offerId: string, decision: 'accept' | 'decline', note?: string): Promise<void> {
    await apiClient.post(`/portal/offers/${offerId}/respond`, { decision, note })
  },

  // ===== Nhân sự =====

  async list(status?: string): Promise<StaffOffer[]> {
    const { data } = await apiClient.get<StaffOffer[]>('/offers', {
      params: status ? { status } : undefined,
    })
    return data
  },

  async create(payload: { applicationId: string } & Partial<StaffOffer>): Promise<StaffOffer> {
    const { data } = await apiClient.post<StaffOffer>('/offers', payload)
    return data
  },

  async update(id: string, payload: Partial<StaffOffer>): Promise<StaffOffer> {
    const { data } = await apiClient.put<StaffOffer>(`/offers/${id}`, payload)
    return data
  },

  async submit(id: string): Promise<void> {
    await apiClient.post(`/offers/${id}/submit`)
  },

  async decide(id: string, decision: 'approved' | 'rejected', note?: string): Promise<void> {
    await apiClient.post(`/offers/${id}/decide`, { decision, note })
  },

  /** Gửi cho ứng viên. `emailOverride`: thư do nhân sự sửa ở trình soạn thảo (ADR-061). */
  async send(id: string, emailOverride?: { subject: string; bodyHtml: string }): Promise<void> {
    await apiClient.post(`/offers/${id}/send`, { emailOverride })
  },

  async withdraw(id: string, reason: string): Promise<void> {
    await apiClient.post(`/offers/${id}/withdraw`, { reason })
  },
}
