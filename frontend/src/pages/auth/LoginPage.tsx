import { useState, useEffect } from 'react'
import { useNavigate, Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Mail,
  Lock,
  ArrowRight,
  Eye,
  EyeOff,
  Loader2,
  AlertCircle,
  ShieldAlert,
} from 'lucide-react'
import { authService } from '@services/auth/authService'
import { useAuthStore } from '@store/auth/authStore'

// Logo component
function Logo({ size = 'default' }: { size?: 'sm' | 'default' }) {
  const h = size === 'sm' ? 8 : 10
  const w = size === 'sm' ? 8 : 10
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
          id="lg-login"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-login)" />
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

// Get dashboard path based on role
function getRoleDashboard(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_')
  switch (r) {
    case 'super_admin':
      return '/super-admin/dashboard'
    case 'hr_admin':
      return '/hr/dashboard'
    case 'recruiter':
      return '/recruiter/dashboard'
    case 'candidate':
      return '/'
    default:
      return '/hr/dashboard'
  }
}

export default function LoginPage() {
  const { t } = useTranslation('auth')
  const { t: tCommon } = useTranslation('common')
  const { t: tErrors } = useTranslation('errors')
  const navigate = useNavigate()
  const location = useLocation()
  const setAuthFromResponse = useAuthStore((state) => state.setAuthFromResponse)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [oauthLoading, setOauthLoading] = useState(false)
  const [error, setError] = useState('')

  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)
  const userRole = useAuthStore((state) => state.user?.role)

  useEffect(() => {
    if (isAuthenticated && userRole) {
      const from = (location.state as any)?.from?.pathname
      if (from) {
        navigate(from, { replace: true })
      } else {
        navigate(getRoleDashboard(userRole), { replace: true })
      }
    }
  }, [isAuthenticated, userRole, location, navigate])

  const handleOAuthLogin = () => {
    setOauthLoading(true)
    const returnUrl = `${window.location.origin}/auth/callback`
    window.location.href = authService.buildOAuthRedirectUrl('Google', returnUrl)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setIsLoading(true)

    try {
      const response = await authService.staffLogin({ email, password })
      setAuthFromResponse(response)

      const dashboard = getRoleDashboard(response.role || 'Hr_admin')
      const from = (location.state as any)?.from?.pathname
      navigate(from || dashboard, { replace: true })
    } catch (err: any) {
      setError(err.message || tErrors('auth.invalidCredentials'))
      setOauthLoading(false)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased flex items-center justify-center p-6">
      <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-ink-100 to-ai-50" />

      <div className="relative w-full max-w-md">
        {/* Logo */}
        <div className="mb-6 flex flex-col items-center text-center">
          <Logo />
          <h1 className="mt-4 font-display text-2xl font-extrabold">{tCommon('appName')}</h1>
          <p className="mt-1 text-sm text-ink-500">{t('login.subtitle')}</p>
        </div>

        {/* Card */}
        <div className="rounded-2xl border border-ink-200 bg-white p-7 shadow-xl">
          {/* Google OAuth (primary cho nội bộ) */}
          <button
            onClick={handleOAuthLogin}
            disabled={oauthLoading}
            className="w-full rounded-xl border border-ink-200 px-4 py-3 text-sm font-semibold text-ink-700 hover:bg-ink-50 flex items-center justify-center gap-3 disabled:opacity-50"
          >
            {oauthLoading ? (
              <Loader2 className="w-5 h-5 animate-spin" />
            ) : (
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
            )}
            {t('login.signInWithGoogle')}
          </button>

          <div className="my-5 flex items-center gap-3 text-xs text-ink-400">
            <span className="h-px flex-1 bg-ink-200"></span>
            {t('login.orContinueWith')}
            <span className="h-px flex-1 bg-ink-200"></span>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600">
                {t('login.emailLabel')}
              </label>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Mail className="w-4 h-4 text-ink-400" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={t('login.emailPlaceholder')}
                  className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                  required
                />
              </div>
            </div>

            <div>
              <div className="mb-1.5 flex items-center justify-between">
                <label className="block text-sm font-medium text-ink-600">
                  {t('login.passwordLabel')}
                </label>
                <button
                  type="button"
                  onClick={() => navigate('/auth/forgot-password?audience=staff')}
                  className="text-sm font-medium text-brand-600 hover:underline"
                >
                  {t('login.forgotPassword')}
                </button>
              </div>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Lock className="w-4 h-4 text-ink-400" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder={t('login.passwordPlaceholder')}
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

            <button
              type="submit"
              disabled={isLoading}
              className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700 transition flex items-center justify-center gap-2 disabled:opacity-50"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  {t('login.submit')}
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          {/* Pre-provisioning note */}
          <div className="mt-5 flex gap-2.5 rounded-xl bg-ink-50 p-3 text-xs text-ink-500">
            <ShieldAlert className="w-4 h-4 shrink-0 text-ink-400 mt-0.5" />
            <span>{t('login.provisioningNote')}</span>
          </div>
        </div>

        <p className="mt-6 text-center text-sm text-ink-500">
          {t('login.candidatePrompt')}
          <Link to="/auth/candidate-login" className="font-semibold text-brand-600 hover:underline">
            {t('login.jobBoardLink')}
          </Link>
        </p>
      </div>
    </div>
  )
}
