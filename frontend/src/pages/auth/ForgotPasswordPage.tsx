import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Mail, KeyRound, MailCheck, Clock, ArrowLeft, Loader2, AlertCircle } from 'lucide-react'
import { authService } from '@services/auth/authService'

const RESEND_COOLDOWN = 30

function Logo() {
  return (
    <svg
      className="h-12 w-12"
      viewBox="0 0 96 96"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-label="ARISP"
    >
      <defs>
        <linearGradient
          id="lg-forgot"
          x1="12"
          y1="10"
          x2="84"
          y2="86"
          gradientUnits="userSpaceOnUse"
        >
          <stop stopColor="#4f46e5" />
          <stop offset="1" stopColor="#9333ea" />
        </linearGradient>
      </defs>
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-forgot)" />
      <path
        d="M30 70 L48 26 L66 70"
        stroke="white"
        strokeWidth="8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M38 56 H58" stroke="white" strokeWidth="8" strokeLinecap="round" />
      <path
        d="M70 20 C71.4 27 72.5 28.1 79.5 29.5 C72.5 30.9 71.4 32 70 39 C68.6 32 67.5 30.9 60.5 29.5 C67.5 28.1 68.6 27 70 20 Z"
        fill="white"
        fillOpacity="0.95"
      />
    </svg>
  )
}

export default function ForgotPasswordPage() {
  const [searchParams] = useSearchParams()
  // Cùng 1 màn dùng cho 2 cổng tách biệt: candidate (mặc định) vs staff nội bộ
  const isStaff = searchParams.get('audience') === 'staff'
  const loginPath = isStaff ? '/auth/login' : '/auth/candidate-login'

  const [email, setEmail] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [sent, setSent] = useState(false)
  const [cooldown, setCooldown] = useState(0)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    return () => {
      if (timerRef.current) clearInterval(timerRef.current)
    }
  }, [])

  const startCooldown = () => {
    setCooldown(RESEND_COOLDOWN)
    if (timerRef.current) clearInterval(timerRef.current)
    timerRef.current = setInterval(() => {
      setCooldown((s) => {
        if (s <= 1) {
          if (timerRef.current) clearInterval(timerRef.current)
          return 0
        }
        return s - 1
      })
    }, 1000)
  }

  const requestReset = async () => {
    setError('')
    if (!email.trim()) {
      setError('Vui lòng nhập email của bạn.')
      return
    }

    setLoading(true)
    try {
      const payload = { email: email.trim() }
      await (isStaff
        ? authService.staffForgotPassword(payload)
        : authService.forgotPassword(payload))
      setSent(true)
      startCooldown()
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Có lỗi xảy ra khi gửi yêu cầu. Vui lòng thử lại.'
      )
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    void requestReset()
  }

  const handleResend = () => {
    if (cooldown > 0 || loading) return
    void requestReset()
  }

  const useAnotherEmail = () => {
    setSent(false)
    setError('')
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center bg-ink-100 p-6 text-ink-900 antialiased">
      <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-ink-100 to-ai-50" />

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
        className="relative w-full max-w-md"
      >
        {/* Logo */}
        <div className="mb-6 flex flex-col items-center text-center">
          <Logo />
        </div>

        {!sent ? (
          /* ---------- Request state ---------- */
          <div className="rounded-2xl border border-ink-200 bg-white p-7 shadow-xl">
            <div className="mb-5 text-center">
              <span className="mx-auto mb-3 grid h-12 w-12 place-items-center rounded-2xl bg-brand-50 text-brand-600">
                <KeyRound className="w-6 h-6" />
              </span>
              <h1 className="font-display text-xl font-extrabold">Quên mật khẩu?</h1>
              <p className="mt-1.5 text-sm text-ink-500">
                Nhập email tài khoản của bạn. Chúng tôi sẽ gửi liên kết đặt lại mật khẩu.
              </p>
            </div>

            <form className="space-y-4" onSubmit={handleSubmit}>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600">Email</label>
                <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                  <Mail className="w-4 h-4 text-ink-400" />
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="ban@email.com"
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    required
                  />
                </div>
              </div>

              {error && (
                <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
                  <p className="text-sm text-red-600">{error}</p>
                </div>
              )}

              <button
                type="submit"
                disabled={loading}
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-brand-700 disabled:opacity-50"
              >
                {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Gửi liên kết đặt lại'}
              </button>
            </form>

            <div className="mt-5 flex gap-2.5 rounded-xl bg-ink-50 p-3 text-xs text-ink-500">
              <Clock className="mt-0.5 w-4 h-4 shrink-0 text-ink-400" />
              <span>
                Liên kết đặt lại có hiệu lực <b>2 giờ</b> và chỉ dùng được <b>1 lần</b>. Nếu email
                tồn tại trong hệ thống, bạn sẽ nhận được thư.
              </span>
            </div>
          </div>
        ) : (
          /* ---------- Sent state ---------- */
          <div className="rounded-2xl border border-ink-200 bg-white p-7 text-center shadow-xl">
            <span className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-emerald-50 text-emerald-600">
              <MailCheck className="w-7 h-7" />
            </span>
            <h1 className="font-display text-xl font-extrabold">Kiểm tra email của bạn</h1>
            <p className="mt-2 text-sm text-ink-500">
              Chúng tôi đã gửi liên kết đặt lại mật khẩu đến
              <br />
              <b className="text-ink-800">{email}</b>
            </p>

            <div className="mt-5 text-sm text-ink-500">
              Không nhận được thư?{' '}
              {cooldown > 0 ? (
                <span className="text-ink-400">
                  Gửi lại sau <b>{cooldown}s</b>
                </span>
              ) : (
                <button
                  onClick={handleResend}
                  disabled={loading}
                  className="font-semibold text-brand-600 hover:underline disabled:opacity-50"
                >
                  {loading ? 'Đang gửi…' : 'Gửi lại'}
                </button>
              )}
            </div>

            {error && (
              <div className="mt-4 flex items-center justify-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3">
                <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
                <p className="text-sm text-red-600">{error}</p>
              </div>
            )}

            <button
              onClick={useAnotherEmail}
              className="mt-3 text-sm font-medium text-ink-500 hover:text-brand-600"
            >
              Dùng email khác
            </button>
          </div>
        )}

        {/* Back to login */}
        <p className="mt-6 text-center text-sm text-ink-500">
          <Link
            to={loginPath}
            className="inline-flex items-center gap-1.5 font-semibold text-brand-600 hover:underline"
          >
            <ArrowLeft className="w-4 h-4" /> Quay lại đăng nhập
          </Link>
        </p>
      </motion.div>
    </div>
  )
}
