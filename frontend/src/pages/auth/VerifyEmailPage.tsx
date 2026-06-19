import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckCircle2, XCircle, Loader2, Mail, ArrowRight } from 'lucide-react'
import { authService } from '@services/auth/authService'

type VerifyState =
  | { kind: 'loading' }
  | { kind: 'success'; message: string }
  | { kind: 'error'; message: string }

export default function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const email = searchParams.get('email') ?? ''
  const token = searchParams.get('token') ?? ''

  const [state, setState] = useState<VerifyState>({ kind: 'loading' })
  const [resending, setResending] = useState(false)
  const [resendMessage, setResendMessage] = useState('')
  const hasRun = useRef(false)

  useEffect(() => {
    // Tránh gọi 2 lần ở React StrictMode (dev) — token one-time-use sẽ bị đánh dấu đã dùng
    if (hasRun.current) return
    hasRun.current = true

    if (!email || !token) {
      setState({ kind: 'error', message: 'Liên kết xác minh không hợp lệ hoặc thiếu thông tin.' })
      return
    }

    authService
      .verifyEmail(email, token)
      .then((res) => setState({ kind: 'success', message: res.message }))
      .catch((err: any) =>
        setState({ kind: 'error', message: err.message || 'Xác minh thất bại. Vui lòng thử lại.' })
      )
  }, [email, token])

  const handleResend = async () => {
    if (!email) return
    setResending(true)
    setResendMessage('')
    try {
      const res = await authService.resendVerification(email)
      setResendMessage(res.message)
    } catch (err: any) {
      setResendMessage(err.message || 'Không thể gửi lại email. Vui lòng thử lại.')
    } finally {
      setResending(false)
    }
  }

  const isLoading = state.kind === 'loading'
  const isSuccess = state.kind === 'success'

  return (
    <div className="min-h-screen flex items-center justify-center bg-ink-50 px-6">
      <div className="w-full max-w-md rounded-3xl border border-ink-200 bg-white p-8 shadow-xl">
        <div className="flex flex-col items-center text-center">
          <div
            className={`mb-5 flex h-16 w-16 items-center justify-center rounded-full ${
              isLoading
                ? 'bg-brand-50 text-brand-600'
                : isSuccess
                  ? 'bg-emerald-50 text-emerald-600'
                  : 'bg-red-50 text-red-500'
            }`}
          >
            {isLoading ? (
              <Loader2 className="h-8 w-8 animate-spin" />
            ) : isSuccess ? (
              <CheckCircle2 className="h-8 w-8" />
            ) : (
              <XCircle className="h-8 w-8" />
            )}
          </div>

          <h1 className="font-display text-2xl font-extrabold text-ink-900">
            {isLoading
              ? 'Đang xác minh email...'
              : isSuccess
                ? 'Xác minh thành công'
                : 'Không thể xác minh'}
          </h1>

          {!isLoading && (
            <p className="mt-2 text-sm leading-6 text-ink-500">{state.message}</p>
          )}

          {isSuccess && (
            <Link
              to="/auth/candidate-login"
              className="mt-8 flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-brand-700"
            >
              Đăng nhập ngay
              <ArrowRight className="h-4 w-4" />
            </Link>
          )}

          {state.kind === 'error' && (
            <>
              {email && (
                <button
                  type="button"
                  onClick={handleResend}
                  disabled={resending}
                  className="mt-8 flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-brand-700 disabled:opacity-50"
                >
                  {resending ? (
                    <Loader2 className="h-5 w-5 animate-spin" />
                  ) : (
                    <>
                      <Mail className="h-4 w-4" />
                      Gửi lại email xác minh
                    </>
                  )}
                </button>
              )}
              {resendMessage && (
                <p className="mt-3 text-sm text-emerald-600">{resendMessage}</p>
              )}
              <Link
                to="/auth/candidate-login"
                className="mt-4 text-sm font-medium text-ink-500 hover:text-brand-600 hover:underline"
              >
                Quay lại đăng nhập
              </Link>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
