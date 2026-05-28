import { apiClient } from '../apiClient';
import type { EvaluationReport, HRReview, EvaluationFilter } from '../../types/evaluation';

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

  async confirmEvaluation(evaluationId: string): Promise<HRReview> {
    const { data } = await apiClient.post<HRReview>(`/evaluations/${evaluationId}/confirm`);
    return data;
  },

  async overrideEvaluation(evaluationId: string, reason: string): Promise<HRReview> {
    const { data } = await apiClient.post<HRReview>(`/evaluations/${evaluationId}/override`, {
      overrideReason: reason,
    });
    return data;
  },

  async getMyEvaluations(): Promise<EvaluationReport[]> {
    const { data } = await apiClient.get<EvaluationReport[]>('/candidate/evaluations');
    return data;
  },
};
