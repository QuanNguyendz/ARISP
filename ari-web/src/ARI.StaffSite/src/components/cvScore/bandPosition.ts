import type { CvScoreCriterion } from '@ari/shared/types/application'

export interface BandPosition {
  min: number
  max: number
  /** Độ rộng dải (đỉnh − đáy). */
  span: number
  met: number
  answered: number
  /** Trước khi làm tròn: đáy + met ÷ answered × span. */
  exact: number
}

/**
 * Phép tính vị trí điểm trong dải từ ý kiểm — viết lại đúng công thức backend (CvCriterionScoring) để màn hình
 * in ra được "90 + 2/4 × 10 = 95". `null` khi điểm tiêu chí không đến từ ý kiểm (AI ước lượng / bản chấm cũ).
 */
export function bandPosition(c: CvScoreCriterion): BandPosition | null {
  if (c.scoreSource !== 'checklist' || c.bandMin == null || c.bandMax == null || !c.checksAnswered) return null
  const met = c.checksMet ?? 0
  const span = c.bandMax - c.bandMin
  return { min: c.bandMin, max: c.bandMax, span, met, answered: c.checksAnswered, exact: c.bandMin + (span * met) / c.checksAnswered }
}
