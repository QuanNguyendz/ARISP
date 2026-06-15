import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  Mail,
  Lock,
  Eye,
  EyeOff,
  ArrowRight,
  Loader2,
  AlertCircle,
  Sparkles,
  Check,
} from 'lucide-react'
import { useAuthStore } from '@store/auth/authStore'
import { authService } from '@services/auth/authService'

// Logo component
function Logo({ size = 'default' }: { size?: 'sm' | 'default' }) {
  const h = size === 'sm' ? 9 : 10
  const w = size === 'sm' ? 9 : 10
  return (
    <svg
      className={`h-${h} w-${w}`}
      viewBox="0 0 96 96"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-label="ARISP"
    >
      <defs>
        <linearGradient
          id="lg-cand-login"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-cand-login)" />
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

export default function CandidateLoginPage() {
  const navigate = useNavigate()
  const { setAuthFromResponse } = useAuthStore()
  const [showPassword, setShowPassword] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setIsLoading(true)

    try {
      const response = await authService.candidateLogin({ email, password })
      const user = setAuthFromResponse(response)

      if (user.role !== 'Candidate') {
        setError(
          'Tài khoản này không phải ứng viên. Vui lòng đăng nhập ở màn dành cho nhà tuyển dụng.'
        )
        return
      }

      navigate('/')
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi. Vui lòng thử lại.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen grid lg:grid-cols-2">
      {/* Left brand panel */}
      <div className="relative hidden lg:flex flex-col justify-between overflow-hidden bg-gradient-to-br from-brand-700 via-brand-600 to-ai-600 p-12 text-white">
        <div className="absolute -top-24 -right-24 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
        <div className="absolute -bottom-32 -left-16 h-96 w-96 rounded-full bg-ai-400/20 blur-3xl" />

        <Link to="/" className="relative flex items-center gap-2.5">
          <svg
            className="h-10 w-10"
            viewBox="0 0 96 96"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-label="ARISP"
          >
            <rect x="4" y="4" width="88" height="88" rx="22" fill="white" fillOpacity="0.15" />
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
            />
          </svg>
          <span className="font-display text-2xl font-extrabold">ARISP</span>
        </Link>

        <div className="relative">
          <h2 className="font-display text-4xl font-extrabold leading-[1.25]">
            Sự nghiệp IT của bạn,
            <br />
            bắt đầu từ đây.
          </h2>
          <p className="mt-4 max-w-md text-white/80">
            Ứng tuyển trực tiếp, được AI phỏng vấn và đánh giá khách quan qua nhiều vòng.
          </p>
          <ul className="mt-8 space-y-3 text-white/90">
            <li className="flex items-center gap-3">
              <Sparkles className="w-5 h-5" />
              Phân tích độ phù hợp CV–JD tức thì
            </li>
            <li className="flex items-center gap-3">
              <svg
                className="w-5 h-5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth="2"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
                />
              </svg>
              Đánh giá minh bạch, không thiên vị
            </li>
            <li className="flex items-center gap-3">
              <svg
                className="w-5 h-5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth="2"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
              Phỏng vấn linh hoạt, có bản luyện tập
            </li>
          </ul>
        </div>

        <div className="relative text-sm text-white/60">© 2026 ARISP</div>
      </div>

      {/* Right form */}
      <div className="flex items-center justify-center p-6 sm:p-12">
        <div className="w-full max-w-sm">
          {/* Mobile logo */}
          <Link to="/" className="lg:hidden mb-8 flex items-center gap-2.5">
            <Logo />
            <span className="font-display text-xl font-extrabold">ARISP</span>
          </Link>

          <h1 className="font-display text-2xl font-extrabold leading-snug">
            Chào mừng trở lại 👋
          </h1>
          <p className="mt-1 text-ink-500">Đăng nhập để tiếp tục ứng tuyển.</p>

          <form onSubmit={handleSubmit} className="mt-8 space-y-4">
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

            <div>
              <div className="mb-1.5 flex items-center justify-between">
                <label className="block text-sm font-medium text-ink-600">Mật khẩu</label>
                <button
                  type="button"
                  onClick={() => navigate('/auth/forgot-password')}
                  className="text-sm font-medium text-brand-600 hover:underline"
                >
                  Quên mật khẩu?
                </button>
              </div>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Lock className="w-4 h-4 text-ink-400" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full bg-transparent text-sm outline-none"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="text-ink-400 hover:text-ink-600"
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {error && (
              <div className="p-3 rounded-xl bg-red-50 border border-red-200 flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />
                <p className="text-sm text-red-600">{error}</p>
              </div>
            )}

            <label className="flex items-center gap-2 text-sm text-ink-600">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
                className="rounded border-ink-300 text-brand-600"
              />
              Ghi nhớ đăng nhập
            </label>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700 transition flex items-center justify-center gap-2 disabled:opacity-50"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  Đăng nhập
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          <div className="my-6 flex items-center gap-3 text-xs text-ink-400">
            <span className="h-px flex-1 bg-ink-200"></span>
            HOẶC
            <span className="h-px flex-1 bg-ink-200"></span>
          </div>

          <button className="w-full rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50 flex items-center justify-center gap-3">
            <svg className="h-5 w-5" viewBox="0 0 48 48">
              <path
                fill="#FFC107"
                d="M43.611 20.083H42V20H24v8h11.303c-1.649 4.657-6.08 8-11.303 8c-6.627 0-12-5.373-12-12s5.373-12 12-12c3.059 0 5.842 1.154 7.961 3.039l5.657-5.657C34.046 6.053 29.268 4 24 4C12.955 4 4 12.955 4 24s8.955 20 20 20s20-8.955 20-20c0-1.341-.138-2.65-.389-3.917z"
              />
              <path
                fill="#FF3D00"
                d="M6.306 14.691l6.571 4.819C14.655 15.108 18.961 12 24 12c3.059 0 5.842 1.154 7.961 3.039l5.657-5.657C34.046 6.053 29.268 4 24 4C16.318 4 9.656 8.337 6.306 14.691z"
              />
              <path
                fill="#4CAF50"
                d="M24 44c5.166 0 9.86-1.977 13.409-5.192l-6.19-5.238C29.211 35.091 26.715 36 24 36c-5.202 0-9.619-3.317-11.283-7.946l-6.522 5.025C9.505 39.556 16.227 44 24 44z"
              />
              <path
                fill="#1976D2"
                d="M43.611 20.083H42V20H24v8h11.303a12.04 12.04 0 0 1-4.087 5.571l.003-.002l6.19 5.238C36.971 39.205 44 34 44 24c0-1.341-.138-2.65-.389-3.917z"
              />
            </svg>
            Đăng nhập với Google
          </button>

          <p className="mt-8 text-center text-sm text-ink-500">
            Chưa có tài khoản?
            <Link
              to="/auth/candidate-register"
              className="font-semibold text-brand-600 hover:underline"
            >
              {' '}
              Đăng ký ngay
            </Link>
          </p>

          <p className="mt-3 text-center text-xs text-ink-400">
            Bạn là HR / Recruiter?
            <Link
              to="/auth/login"
              className="font-medium text-ink-600 hover:text-brand-600 hover:underline"
            >
              {' '}
              Đăng nhập nội bộ
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
