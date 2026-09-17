import { describe, expect, it } from 'vitest'
import { cvRetryTime } from '@/components/cvScore/useCvScoreText'

// Trạng thái "chấm lỗi · sẽ thử lại": mốc thử lại do server tính theo lịch giãn dần. Đã qua mốc mà chưa có
// kết quả nghĩa là đang chờ lượt quét kế tiếp — không được hiện một giờ trong quá khứ như lời hứa.

describe('cvRetryTime', () => {
  const now = new Date(2026, 8, 17, 10, 0, 0)

  it('shows only the time when the retry is later today', () => {
    expect(cvRetryTime(new Date(2026, 8, 17, 10, 15).toISOString(), now)).toBe('10:15')
  })

  it('shows date and time when the retry is on another day', () => {
    expect(cvRetryTime(new Date(2026, 8, 18, 1, 5).toISOString(), now)).toBe('18/09/2026 01:05')
  })

  it('reports a past retry time as due', () => {
    expect(cvRetryTime(new Date(2026, 8, 17, 9, 59).toISOString(), now)).toBe('due')
  })

  it('returns null without a usable time', () => {
    expect(cvRetryTime(null, now)).toBeNull()
    expect(cvRetryTime('not-a-date', now)).toBeNull()
  })
})
