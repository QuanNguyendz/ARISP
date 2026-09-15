import { describe, expect, it } from 'vitest'
import {
  formatDateTime24,
  formatTime24,
  isCompleteLocalDateTime,
  joinLocalDateTime,
  parseTime24,
  splitLocalDateTime,
  timeSlots,
} from '@ari/shared/utils/time24'

/**
 * Giờ 24 tiếng dùng chung (`@ari/shared/utils/time24`). Ô nhập giờ đọc chữ người dùng gõ bằng
 * `parseTime24` — đọc sai là xếp nhầm ca, nên từng cách viết hay gặp đều được khoá ở đây.
 */
describe('parseTime24', () => {
  it.each([
    ['15', '15:00'],
    ['9', '09:00'],
    ['0', '00:00'],
    ['1530', '15:30'],
    ['930', '09:30'],
    ['0930', '09:30'],
    ['15:30', '15:30'],
    ['15h30', '15:30'],
    ['15h', '15:00'],
    ['15g30', '15:30'],
    ['9.05', '09:05'],
    [' 15 : 30 ', '15:30'],
    ['23:59', '23:59'],
  ])('đọc "%s" thành %s', (text, expected) => {
    expect(parseTime24(text)).toBe(expected)
  })

  it.each([
    ['3ch', '15:00'],
    ['3 CH', '15:00'],
    ['11:30 ch', '23:30'],
    ['12:01 sa', '00:01'],
    ['12ch', '12:00'],
    ['3pm', '15:00'],
    ['12am', '00:00'],
    ['9a', '09:00'],
  ])('hậu tố 12 giờ "%s" vẫn ra đúng %s', (text, expected) => {
    expect(parseTime24(text)).toBe(expected)
  })

  it.each(['', '   ', '24', '24:00', '15:60', '9999', 'abc', '13ch', '0sa', '1:2:3', '15-30'])(
    'không hiểu "%s" thì trả null, không đoán',
    (text) => {
      expect(parseTime24(text)).toBeNull()
    }
  )
})

describe('formatTime24 / formatDateTime24', () => {
  it('luôn 24 giờ, có số 0 đứng trước', () => {
    expect(formatTime24(new Date(2026, 8, 14, 15, 5))).toBe('15:05')
    expect(formatTime24(new Date(2026, 8, 14, 0, 1))).toBe('00:01')
    expect(formatDateTime24(new Date(2026, 8, 4, 9, 0))).toBe('04/09/2026 09:00')
  })

  it('mốc hỏng hoặc trống thì trả chuỗi rỗng', () => {
    expect(formatTime24('')).toBe('')
    expect(formatTime24(null)).toBe('')
    expect(formatTime24('không-phải-ngày')).toBe('')
    expect(formatDateTime24(undefined)).toBe('')
  })
})

describe('timeSlots', () => {
  it('mốc cách đều từ 00:00 tới trước 24:00', () => {
    const slots = timeSlots(15)
    expect(slots).toHaveLength(96)
    expect(slots[0]).toBe('00:00')
    expect(slots[61]).toBe('15:15')
    expect(slots[slots.length - 1]).toBe('23:45')
  })
})

describe('chuỗi ngày-giờ local', () => {
  it('chỉ coi là ĐỦ khi có cả ngày lẫn giờ', () => {
    expect(isCompleteLocalDateTime('2026-09-15T15:30')).toBe(true)
    expect(isCompleteLocalDateTime('2026-09-15T')).toBe(false)
    expect(isCompleteLocalDateTime('T15:30')).toBe(false)
    expect(isCompleteLocalDateTime('')).toBe(false)
  })

  it('tách và ghép giữ nguyên phần dở, cả hai trống thì ra chuỗi rỗng', () => {
    expect(splitLocalDateTime('2026-09-15T15:30')).toEqual({ date: '2026-09-15', time: '15:30' })
    expect(splitLocalDateTime('2026-09-15T')).toEqual({ date: '2026-09-15', time: '' })
    expect(joinLocalDateTime('2026-09-15', '')).toBe('2026-09-15T')
    expect(joinLocalDateTime('', '15:30')).toBe('T15:30')
    expect(joinLocalDateTime('', '')).toBe('')
  })
})
