import { apiClient } from '@ari/shared/api/apiClient'
import type {
  CvRubricCriterion,
  CvRubricDraft,
  CvRubricSuggestionInput,
  CvRubricTemplate,
} from '@ari/shared/fservices/cvRubric'

/**
 * Bộ tiêu chí chấm PHỎNG VẤN theo tin (ADR-073) — Hiring Manager khai cho từng tin: một bộ CHUNG áp mọi vòng
 * hội thoại, và bộ RIÊNG cho vòng cần chấm khác. Không có bộ công ty dự phòng: thiếu bộ tiêu chí thì buổi phỏng
 * vấn của vòng đó không ra báo cáo, và tin không đăng được.
 *
 * Cùng trình soạn với bộ tiêu chí chấm CV (tên · trọng số · chuẩn chấm · mức neo) nhưng KHÔNG có ý kiểm — ý kiểm
 * là dấu hiệu tra được trên CV, câu trả lời phỏng vấn được chấm theo chuẩn chấm + mức neo.
 */

/** Một bộ tiêu chí đang sống: chung của tin (`roundNumber` null) hoặc riêng của một vòng. */
export interface InterviewRubricSet {
  roundNumber?: number | null
  criteria: CvRubricCriterion[]
  documentId?: string | null
  savedAt?: string | null
  savedBy?: string | null
}

/** Một vòng hội thoại của tin và bộ tiêu chí nào đang áp vào nó. */
export interface InterviewRubricRound {
  roundNumber: number
  roundType: string
  /** Vòng có bộ riêng (không dùng bộ chung). */
  hasOwnRubric: boolean
  /** Vòng đã có bộ nào áp vào (riêng hoặc chung). */
  covered: boolean
}

export interface JobInterviewRubric {
  jobLevel: InterviewRubricSet
  roundSets: InterviewRubricSet[]
  rounds: InterviewRubricRound[]
  /** Vòng hội thoại chưa có bộ nào — rỗng mới đăng tin được. */
  missingRounds: number[]
  /** Do server quyết: HM chính của tin hoặc quản trị viên. */
  canEdit: boolean
  /** Buổi phỏng vấn đã xong nhưng đang chờ bộ tiêu chí để chấm. */
  waitingSessionCount: number
}

export const interviewRubricService = {
  async getForJob(jobId: string): Promise<JobInterviewRubric> {
    const { data } = await apiClient.get<JobInterviewRubric>(`/jobs/${jobId}/interview-rubric`)
    return data
  },

  /** `roundNumber` null = bộ chung của tin. */
  async save(jobId: string, roundNumber: number | null, criteria: CvRubricCriterion[]): Promise<JobInterviewRubric> {
    const { data } = await apiClient.put<JobInterviewRubric>(`/jobs/${jobId}/interview-rubric`, {
      roundNumber,
      criteria: criteria.map((c) => ({ ...c, checks: null })),
    })
    return data
  },

  /** Bỏ bộ riêng của một vòng — vòng quay về dùng bộ chung. */
  async removeRound(jobId: string, roundNumber: number): Promise<JobInterviewRubric> {
    const { data } = await apiClient.delete<JobInterviewRubric>(`/jobs/${jobId}/interview-rubric/rounds/${roundNumber}`)
    return data
  },

  async templates(): Promise<CvRubricTemplate[]> {
    const { data } = await apiClient.get<CvRubricTemplate[]>('/playbooks/interview-rubric/templates')
    return data
  },

  async suggest(input: CvRubricSuggestionInput): Promise<CvRubricDraft> {
    const { data } = await apiClient.post<CvRubricDraft>('/playbooks/interview-rubric/suggest', input)
    return data
  },
}

export default interviewRubricService
