import { describe, expect, it } from 'vitest'
import { bandPosition } from '@/components/cvScore/bandPosition'
import { checkCount, cvRubricProblems } from '@ari/shared/fservices/cvRubric'
import type { CvScoreCriterion } from '@ari/shared/types/application'

// Màn giải thích điểm phải in lại ĐÚNG phép tính server dùng (CvCriterionScoring): đáy + đạt/đã trả lời × độ rộng.

const criterion = (patch: Partial<CvScoreCriterion>): CvScoreCriterion => ({ key: 'k', label: 'Kinh nghiệm', ...patch })

describe('bandPosition', () => {
  it('rebuilds the in-band formula from the checklist', () => {
    const pos = bandPosition(
      criterion({ scoreSource: 'checklist', bandMin: 90, bandMax: 100, checksMet: 2, checksAnswered: 4, score: 95 })
    )
    expect(pos).toEqual({ min: 90, max: 100, span: 10, met: 2, answered: 4, exact: 95 })
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
