import { apiClient } from '../apiClient'

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
}

export interface UploadPlaybookPayload {
  file: File
  scope: string
  documentType: string
  scopeRefId?: string
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
}

export default playbookService
