import { useMemo, useState } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  Lock,
  LockKeyhole,
  ShieldCheck,
  LogIn,
  Eye,
  EyeOff,
  Check,
  CheckCircle2,
  XCircle,
  Loader2,
  AlertCircle,
} from 'lucide-react'
import { authService } from '@services/auth/authService'

// Quy tắc mật khẩu mirror theo backend (AuthController.IsValidCandidatePassword)
const SPECIAL_CHARS = '!@#$%^&*'

function scorePassword(pw: string): number {
  let score = 0
  if (pw.length >= 8) score++
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) score++
  if (/[0-9]/.test(pw)) score++
  if (pw.split('').some((c) => SPECIAL_CHARS.includes(c))) score++
  return score
}

function validatePassword(pw: string): string {
  if (pw.length < 8) return 'Mật khẩu phải có ít nhất 8 ký tự.'
  if (!/[A-Z]/.test(pw)) return 'Mật khẩu phải chứa ít nhất một chữ hoa.'
  if (!/[0-9]/.test(pw)) return 'Mật khẩu phải chứa ít nhất một chữ số.'
  if (!pw.split('').some((c) => SPECIAL_CHARS.includes(c)))
    return 'Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.'
  return ''
}

const STRENGTH_LABELS = ['Yếu', 'Trung bình', 'Khá', 'Mạnh']
const STRENGTH_COLORS = ['bg-red-500', 'bg-amber-500', 'bg-amber-500', 'bg-emerald-500']

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
          id="lg-reset"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-reset)" />
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

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams()

  // Token, email & audience được đính kèm trong liên kết email do backend gửi
  const token = searchParams.get('token') || ''
  const email = searchParams.get('email') || ''
  // Cổng staff nội bộ vs candidate (mặc định) — quyết định endpoint & trang đăng nhập đích
  const isStaff = searchParams.get('audience') === 'staff'
  const loginPath = isStaff ? '/auth/login' : '/auth/candidate-login'

  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)

  const invalidLink = !token || !email

  const strength = useMemo(() => scorePassword(password), [password])
  const matches = confirmPassword.length > 0 && password === confirmPassword

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (invalidLink) {
      setError('Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.')
      return
    }

    const pwError = validatePassword(password)
    if (pwError) {
      setError(pwError)
      return
    }

    if (password !== confirmPassword) {
      setError('Mật khẩu xác nhận không trùng khớp.')
      return
    }

    setLoading(true)
    try {
      const payload = { email, token, newPassword: password }
      await (isStaff
        ? authService.staffResetPassword(payload)
        : authService.resetPassword(payload))
      setDone(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Có lỗi xảy ra, vui lòng thử lại sau.')
    } finally {
      setLoading(false)
    }
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

        {!done ? (
          /* ---------- Reset form ---------- */
          <div className="rounded-2xl border border-ink-200 bg-white p-7 shadow-xl">
            <div className="mb-5 text-center">
              <span className="mx-auto mb-3 grid h-12 w-12 place-items-center rounded-2xl bg-brand-50 text-brand-600">
                <LockKeyhole className="w-6 h-6" />
              </span>
              <h1 className="font-display text-xl font-extrabold">Đặt lại mật khẩu</h1>
              <p className="mt-1.5 text-sm text-ink-500">
                Tạo mật khẩu mới cho <b className="text-ink-700">{email || 'tài khoản của bạn'}</b>
              </p>
            </div>

            {invalidLink && (
              <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3">
                <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
                <p className="text-sm text-red-600">
                  Liên kết không hợp lệ hoặc thiếu thông tin. Vui lòng yêu cầu liên kết mới.
                </p>
              </div>
            )}

            <form className="space-y-4" onSubmit={handleSubmit}>
              {/* Mật khẩu mới */}
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600">
                  Mật khẩu mới
                </label>
                <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                  <Lock className="w-4 h-4 text-ink-400" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Tối thiểu 8 ký tự"
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((v) => !v)}
                    className="text-ink-400 hover:text-ink-600"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>

                {/* Strength meter */}
                <div className="mt-2 flex items-center gap-2">
                  <div className="flex flex-1 gap-1">
                    {[0, 1, 2, 3].map((i) => (
                      <span
                        key={i}
                        className={`h-1.5 flex-1 rounded-full ${
                          i < strength ? STRENGTH_COLORS[Math.min(strength - 1, 3)] : 'bg-ink-200'
                        }`}
                      />
                    ))}
                  </div>
                  <span
                    className={`w-16 text-right text-xs font-medium ${
                      strength === 0
                        ? 'text-ink-400'
                        : strength >= 3
                          ? 'text-emerald-600'
                          : strength === 2
                            ? 'text-amber-600'
                            : 'text-red-600'
                    }`}
                  >
                    {strength === 0 ? '—' : STRENGTH_LABELS[Math.min(strength - 1, 3)]}
                  </span>
                </div>
              </div>

              {/* Xác nhận mật khẩu */}
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600">
                  Xác nhận mật khẩu
                </label>
                <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                  <Lock className="w-4 h-4 text-ink-400" />
                  <input
                    type={showConfirmPassword ? 'text' : 'password'}
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="Nhập lại mật khẩu mới"
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    className="text-ink-400 hover:text-ink-600"
                  >
                    {showConfirmPassword ? (
                      <EyeOff className="w-4 h-4" />
                    ) : (
                      <Eye className="w-4 h-4" />
                    )}
                  </button>
                </div>
                {confirmPassword.length > 0 && (
                  <p
                    className={`mt-1.5 flex items-center gap-1 text-xs ${
                      matches ? 'text-emerald-600' : 'text-red-600'
                    }`}
                  >
                    {matches ? (
                      <>
                        <CheckCircle2 className="w-3.5 h-3.5" /> Mật khẩu khớp
                      </>
                    ) : (
                      <>
                        <XCircle className="w-3.5 h-3.5" /> Mật khẩu chưa khớp
                      </>
                    )}
                  </p>
                )}
              </div>

              <ul className="space-y-1 text-xs text-ink-500">
                <li className="flex items-center gap-1.5">
                  <Check className="w-3.5 h-3.5 text-emerald-500" /> Ít nhất 8 ký tự
                </li>
                <li className="flex items-center gap-1.5">
                  <Check className="w-3.5 h-3.5 text-emerald-500" /> Có chữ hoa, chữ số &amp; ký tự
                  đặc biệt (!@#$%^&amp;*)
                </li>
              </ul>

              {error && (
                <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-500" />
                  <p className="text-sm text-red-600">{error}</p>
                </div>
              )}

              <button
                type="submit"
                disabled={loading || invalidLink}
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-brand-700 disabled:opacity-50"
              >
                {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Đặt lại mật khẩu'}
              </button>
            </form>
          </div>
        ) : (
          /* ---------- Done state ---------- */
          <div className="rounded-2xl border border-ink-200 bg-white p-7 text-center shadow-xl">
            <span className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-emerald-50 text-emerald-600">
              <ShieldCheck className="w-7 h-7" />
            </span>
            <h1 className="font-display text-xl font-extrabold">Đổi mật khẩu thành công</h1>
            <p className="mt-2 text-sm text-ink-500">
              Mật khẩu của bạn đã được cập nhật. Bạn có thể đăng nhập bằng mật khẩu mới.
            </p>
            <Link
              to={loginPath}
              className="mt-5 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700"
            >
              <LogIn className="w-4 h-4" /> Đến trang đăng nhập
            </Link>
          </div>
        )}

        {/* Expired note */}
        {!done && (
          <p className="mt-6 text-center text-xs text-ink-400">
            Liên kết hết hạn?{' '}
            <Link
              to={isStaff ? '/auth/forgot-password?audience=staff' : '/auth/forgot-password'}
              className="font-semibold text-brand-600 hover:underline"
            >
              Yêu cầu liên kết mới
            </Link>
          </p>
        )}
      </motion.div>
    </div>
  )
}
