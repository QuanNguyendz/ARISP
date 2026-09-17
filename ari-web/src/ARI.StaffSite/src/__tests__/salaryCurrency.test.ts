import { describe, expect, it } from 'vitest'
import {
  DEFAULT_SALARY_CURRENCY,
  SALARY_CURRENCIES,
  normalizeSalaryCurrency,
} from '@ari/shared/utils/jobOptions'

/**
 * Đơn vị tiền dùng chung (`@ari/shared/utils/jobOptions`). Dữ liệu lập khi các ô này còn gõ tự do
 * phải hiện ra thành một lựa chọn hợp lệ — nếu không, danh sách chọn trống và lượt lưu bị server chặn.
 */
describe('SALARY_CURRENCIES', () => {
  it('chỉ có VND và USD', () => {
    expect(SALARY_CURRENCIES.map((o) => o.value)).toEqual(['VND', 'USD'])
  })
})

describe('normalizeSalaryCurrency', () => {
  it.each([
    ['USD', 'USD'],
    [' usd ', 'USD'],
    ['vnd', 'VND'],
    ['VND', 'VND'],
  ])('%j → %s', (input, expected) => {
    expect(normalizeSalaryCurrency(input)).toBe(expected)
  })

  it.each([null, undefined, '', 'EUR', 'VNĐ'])('%j → mặc định', (input) => {
    expect(normalizeSalaryCurrency(input)).toBe(DEFAULT_SALARY_CURRENCY)
  })
})
