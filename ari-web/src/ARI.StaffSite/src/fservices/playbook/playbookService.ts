import { apiClient } from '@ari/shared/api/apiClient'

export interface PlaybookItem {
  id: string
  scope: string // org | job_posting | round
  scopeRefId?: string | null
  roundNumber?: number | null
  documentType: string
  fileName: string
  fileFormat: string
  status: string // processing | ready | error
  createdAt: string
  uploadedBy?: string | null
  /** Số tiêu chí đọc được — chỉ có ở tài liệu bộ tiêu chí chấm điểm (ADR-060). */
  criteriaCount?: number | null
}

/** Hai loại tài liệu là BẢNG SỐ LIỆU (.xlsx theo mẫu), không phải văn xuôi (ADR-060). */
export const RUBRIC_DOC_TYPES = ['cv_rubric', 'interview_rubric'] as const

export const isRubricDocType = (documentType: string) =>
  (RUBRIC_DOC_TYPES as readonly string[]).includes(documentType)

export interface UploadPlaybookPayload {
  file: File
  scope: string
  documentType: string
  scopeRefId?: string
  roundNumber?: number
}

/** Playbook của một tin + quyền thêm/xoá của người gọi — cờ do server quyết (ADR-069). */
export interface JobPlaybooks {
  items: PlaybookItem[]
  canManage: boolean
}

/** Playbook trong màn tin: áp cho cả tin, hoặc cho đúng một vòng. Tin lấy từ URL. */
export interface UploadJobPlaybookPayload {
  file: File
  scope: 'job_posting' | 'round'
  documentType: string
  roundNumber?: number
}

export const playbookService = {
  async getPlaybooks(scope?: string): Promise<PlaybookItem[]> {
    const { data } = await apiClient.get<PlaybookItem[]>('/playbooks', { params: scope ? { scope } : undefined })
    return data
  },

  async uploadPlaybook(payload: UploadPlaybookPayload): Promise<PlaybookItem> {
    const fd = new FormData()
    fd.append('file', payload.file)
    fd.append('scope', payload.scope)
    fd.append('documentType', payload.documentType)
    if (payload.scopeRefId) fd.append('scopeRefId', payload.scopeRefId)
    if (payload.roundNumber != null) fd.append('roundNumber', String(payload.roundNumber))
    const { data } = await apiClient.post<PlaybookItem>('/playbooks', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async deletePlaybook(id: string): Promise<void> {
    await apiClient.delete(`/playbooks/${id}`)
  },

  // ===== Playbook THEO TIN — quản lý ngay trong màn tin (ADR-069) =====

  async getJobPlaybooks(jobId: string): Promise<JobPlaybooks> {
    const { data } = await apiClient.get<JobPlaybooks>(`/jobs/${jobId}/playbooks`)
    return data
  },

  async uploadJobPlaybook(jobId: string, payload: UploadJobPlaybookPayload): Promise<PlaybookItem> {
    const fd = new FormData()
    fd.append('file', payload.file)
    fd.append('scope', payload.scope)
    fd.append('documentType', payload.documentType)
    if (payload.scope === 'round' && payload.roundNumber != null)
      fd.append('roundNumber', String(payload.roundNumber))
    const { data } = await apiClient.post<PlaybookItem>(`/jobs/${jobId}/playbooks`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async deleteJobPlaybook(jobId: string, id: string): Promise<void> {
    await apiClient.delete(`/jobs/${jobId}/playbooks/${id}`)
  },

  /**
   * Tải file Excel mẫu bộ tiêu chí. Đi qua apiClient (không mở tab thẳng URL) vì endpoint cần
   * Authorization header — mở tab sẽ dính 401.
   */
  async downloadRubricTemplate(documentType: string): Promise<void> {
    const { data } = await apiClient.get<Blob>('/playbooks/rubric-template', {
      params: { type: documentType },
      responseType: 'blob',
    })
    const url = URL.createObjectURL(data)
    const a = document.createElement('a')
    a.href = url
    a.download =
      documentType === 'cv_rubric' ? 'mau-tieu-chi-cham-cv.xlsx' : 'mau-tieu-chi-cham-phong-van.xlsx'
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
}

export default playbookService
