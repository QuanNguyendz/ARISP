import { describe, expect, it } from 'vitest'
import {
  DEFAULT_CV_SCORING_POLICY,
  bandRanges,
  cvPolicyProblems,
  cvRubricProblems,
  tierOf,
  tierRanges,
  toPayload,
  totalWeight,
} from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion, CvScoringPolicy } from '@ari/shared/fservices/cvRubric'
import { cvScoreTextClass, cvTierTone } from '@/components/cvScore/cvTier'
import { gateSummary, hasGates, weightedExpression } from '@/components/cvRubric/rubricFormula'

// ADR-075 — công thức chấm CV do Hiring Manager quyết định. Luật phía client phải khớp server
// (ScoringRubric.Validate / CvScoringPolicy.Validate) để lỗi hiện ngay cạnh trình soạn.

const row = (name: string, weight: number, patch: Partial<CvRubricCriterion> = {}): CvRubricCriterion => ({
  name,
  weight,
  description: 'chuẩn chấm',
  ...patch,
})
const knockout = (name: string): CvRubricCriterion => ({ name, weight: 0, kind: 'knockout' })

const policy = (bands: [number, number, number], tiers: [number, number, number]): CvScoringPolicy => ({
  bands: { excellentFrom: bands[0], goodFrom: bands[1], fairFrom: bands[2] },
  tiers: { strongHireFrom: tiers[0], hireFrom: tiers[1], cautionFrom: tiers[2] },
})

describe('band and tier ranges', () => {
  it('default formula = the ranges used before ADR-075', () => {
    expect(bandRanges()).toEqual({
      excellent: { min: 90, max: 100 },
      good: { min: 70, max: 89 },
      fair: { min: 40, max: 69 },
      poor: { min: 0, max: 39 },
    })
    expect(tierRanges()).toEqual({
      'Strong Hire': { from: 80, to: 100 },
      Hire: { from: 65, to: 79 },
      'Proceed with caution': { from: 50, to: 64 },
      Reject: { from: 0, to: 49 },
    })
  })

  it('follows the thresholds of the job', () => {
    const p = policy([85, 65, 40], [85, 70, 55])
    expect(bandRanges(p).good).toEqual({ min: 65, max: 84 })
    expect(tierOf(80, p)).toBe('Hire')
    expect(tierOf(80)).toBe('Strong Hire')
  })
})

describe('cvPolicyProblems', () => {
  it('accepts the default and a sensible custom formula', () => {
    expect(cvPolicyProblems(DEFAULT_CV_SCORING_POLICY)).toEqual([])
    expect(cvPolicyProblems(policy([85, 65, 35], [85, 70, 55]))).toEqual([])
  })

  it('refuses thresholds that are not strictly decreasing or empty', () => {
    expect(cvPolicyProblems(policy([70, 80, 40], [80, 65, 50]))).toContain('badBands')
    expect(cvPolicyProblems(policy([90, 70, Number.NaN], [80, 65, 50]))).toContain('badBands')
    expect(cvPolicyProblems(policy([90, 70, 40], [80, 80, 50]))).toContain('badTiers')
  })

  it('refuses bands narrower than 5 points', () => {
    expect(cvPolicyProblems(policy([97, 70, 40], [80, 65, 50]))).toContain('bandTooNarrow')
    expect(cvPolicyProblems(policy([90, 86, 40], [80, 65, 50]))).toContain('bandTooNarrow')
  })
})

describe('cvRubricProblems with knockouts, minimums and check weights', () => {
  it('knockouts carry no weight: the total counts scored criteria only', () => {
    const rubric = [row('Kinh nghiệm', 60), row('Kỹ năng', 40), knockout('JLPT N2')]
    expect(totalWeight(rubric)).toBe(100)
    expect(cvRubricProblems(rubric)).toEqual([])
  })

  it('a knockout needs no scoring guide', () => {
    expect(cvRubricProblems([row('A', 100), { name: 'Giấy phép', weight: 0, kind: 'knockout', description: null }])).toEqual([])
  })

  it('at least one scored criterion', () => {
    expect(cvRubricProblems([knockout('JLPT N2')])).toContain('noScored')
  })

  it('at most five knockouts', () => {
    const rubric = [row('A', 100), ...Array.from({ length: 6 }, (_, i) => knockout(`ĐK ${i}`))]
    expect(cvRubricProblems(rubric)).toContain('tooManyKnockouts')
  })

  it('minimums are whole numbers from 1 to 100', () => {
    expect(cvRubricProblems([row('A', 100, { minScore: 0 })])).toContain('badMinScore')
    expect(cvRubricProblems([row('A', 100, { minScore: 60.5 })])).toContain('badMinScore')
    expect(cvRubricProblems([row('A', 100, { minScore: 60 })])).toEqual([])
  })

  it('check weights are ×1, ×2 or ×3', () => {
    expect(cvRubricProblems([row('A', 100, { checks: [{ text: 'ý', weight: 4 }] })])).toContain('badCheckWeight')
    expect(cvRubricProblems([row('A', 100, { checks: [{ text: 'ý', weight: 3 }] })])).toEqual([])
  })
})

describe('toPayload', () => {
  it('sends a knockout without weight, bands, checklist or minimum — the editor keeps them locally', () => {
    const [k, s] = toPayload([
      { name: 'JLPT', weight: 30, kind: 'knockout', minScore: 50, levels: { good: 'x' }, checks: [{ text: 'y' }] },
      row('A', 100),
    ])
    expect(k).toEqual({ key: null, name: 'JLPT', weight: 0, description: null, kind: 'knockout' })
    expect(s.kind).toBeNull()
    expect(s.weight).toBe(100)
  })
})

describe('cvTier', () => {
  it('colours by the recommendation the server computed, not by hard-coded numbers', () => {
    expect(cvTierTone('Strong Hire')).toBe('strong')
    expect(cvTierTone('Reject')).toBe('reject')
    expect(cvTierTone(null)).toBe('none')
    expect(cvScoreTextClass('Proceed with caution')).toContain('amber')
  })
})

describe('rubricFormula', () => {
  it('writes the live formula with the criteria names and weights', () => {
    expect(weightedExpression([row('Kinh nghiệm', 60), row('Kỹ năng', 40), knockout('JLPT N2')])).toBe(
      '(Kinh nghiệm × 60 + Kỹ năng × 40) ÷ 100'
    )
    expect(weightedExpression([knockout('JLPT N2')])).toBe('')
  })

  it('lists the gates of a hybrid formula', () => {
    const g = gateSummary([row('Kinh nghiệm', 100, { minScore: 60 }), knockout('JLPT N2')])
    expect(g).toEqual({ knockouts: ['JLPT N2'], minScores: [{ name: 'Kinh nghiệm', min: 60 }] })
    expect(hasGates(g)).toBe(true)
    expect(hasGates(gateSummary([row('A', 100)]))).toBe(false)
  })
})
