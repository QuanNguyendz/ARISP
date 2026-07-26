import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  KeyRound,
  User,
  FileText,
  CheckCircle,
  Play,
  Briefcase,
  MapPin,
  DollarSign,
  Building2,
  ArrowLeft,
} from 'lucide-react'
import { LoadingButton } from '@ari/shared/ui/common'

const mockInterviewData = {
  code: 'INT001',
  candidate: {
    name: 'Nguyen Van A',
    email: 'an.nguyen@email.com',
    phone: '0912 345 678',
    location: 'Ho Chi Minh City',
  },
  job: {
    title: 'Senior Backend Developer',
    company: 'Tech Solutions Vietnam',
    logo: 'TV',
    location: 'Ho Chi Minh City',
    salary: '$3,000 - $5,000',
    type: 'Full-time',
  },
  cv: {
    uploadedAt: '15/05/2026',
    fileName: 'NguyenVanAn_CV.pdf',
  },
  appliedDate: '18/05/2026',
}

export default function InterviewSchedulePage() {
  const { t } = useTranslation('modules/candidate/interviewSchedule')
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [interviewData, setInterviewData] = useState<typeof mockInterviewData | null>(null)
  const [isConfirmed, setIsConfirmed] = useState(false)
  const [error, setError] = useState('')

  const handleVerifyCode = async () => {
    if (!code.trim()) {
      setError(t('validation.emptyCode'))
      return
    }

    setIsLoading(true)
    setError('')

    setTimeout(() => {
      if (code.toUpperCase() === 'INT001' || code.toUpperCase() === 'TEST') {
        setInterviewData(mockInterviewData)
      } else {
        setError(t('validation.invalidCode'))
      }
      setIsLoading(false)
    }, 1500)
  }

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleVerifyCode()
    }
  }

  const handleConfirm = () => {
    setIsConfirmed(true)
  }

  const handleStartInterview = () => {
    navigate(`/interview/room/${interviewData?.code || 'session'}`)
  }

  const handlePracticeInterview = () => {
    navigate('/interview/practice/demo')
  }

  return (
    <div className="min-h-screen bg-ink-50 py-6 sm:py-8">
      <div className="mx-auto max-w-3xl px-4 sm:px-6">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-6 sm:mb-8">
          <button
            onClick={() => navigate(-1)}
            className="mb-4 flex items-center gap-2 text-sm font-medium text-ink-600 transition-colors hover:text-ink-900"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('back')}
          </button>
          <h1 className="mb-2 text-2xl font-bold text-ink-900 sm:text-3xl">{t('title')}</h1>
          <p className="text-sm text-ink-600 sm:text-base">{t('subtitle')}</p>
        </motion.div>

        {!interviewData ? (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card sm:p-8"
          >
            <div className="mb-6 text-center sm:mb-8">
              <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-accent-primary to-violet sm:mb-6 sm:h-20 sm:w-20">
                <KeyRound className="h-8 w-8 text-white sm:h-10 sm:w-10" />
              </div>
              <h2 className="mb-2 text-xl font-semibold text-ink-900 sm:text-2xl">{t('enterCode.title')}</h2>
              <p className="text-sm text-ink-600 sm:text-base">{t('enterCode.description')}</p>
            </div>

            <div className="space-y-4">
              <div>
                <input
                  type="text"
                  value={code}
                  onChange={(e) => setCode(e.target.value.toUpperCase())}
                  onKeyPress={handleKeyPress}
                  placeholder={t('enterCode.placeholder')}
                  className="w-full rounded-xl border border-ink-200 bg-white px-4 py-3 text-center font-mono text-lg text-ink-900 placeholder:text-ink-400 transition-colors focus:border-accent-primary focus:outline-none sm:px-6 sm:py-4 sm:text-2xl"
                  maxLength={10}
                />
              </div>

              {error && (
                <motion.p
                  initial={{ opacity: 0, y: -10 }}
                  animate={{ opacity: 1, y: 0 }}
                  className="text-center text-sm text-red-600"
                >
                  {error}
                </motion.p>
              )}

              <LoadingButton
                loading={isLoading}
                onClick={handleVerifyCode}
                fullWidth
                className="bg-gradient-to-br from-cyan-500 to-violet-500 text-base text-white"
              >
                {t('enterCode.verify')}
              </LoadingButton>
            </div>

            <div className="mt-6 border-t border-ink-200 pt-6 text-center">
              <p className="text-sm text-ink-500">
                {t('enterCode.noCode')}{' '}
                <a href="/jobs" className="text-accent-primary hover:underline">
                  {t('enterCode.findJob')}
                </a>
              </p>
            </div>
          </motion.div>
        ) : !isConfirmed ? (
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
            <div className="mb-4 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
              <div className="mb-4 flex items-start gap-3 sm:items-center sm:gap-4">
                <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-accent-primary to-violet text-base font-bold text-white sm:h-14 sm:w-14 sm:text-lg">
                  {interviewData.job.logo}
                </div>
                <div className="min-w-0">
                  <h2 className="truncate text-lg font-semibold text-ink-900 sm:text-xl">{interviewData.job.title}</h2>
                  <p className="truncate text-sm text-ink-600">{interviewData.job.company}</p>
                </div>
              </div>
              <div className="flex flex-wrap gap-3 text-xs text-ink-600 sm:gap-4 sm:text-sm">
                <span className="flex items-center gap-1">
                  <MapPin className="h-4 w-4" />
                  {interviewData.job.location}
                </span>
                <span className="flex items-center gap-1">
                  <DollarSign className="h-4 w-4" />
                  {interviewData.job.salary}
                </span>
                <span className="flex items-center gap-1">
                  <Building2 className="h-4 w-4" />
                  {interviewData.job.type}
                </span>
              </div>
            </div>

            <div className="mb-4 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
              <div className="mb-4 flex items-center gap-2">
                <User className="h-5 w-5 text-accent-primary" />
                <h3 className="text-base font-semibold text-ink-900 sm:text-lg">{t('candidateInfo.title')}</h3>
              </div>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="min-w-0">
                  <p className="mb-1 text-xs text-ink-500 sm:text-sm">{t('candidateInfo.fullName')}</p>
                  <p className="truncate text-sm text-ink-900 sm:text-base">{interviewData.candidate.name}</p>
                </div>
                <div className="min-w-0">
                  <p className="mb-1 text-xs text-ink-500 sm:text-sm">{t('candidateInfo.email')}</p>
                  <p className="truncate text-sm text-ink-900 sm:text-base">{interviewData.candidate.email}</p>
                </div>
                <div className="min-w-0">
                  <p className="mb-1 text-xs text-ink-500 sm:text-sm">{t('candidateInfo.phone')}</p>
                  <p className="truncate text-sm text-ink-900 sm:text-base">{interviewData.candidate.phone}</p>
                </div>
                <div className="min-w-0">
                  <p className="mb-1 text-xs text-ink-500 sm:text-sm">{t('candidateInfo.location')}</p>
                  <p className="truncate text-sm text-ink-900 sm:text-base">{interviewData.candidate.location}</p>
                </div>
              </div>
            </div>

            <div className="mb-6 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
              <div className="mb-4 flex items-center gap-2">
                <FileText className="h-5 w-5 text-accent-primary" />
                <h3 className="text-base font-semibold text-ink-900 sm:text-lg">{t('cvInfo.title')}</h3>
              </div>
              <div className="flex items-center justify-between rounded-xl bg-ink-50 p-3 sm:p-4">
                <div className="flex min-w-0 items-center gap-3">
                  <FileText className="h-7 w-7 shrink-0 text-ink-500 sm:h-8 sm:w-8" />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-ink-900 sm:text-base">{interviewData.cv.fileName}</p>
                    <p className="truncate text-xs text-ink-500 sm:text-sm">
                      {t('cvInfo.uploadedAt')} {interviewData.cv.uploadedAt}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-brand-200 bg-brand-50 p-4 sm:p-6">
              <div className="mb-4 flex items-start gap-3 sm:mb-6">
                <CheckCircle className="mt-0.5 h-5 w-5 shrink-0 text-brand-600 sm:h-6 sm:w-6" />
                <div>
                  <h3 className="mb-1 text-base font-semibold text-ink-900 sm:text-lg">
                    {t('confirmInfo.title')}
                  </h3>
                  <p className="text-xs text-ink-600 sm:text-sm">{t('confirmInfo.description')}</p>
                </div>
              </div>

              <button
                onClick={handleConfirm}
                className="w-full rounded-xl bg-gradient-to-r from-accent-primary to-violet py-3.5 font-semibold text-white transition-opacity hover:opacity-90 sm:py-4"
              >
                {t('confirmInfo.button')}
              </button>
            </div>
          </motion.div>
        ) : (
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="text-center"
          >
            <div className="mb-6 sm:mb-8">
              <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-50 sm:mb-6 sm:h-20 sm:w-20">
                <CheckCircle className="h-8 w-8 text-emerald-600 sm:h-10 sm:w-10" />
              </div>
              <h2 className="mb-2 text-xl font-bold text-ink-900 sm:text-2xl">{t('success.title')}</h2>
              <p className="text-sm text-ink-600 sm:text-base">{t('success.description')}</p>
            </div>

            <div className="mb-6 rounded-2xl border border-ink-200 bg-white p-4 text-left shadow-card sm:mb-8 sm:p-6">
              <div className="mb-4 flex items-center gap-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-accent-primary to-violet font-bold text-white">
                  {interviewData.job.logo}
                </div>
                <div className="min-w-0">
                  <p className="truncate font-semibold text-ink-900">{interviewData.job.title}</p>
                  <p className="truncate text-xs text-ink-600 sm:text-sm">{interviewData.job.company}</p>
                </div>
              </div>
              <div className="flex items-center gap-2 text-xs text-emerald-700 sm:text-sm">
                <CheckCircle className="h-4 w-4" />
                <span>{t('success.infoConfirmed')}</span>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:gap-4">
              <button
                onClick={handleStartInterview}
                className="group rounded-2xl bg-gradient-to-br from-accent-primary to-violet p-5 text-left transition-opacity hover:opacity-90 sm:p-6"
              >
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 transition-transform group-hover:scale-110 sm:mb-4 sm:h-12 sm:w-12">
                  <Play className="h-5 w-5 text-white sm:h-6 sm:w-6" />
                </div>
                <h3 className="mb-1 text-base font-semibold text-white sm:text-lg">
                  {t('interviewType.startInterview')}
                </h3>
                <p className="text-xs text-white/80 sm:text-sm">{t('interviewType.startInterviewDesc')}</p>
              </button>

              <button
                onClick={handlePracticeInterview}
                className="group rounded-2xl border border-ink-200 bg-white p-5 text-left transition-colors hover:border-ink-300 sm:p-6"
              >
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-ink-100 transition-colors group-hover:bg-ink-200 sm:mb-4 sm:h-12 sm:w-12">
                  <Briefcase className="h-5 w-5 text-ink-700 sm:h-6 sm:w-6" />
                </div>
                <h3 className="mb-1 text-base font-semibold text-ink-900 sm:text-lg">
                  {t('interviewType.practiceInterview')}
                </h3>
                <p className="text-xs text-ink-600 sm:text-sm">
                  {t('interviewType.practiceInterviewDesc')}
                </p>
              </button>
            </div>

            <div className="mt-6 rounded-xl border border-amber-200 bg-amber-50 p-3 sm:mt-8 sm:p-4">
              <p className="text-center text-xs text-amber-800 sm:text-sm">
                <strong>{t('note.title')}</strong> {t('note.description')}
              </p>
            </div>
          </motion.div>
        )}
      </div>
    </div>
  )
}
