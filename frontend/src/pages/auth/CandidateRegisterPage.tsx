import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Mail,
  Lock,
  Eye,
  EyeOff,
  User,
  ArrowRight,
  Loader2,
  AlertCircle,
  UploadCloud,
  Target,
  MessageSquare,
} from 'lucide-react'
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
          id="lg-cand-reg"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-cand-reg)" />
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

export default function CandidateRegisterPage() {
  const { t } = useTranslation('auth')
  const { t: tCommon } = useTranslation('common')
  const navigate = useNavigate()
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [agreeTerms, setAgreeTerms] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const passwordRequirements = [
    { label: t('candidateRegister.passwordReqMin'), met: password.length >= 8 },
    { label: t('candidateRegister.passwordReqUppercase'), met: /[A-Z]/.test(password) },
    { label: t('candidateRegister.passwordReqNumber'), met: /[0-9]/.test(password) },
    { label: t('candidateRegister.passwordReqSpecial'), met: /[!@#$%^&*]/.test(password) },
  ]

  const metCount = passwordRequirements.filter((r) => r.met).length
  const allRequirementsMet = passwordRequirements.every((r) => r.met)
  const passwordsMatch = password === confirmPassword && confirmPassword.length > 0

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!allRequirementsMet) {
      setError(t('candidateRegister.passwordTooWeak'))
      return
    }
    if (!passwordsMatch) {
      setError(t('candidateRegister.passwordMismatch'))
      return
    }
    if (!agreeTerms) {
      setError(t('candidateRegister.termsRequired'))
      return
    }

    setIsLoading(true)
    try {
      await authService.candidateRegister({ email, password, fullName })
      navigate(`/auth/candidate-login?verify=sent&email=${encodeURIComponent(email)}`)
    } catch (err: any) {
      setError(err.message || t('candidateRegister.registerFailed'))
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
            {t('candidateRegister.heroTitle1')}
            <br />
            {t('candidateRegister.heroTitle2')}
          </h2>
          <p className="mt-4 max-w-md text-white/80">{t('candidateRegister.heroSubtitle')}</p>
          <ul className="mt-8 space-y-3 text-white/90">
            <li className="flex items-center gap-3">
              <UploadCloud className="w-5 h-5" />
              {t('candidateRegister.feature1')}
            </li>
            <li className="flex items-center gap-3">
              <Target className="w-5 h-5" />
              {t('candidateRegister.feature2')}
            </li>
            <li className="flex items-center gap-3">
              <MessageSquare className="w-5 h-5" />
              {t('candidateRegister.feature3')}
            </li>
          </ul>
        </div>

        <div className="relative text-sm text-white/60">
          {tCommon('footer.copyright', { year: new Date().getFullYear() })}
        </div>
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
            {t('candidateRegister.title')}
          </h1>
          <p className="mt-1 text-ink-500">{t('candidateRegister.subtitle')}</p>

          <form onSubmit={handleSubmit} className="mt-8 space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600">
                {t('candidateRegister.fullNameLabel')}
              </label>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <User className="w-4 h-4 text-ink-400" />
                <input
                  type="text"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  placeholder={t('candidateRegister.fullNamePlaceholder')}
                  className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                  required
                />
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600">
                {t('candidateRegister.emailLabel')}
              </label>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Mail className="w-4 h-4 text-ink-400" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={t('candidateRegister.emailPlaceholder')}
                  className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                  required
                />
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600">
                {t('candidateRegister.passwordLabel')}
              </label>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Lock className="w-4 h-4 text-ink-400" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder={t('candidateRegister.passwordPlaceholder')}
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

              {/* Password strength */}
              <div className="mt-2 flex gap-1">
                <span
                  className={`h-1 flex-1 rounded-full ${metCount >= 1 ? 'bg-emerald-500' : 'bg-ink-200'}`}
                ></span>
                <span
                  className={`h-1 flex-1 rounded-full ${metCount >= 2 ? 'bg-emerald-500' : 'bg-ink-200'}`}
                ></span>
                <span
                  className={`h-1 flex-1 rounded-full ${metCount >= 3 ? 'bg-emerald-500' : 'bg-ink-200'}`}
                ></span>
                <span
                  className={`h-1 flex-1 rounded-full ${metCount >= 4 ? 'bg-emerald-500' : 'bg-ink-200'}`}
                ></span>
              </div>
              <p className="mt-1 text-xs text-ink-400">{t('candidateRegister.passwordHint')}</p>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600">
                {t('candidateRegister.confirmPasswordLabel')}
              </label>
              <div className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100">
                <Lock className="w-4 h-4 text-ink-400" />
                <input
                  type={showConfirmPassword ? 'text' : 'password'}
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder={t('candidateRegister.confirmPasswordPlaceholder')}
                  className="w-full bg-transparent text-sm outline-none"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  className="text-ink-400 hover:text-ink-600"
                >
                  {showConfirmPassword ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
              {confirmPassword.length > 0 && !passwordsMatch && (
                <p className="mt-1 text-xs text-red-500">
                  {t('candidateRegister.passwordMismatch')}
                </p>
              )}
            </div>

            <label className="flex items-start gap-2 text-sm text-ink-600">
              <input
                type="checkbox"
                checked={agreeTerms}
                onChange={(e) => setAgreeTerms(e.target.checked)}
                className="mt-0.5 rounded border-ink-300 text-brand-600"
              />
              <span>
                {t('candidateRegister.agreeTerms', {
                  terms: t('candidateRegister.terms'),
                  privacy: t('candidateRegister.privacy'),
                })}
              </span>
            </label>

            {error && (
              <div className="p-3 rounded-xl bg-red-50 border border-red-200 flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />
                <p className="text-sm text-red-600">{error}</p>
              </div>
            )}

            <button
              type="submit"
              disabled={isLoading || !allRequirementsMet || !passwordsMatch || !agreeTerms}
              className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700 transition flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  {t('candidateRegister.submit')}
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          <p className="mt-8 text-center text-sm text-ink-500">
            {t('candidateRegister.haveAccount')}
            <Link
              to="/auth/candidate-login"
              className="font-semibold text-brand-600 hover:underline"
            >
              {t('candidateRegister.signIn')}
            </Link>
          </p>

          <p className="mt-3 text-center text-xs text-ink-400">
            {t('candidateRegister.isRecruiter')}
            <Link
              to="/auth/login"
              className="font-medium text-ink-600 hover:text-brand-600 hover:underline"
            >
              {t('candidateRegister.staffLoginLink')}
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
