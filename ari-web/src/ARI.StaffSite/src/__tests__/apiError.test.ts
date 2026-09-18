import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ari/shared/i18n/core'
import { resolveApiError } from '@ari/shared/utils/apiError'

// Chọn câu báo lỗi: câu server (tiếng Việt, nói rõ lý do) phải tới được màn hình khi giao diện đang tiếng
// Việt — trước đây câu viết sẵn của màn ("Không thể cập nhật trạng thái tin.") luôn thắng nên người dùng chỉ
// thấy một lỗi 400 không nói gì. Giao diện tiếng Anh giữ nguyên thứ tự cũ.

const FALLBACK = 'Không thể cập nhật trạng thái tin.'
const BANK_MESSAGE = 'Ngân hàng đề của tin mới có 10 câu, chưa đủ 20 câu cho mỗi lượt thi.'

/** `t` giả: câu chung trả `errors:<khoá>` để biết đã đi nhánh nào; khoá của màn trả câu viết sẵn. */
const t = (key: string, options?: Record<string, unknown>) =>
  options?.ns === 'errors' ? `errors:${key}` : key === 'statusError' ? FALLBACK : key

/** Lỗi dạng axios: response thật từ server. */
const httpError = (status: number, data: Record<string, unknown>) => ({
  message: `Request failed with status code ${status}`,
  isAxiosError: true,
  response: { status, data },
})

describe('resolveApiError', () => {
  let original: string

  beforeEach(() => {
    original = i18n.language
    i18n.language = 'vi'
  })

  afterEach(() => {
    i18n.language = original
  })

  it('shows the Vietnamese server message instead of the screen fallback', () => {
    const err = httpError(400, { message: BANK_MESSAGE, code: null })

    expect(resolveApiError(err, t, 'statusError')).toBe(BANK_MESSAGE)
  })

  it('still lets a business code win over the server message', () => {
    const err = httpError(400, { message: BANK_MESSAGE, code: 'online_test_bank_insufficient' })

    expect(resolveApiError(err, t, 'statusError')).toBe('errors:job.onlineTestBankInsufficient')
  })

  it('prefers the server message over a generic code', () => {
    const message = 'Tin này chưa có Hiring Manager phụ trách. HR Admin cần gán Hiring Manager cho tin trước khi đi tiếp.'
    const err = httpError(409, { message, code: 'conflict' })

    expect(resolveApiError(err, t, 'statusError')).toBe(message)
  })

  it('keeps an English leftover server message off the Vietnamese UI', () => {
    const err = httpError(400, { message: 'Job posting not found for this id.' })

    expect(resolveApiError(err, t, 'statusError')).toBe(FALLBACK)
  })

  it('does not surface session messages written for developers', () => {
    const err = httpError(401, { message: 'Không xác định được người dùng. Đăng nhập HR và gửi Bearer token.' })

    expect(resolveApiError(err, t, 'statusError')).toBe(FALLBACK)
    expect(resolveApiError(err, t)).toBe('errors:auth.sessionExpired')
  })

  it('ignores the transport error text of axios', () => {
    const err = { message: 'Network Error', isAxiosError: true, code: 'ERR_NETWORK' } // không có response

    expect(resolveApiError(err, t, 'statusError')).toBe(FALLBACK)
    expect(resolveApiError(err, t)).toBe('errors:server.network')
  })

  it('falls back to the HTTP status when the server message is a bare code', () => {
    const err = httpError(400, { message: 'some_internal_code' })

    expect(resolveApiError(err, t)).toBe('errors:server.badRequest')
  })

  describe('English UI keeps the previous order', () => {
    beforeEach(() => {
      i18n.language = 'en'
    })

    it('uses the screen fallback before the Vietnamese server message', () => {
      const err = httpError(400, { message: BANK_MESSAGE })

      expect(resolveApiError(err, t, 'statusError')).toBe(FALLBACK)
    })

    it('translates a generic code', () => {
      const err = httpError(409, { message: BANK_MESSAGE, code: 'conflict' })

      expect(resolveApiError(err, t, 'statusError')).toBe('errors:server.conflict')
    })
  })
})
