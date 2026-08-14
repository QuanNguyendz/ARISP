import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

/**
 * Nhận thông báo lỗi mà `OAuthCallbackPage` đẩy kèm khi trả người dùng về màn đăng nhập
 * (đăng nhập Google thất bại kiểu "thử lại bằng tài khoản khác": sai tên miền, chưa được
 * cấp tài khoản, lỗi kỹ thuật). Chuỗi đã được dịch sẵn ở phía callback nên màn đăng nhập
 * chỉ việc hiển thị.
 *
 * Đọc xong thì XOÁ khỏi history state — nếu để nguyên, F5 hoặc bấm Back sẽ dựng lại banner
 * lỗi cũ dù người dùng chưa hề thử đăng nhập lần nữa. Giữ `search` khi replace vì màn đăng
 * nhập ứng viên mang `?returnUrl=` (deep-link xác nhận lịch từ email) — bỏ đi là mất đích quay về.
 */
export function useOAuthRedirectError(): string {
  const location = useLocation()
  const navigate = useNavigate()
  const [message, setMessage] = useState('')

  useEffect(() => {
    const state = location.state as { authError?: string } | null
    if (!state?.authError) return

    setMessage(state.authError)
    navigate(`${location.pathname}${location.search}`, { replace: true, state: null })
  }, [location, navigate])

  return message
}
