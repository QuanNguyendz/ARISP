import { apiClient } from '@ari/shared/api/apiClient'

/**
 * Bộ tiêu chí chấm CV (ADR-070) — BẮT BUỘC cho mọi tin — cùng CÔNG THỨC chấm do Hiring Manager quyết định (ADR-075).
 *
 * Hiring Manager khai ngay trên phiếu yêu cầu tuyển dụng; dựng tin thì bộ tiêu chí + công thức tự chép sang tin,
 * HM vẫn sửa được ở màn tin. Sửa lời (tiêu chí, lời neo, ý kiểm) → AI chấm lại mọi hồ sơ; sửa CÔNG THỨC (trọng số,
 * ngưỡng, điểm tối thiểu, trọng số ý kiểm) → điểm được tính lại từ câu trả lời cũ của AI, không gọi AI.
 *
 * Excel và "AI gợi ý" chỉ ĐIỀN NHANH vào trình soạn — không đường nào lưu thẳng; server chuẩn hoá lại mọi
 * bản (mã tiêu chí tự sinh, tổng trọng số = 100, mỗi tiêu chí chấm điểm phải có chuẩn chấm hoặc mức neo).
 */

/** Lời neo của bốn dải điểm. Ngưỡng của dải là công thức của tin (`CvScoringPolicy.bands`). */
export interface CvRubricLevels {
  excellent?: string | null
  good?: string | null
  fair?: string | null
  poor?: string | null
}

/**
 * Ý kiểm: dấu hiệu CÓ/KHÔNG kiểm được từ CV. AI chọn dải điểm và trả lời từng ý; vị trí điểm TRONG dải do server
 * tính theo tỉ lệ trọng số ý đạt (vd dải 90–100, đạt 2/4 ý ×1 → 95).
 */
export interface CvRubricCheck {
  /** Để trống với ý mới — server sinh `k1`, `k2`…; ý đã có gửi lại mã cũ. */
  key?: string | null
  text: string
  /** ×1 / ×2 / ×3 (ADR-075). Bỏ trống = ×1. */
  weight?: number | null
}

/** `scored` (hoặc bỏ trống) = tiêu chí chấm điểm · `knockout` = điều kiện bắt buộc (ADR-075). */
export type CvCriterionKind = 'scored' | 'knockout'

export interface CvRubricCriterion {
  /** Để trống với tiêu chí mới — server tự sinh. Tiêu chí đã có thì gửi lại đúng mã cũ. */
  key?: string | null
  name: string
  weight: number
  description?: string | null
  levels?: CvRubricLevels | null
  checks?: CvRubricCheck[] | null
  /** Điều kiện bắt buộc: AI trả lời đạt/không đạt kèm trích dẫn; không trọng số, không vào trung bình. */
  kind?: CvCriterionKind | null
  /** Điểm tối thiểu của tiêu chí (1–100) — thấp hơn thì khuyến nghị bị ép "Chưa phù hợp". */
  minScore?: number | null
}

/**
 * Công thức cấp tin (ADR-075): ngưỡng dưới của ba dải trên, và ngưỡng của nhãn khuyến nghị.
 * Dải: Xuất sắc [excellentFrom, 100] · Tốt [goodFrom, excellentFrom − 1] · Đạt một phần [fairFrom, goodFrom − 1] · Chưa đạt [0, fairFrom − 1].
 */
export interface CvScoringPolicy {
  bands: { excellentFrom: number; goodFrom: number; fairFrom: number }
  tiers: { strongHireFrom: number; hireFrom: number; cautionFrom: number }
}

export const DEFAULT_CV_SCORING_POLICY: CvScoringPolicy = {
  bands: { excellentFrom: 90, goodFrom: 70, fairFrom: 40 },
  tiers: { strongHireFrom: 80, hireFrom: 65, cautionFrom: 50 },
}

/** Tối đa ý kiểm mỗi tiêu chí — cùng giới hạn với server. */
export const CV_RUBRIC_MAX_CHECKS = 8
/** Tối đa điều kiện bắt buộc mỗi bộ — cùng giới hạn với server. */
export const CV_RUBRIC_MAX_KNOCKOUTS = 5
/** Độ rộng tối thiểu (đỉnh − đáy) của một dải. */
export const CV_BAND_MIN_WIDTH = 5

export const isKnockout = (c: Pick<CvRubricCriterion, 'kind'>) => c.kind === 'knockout'

/** Số ý kiểm có nội dung. */
export const checkCount = (c: CvRubricCriterion) => (c.checks ?? []).filter((x) => !!x.text?.trim()).length

export const CV_RUBRIC_LEVEL_KEYS = ['excellent', 'good', 'fair', 'poor'] as const
export type CvRubricLevelKey = (typeof CV_RUBRIC_LEVEL_KEYS)[number]

export type CvRecommendationKey = 'Strong Hire' | 'Hire' | 'Proceed with caution' | 'Reject'
export const CV_RECOMMENDATIONS: CvRecommendationKey[] = ['Strong Hire', 'Hire', 'Proceed with caution', 'Reject']

/** Khoảng điểm của từng dải theo công thức. */
export function bandRanges(p: CvScoringPolicy = DEFAULT_CV_SCORING_POLICY): Record<CvRubricLevelKey, { min: number; max: number }> {
  const b = p.bands
  return {
    excellent: { min: b.excellentFrom, max: 100 },
    good: { min: b.goodFrom, max: b.excellentFrom - 1 },
    fair: { min: b.fairFrom, max: b.goodFrom - 1 },
    poor: { min: 0, max: b.fairFrom - 1 },
  }
}

/** Khoảng điểm tổng của từng nhãn khuyến nghị theo công thức. */
export function tierRanges(p: CvScoringPolicy = DEFAULT_CV_SCORING_POLICY): Record<CvRecommendationKey, { from: number; to: number }> {
  const t = p.tiers
  return {
    'Strong Hire': { from: t.strongHireFrom, to: 100 },
    Hire: { from: t.hireFrom, to: t.strongHireFrom - 1 },
    'Proceed with caution': { from: t.cautionFrom, to: t.hireFrom - 1 },
    Reject: { from: 0, to: t.cautionFrom - 1 },
  }
}

/** Nhãn theo điểm (chưa xét cổng) — cùng luật với server. */
export function tierOf(score: number, p: CvScoringPolicy = DEFAULT_CV_SCORING_POLICY): CvRecommendationKey {
  if (score >= p.tiers.strongHireFrom) return 'Strong Hire'
  if (score >= p.tiers.hireFrom) return 'Hire'
  if (score >= p.tiers.cautionFrom) return 'Proceed with caution'
  return 'Reject'
}

export const samePolicy = (a: CvScoringPolicy, b: CvScoringPolicy) =>
  a.bands.excellentFrom === b.bands.excellentFrom &&
  a.bands.goodFrom === b.bands.goodFrom &&
  a.bands.fairFrom === b.bands.fairFrom &&
  a.tiers.strongHireFrom === b.tiers.strongHireFrom &&
  a.tiers.hireFrom === b.tiers.hireFrom &&
  a.tiers.cautionFrom === b.tiers.cautionFrom

export const clonePolicy = (p?: CvScoringPolicy | null): CvScoringPolicy => {
  const src = p ?? DEFAULT_CV_SCORING_POLICY
  return { bands: { ...src.bands }, tiers: { ...src.tiers } }
}

const isInt = (n: unknown) => typeof n === 'number' && Number.isInteger(n)

/** Cùng luật với server (`CvScoringPolicy.Validate`). */
export function cvPolicyProblems(p: CvScoringPolicy): string[] {
  const problems: string[] = []
  const b = p.bands
  const bandsOk =
    [b.excellentFrom, b.goodFrom, b.fairFrom].every(isInt) &&
    100 > b.excellentFrom &&
    b.excellentFrom > b.goodFrom &&
    b.goodFrom > b.fairFrom &&
    b.fairFrom > 0
  if (!bandsOk) problems.push('badBands')
  else if (Object.values(bandRanges(p)).some((r) => r.max - r.min < CV_BAND_MIN_WIDTH)) problems.push('bandTooNarrow')
  const t = p.tiers
  const tiersOk =
    [t.strongHireFrom, t.hireFrom, t.cautionFrom].every(isInt) &&
    100 >= t.strongHireFrom &&
    t.strongHireFrom > t.hireFrom &&
    t.hireFrom > t.cautionFrom &&
    t.cautionFrom >= 1
  if (!tiersOk) problems.push('badTiers')
  return problems
}

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
  /** Công thức đang dùng — luôn có (không khai thì là mặc định). */
  policy: CvScoringPolicy
  /** Chỉ có ở phản hồi của lệnh lưu. */
  saveOutcome?: CvRubricSaveOutcome | null
}

/**
 * Tác động của một lần lưu: `unchanged` · `recompute` (chỉ đổi công thức — tính lại từ câu trả lời cũ của AI, không
 * gọi AI) · `ai_rescore` (AI chấm lại).
 */
export interface CvRubricSaveOutcome {
  mode: 'unchanged' | 'recompute' | 'ai_rescore'
  affected: number
}

/** Bản nháp từ Excel / AI — kèm cảnh báo để sửa ngay trên trình soạn. */
export interface CvRubricDraft {
  criteria: CvRubricCriterion[]
  warnings: string[]
  /** Công thức đọc được từ file Excel (sheet "Cong thuc"); không có = giữ công thức đang soạn. */
  policy?: CvScoringPolicy | null
}

export interface CvRubricTemplate {
  id: string
  name: string
  createdAt: string
  criteria: CvRubricCriterion[]
  policy?: CvScoringPolicy | null
}

export interface CvRubricSuggestionInput {
  title: string
  description?: string | null
  requirements?: string | null
  experienceLevel?: string | null
  skills?: string[] | null
}

/** Một hồ sơ trong bản xem trước tác động. */
export interface CvRubricPreviewItem {
  applicationId: string
  candidateName: string
  oldScore?: number | null
  oldRecommendation?: string | null
  oldGateStatus?: string | null
  newScore?: number | null
  newRecommendation?: string | null
  newGateStatus?: string | null
  /** Bản nháp hỏi AI câu mới cho hồ sơ này — điểm mới chỉ có sau khi AI chấm lại. */
  requiresAi: boolean
}

/** Xem trước tác động của bản nháp lên mọi hồ sơ của tin — tính trong bộ nhớ, không ghi, không gọi AI. */
export interface CvRubricPreview {
  applicationCount: number
  recomputeCount: number
  aiRescoreCount: number
  pendingCount: number
  invalidCvCount: number
  scoreChangedCount: number
  recommendationChangedCount: number
  gateFailCount: number
  gateReviewCount: number
  items: CvRubricPreviewItem[]
}

/** Trình soạn CV hay trình soạn phỏng vấn — hai bên dùng chung cửa Excel nhưng khác luật. */
export type CvRubricSheetMode = 'cv' | 'interview'

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

/** Tổng trọng số các tiêu chí CHẤM ĐIỂM (điều kiện bắt buộc không mang trọng số). Làm tròn 2 chữ số. */
export const totalWeight = (criteria: CvRubricCriterion[]) =>
  Math.round(criteria.filter((c) => !isKnockout(c)).reduce((sum, c) => sum + (Number(c.weight) || 0), 0) * 100) / 100

/** Cùng luật với server — để nút Lưu/Gửi tắt sớm thay vì chờ lỗi trả về. */
export function cvRubricProblems(criteria: CvRubricCriterion[]): string[] {
  const rows = criteria.filter((c) => c.name.trim() || Number(c.weight) > 0 || isKnockout(c))
  const scored = rows.filter((c) => !isKnockout(c))
  const knockouts = rows.filter(isKnockout)
  const problems: string[] = []
  if (rows.length === 0) problems.push('empty')
  if (rows.some((c) => !c.name.trim())) problems.push('missingName')
  if (rows.length > 0 && scored.length === 0) problems.push('noScored')
  if (scored.some((c) => !(Number(c.weight) > 0))) problems.push('badWeight')
  if (scored.length > 0 && Math.abs(totalWeight(scored) - 100) > 0.01) problems.push('total')
  const hasGuide = (c: CvRubricCriterion) =>
    !!c.description?.trim() || Object.values(c.levels ?? {}).some((v) => !!v?.trim())
  if (scored.some((c) => c.name.trim() && !hasGuide(c))) problems.push('missingGuide')
  if (rows.length > 20) problems.push('tooMany')
  if (knockouts.length > CV_RUBRIC_MAX_KNOCKOUTS) problems.push('tooManyKnockouts')
  if (scored.some((c) => checkCount(c) > CV_RUBRIC_MAX_CHECKS)) problems.push('tooManyChecks')
  if (scored.some((c) => c.minScore != null && (!isInt(c.minScore) || c.minScore < 1 || c.minScore > 100)))
    problems.push('badMinScore')
  if (scored.some((c) => (c.checks ?? []).some((x) => x.weight != null && ![1, 2, 3].includes(x.weight))))
    problems.push('badCheckWeight')
  return problems
}

/** Payload gửi server: điều kiện bắt buộc không mang trọng số / dải / ý kiểm (trình soạn giữ chúng ở máy để bật lại không mất). */
export function toPayload(criteria: CvRubricCriterion[]): CvRubricCriterion[] {
  return criteria.map((c) =>
    isKnockout(c)
      ? { key: c.key ?? null, name: c.name, weight: 0, description: c.description ?? null, kind: 'knockout' }
      : { ...c, kind: null }
  )
}

export const cvRubricService = {
  async getForJob(jobId: string): Promise<JobCvRubric> {
    const { data } = await apiClient.get<JobCvRubric>(`/jobs/${jobId}/cv-rubric`)
    return data
  },

  async saveForJob(jobId: string, criteria: CvRubricCriterion[], policy: CvScoringPolicy): Promise<JobCvRubric> {
    const { data } = await apiClient.put<JobCvRubric>(`/jobs/${jobId}/cv-rubric`, {
      criteria: toPayload(criteria),
      policy,
    })
    return data
  },

  /** Xem trước tác động của bản nháp — không ghi gì, không gọi AI. */
  async previewForJob(jobId: string, criteria: CvRubricCriterion[], policy: CvScoringPolicy): Promise<CvRubricPreview> {
    const { data } = await apiClient.post<CvRubricPreview>(`/jobs/${jobId}/cv-rubric/preview`, {
      criteria: toPayload(criteria),
      policy,
    })
    return data
  },

  async downloadForJob(jobId: string): Promise<void> {
    const { data } = await apiClient.get<Blob>(`/jobs/${jobId}/cv-rubric/export`, { responseType: 'blob' })
    saveBlob(data, XLSX_NAME)
  },

  async parseSheet(file: File, mode: CvRubricSheetMode = 'cv'): Promise<CvRubricDraft> {
    const fd = new FormData()
    fd.append('file', file)
    const { data } = await apiClient.post<CvRubricDraft>('/playbooks/cv-rubric/parse-sheet', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
      params: mode === 'interview' ? { mode } : undefined,
    })
    return data
  },

  /**
   * Xuất bản nháp ra Excel. Cùng bảng mẫu cho bộ tiêu chí chấm CV lẫn chấm phỏng vấn; bộ CV kèm cột Loại / Điểm tối
   * thiểu và sheet công thức.
   */
  async downloadDraft(
    criteria: CvRubricCriterion[],
    fileName: string = XLSX_NAME,
    policy?: CvScoringPolicy | null,
    mode: CvRubricSheetMode = 'cv'
  ): Promise<void> {
    const { data } = await apiClient.post<Blob>(
      '/playbooks/cv-rubric/export-sheet',
      { criteria: mode === 'cv' ? toPayload(criteria) : criteria, policy: mode === 'cv' ? policy ?? null : null, mode },
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
