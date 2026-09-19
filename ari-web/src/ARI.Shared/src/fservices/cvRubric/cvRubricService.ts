import { apiClient } from '@ari/shared/api/apiClient'

/**
 * Bộ tiêu chí chấm CV (ADR-070) — BẮT BUỘC cho mọi tin.
 *
 * Hiring Manager khai ngay trên phiếu yêu cầu tuyển dụng; dựng tin thì bộ tiêu chí tự chép sang tin, HM vẫn
 * sửa được ở màn tin. Lưu bộ mới là mọi hồ sơ của tin được chấm lại ở nền.
 *
 * Excel và "AI gợi ý" chỉ ĐIỀN NHANH vào trình soạn — không đường nào lưu thẳng; server chuẩn hoá lại mọi
 * bản (mã tiêu chí tự sinh, tổng trọng số = 100, mỗi tiêu chí phải có chuẩn chấm hoặc mức neo).
 */

/** Bốn dải điểm cố định: 90–100 · 70–89 · 40–69 · 0–39. */
export interface CvRubricLevels {
  excellent?: string | null
  good?: string | null
  fair?: string | null
  poor?: string | null
}

/**
 * Ý kiểm: dấu hiệu CÓ/KHÔNG kiểm được từ CV. AI chọn dải điểm và trả lời từng ý; vị trí điểm TRONG dải do server
 * tính theo số ý đạt (vd dải 90–100, đạt 2/4 ý → 95).
 */
export interface CvRubricCheck {
  /** Để trống với ý mới — server sinh `k1`, `k2`…; ý đã có gửi lại mã cũ. */
  key?: string | null
  text: string
}

export interface CvRubricCriterion {
  /** Để trống với tiêu chí mới — server tự sinh. Tiêu chí đã có thì gửi lại đúng mã cũ. */
  key?: string | null
  name: string
  weight: number
  description?: string | null
  levels?: CvRubricLevels | null
  checks?: CvRubricCheck[] | null
}

/** Tối đa ý kiểm mỗi tiêu chí — cùng giới hạn với server. */
export const CV_RUBRIC_MAX_CHECKS = 8

/** Số ý kiểm có nội dung. */
export const checkCount = (c: CvRubricCriterion) => (c.checks ?? []).filter((x) => !!x.text?.trim()).length

export const CV_RUBRIC_LEVEL_KEYS = ['excellent', 'good', 'fair', 'poor'] as const
export type CvRubricLevelKey = (typeof CV_RUBRIC_LEVEL_KEYS)[number]

/** Bộ tiêu chí đang dùng của một tin. */
export interface JobCvRubric {
  criteria: CvRubricCriterion[]
  documentId?: string | null
  savedAt?: string | null
  savedBy?: string | null
  /** Do server quyết: HM chính của tin hoặc quản trị viên. */
  canEdit: boolean
  /** Số hồ sơ có CV — lưu bộ mới là chấm lại chừng ấy hồ sơ. */
  applicationCount: number
  /** Số hồ sơ đang chờ chấm / chấm lại theo bộ hiện hành. */
  pendingCount: number
}

/** Bản nháp từ Excel / AI — kèm cảnh báo để sửa ngay trên trình soạn. */
export interface CvRubricDraft {
  criteria: CvRubricCriterion[]
  warnings: string[]
}

export interface CvRubricTemplate {
  id: string
  name: string
  createdAt: string
  criteria: CvRubricCriterion[]
}

export interface CvRubricSuggestionInput {
  title: string
  description?: string | null
  requirements?: string | null
  experienceLevel?: string | null
  skills?: string[] | null
}

const XLSX_NAME = 'bo-tieu-chi-cham-cv.xlsx'

function saveBlob(data: Blob, fileName: string) {
  const url = URL.createObjectURL(data)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

/** Tổng trọng số (làm tròn 2 chữ số để tránh sai số dấu phẩy động). */
export const totalWeight = (criteria: CvRubricCriterion[]) =>
  Math.round(criteria.reduce((sum, c) => sum + (Number(c.weight) || 0), 0) * 100) / 100

/** Cùng luật với server — để nút Lưu/Gửi tắt sớm thay vì chờ lỗi trả về. */
export function cvRubricProblems(criteria: CvRubricCriterion[]): string[] {
  const rows = criteria.filter((c) => c.name.trim() || Number(c.weight) > 0)
  const problems: string[] = []
  if (rows.length === 0) problems.push('empty')
  if (rows.some((c) => !c.name.trim())) problems.push('missingName')
  if (rows.some((c) => !(Number(c.weight) > 0))) problems.push('badWeight')
  if (rows.length > 0 && Math.abs(totalWeight(rows) - 100) > 0.01) problems.push('total')
  const hasGuide = (c: CvRubricCriterion) =>
    !!c.description?.trim() || Object.values(c.levels ?? {}).some((v) => !!v?.trim())
  if (rows.some((c) => c.name.trim() && !hasGuide(c))) problems.push('missingGuide')
  if (rows.length > 20) problems.push('tooMany')
  if (rows.some((c) => checkCount(c) > CV_RUBRIC_MAX_CHECKS)) problems.push('tooManyChecks')
  return problems
}

export const cvRubricService = {
  async getForJob(jobId: string): Promise<JobCvRubric> {
    const { data } = await apiClient.get<JobCvRubric>(`/jobs/${jobId}/cv-rubric`)
    return data
  },

  async saveForJob(jobId: string, criteria: CvRubricCriterion[]): Promise<JobCvRubric> {
    const { data } = await apiClient.put<JobCvRubric>(`/jobs/${jobId}/cv-rubric`, { criteria })
    return data
  },

  async downloadForJob(jobId: string): Promise<void> {
    const { data } = await apiClient.get<Blob>(`/jobs/${jobId}/cv-rubric/export`, { responseType: 'blob' })
    saveBlob(data, XLSX_NAME)
  },

  async parseSheet(file: File): Promise<CvRubricDraft> {
    const fd = new FormData()
    fd.append('file', file)
    const { data } = await apiClient.post<CvRubricDraft>('/playbooks/cv-rubric/parse-sheet', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  /** Xuất bản nháp ra Excel. Cùng bảng mẫu cho bộ tiêu chí chấm CV lẫn chấm phỏng vấn — chỉ khác tên file. */
  async downloadDraft(criteria: CvRubricCriterion[], fileName: string = XLSX_NAME): Promise<void> {
    const { data } = await apiClient.post<Blob>(
      '/playbooks/cv-rubric/export-sheet',
      { criteria },
      { responseType: 'blob' }
    )
    saveBlob(data, fileName)
  },

  async templates(): Promise<CvRubricTemplate[]> {
    const { data } = await apiClient.get<CvRubricTemplate[]>('/playbooks/cv-rubric/templates')
    return data
  },

  async suggest(input: CvRubricSuggestionInput): Promise<CvRubricDraft> {
    const { data } = await apiClient.post<CvRubricDraft>('/playbooks/cv-rubric/suggest', input)
    return data
  },
}

export default cvRubricService
