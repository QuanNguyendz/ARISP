import { useState, useEffect, useMemo } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  MapPin,
  Clock,
  Loader2,
  Languages,
  Briefcase,
  Sparkles,
  Users,
  Code2,
  Server,
  BrainCircuit,
  CheckCircle2,
  Bookmark,
  ChevronRight,
  FileText,
  Calendar,
} from 'lucide-react'
import jobService from '@ari/shared/fservices/job'
import { savedJobService } from '@/fservices/job/savedJobService'
import { profileService } from '@ari/shared/fservices/profile/profileService'
import type { CvMatchResult } from '@ari/shared/fservices/profile/profileService'
import { applicationService } from '@ari/shared/fservices/application'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import type { JobPosting } from '@ari/shared/types/job'
import { useAuthStore } from '@ari/shared/store/auth'
import CandidateHeader from '@/app/layouts/CandidateHeader'

// ============== HELPER FUNCTIONS ==============
type TFunction = (key: string, options?: Record<string, unknown>) => string

function formatSalary(t: TFunction, job: JobPosting): string {
  if (
    job.salaryIsNegotiable ||
    (job.salaryMin == null && job.salaryMax == null) ||
    (job.salaryMin === 0 && job.salaryMax === 0)
  ) {
    return t('jobs.salaryNegotiable')
  }

  const cur = (job.salaryCurrency || 'VND').toUpperCase()

  const formatVal = (n: number) => {
    if (cur === 'VND') {
      return n.toLocaleString('vi-VN')
    }
    return n.toLocaleString('en-US')
  }

  const unit = cur === 'VND' ? ' ₫' : ` ${cur}`

  if (
    job.salaryMin != null &&
    job.salaryMax != null &&
    job.salaryMin !== 0 &&
    job.salaryMax !== 0
  ) {
    return `${formatVal(job.salaryMin)} - ${formatVal(job.salaryMax)}${unit}`
  }

  if (job.salaryMin != null && job.salaryMin !== 0) {
    return t('jobs.salaryFrom', { value: formatVal(job.salaryMin) + unit })
  }

  if (job.salaryMax != null && job.salaryMax !== 0) {
    return t('jobs.salaryTo', { value: formatVal(job.salaryMax) + unit })
  }

  return t('jobs.salaryNegotiable')
}

function formatWorkMode(t: TFunction, mode?: string): string {
  if (!mode) return t('jobs.workModeFulltime')
  const mappings: Record<string, string> = {
    fulltime: t('jobs.workModeFulltime'),
    parttime: t('jobs.workModeParttime'),
    contract: t('jobs.workModeContract'),
    internship: t('jobs.workModeInternship'),
  }
  return mappings[mode.toLowerCase()] || mode
}

function formatPostedDate(t: TFunction, dateStr?: string): string {
  if (!dateStr) return t('jobs.recentlyPosted')
  try {
    const diff = Math.abs(new Date().getTime() - new Date(dateStr).getTime())
    const days = Math.ceil(diff / (1000 * 60 * 60 * 24))
    if (days <= 1) return t('jobs.recentlyPosted')
    if (days <= 2) return t('jobs.yesterday')
    return t('jobs.daysAgo', { count: days })
  } catch {
    return t('jobs.nearby')
  }
}

function getDeadlineText(t: TFunction, deadlineStr?: string | null): string {
  if (!deadlineStr) return ''
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return ''
  const formattedDate = d.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return t('jobs.expired', { date: formattedDate })
  if (diffDays === 0) return t('jobs.expireToday', { date: formattedDate })
  return t('jobs.expireSoon', { date: formattedDate, days: diffDays })
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml')) {
    return <BrainCircuit className="w-8 h-8" />
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return <Server className="w-8 h-8" />
  }
  return <Code2 className="w-8 h-8" />
}

function getIconBg(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) {
    return 'bg-ai-50 text-ai-600'
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return 'bg-emerald-50 text-emerald-600'
  }
  return 'bg-brand-50 text-brand-600'
}

/**
 * Tách phần `summary` của Gemini (định dạng "🌟 Điểm sáng: …\n⚠️ Điểm cần lưu ý: …")
 * thành 2 khối riêng để hiển thị dễ nhìn. Phần không khớp marker được gom vào `rest`.
 */
function parseMatchSummary(summary?: string): {
  strengths?: string
  gaps?: string
  rest?: string
} {
  if (!summary) return {}
  const result: { strengths?: string; gaps?: string; rest: string[] } = { rest: [] }
  // Tách theo emoji marker dù có xuống dòng hay không.
  const parts = summary
    .replace(/⚠️/g, '\n⚠️')
    .replace(/🌟/g, '\n🌟')
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean)
  for (const p of parts) {
    if (p.startsWith('🌟')) {
      result.strengths = p.replace(/^🌟\s*(Điểm sáng\s*:?\s*)?/u, '').trim()
    } else if (p.startsWith('⚠️')) {
      result.gaps = p.replace(/^⚠️\s*(Điểm cần lưu ý\s*:?\s*)?/u, '').trim()
    } else {
      result.rest.push(p)
    }
  }
  return {
    strengths: result.strengths,
    gaps: result.gaps,
    rest: result.rest.length ? result.rest.join(' ') : undefined,
  }
}

export default function JobDetailPage() {
  const { t } = useTranslation('landing')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const { isAuthenticated } = useAuthStore()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Phân tích độ phù hợp CV–JD theo CV trong hồ sơ ứng viên đang đăng nhập.
  const [match, setMatch] = useState<CvMatchResult | null>(null)
  const [matchLoading, setMatchLoading] = useState(false)
  const [matchAuthError, setMatchAuthError] = useState(false)

  // Lưu / bỏ lưu việc làm (bookmark)
  const [isSaved, setIsSaved] = useState(false)
  const [savePending, setSavePending] = useState(false)

  // Ứng tuyển — đã nộp hồ sơ cho tin này chưa (để đổi nút sang "Xem hồ sơ").
  // Dùng chung query key với trang danh sách (['my-applications']) → điều hướng từ danh
  // sang chi tiết đọc thẳng cache, nút hiện đúng "Đã ứng tuyển" ngay, không nháy trạng thái.
  const { data: myApplications } = useQuery({
    queryKey: ['my-applications'],
    queryFn: () => applicationService.getMyApplications(),
    enabled: isAuthenticated && !!id,
    staleTime: 1000 * 60,
    refetchOnWindowFocus: false,
  })
  const appliedId = useMemo(() => {
    if (!id) return null
    const found = (myApplications ?? []).find(
      (a) => a.jobPostingId === id && a.status !== 'withdrawn'
    )
    return found?.id ?? null
  }, [myApplications, id])
  // Đã biết chắc trạng thái ứng tuyển chưa? (chưa đăng nhập = không cần chờ; đã đăng nhập
  // thì đợi query trả về lần đầu). Trước khi biết, KHÔNG render nút "Ứng tuyển ngay" để khỏi nháy.
  const appliedResolved = !isAuthenticated || myApplications !== undefined

  useEffect(() => {
    async function loadJobDetail() {
      if (!id) return
      try {
        setLoading(true)
        const data = await jobService.getJobPostingById(id)
        setJob(data)
      } catch (err) {
        console.error(err)
        setError(t('jobDetail.error.message'))
      } finally {
        setLoading(false)
      }
    }

    loadJobDetail()
  }, [id])

  useEffect(() => {
    if (!id || !isAuthenticated) return
    let cancelled = false
    let attempts = 0
    const MAX_ATTEMPTS = 40 // ~100s ở mức 2.5s/lần — đủ cho Gemini (~25s)
    const POLL_MS = 2500
    setMatchLoading(true)
    setMatchAuthError(false)

    const poll = async () => {
      try {
        const data = await profileService.getCvMatch(id)
        if (cancelled) return
        setMatch(data)
        // Backend phân tích ở nền — poll tiếp tới khi xong (kết quả được cache).
        if (data.status === 'processing' && attempts < MAX_ATTEMPTS) {
          attempts += 1
          window.setTimeout(poll, POLL_MS)
          return
        }
        setMatchLoading(false)
      } catch (err: unknown) {
        if (cancelled) return
        const status = (err as { response?: { status?: number } })?.response?.status
        if (status === 401 || status === 403) {
          setMatchAuthError(true)
          setMatchLoading(false)
        } else if (attempts < MAX_ATTEMPTS) {
          // Lỗi mạng tạm thời → thử lại (kết quả vẫn đang được tính & cache ở backend).
          attempts += 1
          window.setTimeout(poll, POLL_MS)
        } else {
          console.error('CV match failed:', err)
          setMatchLoading(false)
        }
      }
    }

    poll()
    return () => {
      cancelled = true
    }
  }, [id, isAuthenticated])

  // Trạng thái đã lưu của tin này (chỉ với ứng viên đã đăng nhập).
  useEffect(() => {
    if (!id || !isAuthenticated) {
      setIsSaved(false)
      return
    }
    let cancelled = false
    savedJobService
      .getSavedJobIds()
      .then((ids) => {
        if (!cancelled) setIsSaved(ids.includes(id))
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [id, isAuthenticated])

  // Mở màn ứng tuyển. Chưa đăng nhập → trang đăng nhập; đã ứng tuyển → xem hồ sơ.
  const handleApply = () => {
    if (!id) return
    if (!isAuthenticated) {
      navigate('/auth/candidate-login')
      return
    }
    if (appliedId) {
      navigate('/candidate/applications')
      return
    }
    navigate(`/jobs/${id}/apply`)
  }

  // Lưu / bỏ lưu — cập nhật lạc quan, rollback nếu lỗi. Chưa đăng nhập → tới trang đăng nhập.
  const handleToggleSave = async () => {
    if (!id) return
    if (!isAuthenticated) {
      navigate('/auth/candidate-login')
      return
    }
    const wasSaved = isSaved
    setIsSaved(!wasSaved)
    setSavePending(true)
    try {
      if (wasSaved) await savedJobService.unsaveJob(id)
      else await savedJobService.saveJob(id)
    } catch {
      setIsSaved(wasSaved)
    } finally {
      setSavePending(false)
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-ink-50">
        <CandidateHeader />
        <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3">
          <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
          <p className="text-sm text-ink-500">{t('jobDetail.loading')}</p>
        </div>
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="min-h-screen bg-ink-50">
        <CandidateHeader />
        <div className="py-20 text-center">
          <h2 className="mb-2 text-2xl font-display font-bold text-ink-900">
            {t('jobDetail.error.title')}
          </h2>
          <p className="mb-6 text-ink-500">{error || t('jobDetail.error.noData')}</p>
          <button
            type="button"
            onClick={() => navigate('/jobs')}
            className="inline-flex items-center gap-2 rounded-xl border border-ink-200 bg-white px-6 py-3 font-medium text-ink-700 hover:bg-ink-50"
          >
            <ChevronRight className="w-4 h-4 rotate-180" />
            {t('jobDetail.browseOther')}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased">
      <CandidateHeader />

      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-4 sm:px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            {t('jobDetail.breadcrumb')}
          </Link>
          <ChevronRight className="w-4 h-4" />
          <span className="text-ink-600 font-medium truncate max-w-[180px] sm:max-w-none">{job.title}</span>
        </div>
      </div>

      <main className="mx-auto max-w-6xl px-4 sm:px-6 py-6 grid gap-8 lg:grid-cols-[1fr_360px]">
        {/* Left: content */}
        <div className="space-y-6">
          {/* Header card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-4 sm:p-6 shadow-card">
            <div className="flex items-start gap-3 sm:gap-4">
              <div
                className={`grid h-12 w-12 shrink-0 place-items-center rounded-2xl sm:h-16 sm:w-16 ${getIconBg(job.department)}`}
              >
                {getJobIcon(job.department)}
              </div>
              <div className="min-w-0 flex-1">
                <h1 className="font-display text-xl sm:text-2xl font-extrabold leading-snug break-words">{job.title}</h1>
                <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-500">
                  <span className="inline-flex items-center gap-1.5">
                    <Users className="w-4 h-4 shrink-0" />{' '}
                    <span className="truncate">{job.department || t('jobDetail.header.department')}</span>
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="w-4 h-4 shrink-0" /> <span className="truncate">{job.location || t('jobs.noLocation')}</span>
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Clock className="w-4 h-4 shrink-0" />{' '}
                    {t('jobDetail.header.posted', { date: formatPostedDate(t, job.createdAt) })}
                  </span>
                  {job.applicationDeadline && (
                    <span className="inline-flex items-center gap-1.5 text-amber-600 font-medium">
                      <Calendar className="w-4 h-4 shrink-0" />{' '}
                      {t('jobDetail.header.deadline', {
                        text: getDeadlineText(t, job.applicationDeadline),
                      })}
                    </span>
                  )}
                </div>
                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600">
                    <Briefcase className="w-3.5 h-3.5" />{' '}
                    {formatWorkMode(t, job.workMode || job.employmentType)}
                  </span>
                  <span className="inline-flex items-center rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600 whitespace-nowrap">
                    {formatSalary(t, job)}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600 capitalize">
                    <Sparkles className="w-3.5 h-3.5" />{' '}
                    {job.experienceLevel || t('jobDetail.tags.experienceNotRequired')}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-brand-50 px-2.5 py-1 text-brand-700">
                    <Languages className="w-3.5 h-3.5" />{' '}
                    {job.languageRequirement || t('jobDetail.tags.languageDefault')}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card space-y-6">
            <section>
              {/<\/?[a-z][\s\S]*>/i.test(job.jobDescription || '') ? (
                <div
                  className="text-ink-600 leading-relaxed ql-editor-display"
                  dangerouslySetInnerHTML={{ __html: job.jobDescription }}
                />
              ) : (
                <p className="text-ink-600 leading-relaxed whitespace-pre-line">
                  {job.jobDescription}
                </p>
              )}
            </section>

            {job.skills && job.skills.length > 0 && (
              <section>
                <h2 className="font-display text-lg font-bold mb-3">
                  {t('jobDetail.sections.skills')}
                </h2>
                <div className="flex flex-wrap gap-2">
                  {job.skills.map((skill, i) => (
                    <span
                      key={i}
                      className="px-3 py-1.5 rounded-xl border border-ink-200 bg-ink-50 text-ink-700 text-sm font-medium"
                    >
                      {skill}
                    </span>
                  ))}
                </div>
              </section>
            )}
          </div>

          {/* CV-JD Match - mobile inline (desktop uses sticky aside version) */}
          <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-4 shadow-card sm:p-6 lg:hidden">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="w-4 h-4" /> {t('jobDetail.match.title')}
            </div>

            {!isAuthenticated || matchAuthError ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">{t('jobDetail.match.loginRequired')}</p>
                <Link
                  to="/auth/candidate-login"
                  className="mt-3 block w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-center text-sm font-semibold text-white hover:opacity-90"
                >
                  {t('jobDetail.match.login')}
                </Link>
              </div>
            ) : matchLoading ? (
              <div className="mt-4 flex items-start gap-2 text-sm text-ink-500">
                <Loader2 className="w-4 h-4 mt-0.5 shrink-0 animate-spin text-ai-600" />
                <span>{t('jobDetail.match.analyzing')}</span>
              </div>
            ) : match && !match.hasCv ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">{t('jobDetail.match.noCv')}</p>
                <Link
                  to="/candidate/profile?focus=cv"
                  className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
                >
                  <FileText className="w-4 h-4" /> {t('jobDetail.match.uploadCv')}
                </Link>
              </div>
            ) : match && match.hasCv ? (
              <div>
                {/* Bản mobile của khối CV–JD. Cùng hành vi với bản desktop bên dưới: mở
                    trình xem trong trang, KHÔNG nhảy tab. */}
                <button
                  type="button"
                  onClick={() =>
                    openDocument(resolveAssetUrl(match.cvUrl), match.cvFileName || undefined)
                  }
                  title={t('jobDetail.match.viewCv')}
                  className="mt-3 flex w-full items-center gap-2 rounded-xl border border-ai-200 bg-white px-3 py-2 text-left text-sm text-ink-700 transition hover:border-ai-300 hover:bg-ai-50/40"
                >
                  <FileText className="w-4 h-4 shrink-0 text-ai-600" />
                  <span className="flex-1 truncate">{match.cvFileName}</span>
                  <span className="shrink-0 text-xs font-medium text-ai-700 underline decoration-ai-300 underline-offset-2">
                    {t('jobDetail.match.viewCv')}
                  </span>
                </button>

                {match.analysis ? (
                  <>
                    <div className="mt-4 flex items-end gap-2">
                      <div className="font-display text-4xl font-extrabold text-ai-700 leading-none">
                        {match.analysis.matchScore}
                      </div>
                      <div className="pb-1 text-sm text-ink-500">
                        {t('jobDetail.match.scoreLabel')}
                      </div>
                    </div>
                    <div className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-ai-100">
                      <div
                        className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600"
                        style={{ width: `${match.analysis.matchScore}%` }}
                      />
                    </div>
                    <div className="mt-2 flex items-center gap-1 text-xs text-ink-400">
                      <Sparkles className="h-3 w-3" />{' '}
                      {t('jobDetail.match.aiAnalysis', {
                        name: match.analysis.reviewedBy ?? 'Gemini',
                      })}
                    </div>
                    {match.analysis.skillsMatched.length > 0 && (
                      <div className="mt-4">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">
                          {t('jobDetail.match.skillsMatched')}
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsMatched.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {match.analysis.skillsGaps.length > 0 && (
                      <div className="mt-3">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">
                          {t('jobDetail.match.skillsGaps')}
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsGaps.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 text-xs font-medium text-amber-700"
                            >
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {match.analysis.summary && (
                      <div className="mt-4 rounded-xl border border-ink-200 bg-white p-3 text-sm leading-relaxed text-ink-600">
                        {parseMatchSummary(match.analysis.summary).strengths && (
                          <div className="mb-2">
                            <span className="font-semibold text-emerald-700">
                              {t('jobDetail.match.strengthsLabel')}
                            </span>{' '}
                            {parseMatchSummary(match.analysis.summary).strengths}
                          </div>
                        )}
                        {parseMatchSummary(match.analysis.summary).gaps && (
                          <div>
                            <span className="font-semibold text-amber-700">
                              {t('jobDetail.match.gapsLabel')}
                            </span>{' '}
                            {parseMatchSummary(match.analysis.summary).gaps}
                          </div>
                        )}
                      </div>
                    )}
                  </>
                ) : (
                  <p className="mt-3 text-sm text-ink-500">{t('jobDetail.match.noAnalysis')}</p>
                )}
              </div>
            ) : (
              <p className="mt-4 text-sm text-ink-500">{t('jobDetail.match.loadError')}</p>
            )}
          </div>
        </div>

        {/* Right: sticky apply + match (desktop only - mobile uses bottom fixed bar) */}
        <aside className="hidden space-y-5 lg:sticky lg:top-24 lg:block self-start">
          {/* Apply card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            {!appliedResolved ? (
              <div className="flex w-full items-center justify-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-4 py-3 text-sm font-bold text-ink-400">
                <Loader2 className="h-4 w-4 animate-spin" />
              </div>
            ) : appliedId ? (
              <button
                onClick={() => navigate('/candidate/applications')}
                className="flex w-full items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-bold text-emerald-700 hover:bg-emerald-100"
              >
                <CheckCircle2 className="h-4 w-4" /> {t('jobDetail.apply.applied')}
              </button>
            ) : (
              <button
                onClick={handleApply}
                className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700"
              >
                {t('jobDetail.apply.applyNow')}
              </button>
            )}
            <button
              onClick={handleToggleSave}
              disabled={savePending}
              className={`mt-2 w-full rounded-xl border px-4 py-3 text-sm font-semibold flex items-center justify-center gap-2 disabled:opacity-60 ${
                isSaved
                  ? 'border-ai-200 bg-ai-50 text-ai-700 hover:bg-ai-100'
                  : 'border-ink-200 text-ink-700 hover:bg-ink-50'
              }`}
            >
              <Bookmark className={`w-4 h-4 ${isSaved ? 'fill-current' : ''}`} />
              {isSaved ? t('jobDetail.apply.saved') : t('jobDetail.apply.saveJob')}
            </button>
          </div>

          {/* CV-JD Match (signature AI) — dùng CV thật trong hồ sơ ứng viên */}
          <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-6 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="w-4 h-4" /> {t('jobDetail.match.title')}
            </div>

            {!isAuthenticated || matchAuthError ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">{t('jobDetail.match.loginRequired')}</p>
                <Link
                  to="/auth/candidate-login"
                  className="mt-3 block w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-center text-sm font-semibold text-white hover:opacity-90"
                >
                  {t('jobDetail.match.login')}
                </Link>
              </div>
            ) : matchLoading ? (
              <div className="mt-4 flex items-start gap-2 text-sm text-ink-500">
                <Loader2 className="w-4 h-4 mt-0.5 shrink-0 animate-spin text-ai-600" />
                <span>{t('jobDetail.match.analyzing')}</span>
              </div>
            ) : match && !match.hasCv ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">{t('jobDetail.match.noCv')}</p>
                <Link
                  to="/candidate/profile?focus=cv"
                  className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
                >
                  <FileText className="w-4 h-4" /> {t('jobDetail.match.uploadCv')}
                </Link>
              </div>
            ) : match && match.hasCv ? (
              <div>
                {/* CV đã có sẵn — bấm để xem NGAY TRONG TRANG. Mở tab mới sẽ ném người dùng
                    sang trình xem PDF của trình duyệt, mất luôn ngữ cảnh tin tuyển dụng đang
                    đọc; `DocumentViewer` còn xử lý được cả DOCX (tab ngoài thì tải file về). */}
                <button
                  type="button"
                  onClick={() =>
                    openDocument(resolveAssetUrl(match.cvUrl), match.cvFileName || undefined)
                  }
                  title={t('jobDetail.match.viewCv')}
                  className="mt-3 flex w-full items-center gap-2 rounded-xl border border-ai-200 bg-white px-3 py-2 text-left text-sm text-ink-700 transition hover:border-ai-300 hover:bg-ai-50/40"
                >
                  <FileText className="w-4 h-4 shrink-0 text-ai-600" />
                  <span className="flex-1 truncate">{match.cvFileName}</span>
                  <span className="shrink-0 text-xs font-medium text-ai-700 underline decoration-ai-300 underline-offset-2">
                    {t('jobDetail.match.viewCv')}
                  </span>
                </button>

                {match.analysis ? (
                  <>
                    <div className="mt-4 flex items-end gap-2">
                      <div className="font-display text-4xl sm:text-5xl font-extrabold text-ai-700 leading-none">
                        {match.analysis.matchScore}
                      </div>
                      <div className="pb-1 text-sm text-ink-500">
                        {t('jobDetail.match.scoreLabel')}
                      </div>
                    </div>
                    <div className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-ai-100">
                      <div
                        className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600"
                        style={{ width: `${match.analysis.matchScore}%` }}
                      />
                    </div>
                    <div className="mt-2 flex items-center gap-1 text-xs text-ink-400">
                      <Sparkles className="h-3 w-3" />{' '}
                      {t('jobDetail.match.aiAnalysis', {
                        name: match.analysis.reviewedBy ?? 'Gemini',
                      })}
                    </div>
                    {/* Kỹ năng khớp / còn thiếu — dạng chip cho dễ quét */}
                    {match.analysis.skillsMatched.length > 0 && (
                      <div className="mt-4">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">
                          {t('jobDetail.match.skillsMatched')}
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsMatched.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {match.analysis.skillsGaps.length > 0 && (
                      <div className="mt-3">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">
                          {t('jobDetail.match.skillsMissing')}
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsGaps.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 text-xs font-medium text-amber-700"
                            >
                              <span className="shrink-0">⚠️</span>
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Tóm tắt: tách Điểm sáng / Điểm cần lưu ý thành 2 khối */}
                    {(() => {
                      const sum = parseMatchSummary(match.analysis.summary)
                      return (
                        <div className="mt-4 space-y-2.5">
                          {sum.strengths && (
                            <div className="rounded-xl bg-emerald-50 p-3">
                              <div className="flex items-center gap-1.5 text-xs font-semibold text-emerald-700">
                                <CheckCircle2 className="h-4 w-4" />{' '}
                                {t('jobDetail.match.strengths')}
                              </div>
                              <p className="mt-1 text-sm text-ink-600">{sum.strengths}</p>
                            </div>
                          )}
                          {sum.gaps && (
                            <div className="rounded-xl bg-amber-50 p-3">
                              <div className="flex items-center gap-1.5 text-xs font-semibold text-amber-700">
                                <span>⚠️</span> {t('jobDetail.match.considerations')}
                              </div>
                              <p className="mt-1 text-sm text-ink-600">{sum.gaps}</p>
                            </div>
                          )}
                          {sum.rest && <p className="text-sm text-ink-500">{sum.rest}</p>}
                        </div>
                      )
                    })()}

                    <p className="mt-3 text-xs text-ink-400">{t('jobDetail.match.geminiNote')}</p>
                    <Link
                      to="/candidate/profile?focus=cv"
                      className="mt-3 block w-full rounded-xl border border-ai-300 bg-white px-3 py-2 text-center text-sm font-semibold text-ai-700 hover:bg-ai-50"
                    >
                      {t('jobDetail.match.updateCv')}
                    </Link>
                  </>
                ) : (
                  <p className="mt-4 text-sm text-ink-500">
                    {match.message || t('jobDetail.match.cannotAnalyze')}
                  </p>
                )}
              </div>
            ) : (
              <p className="mt-4 text-sm text-ink-500">{t('jobDetail.match.loadError')}</p>
            )}
          </div>
        </aside>
      </main>

      {/* Bottom fixed Apply bar — mobile only (<lg). Desktop uses sticky aside. */}
      <div className="sticky bottom-0 z-30 border-t border-ink-200 bg-white/95 p-3 shadow-[0_-4px_12px_rgba(0,0,0,0.06)] backdrop-blur sm:p-4 lg:hidden dark:border-white/10 dark:bg-ink-900/95">
        <div className="mx-auto flex max-w-6xl items-center gap-2">
          <button
            onClick={handleToggleSave}
            disabled={savePending}
            aria-label={isSaved ? t('jobDetail.apply.saved') : t('jobDetail.apply.saveJob')}
            className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl border disabled:opacity-60 sm:h-12 sm:w-12 ${
              isSaved
                ? 'border-ai-200 bg-ai-50 text-ai-700'
                : 'border-ink-200 text-ink-700'
            }`}
          >
            <Bookmark className={`h-4 w-4 ${isSaved ? 'fill-current' : ''}`} />
          </button>
          {!appliedResolved ? (
            <div className="flex h-11 flex-1 items-center justify-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-4 text-sm font-bold text-ink-400 sm:h-12">
              <Loader2 className="h-4 w-4 animate-spin" />
            </div>
          ) : appliedId ? (
            <button
              onClick={() => navigate('/candidate/applications')}
              className="flex h-11 flex-1 items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 text-sm font-bold text-emerald-700 sm:h-12"
            >
              <CheckCircle2 className="h-4 w-4" /> {t('jobDetail.apply.applied')}
            </button>
          ) : (
            <button
              onClick={handleApply}
              className="h-11 flex-1 rounded-xl bg-brand-600 px-4 text-sm font-bold text-white hover:bg-brand-700 sm:h-12"
            >
              {t('jobDetail.apply.applyNow')}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
