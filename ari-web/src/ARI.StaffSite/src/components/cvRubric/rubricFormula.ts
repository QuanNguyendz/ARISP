import { isKnockout, totalWeight } from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion } from '@ari/shared/fservices/cvRubric'

const fmt = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(2).replace(/\.?0+$/, ''))

/**
 * Công thức sống của bộ tiêu chí, viết bằng đúng tên và trọng số HM đang khai (ADR-075):
 * `(Kinh nghiệm × 60 + Kỹ năng × 40) ÷ 100`. Chỉ tiêu chí chấm điểm có tên; điều kiện bắt buộc không vào tổng.
 */
export function weightedExpression(criteria: CvRubricCriterion[]): string {
  const scored = criteria.filter((c) => !isKnockout(c) && c.name.trim())
  if (scored.length === 0) return ''
  const terms = scored.map((c) => `${c.name.trim()} × ${fmt(Number(c.weight) || 0)}`).join(' + ')
  return `(${terms}) ÷ ${fmt(totalWeight(scored))}`
}

export interface GateSummary {
  /** Tên các điều kiện bắt buộc. */
  knockouts: string[]
  /** Tiêu chí có điểm tối thiểu. */
  minScores: { name: string; min: number }[]
}

/** Các cổng không bù trừ của công thức — trượt cổng nào thì khuyến nghị bị ép "Chưa phù hợp". */
export function gateSummary(criteria: CvRubricCriterion[]): GateSummary {
  return {
    knockouts: criteria.filter((c) => isKnockout(c) && c.name.trim()).map((c) => c.name.trim()),
    minScores: criteria
      .filter((c) => !isKnockout(c) && c.name.trim() && c.minScore != null)
      .map((c) => ({ name: c.name.trim(), min: c.minScore as number })),
  }
}

/** Công thức có cổng nào không — "bù trừ thuần" hay "bù trừ + cổng" (mô hình lai). */
export const hasGates = (g: GateSummary) => g.knockouts.length > 0 || g.minScores.length > 0
