import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import {
  STAFF_APPLICATIONS_REFRESH_EVENT,
  affectsApplication,
  affectsJob,
  createApplicationChangeBatcher,
  invalidateApplicationQueries,
  type ApplicationsChangedDetail,
} from '@ari/shared/realtime/applicationRealtime'

// Chấm CV nền xong (ADR-070) chỉ đổi dòng `applications`; mọi màn đọc hồ sơ phải tự làm mới từ đúng
// một sự kiện đó. Lỗi thật đã gặp: màn tin của Hiring Manager dùng một khoá không nhánh realtime nào
// nhắc tới, nên đứng ở "Đang chấm CV" cho tới khi F5.

const JOB = 'job-1'
const OTHER_JOB = 'job-2'
const APP = 'app-1'
const OTHER_APP = 'app-2'

const seed = (qc: QueryClient, keys: unknown[][]) => keys.forEach((k) => qc.setQueryData(k, { seeded: true }))
const invalidated = (qc: QueryClient, key: unknown[]) => qc.getQueryState(key)?.isInvalidated === true

const scoped = (jobIds: string[], appIds: string[]): ApplicationsChangedDetail => ({
  all: false,
  jobPostingIds: jobIds,
  applicationIds: appIds,
})

describe('invalidateApplicationQueries', () => {
  it('refreshes every screen that shows the scored application of that job', () => {
    const qc = new QueryClient()
    seed(qc, [
      ['job', JOB, 'applications'], // màn tin của HM + Recruiter
      ['job-cv-rubric', JOB], // thẻ bộ tiêu chí: số hồ sơ còn chờ chấm
      ['hr-application', APP], // màn hồ sơ của HM
      ['application', APP],
      ['applications'],
      ['recruiter-candidates'],
      ['hm-jobs'],
      ['hr-dashboard'],
    ])

    invalidateApplicationQueries(qc, scoped([JOB], [APP]))

    for (const key of [
      ['job', JOB, 'applications'],
      ['job-cv-rubric', JOB],
      ['hr-application', APP],
      ['application', APP],
      ['applications'],
      ['recruiter-candidates'],
      ['hm-jobs'],
      ['hr-dashboard'],
    ]) {
      expect(invalidated(qc, key), JSON.stringify(key)).toBe(true)
    }
  })

  it('leaves other jobs and other applications alone when the scope is known', () => {
    const qc = new QueryClient()
    seed(qc, [
      ['job', OTHER_JOB, 'applications'],
      ['job-cv-rubric', OTHER_JOB],
      ['hr-application', OTHER_APP],
      ['job', JOB], // chi tiết tin không đổi vì hồ sơ đổi
    ])

    invalidateApplicationQueries(qc, scoped([JOB], [APP]))

    expect(invalidated(qc, ['job', OTHER_JOB, 'applications'])).toBe(false)
    expect(invalidated(qc, ['job-cv-rubric', OTHER_JOB])).toBe(false)
    expect(invalidated(qc, ['hr-application', OTHER_APP])).toBe(false)
    expect(invalidated(qc, ['job', JOB])).toBe(false)
  })

  it('refreshes all job lists and applications when the scope is unknown', () => {
    const qc = new QueryClient()
    seed(qc, [
      ['job', OTHER_JOB, 'applications'],
      ['job-cv-rubric', OTHER_JOB],
      ['hr-application', OTHER_APP],
    ])

    invalidateApplicationQueries(qc, { all: true, jobPostingIds: [], applicationIds: [] })

    expect(invalidated(qc, ['job', OTHER_JOB, 'applications'])).toBe(true)
    expect(invalidated(qc, ['job-cv-rubric', OTHER_JOB])).toBe(true)
    expect(invalidated(qc, ['hr-application', OTHER_APP])).toBe(true)
  })
})

describe('createApplicationChangeBatcher', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('coalesces a rescoring burst into one refresh carrying every id', () => {
    const qc = new QueryClient()
    const spy = vi.spyOn(qc, 'invalidateQueries')
    const target = new EventTarget()
    const events: ApplicationsChangedDetail[] = []
    target.addEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, (e) =>
      events.push((e as CustomEvent<ApplicationsChangedDetail>).detail)
    )
    const batcher = createApplicationChangeBatcher(qc, { delayMs: 300, maxWaitMs: 1500, target })

    batcher.push({ jobPostingId: JOB, applicationId: 'a1' })
    vi.advanceTimersByTime(100)
    batcher.push({ jobPostingId: JOB, applicationId: 'a2' })
    vi.advanceTimersByTime(100)
    batcher.push({ jobPostingId: JOB, applicationId: 'a3' })
    expect(events).toHaveLength(0)
    expect(spy).not.toHaveBeenCalled()

    vi.advanceTimersByTime(300)
    expect(events).toHaveLength(1)
    expect(events[0]).toEqual({ all: false, jobPostingIds: [JOB], applicationIds: ['a1', 'a2', 'a3'] })
    expect(spy).toHaveBeenCalled()
  })

  it('does not starve a long burst: flushes by maxWait even while events keep coming', () => {
    const qc = new QueryClient()
    const target = new EventTarget()
    let flushes = 0
    target.addEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, () => flushes++)
    const batcher = createApplicationChangeBatcher(qc, { delayMs: 300, maxWaitMs: 1000, target })

    for (let i = 0; i < 12; i++) {
      batcher.push({ jobPostingId: JOB, applicationId: `a${i}` })
      vi.advanceTimersByTime(200) // luôn đến trước khi hết delay
    }
    expect(flushes).toBeGreaterThanOrEqual(2)
  })

  it('an event without ids means refresh everything', () => {
    const qc = new QueryClient()
    const target = new EventTarget()
    let detail: ApplicationsChangedDetail | null = null
    target.addEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, (e) => {
      detail = (e as CustomEvent<ApplicationsChangedDetail>).detail
    })
    const batcher = createApplicationChangeBatcher(qc, { target })

    batcher.push()
    batcher.flush()
    expect(detail).toEqual({ all: true, jobPostingIds: [], applicationIds: [] })
  })

  it('dispose drops pending changes', () => {
    const qc = new QueryClient()
    const target = new EventTarget()
    let flushes = 0
    target.addEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, () => flushes++)
    const batcher = createApplicationChangeBatcher(qc, { target })

    batcher.push({ jobPostingId: JOB, applicationId: APP })
    batcher.dispose()
    vi.advanceTimersByTime(5000)
    expect(flushes).toBe(0)
  })
})

describe('affectsJob / affectsApplication', () => {
  it('matches the screen only when it is in scope or the scope is unknown', () => {
    expect(affectsJob(scoped([JOB], [APP]), JOB)).toBe(true)
    expect(affectsJob(scoped([JOB], [APP]), OTHER_JOB)).toBe(false)
    expect(affectsJob(scoped([], [APP]), OTHER_JOB)).toBe(true)
    expect(affectsJob({ all: true, jobPostingIds: [JOB], applicationIds: [] }, OTHER_JOB)).toBe(true)

    expect(affectsApplication(scoped([JOB], [APP]), APP)).toBe(true)
    expect(affectsApplication(scoped([JOB], [APP]), OTHER_APP)).toBe(false)
    expect(affectsApplication(scoped([JOB], []), OTHER_APP)).toBe(true)
  })
})
