import { apiClient } from '@ari/shared/api/apiClient'
import { API_BASE_URL } from '@ari/shared/config/constants'
import { useAuthStore } from '@ari/shared/store/auth'
import type {
  OnlineTestBank,
  OnlineTestQuestion,
  OnlineTestJobResults,
  OnlineTestSettings,
  OnlineTestImportResult,
  CandidateOnlineTest,
  OnlineTestResult,
  OnlineTestSubmitAck,
  OnlineTestAnswerSheet,
  UpsertOnlineTestQuestion,
} from '@ari/shared/types/onlineTest'

export const onlineTestService = {
  /**
   * Bài làm chi tiết của một ứng viên — `null` khi chưa nộp bài (là câu trả lời hợp lệ, không phải lỗi).
   * Chỉ đường NHÂN SỰ: response mang đáp án đúng.
   */
  async getAnswerSheet(applicationId: string): Promise<OnlineTestAnswerSheet | null> {
    const { data } = await apiClient.get<OnlineTestAnswerSheet | null>(
      `/online-test/applications/${applicationId}/answers`
    )
    return data ?? null
  },

  // ===== HR / Recruiter: quản lý ngân hàng câu hỏi =====
  async getBank(jobId: string): Promise<OnlineTestBank> {
    const { data } = await apiClient.get<OnlineTestBank>(`/online-test/jobs/${jobId}`)
    return data
  },

  async createQuestion(jobId: string, payload: UpsertOnlineTestQuestion): Promise<OnlineTestQuestion> {
    const { data } = await apiClient.post<OnlineTestQuestion>(
      `/online-test/jobs/${jobId}/questions`,
      payload
    )
    return data
  },

  async updateQuestion(id: string, payload: UpsertOnlineTestQuestion): Promise<OnlineTestQuestion> {
    const { data } = await apiClient.put<OnlineTestQuestion>(`/online-test/questions/${id}`, payload)
    return data
  },

  async deleteQuestion(id: string): Promise<void> {
    await apiClient.delete(`/online-test/questions/${id}`)
  },

  /** Nhập hàng loạt câu hỏi từ file Excel (.xlsx). */
  async importQuestions(jobId: string, file: File): Promise<OnlineTestImportResult> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<OnlineTestImportResult>(
      `/online-test/jobs/${jobId}/questions/import`,
      form,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    return data
  },

  /** Tải file Excel mẫu để nhập câu hỏi (blob + tên file gợi ý từ header). */
  async downloadTemplate(jobId: string): Promise<{ blob: Blob; fileName: string }> {
    const res = await apiClient.get(`/online-test/jobs/${jobId}/questions/template`, {
      responseType: 'blob',
    })
    const disposition = (res.headers?.['content-disposition'] as string) || ''
    const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
    const fileName = match
      ? decodeURIComponent(match[1])
      : 'mau-ngan-hang-cau-hoi-trac-nghiem.xlsx'
    return { blob: res.data as Blob, fileName }
  },

  async updateSettings(jobId: string, payload: OnlineTestSettings): Promise<OnlineTestBank> {
    const { data } = await apiClient.put<OnlineTestBank>(`/online-test/jobs/${jobId}/settings`, payload)
    return data
  },

  async getStaffResult(applicationId: string): Promise<OnlineTestResult | null> {
    const { data } = await apiClient.get<OnlineTestResult | null>(
      `/online-test/applications/${applicationId}/result`
    )
    return data
  },

  async getJobResults(jobId: string): Promise<OnlineTestJobResults> {
    const { data } = await apiClient.get<OnlineTestJobResults>(`/online-test/jobs/${jobId}/results`)
    return data
  },

  /** Tải bảng điểm .xlsx (blob). Trả về Blob + tên file gợi ý từ header. */
  async exportJobResults(jobId: string): Promise<{ blob: Blob; fileName: string }> {
    const res = await apiClient.get(`/online-test/jobs/${jobId}/results/export`, {
      responseType: 'blob',
    })
    const disposition = (res.headers?.['content-disposition'] as string) || ''
    const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
    const fileName = match ? decodeURIComponent(match[1]) : `bang-diem-${jobId}.xlsx`
    return { blob: res.data as Blob, fileName }
  },

  // ===== Candidate: làm bài =====
  async getTest(applicationId: string): Promise<CandidateOnlineTest> {
    const { data } = await apiClient.get<CandidateOnlineTest>(`/portal/online-test/${applicationId}`)
    return data
  },

  async submit(
    applicationId: string,
    answers: Record<string, number[]>,
    tabSwitchCount = 0
  ): Promise<OnlineTestSubmitAck> {
    const { data } = await apiClient.post<OnlineTestSubmitAck>(
      `/portal/online-test/${applicationId}/submit`,
      { answers, tabSwitchCount }
    )
    return data
  },

  /**
   * Bản "gửi lúc trang đang đóng": rời bài thi là tự nộp, mà request thường bị huỷ khi unload nên
   * dùng `fetch keepalive` (sendBeacon không đặt được header Authorization). Cùng cách ADR-054 gửi
   * tín hiệu rời phòng lúc đóng trang.
   */
  submitBeacon(
    applicationId: string,
    answers: Record<string, number[]>,
    tabSwitchCount = 0
  ): void {
    const token = useAuthStore.getState().tokens?.accessToken
    if (!token) return
    try {
      void fetch(`${API_BASE_URL}/portal/online-test/${applicationId}/submit`, {
        method: 'POST',
        keepalive: true,
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({ answers, tabSwitchCount }),
      })
    } catch {
      /* trang đang đóng — không còn gì để xử lý */
    }
  },
}

export default onlineTestService
