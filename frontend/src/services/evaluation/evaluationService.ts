import { apiClient } from '../apiClient';
import type {
  EvaluationReport,
  HRReview,
  EvaluationFilter,
  SubmitEvaluationReviewPayload,
} from '../../types/evaluation';

interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const evaluationService = {
  async getEvaluations(filters?: EvaluationFilter): Promise<PaginatedResponse<EvaluationReport>> {
    const { data } = await apiClient.get<PaginatedResponse<EvaluationReport>>('/evaluations', {
      params: filters,
    });
    return data;
  },

  async getEvaluationById(id: string): Promise<EvaluationReport> {
    const { data } = await apiClient.get<EvaluationReport>(`/evaluations/${id}`);
    return data;
  },

  async getEvaluationBySessionId(sessionId: string): Promise<EvaluationReport> {
    const { data } = await apiClient.get<EvaluationReport>(`/evaluations/session/${sessionId}`);
    return data;
  },

  async submitReview(payload: SubmitEvaluationReviewPayload): Promise<{ success: boolean }> {
    const { data } = await apiClient.post<{ success: boolean }>('/interview/review/confirm', payload);
    return data;
  },

  async confirmEvaluation(evaluation: Pick<EvaluationReport, 'id' | 'aiVerdict'>): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: evaluation.aiVerdict === 'not_pass' ? 'not_pass' : 'pass',
    });
  },

  async overrideEvaluation(
    evaluation: Pick<EvaluationReport, 'id' | 'aiVerdict'>,
    reason: string,
  ): Promise<{ success: boolean }> {
    return this.submitReview({
      evaluationId: evaluation.id,
      finalVerdict: evaluation.aiVerdict === 'pass' ? 'not_pass' : 'pass',
      overrideReason: reason,
    });
  },

  async getMyEvaluations(): Promise<EvaluationReport[]> {
    const { data } = await apiClient.get<EvaluationReport[]>('/candidate/evaluations');
    return data;
  },
};

export type { PaginatedResponse, HRReview };
