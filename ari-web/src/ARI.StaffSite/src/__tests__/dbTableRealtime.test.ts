import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  RESYNC_TABLE,
  onDbTableChanged,
  publishDbTableChanged,
  touchesRow,
  type DbTableChangedDetail,
} from '@ari/shared/realtime/dbTableRealtime'

// Màn giữ state cục bộ (phiếu tuyển dụng, mẫu JD, đội, cài đặt…) chỉ tự làm mới qua sự kiện này.
// Lỗi thật đã gặp: server định tuyến các bảng đó từ lâu nhưng không màn nào nghe được, nên HR Leader
// duyệt phiếu xong thì HM và Recruiter phải F5 mới thấy.

describe('onDbTableChanged', () => {
  let target: EventTarget

  beforeEach(() => {
    vi.useFakeTimers()
    target = new EventTarget()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('only fires for the tables it listens to', () => {
    const handler = vi.fn()
    onDbTableChanged(['recruitment_requests'], handler, { target })

    publishDbTableChanged({ table: 'applications', op: 'U', id: 'a' }, target)
    vi.runAllTimers()
    expect(handler).not.toHaveBeenCalled()

    publishDbTableChanged({ table: 'recruitment_requests', op: 'U', id: 'r' }, target)
    vi.runAllTimers()
    expect(handler).toHaveBeenCalledTimes(1)
    expect(handler.mock.calls[0][0]).toEqual([{ table: 'recruitment_requests', op: 'U', id: 'r' }])
  })

  it('always lets a resync through — the listener lost events while reconnecting', () => {
    const handler = vi.fn()
    onDbTableChanged(['departments'], handler, { target })

    publishDbTableChanged({ table: RESYNC_TABLE, op: 'resync' }, target)
    vi.runAllTimers()

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('coalesces a burst of writes from one action into a single reload', () => {
    // Duyệt phiếu = sửa phiếu + tạo tin/ thông báo: vài dòng đổi sát nhau, chỉ được tải lại MỘT lần.
    const handler = vi.fn()
    onDbTableChanged(['recruitment_requests', 'job_postings'], handler, { target, delayMs: 250 })

    publishDbTableChanged({ table: 'recruitment_requests', op: 'U', id: 'r' }, target)
    vi.advanceTimersByTime(100)
    publishDbTableChanged({ table: 'job_postings', op: 'I', id: 'j' }, target)
    vi.advanceTimersByTime(249)
    expect(handler).not.toHaveBeenCalled()

    vi.advanceTimersByTime(1)
    expect(handler).toHaveBeenCalledTimes(1)
    expect((handler.mock.calls[0][0] as DbTableChangedDetail[]).map((c) => c.table)).toEqual([
      'recruitment_requests',
      'job_postings',
    ])
  })

  it('stops listening and drops the pending batch once unsubscribed', () => {
    const handler = vi.fn()
    const off = onDbTableChanged(['users'], handler, { target })

    publishDbTableChanged({ table: 'users', op: 'U', id: 'u' }, target)
    off()
    publishDbTableChanged({ table: 'users', op: 'U', id: 'u' }, target)
    vi.runAllTimers()

    expect(handler).not.toHaveBeenCalled()
  })
})

describe('touchesRow', () => {
  const change = (table: string, id?: string | null): DbTableChangedDetail => ({ table, op: 'U', id })

  it('matches only the row the screen is showing', () => {
    expect(touchesRow([change('recruitment_requests', 'r1')], 'recruitment_requests', 'r1')).toBe(true)
    expect(touchesRow([change('recruitment_requests', 'r2')], 'recruitment_requests', 'r1')).toBe(false)
    expect(touchesRow([change('job_postings', 'r1')], 'recruitment_requests', 'r1')).toBe(false)
  })

  it('treats an unknown scope as relevant — missing a reload is worse than one extra', () => {
    // Resync, dòng không mang khoá, hay màn chưa biết khoá của mình (bản JD chưa lưu lần nào).
    expect(touchesRow([{ table: RESYNC_TABLE, op: 'resync' }], 'users', 'u1')).toBe(true)
    expect(touchesRow([change('jd_documents', null)], 'jd_documents', 'd1')).toBe(true)
    expect(touchesRow([change('jd_documents', 'd9')], 'jd_documents', null)).toBe(true)
  })
})
