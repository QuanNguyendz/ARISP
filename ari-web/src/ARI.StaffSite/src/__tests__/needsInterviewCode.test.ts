import { describe, expect, it } from 'vitest'
import type { HrApplicationItem } from '@ari/shared/types/application'
import { needsInterviewCode } from '@/components/jobCandidates/pipelineStages'

/**
 * Thẻ "Mã vào phòng phỏng vấn" trên danh sách ứng viên chỉ hiện ở vòng HỘI THOẠI đã có lịch mà ứng
 * viên chưa vào phòng. Đây là lọc sơ ở giao diện — server vẫn là nơi quyết định cấp được hay không.
 */
const rounds = [
  { roundNumber: 1, roundType: 'online_test' },
  { roundNumber: 2, roundType: 'screening' },
  { roundNumber: 3, roundType: 'technical' },
]

const app = (patch: Partial<HrApplicationItem>): HrApplicationItem =>
  ({
    id: 'a1',
    jobPostingId: 'j1',
    candidateEmail: 'c@x.io',
    candidateName: 'Nguyen Van A',
    source: 'job_board',
    status: 'interview',
    practiceSessionUsed: false,
    createdAt: '2026-09-15T00:00:00Z',
    currentRound: 2,
    stageStatus: 'schedule_confirmed',
    ...patch,
  }) as HrApplicationItem

describe('needsInterviewCode', () => {
  it.each(['schedule_pending', 'schedule_confirmed', 'missed'])(
    'vòng sơ loại / chuyên môn đã có lịch (%s) → cần mã',
    (stageStatus) => {
      expect(needsInterviewCode(app({ stageStatus }), rounds)).toBe(true)
      expect(needsInterviewCode(app({ stageStatus, currentRound: 3 }), rounds)).toBe(true)
    }
  )

  it('vòng trắc nghiệm làm trong Portal → không dùng mã', () => {
    expect(needsInterviewCode(app({ currentRound: 1, stageStatus: 'schedule_confirmed' }), rounds)).toBe(false)
  })

  it.each(['interview_waiting', 'interview_active', 'interview_done', 'pending_result', 'schedule_declined'])(
    'đã vào phòng / chưa có lịch hiệu lực (%s) → không hiện',
    (stageStatus) => {
      expect(needsInterviewCode(app({ stageStatus }), rounds)).toBe(false)
    }
  )

  it('hồ sơ chưa vào vòng phỏng vấn → không hiện', () => {
    expect(needsInterviewCode(app({ status: 'screening', stageStatus: 'awaiting_schedule' }), rounds)).toBe(false)
    expect(needsInterviewCode(app({ status: 'not_pass' }), rounds)).toBe(false)
  })
})
