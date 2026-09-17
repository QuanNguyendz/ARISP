import { describe, expect, it } from 'vitest'
import { cvRubricProblems, totalWeight } from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion } from '@ari/shared/fservices/cvRubric'

// ADR-070 — luật phía client phải khớp server (CvRubricEditing.Normalize), để nút Lưu/Gửi báo lỗi ngay
// cạnh trình soạn thay vì chờ server trả về.

const row = (name: string, weight: number, description = 'chuẩn chấm'): CvRubricCriterion => ({
  name,
  weight,
  description,
})

describe('cvRubricProblems', () => {
  it('valid rubric has no problems', () => {
    expect(cvRubricProblems([row('Kinh nghiệm', 60), row('Kỹ năng', 40)])).toEqual([])
  })

  it('empty rubric is refused', () => {
    expect(cvRubricProblems([])).toContain('empty')
    expect(cvRubricProblems([row('', 0, '')])).toContain('empty')
  })

  it('weights must total 100 (with float tolerance)', () => {
    expect(cvRubricProblems([row('A', 50), row('B', 40)])).toContain('total')
    expect(cvRubricProblems([row('A', 33.33), row('B', 33.33), row('C', 33.34)])).not.toContain('total')
  })

  it('each criterion needs a name, a positive weight and a guide or a level', () => {
    const problems = cvRubricProblems([
      row('', 50),
      { name: 'Không chuẩn', weight: 50, description: '  ', levels: { good: '  ' } },
    ])
    expect(problems).toContain('missingName')
    expect(problems).toContain('missingGuide')

    expect(cvRubricProblems([row('A', 100), row('B', 0)])).toContain('badWeight')
  })

  it('a level alone is enough guidance', () => {
    expect(
      cvRubricProblems([{ name: 'A', weight: 100, description: null, levels: { excellent: '≥4 năm' } }])
    ).toEqual([])
  })

  it('at most 20 criteria', () => {
    const many = Array.from({ length: 21 }, (_, i) => row(`T${i}`, i === 0 ? 80 : 1))
    expect(cvRubricProblems(many)).toContain('tooMany')
  })
})

describe('totalWeight', () => {
  it('rounds to two decimals', () => {
    expect(totalWeight([row('A', 0.1), row('B', 0.2)])).toBe(0.3)
  })
})
