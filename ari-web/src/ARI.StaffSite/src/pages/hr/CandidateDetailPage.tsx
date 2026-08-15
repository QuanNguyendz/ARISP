import { useEffect, useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import {
  ArrowLeft,
  FileText,
  ExternalLink,
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
  UserCheck,
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
import { CandidateOnlineProfileModal } from './CandidatesPage'
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
} from '../recruiter/_jobUi'
import { JobDetailSkeleton } from '../recruiter/_skeletons'
import { resolveAssetUrl } from '@ari/shared/config/constants'

function apiErr(e: unknown, fallback: string): string {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback
}

export default function HrCandidateDetailPage() {
  const { t } = useTranslation('modules/hr/candidateDetail')
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const [app, setApp] = useState<HrApplicationItem | null>(null)
  const [evals, setEvals] = useState<EvaluationReport[]>([])
  const [sessions, setSessions] = useState<HrInterviewSessionItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [coding, setCoding] = useState(false)
  const [code, setCode] = useState<{ code: string; expiresAt: string } | null>(null)
  const [copied, setCopied] = useState(false)
  const [showProfileModal, setShowProfileModal] = useState(false)

  useEffect(() => {
    if (!id) return
      ; (async () => {
        setLoading(true)
        setError('')
        try {
          const [a, ev, ss] = await Promise.all([
            applicationService.getHrApplicationById(id),
            evaluationService.getEvaluationsByApplicationId(id).catch(() => [] as EvaluationReport[]),
            interviewService.getHrSessions(id).catch(() => [] as HrInterviewSessionItem[]),
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

  // Chỉ hiển thị phiên/đánh giá THẬT — backend đã lọc bỏ phiên thử (riêng tư của ứng viên, ADR-051).

  if (loading) return <JobDetailSkeleton />
  if (!app) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || t('notFound')} />
        <Link
          to="/hr/candidates"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← {t('back')}
        </Link>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-ink-50 p-4 sm:p-6 dark:bg-ink-950 lg:p-8">
      <Link
        to="/hr/candidates"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('backToList')}
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}
      {notice && (
        <div className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4" /> {notice}
        </div>
      )}

      <div className="mb-6 flex flex-col gap-3 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3 sm:gap-4">
          <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-base sm:text-lg font-bold text-white sm:h-16 sm:w-16">
            {initials(app.candidateName || app.candidateEmail)}
          </span>
          <div className="min-w-0">
            <h1 className="text-xl font-bold text-ink-900 dark:text-white">
              {app.candidateName || t('candidate')}
            </h1>
            <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
              <span className="flex items-center gap-1">
                <Mail className="h-3.5 w-3.5" />
                {app.candidateEmail}
              </span>
              {app.candidatePhone ? (
                <span className="flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" />
                  {app.candidatePhone}
                </span>
              ) : null}
              <span className="flex items-center gap-1">
                <Briefcase className="h-3.5 w-3.5" />
                {app.jobTitle || t('position')}
              </span>
            </p>
            <div className="mt-2 flex items-center gap-2">
              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(app.status)}`}
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
          <div className="text-center">
            <div className={`text-3xl font-bold ${scoreColor(app.matchScore)}`}>
              {app.matchScore}%
            </div>
            <div className="text-xs text-ink-400">{t('matchCVJD')}</div>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
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
                  <Link
                    key={ev.id}
                    to={`/hr/evaluations?id=${ev.id}`}
                    className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 hover:border-brand-300 dark:hover:border-brand-500/40"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {roundLabel(ev.roundNumber)}
                      </p>
                      <p className="text-xs text-ink-400">
                        {timeAgo(ev.createdAt)}
                        {ev.hrReview ? ` · ${t('hrReviewed')}` : ''}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      {ev.overallScore != null && (
                        <span className={`text-lg font-bold ${scoreColor(ev.overallScore)}`}>
                          {ev.overallScore}
                        </span>
                      )}
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${verdictBadge(ev.finalVerdict ?? ev.aiVerdict)}`}
                      >
                        {verdictLabel(ev.finalVerdict ?? ev.aiVerdict)}
                      </span>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>

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
                    className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {roundLabel(s.roundNumber, s.roundType)}
                      </p>
                      <p className="flex items-center gap-1 text-xs text-ink-400">
                        <Clock className="h-3 w-3" />
                        {s.durationSeconds
                          ? t('durationMinutes', { minutes: Math.round(s.durationSeconds / 60) })
                          : t('noDuration')}{' '}
                        · {timeAgo(s.createdAt)}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      {s.verdict && (
                        <span
                          className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${verdictBadge(s.verdict)}`}
                        >
                          {verdictLabel(s.verdict)}
                        </span>
                      )}
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${sessionStatusBadge(s.status)}`}
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

        <div className="space-y-6">
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('actions')}
            </h2>
            <div className="space-y-2">
              <button
                type="button"
                onClick={() => setShowProfileModal(true)}
                className="flex w-full items-center gap-3 rounded-xl border border-purple-200 dark:border-purple-500/20 bg-purple-50/50 dark:bg-purple-500/10 px-3 py-3 text-sm font-semibold text-purple-700 dark:text-purple-300 hover:bg-purple-100 dark:hover:bg-purple-500/20 transition-colors cursor-pointer"
              >
                <UserCheck className="h-4 w-4 text-purple-600 dark:text-purple-400" /> Xem Profile Online
              </button>
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
                  <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('viewCV')}{' '}
                  <ExternalLink className="ml-auto h-3.5 w-3.5 text-ink-400" />
                </button>
              )}
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
                <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 dark:border-emerald-500/30 dark:bg-emerald-500/10">
                  <p className="mb-1 text-xs text-emerald-700 dark:text-emerald-400">
                    {t('codeTitle')}
                  </p>
                  <button
                    onClick={copyCode}
                    className="flex w-full items-center justify-between font-mono text-base sm:text-lg font-bold tracking-widest text-emerald-700 dark:text-emerald-300"
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

          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">{t('info')}</h2>
            <dl className="space-y-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('source')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.source === 'job_board' ? t('jobBoard') : t('invited')}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('practiceUsed')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.practiceSessionUsed ? t('used') : t('notUsed')}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('appliedDate')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {new Date(app.createdAt).toLocaleDateString()}
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </div>

      {showProfileModal && (
        <CandidateOnlineProfileModal
          app={app}
          onClose={() => setShowProfileModal(false)}
        />
      )}
    </div>
  )
}
