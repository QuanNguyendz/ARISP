import { apiClient } from '@ari/shared/api/apiClient'
import type {
  EvaluationReport,
  HRReview,
  EvaluationFilter,
  SubmitEvaluationReviewPayload,
} from '@ari/shared/types/evaluation'

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export const evaluationService = {
  async getEvaluations(filters?: EvaluationFilter): Promise<PaginatedResponse<EvaluationReport>> {
    const { data } = await apiClient.get<PaginatedResponse<EvaluationReport>>('/evaluations', {
      params: filters,
    })
    return data
  },

  async getEvaluationById(id: string): Promise<EvaluationReport> {
    const { data } = await apiClient.get<EvaluationReport>(`/evaluations/${id}`)
    return data
  },

  async getEvaluationBySessionId(sessionId: string): Promise<EvaluationReport> {
    const { data } = await apiClient.get<EvaluationReport>(`/evaluations/session/${sessionId}`)
    return data
  },

  async getEvaluationsByApplicationId(applicationId: string): Promise<EvaluationReport[]> {
    const { data } = await apiClient.get<EvaluationReport[]>(
      `/evaluations/application/${applicationId}`
    )
    return data
  },

  async submitReview(payload: SubmitEvaluationReviewPayload): Promise<{ success: boolean }> {
    const { data } = await apiClient.post<{ success: boolean }>(
      '/interview/review/confirm',
      payload
    )
    return data
  },

  async confirmEvaluation(
    evaluation: Pick<EvaluationReport, 'id' | 'aiVerdict'>
  ): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: evaluation.aiVerdict === 'not_pass' ? 'not_pass' : 'pass',
    })
  },

  /**
   * Ghi đè verdict của AI. `finalVerdict` do người dùng chọn — trước đây hàm này tự lật ngược
   * verdict của AI nên hai nút Đạt / Không đạt trên màn duyệt chỉ là trang trí. Vẫn giữ cách
   * lật ngược làm mặc định khi nơi gọi không truyền gì (đúng ý định thường gặp của "Ghi đè").
   */
  async overrideEvaluation(
    evaluation: Pick<EvaluationReport, 'id' | 'aiVerdict'>,
    reason: string,
    finalVerdict?: 'pass' | 'not_pass'
  ): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: finalVerdict ?? (evaluation.aiVerdict === 'pass' ? 'not_pass' : 'pass'),
      overrideReason: reason,
    })
  },

  async getMyEvaluations(): Promise<EvaluationReport[]> {
    const { data } = await apiClient.get<EvaluationReport[]>('/candidate/evaluations')
    return data
  },
}

export type { PaginatedResponse, HRReview }
