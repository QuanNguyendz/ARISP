import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  Brain,
  Mail,
  Lock,
  User,
  Building2,
  Phone,
  ArrowRight,
  Eye,
  EyeOff,
  CheckCircle,
} from 'lucide-react'

export default function RegisterPage() {
  const { t } = useTranslation('auth')
  const { t: tCommon } = useTranslation('common')
  const navigate = useNavigate()
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    password: '',
    confirmPassword: '',
    company: '',
    phone: '',
    acceptTerms: false,
  })
  const [showPassword, setShowPassword] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [step, setStep] = useState(1)

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value, type, checked } = e.target
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }))
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    setTimeout(() => {
      setIsLoading(false)
      navigate('/auth/login')
    }, 2000)
  }

  return (
    <div className="min-h-screen bg-bg-primary flex">
      {/* Left Side - Form */}
      <div className="flex-1 flex items-center justify-center p-8">
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.6 }}
          className="w-full max-w-md"
        >
          {/* Logo */}
          <div className="flex items-center gap-3 mb-12">
            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <Brain className="w-6 h-6 text-white" />
            </div>
            <span className="text-2xl font-semibold text-white">ARISP</span>
          </div>

          {/* Header */}
          <div className="mb-8">
            <h1 className="text-3xl font-semibold text-white mb-3">{t('register.title')}</h1>
            <p className="text-text-secondary">{t('register.subtitle')}</p>
          </div>

          {/* Progress Steps */}
          <div className="flex items-center gap-4 mb-8">
            {[1, 2].map((s) => (
              <div key={s} className="flex items-center gap-2">
                <div
                  className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium transition-colors ${
                    step >= s ? 'bg-accent-primary text-white' : 'bg-white/10 text-text-tertiary'
                  }`}
                >
                  {step > s ? <CheckCircle className="w-4 h-4" /> : s}
                </div>
                <span className={`text-sm ${step >= s ? 'text-white' : 'text-text-tertiary'}`}>
                  {s === 1 ? t('register.step1') : t('register.step2')}
                </span>
                {s < 2 && (
                  <div className={`w-12 h-px ${step > s ? 'bg-accent-primary' : 'bg-white/10'}`} />
                )}
              </div>
            ))}
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-5">
            {step === 1 ? (
              <>
                <div>
                  <label className="block text-sm font-medium text-white/80 mb-2">
                    {t('register.fullNameLabel')}
                  </label>
                  <div className="relative">
                    <User className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                    <input
                      type="text"
                      name="name"
                      value={formData.name}
                      onChange={handleChange}
                      placeholder={t('register.fullNamePlaceholder')}
                      className="w-full pl-12 pr-4 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-white/80 mb-2">
                    {t('register.emailLabel')}
                  </label>
                  <div className="relative">
                    <Mail className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                    <input
                      type="email"
                      name="email"
                      value={formData.email}
                      onChange={handleChange}
                      placeholder={t('register.emailPlaceholder')}
                      className="w-full pl-12 pr-4 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-white/80 mb-2">
                    {t('register.passwordLabel')}
                  </label>
                  <div className="relative">
                    <Lock className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                    <input
                      type={showPassword ? 'text' : 'password'}
                      name="password"
                      value={formData.password}
                      onChange={handleChange}
                      placeholder={t('register.passwordPlaceholder')}
                      className="w-full pl-12 pr-12 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                      required
                      minLength={8}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-4 top-1/2 -translate-y-1/2 text-text-tertiary hover:text-white transition-colors"
                    >
                      {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                    </button>
                  </div>
                </div>

                <motion.button
                  type="button"
                  onClick={() => setStep(2)}
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  className="w-full py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium flex items-center justify-center gap-2"
                >
                  {tCommon('button.next')}
                  <ArrowRight className="w-5 h-5" />
                </motion.button>
              </>
            ) : (
              <>
                <div>
                  <label className="block text-sm font-medium text-white/80 mb-2">
                    {t('register.companyLabel')}
                  </label>
                  <div className="relative">
                    <Building2 className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                    <input
                      type="text"
                      name="company"
                      value={formData.company}
                      onChange={handleChange}
                      placeholder={t('register.companyPlaceholder')}
                      className="w-full pl-12 pr-4 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-white/80 mb-2">
                    {t('register.phoneLabel')}
                  </label>
                  <div className="relative">
                    <Phone className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                    <input
                      type="tel"
                      name="phone"
                      value={formData.phone}
                      onChange={handleChange}
                      placeholder={t('register.phonePlaceholder')}
                      className="w-full pl-12 pr-4 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                      required
                    />
                  </div>
                </div>

                <div className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    name="acceptTerms"
                    id="acceptTerms"
                    checked={formData.acceptTerms}
                    onChange={handleChange}
                    className="w-5 h-5 mt-0.5 rounded border-white/20 bg-white/5 text-accent-primary focus:ring-accent-primary focus:ring-offset-0"
                    required
                  />
                  <label htmlFor="acceptTerms" className="text-sm text-text-secondary">
                    {t('register.agreeTerms', {
                      terms: t('register.terms'),
                      privacy: t('register.privacy'),
                    })}
                  </label>
                </div>

                <div className="flex gap-3">
                  <motion.button
                    type="button"
                    onClick={() => setStep(1)}
                    whileHover={{ scale: 1.02 }}
                    whileTap={{ scale: 0.98 }}
                    className="flex-1 py-4 rounded-xl bg-white/5 border border-white/10 text-white font-medium hover:bg-white/10 transition-colors"
                  >
                    {tCommon('button.back')}
                  </motion.button>
                  <motion.button
                    type="submit"
                    disabled={isLoading}
                    whileHover={{ scale: 1.02 }}
                    whileTap={{ scale: 0.98 }}
                    className="flex-1 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium flex items-center justify-center gap-2 disabled:opacity-50"
                  >
                    {isLoading ? (
                      <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                    ) : (
                      <>
                        {t('register.submit')}
                        <ArrowRight className="w-5 h-5" />
                      </>
                    )}
                  </motion.button>
                </div>
              </>
            )}
          </form>

          {/* Login */}
          <p className="text-center mt-8 text-text-secondary">
            {t('register.haveAccount')}{' '}
            <button
              onClick={() => navigate('/auth/login')}
              className="text-accent-primary hover:text-accent-secondary transition-colors font-medium"
            >
              {t('register.signIn')}
            </button>
          </p>
        </motion.div>
      </div>

      {/* Right Side - Decorative */}
      <div className="hidden lg:flex flex-1 items-center justify-center bg-gradient-to-br from-accent-primary/10 via-bg-secondary to-violet/10 relative overflow-hidden">
        <div className="absolute inset-0">
          <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-accent-primary/20 rounded-full blur-[100px]" />
          <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-violet/20 rounded-full blur-[100px]" />
        </div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3, duration: 0.6 }}
          className="relative text-center max-w-md px-8"
        >
          <h2 className="text-4xl font-semibold text-white mb-6">
            Tham gia cùng{' '}
            <span className="bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
              500+ doanh nghiệp
            </span>
          </h2>
          <p className="text-text-secondary text-lg mb-8">
            Tăng 60% hiệu suất tuyển dụng với AI thông minh
          </p>

          <div className="grid grid-cols-3 gap-4">
            {[
              { value: '2M+', label: 'Phỏng vấn' },
              { value: '94%', label: 'Độ chính xác' },
              { value: '60%', label: 'Tiết kiệm' },
            ].map((stat) => (
              <div
                key={stat.label}
                className="p-4 rounded-2xl bg-white/5 backdrop-blur-xl border border-white/10"
              >
                <div className="text-2xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent mb-1">
                  {stat.value}
                </div>
                <div className="text-xs text-text-secondary">{stat.label}</div>
              </div>
            ))}
          </div>
        </motion.div>
      </div>
    </div>
  )
}
