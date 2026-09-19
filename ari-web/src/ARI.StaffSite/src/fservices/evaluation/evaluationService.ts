import { apiClient } from '@ari/shared/api/apiClient'
import type {
  EvaluationReport,
  HRReview,
  EvaluationFilter,
  InterviewResultRow,
  SubmitEvaluationReviewPayload,
} from '@ari/shared/types/evaluation'

/**
 * Phần bổ sung của ADR-061 khi chốt kết quả phỏng vấn: lý do chốt thay Hiring Manager, và các đề
 * xuất lương/cấp bậc để điền sẵn thư mời nhận việc về sau.
 */
export type HiringDecisionExtras = Pick<
  SubmitEvaluationReviewPayload,
  | 'fallbackReason'
  | 'suggestedLevel'
  | 'suggestedSalaryMin'
  | 'suggestedSalaryMax'
  | 'suggestedSalaryCurrency'
  | 'strengths'
  | 'concerns'
>

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

  /**
   * Các buổi phỏng vấn thật của một hồ sơ theo vòng (ADR-069): ca · diễn biến · báo cáo · có video /
   * transcript. Có cả vòng AI còn đang chấm — thứ mà danh sách đánh giá không thể hiện.
   */
  /** Chấm lại buổi phỏng vấn chưa có báo cáo (ADR-073) — chạy nền, màn hình tự cập nhật qua realtime. */
  async retrySessionEvaluation(sessionId: string): Promise<void> {
    await apiClient.post(`/evaluations/sessions/${sessionId}/retry`)
  },

  async getApplicationInterviews(applicationId: string): Promise<InterviewResultRow[]> {
    const { data } = await apiClient.get<InterviewResultRow[]>(
      `/evaluations/application/${applicationId}/interviews`
    )
    return data
  },

  /** Buổi phỏng vấn thật vừa kết thúc trong phạm vi của người gọi (mặc định 24 giờ qua). */
  async getRecentInterviews(hours = 24): Promise<InterviewResultRow[]> {
    const { data } = await apiClient.get<InterviewResultRow[]>('/evaluations/recent-interviews', {
      params: { hours },
    })
    return data
  },

  async submitReview(payload: SubmitEvaluationReviewPayload): Promise<{ success: boolean }> {
    const { data } = await apiClient.post<{ success: boolean }>(
      '/interview/review/confirm',
      payload
    )
    return data
  },

  /**
   * `extras` mang phần ADR-061: lý do chốt thay Hiring Manager và các đề xuất lương/cấp bậc.
   * Để trống thì payload y hệt trước đây — mọi nơi gọi cũ không phải đổi gì.
   */
  async confirmEvaluation(
    evaluation: Pick<EvaluationReport, 'id' | 'aiVerdict'>,
    extras?: HiringDecisionExtras
  ): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: evaluation.aiVerdict === 'not_pass' ? 'not_pass' : 'pass',
      ...extras,
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
    finalVerdict?: 'pass' | 'not_pass',
    extras?: HiringDecisionExtras
  ): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: finalVerdict ?? (evaluation.aiVerdict === 'pass' ? 'not_pass' : 'pass'),
      overrideReason: reason,
      ...extras,
    })
  },

  async getMyEvaluations(): Promise<EvaluationReport[]> {
    const { data } = await apiClient.get<EvaluationReport[]>('/candidate/evaluations')
    return data
  },
}

export type { PaginatedResponse, HRReview }
