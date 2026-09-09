import { apiClient } from '@ari/shared/api/apiClient'

/** Nội dung thư do nhân sự sửa tay, gửi KÈM lệnh nghiệp vụ (ADR-061). */
export interface EmailOverride {
  subject: string
  bodyHtml: string
}

/** Thư đã dựng sẵn từ mẫu, mọi giá trị đã thay thật — không còn placeholder. */
export interface RenderedEmail {
  subject: string
  html: string
  toEmail: string
  toName?: string | null
}

export interface PreviewEmailParams {
  templateKey: string
  contextId: string
  secondaryId?: string
}

/** Một thư đã gửi cho ứng viên — tab "Lịch sử email". */
export interface EmailLogItem {
  id: string
  templateKey: string
  toEmail: string
  subject: string
  bodyHtml: string
  wasEdited: boolean
  sentByName?: string | null
  status: string
  errorMessage?: string | null
  createdAt: string
}

/** Khoá mẫu thư — khớp `EmailTemplateKeys` phía backend. */
export const EMAIL_TEMPLATES = {
  InterviewInvite: 'interview_invite',
  ApplicationRejected: 'application_rejected',
} as const

export const emailService = {
  /**
   * Nội dung thư đã điền đầy đủ để nhân sự sửa. KHÔNG có endpoint "gửi thư rời" đi cùng: việc gửi
   * luôn đi kèm chính lệnh nghiệp vụ (duyệt CV + xếp lịch, loại hồ sơ…) để giữ tính nguyên tử —
   * huỷ trình soạn = không có gì xảy ra.
   */
  async preview(params: PreviewEmailParams): Promise<RenderedEmail> {
    const { data } = await apiClient.post<RenderedEmail>('/emails/preview', params)
    return data
  },

  async getApplicationEmails(applicationId: string): Promise<EmailLogItem[]> {
    const { data } = await apiClient.get<EmailLogItem[]>(`/applications/${applicationId}/emails`)
    return data
  },
}
