import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  ChevronRight,
  CalendarClock,
  Clock,
  CalendarPlus,
  Check,
  Sparkles,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Video,
  Download,
  FileText,
  Languages,
  ArrowRightCircle,
  MessageSquareText,
  User,
  Globe,
} from 'lucide-react'
import { applicationService } from '@ari/shared/fservices/application'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { Skeleton } from '@ari/shared/ui/Skeleton'
import OnlineTestEntry from '@components/OnlineTestEntry'
import CriterionBar from '@components/CriterionBar'
import { formatDate, formatDuration, langLevel } from './_reportUi'
import type { MyApplicationDetail, MyApplicationSession } from '@ari/shared/types/application'

function ReportPanel({
  s,
  jobTitle,
  t,
}: {
  s: MyApplicationSession
  jobTitle: string
  t: (key: string, opts?: any) => string
}) {
  const [tab, setTab] = useState<'criteria' | 'questions'>('criteria')
  const ev = s.evaluation!
  const verdict = s.hrFinalVerdict || ev.aiVerdict
  const isPass = verdict === 'pass'
  const lang = ev.languageAssessment
  const score = typeof ev.overallScore === 'number' ? Math.round(ev.overallScore) : null
  const langCode = lang?.language?.toUpperCase() || 'NN'

  const roundTypeLabels: Record<string, string> = {
    screening: t('round.screening'),
    technical: t('round.technical'),
    online_test: t('round.online_test'),
    final: t('round.final'),
  }
  const roundType = roundTypeLabels[(s.roundType || '').toLowerCase()] || s.roundType || ''

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
        <div
          className={`flex flex-wrap items-start justify-between gap-4 border-b border-ink-100 bg-gradient-to-r p-4 sm:p-6 ${isPass ? 'from-emerald-50' : 'from-red-50'} to-white`}
        >
          <div>
            <div className="flex flex-wrap items-center gap-2 text-sm text-ink-500">
              <span className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
                {t('report.roundBadge', {
                  number: s.roundNumber,
                  type: roundType ? ` (${roundType})` : '',
                })}
              </span>
              {lang && (
                <span className="inline-flex items-center gap-1 rounded-full bg-ink-100 px-2.5 py-0.5 text-xs font-medium text-ink-600">
                  <Globe className="h-3 w-3" /> {langCode}
                </span>
              )}
            </div>
            <h1 className="mt-2 font-display text-xl font-extrabold">{jobTitle}</h1>
            <p className="text-sm text-ink-500">
              {t('badge.realInterview')}
              {s.endedAt ? ` · ${formatDate(s.endedAt)}` : ''}
              {formatDuration(s.durationSeconds) ? ` · ${formatDuration(s.durationSeconds)}` : ''}
            </p>
          </div>
          <div className="text-center">
            <div
              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-bold ring-1 ${isPass ? 'bg-emerald-50 text-emerald-700 ring-emerald-200' : 'bg-red-50 text-red-700 ring-red-200'}`}
            >
              {isPass ? <CheckCircle2 className="h-4 w-4" /> : <XCircle className="h-4 w-4" />}{' '}
              {isPass ? t('badge.pass') : t('badge.notPass')}
            </div>
            {score !== null && (
              <div className="mt-2 font-display text-3xl sm:text-4xl font-extrabold text-ink-900">
                {score}
                <span className="text-lg text-ink-400">/100</span>
              </div>
            )}
          </div>
        </div>

        {s.recordingUrl && (
          <div className="p-4 sm:p-6">
            <video
              src={resolveAssetUrl(s.recordingUrl)}
              controls
              className="aspect-video w-full rounded-xl bg-ink-900"
            />
            <div className="mt-3 flex flex-wrap gap-2">
              <a
                href={resolveAssetUrl(s.recordingUrl)}
                download
                className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                <Download className="h-4 w-4" /> {t('report.downloadRecording')}
              </a>
            </div>
          </div>
        )}
      </div>

      {ev.reasoning && (
        <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
          <h2 className="mb-2 font-display text-lg font-bold">{t('report.title')}</h2>
          <p className="text-sm text-ink-600">{ev.reasoning}</p>
        </div>
      )}

      {(ev.criterionScores.length > 0 || ev.questionAnalyses.length > 0) && (
        <div className="flex items-center gap-1 rounded-xl border border-ink-200 bg-white p-1 text-sm shadow-card">
          <button
            onClick={() => setTab('criteria')}
            className={`flex-1 rounded-lg px-3 py-2 ${tab === 'criteria' ? 'bg-brand-600 font-semibold text-white' : 'font-medium text-ink-600 hover:bg-ink-100'}`}
          >
            {t('criteria.byCriteria')}
          </button>
          <button
            onClick={() => setTab('questions')}
            className={`flex-1 rounded-lg px-3 py-2 ${tab === 'questions' ? 'bg-brand-600 font-semibold text-white' : 'font-medium text-ink-600 hover:bg-ink-100'}`}
          >
            {t('criteria.byQuestions')}
          </button>
        </div>
      )}

      {tab === 'criteria' && ev.criterionScores.length > 0 && (
        <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
          <h2 className="mb-4 font-display text-lg font-bold">{t('report.scoresByCriteria')}</h2>
          <div className="space-y-4">
            {ev.criterionScores.map((c, i) => (
              <CriterionBar key={i} c={c} t={t} />
            ))}
          </div>
        </div>
      )}

      {tab === 'questions' && (
        <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6">
          <h2 className="mb-4 font-display text-lg font-bold">{t('questionAnalysis.title')}</h2>
          {ev.questionAnalyses.length === 0 ? (
            <p className="text-sm text-ink-500">{t('questionAnalysis.empty')}</p>
          ) : (
            <div className="space-y-3">
              {ev.questionAnalyses.map((q, i) => {
                const good = q.score >= 70
                const mid = q.score >= 50 && q.score < 70
                return (
                  <details
                    key={i}
                    className="group rounded-xl border border-ink-200 p-4"
                    open={i === 0}
                  >
                    <summary className="flex cursor-pointer items-center justify-between gap-3">
                      <span className="flex items-center gap-2 text-sm font-medium text-ink-800">
                        {good ? (
                          <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-500" />
                        ) : (
                          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />
                        )}
                        {q.question}
                      </span>
                      <span
                        className={`shrink-0 text-xs font-semibold ${good ? 'text-emerald-600' : mid ? 'text-amber-600' : 'text-red-600'}`}
                      >
                        {good
                          ? t('questionAnalysis.good')
                          : mid
                            ? t('questionAnalysis.fair')
                            : t('questionAnalysis.needsImprovement')}
                      </span>
                    </summary>
                    {q.answer && (
                      <p className="mt-3 rounded-lg bg-ink-50 p-3 text-sm text-ink-600">
                        <span className="font-medium text-ink-700">
                          {t('questionAnalysis.answer')}{' '}
                        </span>
                        {q.answer}
                      </p>
                    )}
                    {q.analysis && <p className="mt-2 text-sm text-ink-600">{q.analysis}</p>}
                    {q.feedback && (
                      <p className="mt-2 text-sm text-ink-500">
                        <span className="font-medium text-ink-600">
                          {t('questionAnalysis.feedback')}{' '}
                        </span>
                        {q.feedback}
                      </p>
                    )}
                  </details>
                )
              })}
            </div>
          )}
        </div>
      )}

      {lang && (
        <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-4 shadow-card sm:p-6">
          <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
            <Languages className="h-4 w-4" /> {t('languageAssessment.title', { lang: langCode })}
          </div>
          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">
                {langLevel(lang.overallScore)}
              </div>
              <div className="text-xs text-ink-500">{t('languageAssessment.overallLevel')}</div>
            </div>
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">{lang.fluency}</div>
              <div className="text-xs text-ink-500">{t('languageAssessment.fluency')}</div>
            </div>
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">{lang.grammar}</div>
              <div className="text-xs text-ink-500">{t('languageAssessment.grammar')}</div>
            </div>
          </div>
        </div>
      )}

      {(ev.recommendedNextStep || s.hrFeedback) && (
        <div className="grid gap-4 sm:grid-cols-2">
          {ev.recommendedNextStep && (
            <div className="rounded-2xl border border-brand-200 bg-brand-50/60 p-5 shadow-card">
              <div className="flex items-center gap-2 text-sm font-semibold text-brand-700">
                <ArrowRightCircle className="h-4 w-4" /> {t('nextStep.title')}
              </div>
              <p className="mt-2 text-sm text-ink-600">{ev.recommendedNextStep}</p>
              <Link
                to="/candidate/applications"
                className="mt-3 block w-full rounded-xl bg-brand-600 px-3 py-2 text-center text-sm font-semibold text-white hover:bg-brand-700"
              >
                {t('nextStep.goToApplication')}
              </Link>
            </div>
          )}
          {s.hrFeedback && (
            <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
              <div className="flex items-center gap-2 text-sm font-semibold text-ink-700">
                <MessageSquareText className="h-4 w-4 text-brand-600" /> {t('hrFeedback.title')}
              </div>
              <p className="mt-2 text-sm text-ink-600">{s.hrFeedback}</p>
              <div className="mt-3 flex items-center gap-2 text-xs text-ink-400">
                <User className="h-3.5 w-3.5" /> {t('hrFeedback.hrLeader')}
              </div>
            </div>
          )}
        </div>
      )}

      <p className="text-center text-xs text-ink-400">{t('report.footer')}</p>
    </div>
  )
}

function RoundPlaceholder({
  s,
  t,
}: {
  s: MyApplicationSession
  t: (key: string, opts?: any) => string
}) {
  if (s.pendingHrReview) {
    return (
      <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-card sm:p-10">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-amber-50 text-amber-600">
          <Clock className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-700">{t('pendingHrReview.title')}</p>
        <p className="mt-1 text-sm text-ink-500">
          {t('pendingHrReview.description', { round: s.roundNumber })}
        </p>
      </div>
    )
  }
  if (s.status === 'scheduled') {
    return (
      <div className="rounded-2xl border border-blue-200 bg-blue-50/50 p-10 text-center shadow-card">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-blue-100 text-blue-600">
          <CalendarClock className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-800">
          {t('scheduled.title', { round: s.roundNumber })}
        </p>
        {s.scheduledAt && (
          <p className="mt-2 text-base font-bold text-blue-700">
            {formatDate(s.scheduledAt)}{' '}
            {new Date(s.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </p>
        )}
        <p className="mt-2 text-xs text-ink-500">
          {t('scheduled.preparationHint')}
        </p>
      </div>
    )
  }
  if (s.status === 'missed') {
    return (
      <div className="rounded-2xl border border-amber-200 bg-amber-50/50 p-10 text-center shadow-card">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-amber-100 text-amber-600">
          <AlertTriangle className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-800">
          {t('missed.title', { round: s.roundNumber })}
        </p>
        {s.scheduledAt && (
          <p className="mt-2 text-base font-bold text-amber-700">
            {formatDate(s.scheduledAt)}{' '}
            {new Date(s.scheduledAt).toLocaleTimeString([], {
              hour: '2-digit',
              minute: '2-digit',
            })}
          </p>
        )}
        <p className="mt-2 text-sm text-ink-500">{t('missed.description')}</p>
      </div>
    )
  }
  if (s.status === 'invited') {
    return (
      <div className="rounded-2xl border border-purple-200 bg-purple-50/50 p-10 text-center shadow-card">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-purple-100 text-purple-600">
          <CalendarPlus className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-800">
          {t('invited.title', { round: s.roundNumber })}
        </p>
        <p className="mt-1 text-sm text-ink-500">
          {t('invited.description')}
        </p>
      </div>
    )
  }
  if (s.status === 'not_started') {
    return (
      <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-card">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
          <Video className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-700">{t('notStarted.title', { round: s.roundNumber })}</p>
        <p className="mt-1 text-sm text-ink-500">
          {t('notStarted.description')}
        </p>
      </div>
    )
  }
  return (
    <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-card sm:p-10">
      <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
        <Video className="h-6 w-6" />
      </div>
      <p className="mt-3 font-semibold text-ink-700">{t('noReport.title')}</p>
      <p className="mt-1 text-sm text-ink-500">{t('noReport.description')}</p>
    </div>
  )
}

function RoundButton({
  s,
  active,
  onClick,
  t,
}: {
  s: MyApplicationSession
  active: boolean
  onClick: () => void
  t: (key: string, opts?: any) => string
}) {
  const score = s.evaluation?.overallScore

  const getBadge = () => {
    const verdict = s.hrFinalVerdict || s.evaluation?.aiVerdict
    if (s.evaluation && verdict) {
      return verdict === 'pass'
        ? { cls: 'bg-emerald-50 text-emerald-700', icon: Check, label: t('badge.pass') }
        : { cls: 'bg-red-50 text-red-700', icon: XCircle, label: t('badge.notPass') }
    }
    if (s.pendingHrReview) {
      return { cls: 'bg-amber-50 text-amber-700', icon: Clock, label: t('badge.pendingHr') }
    }
    if (s.status === 'in_progress' || s.status === 'active') {
      return { cls: 'bg-brand-50 text-brand-700', icon: Clock, label: t('badge.inProgress') }
    }
    if (s.status === 'scheduled') {
      return { cls: 'bg-blue-50 text-blue-700', icon: CalendarClock, label: t('badge.scheduled') }
    }
    if (s.status === 'missed') {
      return { cls: 'bg-amber-50 text-amber-700', icon: AlertTriangle, label: t('badge.missed') }
    }
    if (s.status === 'invited') {
      return { cls: 'bg-purple-50 text-purple-700', icon: CalendarPlus, label: t('badge.invited') }
    }
    if (s.status === 'not_started') {
      return { cls: 'bg-ink-100 text-ink-400', icon: Clock, label: t('badge.notStarted') }
    }
    return { cls: 'bg-ink-100 text-ink-500', icon: Clock, label: t('badge.noResult') }
  }

  const badge = getBadge()
  const BadgeIcon = badge.icon
  const roundTypeLabels: Record<string, string> = {
    screening: t('round.screening'),
    technical: t('round.technical'),
    online_test: t('round.online_test'),
    final: t('round.final'),
  }
  const roundType = roundTypeLabels[(s.roundType || '').toLowerCase()] || s.roundType || ''

  const displayDate = s.endedAt || s.startedAt || s.scheduledAt

  return (
    <button
      onClick={onClick}
      className={`w-full rounded-2xl border bg-white p-4 text-left shadow-card transition ${active ? 'border-2 border-brand-300' : 'border border-ink-200 hover:border-brand-200'}`}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-ink-900">
          {t('report.roundBadge', { number: s.roundNumber, type: roundType ? ` (${roundType})` : '' })}
        </span>
        <span
          className={`inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${badge.cls}`}
        >
          <BadgeIcon className="h-3 w-3" /> {badge.label}
        </span>
      </div>
      <div className="mt-1 flex items-center justify-between text-xs text-ink-500">
        <span>{t('badge.realInterview')}</span>
        <span className="font-semibold text-ink-700">
          {typeof score === 'number' ? `${Math.round(score)}/100` : '—'}
        </span>
      </div>
      {displayDate && (
        <div className="mt-1 text-[11px] text-ink-400">
          {formatDate(displayDate)}
          {s.scheduledAt && !s.endedAt && !s.startedAt ? ` · ${new Date(s.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : ''}
          {formatDuration(s.durationSeconds) ? ` · ${formatDuration(s.durationSeconds)}` : ''}
        </div>
      )}
    </button>
  )
}

function DetailSkeleton() {
  return (
    <div className="mx-auto grid max-w-6xl gap-6 px-4 sm:px-6 py-6 lg:grid-cols-[320px_1fr] lg:gap-8">
      <div className="space-y-5">
        <Skeleton className="h-40 w-full rounded-2xl" />
        <div className="hidden space-y-2 lg:block">
          <Skeleton className="h-24 w-full rounded-2xl" />
          <Skeleton className="h-24 w-full rounded-2xl" />
        </div>
        <div className="-mx-4 flex gap-2 overflow-x-auto px-4 py-1 lg:hidden">
          <Skeleton className="h-24 w-64 shrink-0 rounded-2xl" />
          <Skeleton className="h-24 w-64 shrink-0 rounded-2xl" />
        </div>
      </div>
      <div className="space-y-6">
        <Skeleton className="h-64 w-full rounded-2xl" />
        <Skeleton className="h-12 w-full rounded-xl" />
        <Skeleton className="h-48 w-full rounded-2xl" />
      </div>
    </div>
  )
}

export default function ApplicationDetailPage() {
  const { t } = useTranslation('modules/candidate/applicationDetail')
  const { id } = useParams<{ id: string }>()
  const [detail, setDetail] = useState<MyApplicationDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let active = true
    setLoading(true)
    setError('')
    applicationService
      .getMyApplicationDetail(id)
      .then((d) => {
        if (!active) return
        setDetail(d)
        const withEval = [...d.sessions].reverse().find((s) => s.evaluation)
        const activeOrScheduled = d.sessions.find(
          (s) => s.status === 'scheduled' || s.status === 'in_progress' || s.status === 'invited'
        )
        const fallback = d.sessions.length ? d.sessions[0] : null
        setSelectedId((withEval || activeOrScheduled || fallback)?.id ?? null)
      })
      .catch(
        (err: any) =>
          active && setError(err?.response?.data?.message || err?.message || t('loadError'))
      )
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [id, t])

  const selected = useMemo(
    () => detail?.sessions.find((s) => s.id === selectedId) ?? null,
    [detail, selectedId]
  )

  const jobTitle = detail?.jobTitle || t('position')

  return (
    <>
      <div className="mx-auto max-w-6xl px-4 sm:px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            {t('breadcrumb.home')}
          </Link>
          <ChevronRight className="h-4 w-4" />
          <Link to="/candidate/applications" className="hover:text-brand-600">
            {t('breadcrumb.applications')}
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="truncate font-medium text-ink-600">{loading ? '...' : jobTitle}</span>
        </div>
      </div>

      {loading ? (
        <DetailSkeleton />
      ) : error ? (
        <div className="mx-auto max-w-6xl px-4 sm:px-6 py-6">
          <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <XCircle className="h-4 w-4" /> {error}
          </div>
        </div>
      ) : !detail ? null : (
        <main className="mx-auto grid max-w-6xl gap-6 px-4 sm:px-6 py-6 lg:grid-cols-[320px_1fr] lg:gap-8">
          <div className="space-y-5">
            {id && <OnlineTestEntry applicationId={id} />}
            {detail.upcomingInterview && (
              <div className="rounded-2xl border border-brand-200 bg-brand-50/60 p-5 shadow-card">
                <div className="flex items-center gap-2 text-sm font-semibold">
                  <CalendarClock className="h-4 w-4 text-brand-600" />{' '}
                  {t('upcomingInterview.title')}
                </div>
                <div className="mt-3 flex items-start gap-3">
                  <div className="grid h-12 w-12 shrink-0 flex-col place-items-center rounded-xl bg-white text-brand-700 ring-1 ring-brand-200">
                    <span className="text-[10px] font-semibold leading-none">
                      {new Date(detail.upcomingInterview.startTime).getMonth() + 1}
                    </span>
                    <span className="font-display text-lg font-extrabold leading-none">
                      {new Date(detail.upcomingInterview.startTime).getDate()}
                    </span>
                  </div>
                  <div className="min-w-0">
                    <div className="text-sm font-semibold text-ink-800">
                      {t('upcomingInterview.interview')} {detail.upcomingInterview.roundNumber}
                    </div>
                    <div className="truncate text-xs text-ink-500">{jobTitle}</div>
                    <div className="mt-1 inline-flex items-center gap-1 text-xs text-ink-400">
                      <Clock className="h-3.5 w-3.5" />
                      {new Date(detail.upcomingInterview.startTime).toLocaleTimeString(undefined, {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                      {detail.location ? ` · ${detail.location}` : ''}
                    </div>
                  </div>
                </div>
                <Link
                  to="/candidate/applications"
                  className="mt-3 flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  <CalendarPlus className="h-4 w-4" /> {t('viewCode')}
                </Link>
              </div>
            )}

            {/* Round list — dọc từ lg, horizontal scroll dưới lg */}
            <div>
              <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                {t('roundList.title')}
              </div>
              {detail.sessions.length === 0 ? (
                <div className="hidden rounded-2xl border border-dashed border-ink-300 bg-white p-4 text-center text-sm text-ink-500 shadow-card sm:p-6 lg:block">
                  {t('roundList.empty')}
                </div>
              ) : (
                <>
                  <div className="-mx-4 flex gap-2 overflow-x-auto px-4 py-1 sm:flex-wrap sm:px-0 lg:hidden">
                    {detail.sessions.map((s) => (
                      <div key={`m-${s.id}`} className="w-64 shrink-0">
                        <RoundButton
                          s={s}
                          active={s.id === selectedId}
                          onClick={() => setSelectedId(s.id)}
                          t={t}
                        />
                      </div>
                    ))}
                  </div>
                  <div className="hidden space-y-2 lg:block">
                    {detail.sessions.map((s) => (
                      <RoundButton
                        key={s.id}
                        s={s}
                        active={s.id === selectedId}
                        onClick={() => setSelectedId(s.id)}
                        t={t}
                      />
                    ))}
                  </div>
                </>
              )}
            </div>

            {/* Buổi phỏng vấn thử — riêng tư của ứng viên, xem lại không giới hạn (ADR-051) */}
            {detail.practiceSessions?.length > 0 && (
              <div>
                <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                  {t('practiceList.title')}
                </div>
                <div className="space-y-2">
                  {detail.practiceSessions.map((p) => (
                    <Link
                      key={p.id}
                      to={`/candidate/practice/${p.id}`}
                      className="block rounded-2xl border border-ai-200 bg-ai-50/40 p-4 shadow-card transition hover:border-ai-300"
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-sm font-semibold text-ink-900">
                          {t('report.roundBadge', { number: p.roundNumber, type: '' })}
                        </span>
                        <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-ai-50 px-2 py-0.5 text-[11px] font-semibold text-ai-700">
                          <Sparkles className="h-3 w-3" /> {t('badge.practice')}
                        </span>
                      </div>
                      <div className="mt-1 flex items-center justify-between text-xs text-ink-500">
                        <span>
                          {p.hasEvaluation
                            ? t('practiceList.hasReview')
                            : t('practiceList.transcriptOnly')}
                        </span>
                        <span className="font-semibold text-ai-700">
                          {t('practiceList.view')}
                        </span>
                      </div>
                      {(p.endedAt || p.startedAt) && (
                        <div className="mt-1 text-[11px] text-ink-400">
                          {formatDate(p.endedAt || p.startedAt)}
                          {formatDuration(p.durationSeconds)
                            ? ` · ${formatDuration(p.durationSeconds)}`
                            : ''}
                        </div>
                      )}
                    </Link>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div>
            {!selected ? (
              <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-card sm:p-10">
                <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
                  <FileText className="h-6 w-6" />
                </div>
                <p className="mt-3 font-semibold text-ink-700">{t('noSession.title')}</p>
                <p className="mt-1 text-sm text-ink-500">{t('noSession.description')}</p>
              </div>
            ) : selected.evaluation ? (
              <ReportPanel s={selected} jobTitle={jobTitle} t={t} />
            ) : (
              <RoundPlaceholder s={selected} t={t} />
            )}
          </div>
        </main>
      )}
    </>
  )
}
