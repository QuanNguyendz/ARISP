import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeft,
  FileText,
  UploadCloud,
  Loader2,
  CheckCircle2,
  Mail,
  Phone,
  User,
  MapPin,
  AlertCircle,
  Sparkles,
  X,
  Briefcase,
} from 'lucide-react'
import { useAuthStore } from '@ari/shared/store/auth'
import { jobService } from '@ari/shared/fservices/job'
import { profileService } from '@ari/shared/fservices/profile/profileService'
import type { CandidateProfile } from '@ari/shared/fservices/profile/profileService'
import { applicationService } from '@ari/shared/fservices/application'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import type { JobPosting } from '@ari/shared/types/job'

/** Nhãn bắt buộc — dấu * đỏ. */
function Req() {
  return <span className="text-red-500"> *</span>
}

const MAX_CV_MB = 10
const ACCEPTED = ['.pdf', '.docx']

export default function ApplyPage() {
  const { t } = useTranslation('modules/job-board/apply')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuthStore()

  const [job, setJob] = useState<JobPosting | null>(null)
  const [profile, setProfile] = useState<CandidateProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  // Form state
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [coverLetter, setCoverLetter] = useState('')
  const [noticePeriod, setNoticePeriod] = useState('')
  // CV: 'profile' = dùng CV hồ sơ; 'upload' = nộp CV khác cho tin này.
  const [cvSource, setCvSource] = useState<'profile' | 'upload'>('profile')
  const [cvFile, setCvFile] = useState<File | null>(null)

  // AI Verification state
  const [verifyingInfo, setVerifyingInfo] = useState(false)
  const [verificationResult, setVerificationResult] = useState<{
    isMatch: boolean
    mismatchDetails: string | null
  } | null>(null)
  const [verificationError, setVerificationError] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)

  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  // Validate theo từng trường: chỉ hiện lỗi khi trường đã blur (rời focus) hoặc sau khi bấm gửi.
  // Đang gõ thì xoá trạng thái touched của trường đó → không nhắc lỗi liên tục mỗi ký tự.
  const [touchedFields, setTouchedFields] = useState<Set<string>>(new Set())
  const [showCancel, setShowCancel] = useState(false)
  const [showVerificationModal, setShowVerificationModal] = useState(false)
  const [showPendingVerificationModal, setShowPendingVerificationModal] = useState(false)

  const markTouched = (key: string) =>
    setTouchedFields((prev) => (prev.has(key) ? prev : new Set(prev).add(key)))
  const clearTouched = (key: string) =>
    setTouchedFields((prev) => {
      if (!prev.has(key)) return prev
      const next = new Set(prev)
      next.delete(key)
      return next
    })

  // Chưa đăng nhập → về trang đăng nhập ứng viên.
  useEffect(() => {
    if (!isAuthenticated) navigate('/auth/candidate-login')
  }, [isAuthenticated, navigate])

  useEffect(() => {
    if (!id) return
    let active = true
    setLoading(true)
    Promise.all([jobService.getJobPostingById(id), profileService.getProfile().catch(() => null)])
      .then(([j, p]) => {
        if (!active) return
        setJob(j)
        setProfile(p)
        if (p) {
          setFullName(p.fullName || '')
          setPhone(p.phone || '')
        }
        // Không có CV hồ sơ → mặc định bắt buộc tải lên.
        if (!p?.profileCvUrl) setCvSource('upload')
      })
      .catch(() => active && setLoadError(t('loading.error')))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [id, t])

  const hasProfileCv = !!profile?.profileCvUrl

  // AI CV-Form Contact Verification
  useEffect(() => {
    const hasCv = (cvSource === 'upload' && !!cvFile) || (cvSource === 'profile' && hasProfileCv)
    if (!fullName.trim() || !phone.trim() || !hasCv) {
      setVerificationResult(null)
      return
    }

    const delayDebounceFn = setTimeout(async () => {
      setVerifyingInfo(true)
      setVerificationResult(null)
      setVerificationError('')
      try {
        const res = await applicationService.verifyCvContactInfo({
          candidateName: fullName.trim(),
          candidatePhone: phone.trim(),
          cvFile: cvSource === 'upload' ? cvFile : null,
        })
        setVerificationResult(res)
      } catch (err: any) {
        console.error('Lỗi khi đối chiếu CV:', err)
        setVerificationError(t('aiVerification.error'))
      } finally {
        setVerifyingInfo(false)
      }
    }, 1000)

    return () => clearTimeout(delayDebounceFn)
  }, [fullName, phone, cvSource, cvFile, hasProfileCv, t])

  const onPickFile = (f: File | null) => {
    setSubmitError('')
    if (!f) {
      setCvFile(null)
      return
    }
    const ext = '.' + (f.name.split('.').pop() || '').toLowerCase()
    if (!ACCEPTED.includes(ext)) {
      setSubmitError(t('cv.errors.invalidFormat'))
      return
    }
    if (f.size > MAX_CV_MB * 1024 * 1024) {
      setSubmitError(t('cv.errors.tooLarge', { max: MAX_CV_MB }))
      return
    }
    setCvFile(f)
  }

  // Lỗi từng trường (chỉ hiện sau khi bấm gửi hoặc blur).
  const errors = useMemo(() => {
    const e: Record<string, string> = {}
    if (!fullName.trim()) e.fullName = t('contact.errors.fullNameRequired')
    if (!phone.trim()) e.phone = t('contact.errors.phoneRequired')
    else if (phone.length < 8 || phone.length > 15) e.phone = t('contact.errors.phoneInvalid')
    if (!noticePeriod.trim()) e.noticePeriod = t('coverLetter.errors.noticePeriodRequired')
    if (cvSource === 'upload' && !cvFile) e.cv = t('cv.errors.required')
    if (cvSource === 'profile' && !hasProfileCv) e.cv = t('cv.errors.noProfileCv')
    return e
  }, [fullName, phone, noticePeriod, cvSource, cvFile, hasProfileCv, t])

  const executeSubmit = async () => {
    setSubmitting(true)
    setSubmitError('')
    try {
      await applicationService.applyToJob(id!, {
        candidateName: fullName.trim(),
        candidatePhone: phone.trim(),
        coverLetter: coverLetter.trim(),
        noticePeriod: noticePeriod.trim(),
        cvFile: cvSource === 'upload' ? cvFile : null,
      })
      navigate('/candidate/applications')
    } catch (err: unknown) {
      const e = err as { response?: { status?: number; data?: { message?: string } } }
      if (e?.response?.status === 409) {
        navigate('/candidate/applications')
        return
      }
      if (e?.response?.status === 401 || e?.response?.status === 403) {
        navigate('/auth/candidate-login')
        return
      }
      setSubmitError(e?.response?.data?.message || t('submit.submitError'))
    } finally {
      setSubmitting(false)
    }
  }

  const handleSubmit = async () => {
    // Đánh dấu tất cả trường là "đã chạm" để hiện lỗi (nếu có) khi bấm gửi.
    setTouchedFields(new Set(['fullName', 'phone', 'noticePeriod', 'cv']))
    setSubmitError('')
    if (Object.keys(errors).length > 0) return
    if (!id) return

    // 1. Nếu AI đang trong quá trình phân tích
    if (verifyingInfo) {
      setShowPendingVerificationModal(true)
      return
    }

    // 2. Nếu AI đã phân tích và phát hiện lệch thông tin liên hệ
    if (verificationResult && !verificationResult.isMatch) {
      setShowVerificationModal(true)
      return
    }

    // 3. Nếu thông tin khớp hoặc không có cảnh báo
    await executeSubmit()
  }

  const goBack = () => navigate(`/jobs/${id}`)

  // Có dữ liệu đã nhập → hỏi xác nhận trước khi rời đi.
  const dirty =
    !!coverLetter.trim() ||
    !!noticePeriod.trim() ||
    !!cvFile ||
    fullName !== (profile?.fullName || '') ||
    phone !== (profile?.phone || '')

  const requestCancel = () => {
    if (dirty) setShowCancel(true)
    else goBack()
  }

  const inputCls = (hasErr: boolean) =>
    `w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition focus:ring-2 ${
      hasErr
        ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
        : 'border-ink-200 focus:border-brand-500 focus:ring-brand-100'
    }`

  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-50">
        <Loader2 className="h-10 w-10 animate-spin text-brand-600" />
      </div>
    )
  }

  if (loadError || !job) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-50 px-6 text-center">
        <div>
          <p className="text-ink-600">{loadError || t('loading.notFound')}</p>
          <Link
            to="/jobs"
            className="mt-4 inline-block rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
          >
            {t('loading.backToJobs')}
          </Link>
        </div>
      </div>
    )
  }

  const showErr = (key: string) => touchedFields.has(key) && errors[key]

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased">
      {/* Top bar */}
      <header className="sticky top-0 z-30 border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-3xl items-center gap-3 px-4 sm:px-6">
          <button
            onClick={requestCancel}
            className="inline-flex items-center gap-1.5 rounded-lg px-2 py-1.5 text-sm font-medium text-ink-600 hover:bg-ink-100"
          >
            <ArrowLeft className="h-4 w-4" /> {t('header.back')}
          </button>
          <span className="ml-auto font-display text-sm font-bold text-ink-700">
            {t('header.title')}
          </span>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
        {/* Job summary */}
        <div className="mb-6 rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
          <div className="text-xs font-semibold uppercase tracking-wide text-ink-400">
            {t('jobSummary.label')}
          </div>
          <h1 className="mt-1 font-display text-xl font-extrabold">{job.title}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-500">
            <span className="inline-flex items-center gap-1.5">
              <Briefcase className="h-4 w-4" /> {job.department || t('jobSummary.department')}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <MapPin className="h-4 w-4" /> {job.location || t('jobSummary.location')}
            </span>
          </div>
        </div>

        <div className="space-y-6">
          {/* CV */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">
              {t('cv.title')}
              <Req />
            </h2>
            <p className="mt-0.5 text-sm text-ink-500">{t('cv.helpText')}</p>

            <div className="mt-4 space-y-2">
              {/* Dùng CV hồ sơ */}
              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition ${
                  cvSource === 'profile'
                    ? 'border-brand-300 bg-brand-50/60'
                    : 'border-ink-200 hover:border-brand-200'
                } ${!hasProfileCv ? 'cursor-not-allowed opacity-60' : ''}`}
              >
                <input
                  type="radio"
                  name="cvSource"
                  className="text-brand-600"
                  checked={cvSource === 'profile'}
                  disabled={!hasProfileCv}
                  onChange={() => setCvSource('profile')}
                />
                <FileText className="h-5 w-5 shrink-0 text-brand-600" />
                <span className="min-w-0 flex-1">
                  {hasProfileCv ? (
                    <>
                      <span className="block truncate text-sm font-medium text-ink-800">
                        {profile?.cvFileName || t('cv.profile.fallbackName')}
                      </span>
                      <span className="text-xs text-ink-400">{t('cv.profile.label')}</span>
                    </>
                  ) : (
                    <span className="text-sm text-ink-500">{t('cv.profile.noCv')}</span>
                  )}
                </span>
                {hasProfileCv && profile?.profileCvUrl && (
                  <a
                    href={resolveAssetUrl(profile.profileCvUrl)}
                    target="_blank"
                    rel="noopener noreferrer"
                    onClick={(e) => e.stopPropagation()}
                    className="shrink-0 text-xs font-medium text-brand-600 hover:underline"
                  >
                    {t('cv.profile.view')}
                  </a>
                )}
              </label>

              {/* Tải CV khác */}
              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition ${
                  cvSource === 'upload'
                    ? 'border-brand-300 bg-brand-50/60'
                    : 'border-ink-200 hover:border-brand-200'
                }`}
              >
                <input
                  type="radio"
                  name="cvSource"
                  className="text-brand-600"
                  checked={cvSource === 'upload'}
                  onChange={() => setCvSource('upload')}
                />
                <UploadCloud className="h-5 w-5 shrink-0 text-ai-600" />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium text-ink-800">
                    {t('cv.upload.label')}
                  </span>
                  <span className="text-xs text-ink-400">
                    {t('cv.upload.hint', { max: MAX_CV_MB })}
                  </span>
                </span>
              </label>

              {cvSource === 'upload' && (
                <div className="rounded-xl border border-dashed border-ink-300 p-3">
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".pdf,.docx"
                    className="hidden"
                    onChange={(e) => onPickFile(e.target.files?.[0] ?? null)}
                  />
                  {cvFile ? (
                    <div className="flex items-center gap-3">
                      <FileText className="h-5 w-5 shrink-0 text-ai-600" />
                      <span className="min-w-0 flex-1 truncate text-sm text-ink-700">
                        {cvFile.name}
                      </span>
                      <button
                        onClick={() => onPickFile(null)}
                        className="shrink-0 rounded-lg p-1.5 text-ink-400 hover:bg-ink-100 hover:text-red-600"
                        aria-label={t('cv.upload.removeFile')}
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => fileRef.current?.click()}
                      className="flex w-full items-center justify-center gap-2 rounded-lg bg-ink-50 px-3 py-2 text-sm font-medium text-ink-600 hover:bg-ink-100"
                    >
                      <UploadCloud className="h-4 w-4" /> {t('cv.upload.chooseFile')}
                    </button>
                  )}
                </div>
              )}
            </div>
            {showErr('cv') && <p className="mt-2 text-xs text-red-600">{errors.cv}</p>}
          </section>

          {/* Thông tin liên hệ */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">{t('contact.title')}</h2>
            <div className="mt-4 grid gap-4 grid-cols-1 sm:grid-cols-2">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-700">
                  {t('contact.fullName')}
                  <Req />
                </label>
                <div className="relative">
                  <User className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                  <input
                    value={fullName}
                    onChange={(e) => {
                      setFullName(e.target.value)
                      clearTouched('fullName')
                    }}
                    onBlur={() => markTouched('fullName')}
                    placeholder={t('contact.fullNamePlaceholder')}
                    className={inputCls(!!showErr('fullName')) + ' pl-9'}
                  />
                </div>
                {showErr('fullName') && (
                  <p className="mt-1 text-xs text-red-600">{errors.fullName}</p>
                )}
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-700">
                  {t('contact.phone')}
                  <Req />
                </label>
                <div className="relative">
                  <Phone className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                  <input
                    value={phone}
                    inputMode="numeric"
                    onChange={(e) => {
                      // Chỉ cho nhập chữ số. Validate ngay (live) → hiện lỗi tới khi hợp lệ.
                      setPhone(e.target.value.replace(/\D/g, ''))
                      markTouched('phone')
                    }}
                    onBlur={() => markTouched('phone')}
                    placeholder={t('contact.phonePlaceholder')}
                    className={inputCls(!!showErr('phone')) + ' pl-9'}
                  />
                </div>
                {showErr('phone') && <p className="mt-1 text-xs text-red-600">{errors.phone}</p>}
              </div>
            </div>

            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                {t('contact.email')}
              </label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                <input
                  value={profile?.email || ''}
                  readOnly
                  className="w-full cursor-not-allowed rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 pl-9 text-sm text-ink-500 outline-none"
                />
              </div>
            </div>

            {/* AI Contact Verification Warning/Status */}
            {(verifyingInfo || verificationResult || verificationError) && (
              <div className="mt-4 rounded-xl border p-4 transition-all duration-300 bg-blue-50/10 border-blue-200">
                <div className="flex items-start gap-3">
                  <span
                    className={`grid h-8 w-8 shrink-0 place-items-center rounded-full ${
                      verifyingInfo
                        ? 'bg-blue-50 text-blue-600'
                        : verificationError
                          ? 'bg-red-50 text-red-600'
                          : verificationResult?.isMatch
                            ? 'bg-green-50 text-green-600'
                            : 'bg-amber-50 text-amber-600'
                    }`}
                  >
                    {verifyingInfo ? (
                      <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
                    ) : (
                      <Sparkles className="h-4 w-4" />
                    )}
                  </span>
                  <div className="flex-1">
                    <h4 className="font-display text-sm font-bold text-ink-900 flex items-center gap-1.5">
                      {t('aiVerification.title')}
                      {!verifyingInfo && verificationResult?.isMatch && (
                        <span className="inline-flex items-center rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700 ring-1 ring-inset ring-green-600/20">
                          {t('aiVerification.matchBadge')}
                        </span>
                      )}
                      {!verifyingInfo && verificationResult && !verificationResult.isMatch && (
                        <span className="inline-flex items-center rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700 ring-1 ring-inset ring-amber-600/20">
                          {t('aiVerification.mismatchBadge')}
                        </span>
                      )}
                    </h4>

                    {verifyingInfo && (
                      <p className="mt-1 text-xs text-ink-500 animate-pulse">
                        {t('aiVerification.verifying')}
                      </p>
                    )}

                    {verificationError && (
                      <p className="mt-1 text-xs text-red-600">{verificationError}</p>
                    )}

                    {!verifyingInfo && verificationResult && (
                      <div className="mt-1.5">
                        {verificationResult.isMatch ? (
                          <p className="text-xs text-green-600 font-medium">
                            {t('aiVerification.matchSuccess')}
                          </p>
                        ) : (
                          <div className="space-y-1">
                            <p className="text-xs text-amber-600 font-medium">
                              {t('aiVerification.mismatchTitle')}
                            </p>
                            <p className="text-xs text-ink-600 bg-amber-50/50 p-2 rounded-lg border border-amber-100 italic font-mono">
                              {verificationResult.mismatchDetails}
                            </p>
                            <p className="text-[11px] text-ink-400 mt-1">
                              {t('aiVerification.mismatchNote')}
                            </p>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            )}
          </section>

          {/* Thư giới thiệu / câu trả lời */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">{t('coverLetter.title')}</h2>
            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                {t('coverLetter.question1')}{' '}
                <span className="text-xs font-normal text-ink-400">
                  {t('coverLetter.optional')}
                </span>
              </label>
              <textarea
                value={coverLetter}
                onChange={(e) => setCoverLetter(e.target.value)}
                rows={5}
                placeholder={t('coverLetter.question1Placeholder')}
                className={inputCls(false)}
              />
            </div>
            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                {t('coverLetter.question2')}
                <Req />
              </label>
              <input
                value={noticePeriod}
                onChange={(e) => {
                  setNoticePeriod(e.target.value)
                  clearTouched('noticePeriod')
                }}
                onBlur={() => markTouched('noticePeriod')}
                placeholder={t('coverLetter.question2Placeholder')}
                className={inputCls(!!showErr('noticePeriod'))}
              />
              {showErr('noticePeriod') && (
                <p className="mt-1 text-xs text-red-600">{errors.noticePeriod}</p>
              )}
            </div>
          </section>

          {submitError && (
            <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              <AlertCircle className="h-4 w-4 shrink-0" /> {submitError}
            </div>
          )}

          {/* Actions */}
          <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">
            <button
              onClick={requestCancel}
              className="inline-flex items-center justify-center gap-2 rounded-xl border border-ink-200 bg-white px-5 py-3 text-sm font-semibold text-ink-700 hover:bg-ink-50"
            >
              <ArrowLeft className="h-4 w-4" /> {t('submit.backToJob')}
            </button>
            <button
              onClick={handleSubmit}
              disabled={submitting}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-6 py-3 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-60"
            >
              {submitting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" /> {t('submit.submitting')}
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-4 w-4" /> {t('submit.submitApplication')}
                </>
              )}
            </button>
          </div>

          <p className="flex items-center justify-center gap-1.5 pb-6 text-center text-xs text-ink-400">
            <Sparkles className="h-3.5 w-3.5" /> {t('submit.footerHint')}
          </p>
        </div>
      </main>

      {/* Popup xác nhận hủy */}
      {showCancel && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4">
          <div className="w-full max-w-sm rounded-2xl border border-ink-200 bg-white p-6 shadow-xl">
            <div className="flex items-start gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-amber-50 text-amber-600">
                <AlertCircle className="h-5 w-5" />
              </span>
              <div>
                <h3 className="font-display text-base font-bold text-ink-900">
                  {t('cancelModal.title')}
                </h3>
                <p className="mt-1 text-sm text-ink-500">{t('cancelModal.description')}</p>
              </div>
            </div>
            <div className="mt-5 flex gap-3">
              <button
                onClick={() => setShowCancel(false)}
                className="flex-1 rounded-xl border border-ink-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                {t('cancelModal.continue')}
              </button>
              <button
                onClick={goBack}
                className="flex-1 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
              >
                {t('cancelModal.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Popup xác nhận khi AI đang phân tích */}
      {showPendingVerificationModal && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl border border-ink-200 bg-white p-6 shadow-xl">
            <div className="flex items-start gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-blue-50 text-blue-600">
                <Loader2 className="h-5 w-5 animate-spin" />
              </span>
              <div>
                <h3 className="font-display text-base font-bold text-ink-900 flex items-center gap-1.5">
                  {t('aiVerification.pendingModal.title')}
                </h3>
                <p className="mt-2 text-sm text-ink-600 leading-relaxed">
                  {t('aiVerification.pendingModal.description')}
                </p>
                <p className="mt-1.5 text-xs text-ink-400 italic">
                  {t('aiVerification.pendingModal.hint')}
                </p>
              </div>
            </div>
            <div className="mt-5 flex gap-3">
              <button
                onClick={() => setShowPendingVerificationModal(false)}
                className="flex-1 rounded-xl border border-ink-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                {t('aiVerification.pendingModal.continueWaiting')}
              </button>
              <button
                onClick={async () => {
                  setShowPendingVerificationModal(false)
                  await executeSubmit()
                }}
                className="flex-1 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
              >
                {t('aiVerification.pendingModal.skipAndSubmit')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Popup cảnh báo sai lệch thông tin */}
      {showVerificationModal && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl border border-red-200 bg-white p-6 shadow-xl">
            <div className="flex items-start gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-amber-50 text-amber-600">
                <AlertCircle className="h-5 w-5" />
              </span>
              <div>
                <h3 className="font-display text-base font-bold text-ink-900">
                  {t('aiVerification.mismatchModal.title')}
                </h3>
                <p className="mt-2 text-sm text-ink-600 leading-relaxed">
                  {t('aiVerification.mismatchModal.description')}
                </p>
                <div className="mt-2 text-xs text-ink-700 bg-amber-50/50 p-3 rounded-lg border border-amber-100 italic font-mono whitespace-pre-line leading-relaxed">
                  {verificationResult?.mismatchDetails}
                </div>
                <p className="mt-3 text-xs text-ink-500">
                  {t('aiVerification.mismatchModal.hint')}
                </p>
              </div>
            </div>
            <div className="mt-5 flex gap-3">
              <button
                onClick={() => setShowVerificationModal(false)}
                className="flex-1 rounded-xl border border-ink-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                {t('aiVerification.mismatchModal.edit')}
              </button>
              <button
                onClick={async () => {
                  setShowVerificationModal(false)
                  await executeSubmit()
                }}
                className="flex-1 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
              >
                {t('aiVerification.mismatchModal.submitAnyway')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
