import { describe, expect, it } from 'vitest'
import { aiPosition, bandPosition } from '@/components/cvScore/bandPosition'
import { checkCount, cvRubricProblems } from '@ari/shared/fservices/cvRubric'
import type { CvScoreCriterion } from '@ari/shared/types/application'

// Màn giải thích điểm phải in lại ĐÚNG phép tính server dùng (CvCriterionScoring): đáy + đạt/đã trả lời × độ rộng.

const criterion = (patch: Partial<CvScoreCriterion>): CvScoreCriterion => ({ key: 'k', label: 'Kinh nghiệm', ...patch })

describe('bandPosition', () => {
  it('rebuilds the in-band formula from the checklist', () => {
    const pos = bandPosition(
      criterion({ scoreSource: 'checklist', bandMin: 90, bandMax: 100, checksMet: 2, checksAnswered: 4, score: 95 })
    )
    expect(pos).toEqual({ min: 90, max: 100, span: 10, met: 2, answered: 4, weighted: false, exact: 95 })
  })

  // ADR-075: ý kiểm có trọng số → tử / mẫu là tổng trọng số, đúng như server.
  it('uses the weights of the checklist items when they are not all ×1', () => {
    const pos = bandPosition(
      criterion({
        scoreSource: 'checklist',
        bandMin: 70,
        bandMax: 89,
        checksMet: 1,
        checksAnswered: 3,
        checksMetWeight: 2,
        checksAnsweredWeight: 4,
        checks: [
          { key: 'k1', text: 'a', met: true, weight: 2 },
          { key: 'k2', text: 'b', met: false },
          { key: 'k3', text: 'c', met: false },
        ],
        score: 80,
      })
    )
    expect(pos).toMatchObject({ met: 2, answered: 4, weighted: true })
    expect(pos?.exact).toBeCloseTo(79.5, 5) // 70 + 19 × 2/4
  })

  it('keeps the unrounded value so the rounding step stays visible', () => {
    const pos = bandPosition(
      criterion({ scoreSource: 'checklist', bandMin: 70, bandMax: 89, checksMet: 1, checksAnswered: 3, score: 76 })
    )
    expect(pos?.exact).toBeCloseTo(76.333, 2)
  })

  it('returns null when the position was estimated by the AI', () => {
    expect(bandPosition(criterion({ scoreSource: 'ai', bandMin: 70, bandMax: 89 }))).toBeNull()
    expect(bandPosition(criterion({ scoreSource: 'checklist', bandMin: 90, bandMax: 100, checksAnswered: 0 }))).toBeNull()
    expect(bandPosition(criterion({}))).toBeNull()
  })
})

describe('aiPosition', () => {
  it('maps the position the AI gave onto the band of this job', () => {
    const pos = aiPosition(criterion({ scoreSource: 'ai', bandMin: 70, bandMax: 89, position: 0.5, score: 80 }))
    expect(pos).toMatchObject({ min: 70, max: 89, span: 19, position: 0.5 })
    expect(pos?.exact).toBeCloseTo(79.5, 5)
  })

  it('is null for old scores where the AI gave a number instead of a position', () => {
    expect(aiPosition(criterion({ scoreSource: 'ai', bandMin: 70, bandMax: 89 }))).toBeNull()
    expect(aiPosition(criterion({ scoreSource: 'checklist', bandMin: 70, bandMax: 89, position: 0.5 }))).toBeNull()
  })
})

describe('checklist limits in the rubric editor', () => {
  const base = { name: 'Kinh nghiệm', weight: 100, description: 'chuẩn' }

  it('counts only non-empty checklist items', () => {
    expect(checkCount({ ...base, checks: [{ text: 'a' }, { text: '  ' }, { text: 'b' }] })).toBe(2)
  })

  it('refuses more than 8 checklist items per criterion', () => {
    const checks = Array.from({ length: 9 }, (_, i) => ({ text: `ý ${i + 1}` }))
    expect(cvRubricProblems([{ ...base, checks }])).toContain('tooManyChecks')
    expect(cvRubricProblems([{ ...base, checks: checks.slice(0, 8) }])).not.toContain('tooManyChecks')
  })
})
