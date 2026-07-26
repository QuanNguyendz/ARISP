import { useEffect, useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import {
  ArrowLeft,
  FileText,
  ExternalLink,
  Send,
  KeyRound,
  Loader2,
  Mail,
  Phone,
  Briefcase,
  ClipboardList,
  Video,
  Copy,
  Check,
  CheckCircle2,
  Clock,
  CalendarClock,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ErrorAlert } from '@ari/shared/ui'
import AssignSchedulePanel from '@ari/shared/ui/AssignSchedulePanel'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import { applicationService } from '@ari/shared/fservices/application'
import { evaluationService } from '@/fservices/evaluation/evaluationService'
import { interviewService, type HrInterviewSessionItem } from '@ari/shared/fservices/interview'
import type { HrApplicationItem } from '@ari/shared/types/application'
import type { EvaluationReport } from '@ari/shared/types/evaluation'
import {
  appStatusBadge,
  appStatusLabel,
  verdictBadge,
  verdictLabel,
  sessionStatusBadge,
  sessionStatusLabel,
  initials,
  scoreColor,
  timeAgo,
} from './_jobUi'
import { JobDetailSkeleton } from './_skeletons'
import { resolveAssetUrl } from '@ari/shared/config/constants'

function apiErr(e: unknown, fallback: string): string {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback
}

export default function RecruiterCandidateDetailPage() {
  const { t } = useTranslation('modules/recruiter/candidateDetail')
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const [app, setApp] = useState<HrApplicationItem | null>(null)
  const [evals, setEvals] = useState<EvaluationReport[]>([])
  const [sessions, setSessions] = useState<HrInterviewSessionItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [inviting, setInviting] = useState(false)
  const [coding, setCoding] = useState(false)
  const [code, setCode] = useState<{ code: string; expiresAt: string } | null>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!id) return
      ; (async () => {
        setLoading(true)
        setError('')
        try {
          const [a, ev, ss] = await Promise.all([
            applicationService.getHrApplicationById(id),
            evaluationService.getEvaluationsByApplicationId(id).catch(() => [] as EvaluationReport[]),
            interviewService.getHrSessions().catch(() => [] as HrInterviewSessionItem[]),
          ])
          setApp(a)
          setEvals(ev)
          setSessions(ss)
        } catch (e) {
          setError(apiErr(e, t('loadingError')))
        } finally {
          setLoading(false)
        }
      })()
  }, [id, t])

  const mySessions = useMemo(() => sessions.filter((s) => s.applicationId === id), [sessions, id])

  const sendInvite = async () => {
    if (!id) return
    setInviting(true)
    setError('')
    setNotice('')
    try {
      await applicationService.sendInvite(id)
      setNotice(t('sent'))
    } catch (e) {
      setError(apiErr(e, t('sendError')))
    } finally {
      setInviting(false)
    }
  }

  const genCode = async () => {
    if (!id) return
    setCoding(true)
    setError('')
    setNotice('')
    try {
      const r = await interviewService.generateCode(id)
      setCode({ code: r.code, expiresAt: r.expiresAt })
    } catch (e) {
      setError(apiErr(e, t('codeError')))
    } finally {
      setCoding(false)
    }
  }

  const copyCode = async () => {
    if (!code) return
    try {
      await navigator.clipboard.writeText(code.code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* ignore */
    }
  }

  const refreshApp = async () => {
    if (!id) return
    try {
      setApp(await applicationService.getHrApplicationById(id))
    } catch {
      /* giữ nguyên hồ sơ hiện tại nếu refetch lỗi */
    }
  }

  const roundLabel = (num: number, type?: string) =>
    `${t('round', { number: num })} · ${type === 'technical' ? t('technical') : t('screening')}`

  const sessionTypeLabel = (type?: string) => (type === 'practice' ? t('practice') : t('real'))

  if (loading) return <JobDetailSkeleton />
  if (!app) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || t('notFound')} />
        <Link
          to="/recruiter/candidates"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← {t('back')}
        </Link>
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <Link
        to="/recruiter/candidates"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('backToList')}
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}
      {notice && (
        <div className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4" /> {notice}
        </div>
      )}

      {/* Header */}
      <div className="mb-6 flex flex-col gap-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex items-center gap-3 sm:gap-4">
          <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-base font-bold text-white sm:h-16 sm:w-16 sm:text-lg">
            {initials(app.candidateName || app.candidateEmail)}
          </span>
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-lg font-bold text-ink-900 dark:text-white sm:text-xl">
              {app.candidateName || t('candidate')}
            </h1>
            <p className="mt-0.5 flex flex-col gap-1 text-sm text-ink-500 dark:text-ink-400 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-3 sm:gap-y-1">
              <span className="flex min-w-0 items-center gap-1">
                <Mail className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{app.candidateEmail}</span>
              </span>
              {app.candidatePhone ? (
                <span className="flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" />
                  {app.candidatePhone}
                </span>
              ) : null}
              <span className="flex min-w-0 items-center gap-1">
                <Briefcase className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{app.jobTitle || t('position')}</span>
              </span>
            </p>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <span
                className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(app.status)}`}
              >
                {appStatusLabel(app.status)}
              </span>
              <span className="text-xs text-ink-400">
                {t('applied')} {timeAgo(app.createdAt)}
              </span>
            </div>
          </div>
        </div>
        {app.matchScore != null && (
          <div className="flex items-center justify-between gap-2 self-stretch rounded-xl bg-ink-50 px-3 py-2 dark:bg-white/[0.03] sm:flex-col sm:items-center sm:justify-center sm:bg-transparent sm:px-0 sm:py-0 sm:dark:bg-transparent">
            <span className="text-xs text-ink-400 sm:hidden">{t('matchCVJD')}</span>
            <div className={`text-2xl font-bold sm:text-3xl ${scoreColor(app.matchScore)}`}>
              {app.matchScore}%
            </div>
            <div className="hidden text-xs text-ink-400 sm:block">{t('matchCVJD')}</div>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left */}
        <div className="space-y-6 lg:col-span-2">
          {/* Evaluations */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
            <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <ClipboardList className="h-5 w-5 text-brand-600 dark:text-brand-400" />{' '}
              {t('reportTitle')} {t('reportCount', { count: evals.length })}
            </h2>
            {evals.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-500 dark:text-ink-400">
                {t('noReport')}
              </p>
            ) : (
              <div className="space-y-3">
                {evals.map((ev) => (
                  <div
                    key={ev.id}
                    className="flex flex-col gap-2 rounded-xl border border-ink-100 dark:border-white/10 p-3 sm:flex-row sm:items-center sm:justify-between sm:gap-3"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {roundLabel(ev.roundNumber)} · {sessionTypeLabel(ev.sessionType)}
                      </p>
                      <p className="text-xs text-ink-400">
                        {timeAgo(ev.createdAt)}
                        {ev.hrReview ? ` · ${t('hrReviewed')}` : ''}
                      </p>
                    </div>
                    <div className="flex items-center gap-3 sm:shrink-0">
                      {ev.overallScore != null && (
                        <span className={`whitespace-nowrap text-lg font-bold ${scoreColor(ev.overallScore)}`}>
                          {ev.overallScore}
                        </span>
                      )}
                      <span
                        className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${verdictBadge(ev.finalVerdict ?? ev.aiVerdict)}`}
                      >
                        {verdictLabel(ev.finalVerdict ?? ev.aiVerdict)}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Interview sessions */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
            <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <Video className="h-5 w-5 text-ai-600 dark:text-ai-400" /> {t('sessionTitle')}{' '}
              {t('sessionCount', { count: mySessions.length })}
            </h2>
            {mySessions.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-500 dark:text-ink-400">
                {t('noSession')}
              </p>
            ) : (
              <div className="space-y-3">
                {mySessions.map((s) => (
                  <div
                    key={s.id}
                    className="flex flex-col gap-2 rounded-xl border border-ink-100 dark:border-white/10 p-3 sm:flex-row sm:items-center sm:justify-between sm:gap-3"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {roundLabel(s.roundNumber, s.roundType)} · {sessionTypeLabel(s.sessionType)}
                      </p>
                      <p className="flex items-center gap-1 text-xs text-ink-400">
                        <Clock className="h-3 w-3" />
                        {s.durationSeconds
                          ? t('durationMinutes', { minutes: Math.round(s.durationSeconds / 60) })
                          : t('noDuration')}{' '}
                        · {timeAgo(s.createdAt)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2 sm:shrink-0">
                      {s.verdict && (
                        <span
                          className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${verdictBadge(s.verdict)}`}
                        >
                          {verdictLabel(s.verdict)}
                        </span>
                      )}
                      <span
                        className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusBadge(s.status)}`}
                      >
                        {sessionStatusLabel(s.status)}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Right */}
        <div className="space-y-6">
          {/* Actions */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:p-5">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('actions')}
            </h2>
            <div className="space-y-2">
              {app.cvFileUrl && (
                <button
                  type="button"
                  onClick={() =>
                    openDocument(
                      resolveAssetUrl(app.cvFileUrl),
                      `${app.candidateName || t('candidate')} - CV`
                    )
                  }
                  className="flex w-full items-center gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 text-sm text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('viewCV')}
                  <ExternalLink className="ml-auto h-3.5 w-3.5 text-ink-400" />
                </button>
              )}
              <button
                onClick={sendInvite}
                disabled={inviting}
                className="flex w-full items-center gap-3 rounded-xl bg-brand-600 px-3 py-3 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {inviting ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}{' '}
                {t('sendMagicLink')}
              </button>
              <button
                onClick={genCode}
                disabled={coding || !app.hasScheduledInterview}
                title={app.hasScheduledInterview ? undefined : t('noSchedule')}
                className="flex w-full items-center gap-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-3 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {coding ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <KeyRound className="h-4 w-4" />
                )}{' '}
                {t('generateCode')}
              </button>
              {!app.hasScheduledInterview && (
                <p className="-mt-1 flex items-center gap-1.5 text-xs text-ink-400">
                  <CalendarClock className="h-3.5 w-3.5" /> {t('waitForSchedule')}
                </p>
              )}

              {code && (
                <div className="rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-3">
                  <p className="mb-1 text-xs text-emerald-700 dark:text-emerald-400">
                    {t('codeTitle')}
                  </p>
                  <button
                    onClick={copyCode}
                    className="flex w-full items-center justify-between font-mono text-lg font-bold tracking-widest text-emerald-700 dark:text-emerald-300"
                  >
                    {code.code}
                    {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                  </button>
                </div>
              )}
            </div>
          </div>

          {/* Xếp lịch phỏng vấn (HR gán cứng 1 giờ cho ứng viên — ADR-048) */}
          <AssignSchedulePanel
            applicationId={app.id}
            jobPostingId={app.jobPostingId}
            round={app.currentRound || 1}
            hasScheduled={!!app.hasScheduledInterview}
            scheduledAt={app.interviewDate}
            confirmationStatus={app.scheduleConfirmationStatus}
            declineReason={app.scheduleDeclineReason}
            status={app.status}
            onAssigned={refreshApp}
          />

          {/* Info */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:p-5">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">{t('info')}</h2>
            <dl className="space-y-3 text-sm">
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('source')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.source === 'job_board' ? t('jobBoard') : t('invited')}
                </dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('practiceUsed')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.practiceSessionUsed ? t('used') : t('notUsed')}
                </dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('appliedDate')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {new Date(app.createdAt).toLocaleDateString()}
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </div>
    </div>
  )
}
