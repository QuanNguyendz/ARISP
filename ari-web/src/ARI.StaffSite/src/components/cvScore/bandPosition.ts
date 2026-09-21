import type { CvScoreCriterion } from '@ari/shared/types/application'

export interface BandPosition {
  min: number
  max: number
  /** Độ rộng dải (đỉnh − đáy). */
  span: number
  /** Σ trọng số ý đạt / Σ trọng số ý đã trả lời — bằng số ý khi mọi ý ×1. */
  met: number
  answered: number
  /** Có ý nào nặng hơn ×1 không — để màn hình nói "trọng số ý đạt" thay vì "số ý đạt". */
  weighted: boolean
  /** Trước khi làm tròn: đáy + met ÷ answered × span. */
  exact: number
}

/**
 * Phép tính vị trí điểm trong dải từ ý kiểm — viết lại đúng công thức backend (CvCriterionScoring) để màn hình
 * in ra được "90 + 2/4 × 10 = 95". Ý kiểm có trọng số (ADR-075) thì tử / mẫu là tổng trọng số. `null` khi điểm tiêu
 * chí không đến từ ý kiểm (xem {@link aiPosition}).
 */
export function bandPosition(c: CvScoreCriterion): BandPosition | null {
  if (c.scoreSource !== 'checklist' || c.bandMin == null || c.bandMax == null) return null
  const answered = c.checksAnsweredWeight ?? c.checksAnswered ?? 0
  if (!answered) return null
  const met = c.checksMetWeight ?? c.checksMet ?? 0
  const span = c.bandMax - c.bandMin
  const weighted = (c.checks ?? []).some((x) => (x.weight ?? 1) !== 1)
  return { min: c.bandMin, max: c.bandMax, span, met, answered, weighted, exact: c.bandMin + (span * met) / answered }
}

/**
 * Điểm từ VỊ TRÍ AI cho trong dải (tiêu chí không có ý kiểm, ADR-075): đáy + vị trí × độ rộng. `null` khi bản chấm
 * không có vị trí (bản cũ — AI tự cho số).
 */
export function aiPosition(c: CvScoreCriterion): { min: number; max: number; span: number; position: number; exact: number } | null {
  if (c.scoreSource !== 'ai' || c.position == null || c.bandMin == null || c.bandMax == null) return null
  const span = c.bandMax - c.bandMin
  return { min: c.bandMin, max: c.bandMax, span, position: c.position, exact: c.bandMin + span * c.position }
}
