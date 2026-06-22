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
} from 'lucide-react'
import { ErrorAlert } from '@components/shared'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import { applicationService } from '@services/application/applicationService'
import { evaluationService } from '@services/evaluation/evaluationService'
import { interviewService, type HrInterviewSessionItem } from '@services/interview/interviewService'
import type { HrApplicationItem } from '@/types/application'
import type { EvaluationReport } from '@/types/evaluation'
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

export default function RecruiterCandidateDetailPage() {
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
    ;(async () => {
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
      } catch (e: any) {
        setError(e?.response?.data?.message || 'Không tải được hồ sơ ứng viên.')
      } finally {
        setLoading(false)
      }
    })()
  }, [id])

  const mySessions = useMemo(() => sessions.filter((s) => s.applicationId === id), [sessions, id])

  const sendInvite = async () => {
    if (!id) return
    setInviting(true)
    setError('')
    setNotice('')
    try {
      await applicationService.sendInvite(id)
      setNotice('Đã gửi magic link mời ứng viên.')
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể gửi lời mời.')
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
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể cấp mã phỏng vấn.')
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

  if (loading) return <JobDetailSkeleton />
  if (!app) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || 'Không tìm thấy hồ sơ ứng viên.'} />
        <Link
          to="/recruiter/candidates"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← Quay lại
        </Link>
      </div>
    )
  }

  return (
    <div className="p-6 lg:p-8">
      <Link
        to="/recruiter/candidates"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> Quay lại danh sách
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}
      {notice && (
        <div className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4" /> {notice}
        </div>
      )}

      {/* Header */}
      <div className="mb-6 flex flex-col gap-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          <span className="grid h-16 w-16 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-lg font-bold text-white">
            {initials(app.candidateName || app.candidateEmail)}
          </span>
          <div className="min-w-0">
            <h1 className="text-xl font-bold text-ink-900 dark:text-white">
              {app.candidateName || 'Ứng viên'}
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
                {app.jobTitle || 'Vị trí'}
              </span>
            </p>
            <div className="mt-2 flex items-center gap-2">
              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(app.status)}`}
              >
                {appStatusLabel(app.status)}
              </span>
              <span className="text-xs text-ink-400">Ứng tuyển {timeAgo(app.createdAt)}</span>
            </div>
          </div>
        </div>
        {app.matchScore != null && (
          <div className="text-center">
            <div className={`text-3xl font-bold ${scoreColor(app.matchScore)}`}>
              {app.matchScore}%
            </div>
            <div className="text-xs text-ink-400">Match CV–JD</div>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left */}
        <div className="space-y-6 lg:col-span-2">
          {/* Evaluations */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
            <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <ClipboardList className="h-5 w-5 text-brand-600 dark:text-brand-400" /> Báo cáo đánh
              giá ({evals.length})
            </h2>
            {evals.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-500 dark:text-ink-400">
                Chưa có báo cáo đánh giá nào.
              </p>
            ) : (
              <div className="space-y-3">
                {evals.map((ev) => (
                  <div
                    key={ev.id}
                    className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        Vòng {ev.roundNumber} · {ev.sessionType === 'practice' ? 'Thử' : 'Thật'}
                      </p>
                      <p className="text-xs text-ink-400">{timeAgo(ev.createdAt)}</p>
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
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Interview sessions */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
            <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <Video className="h-5 w-5 text-ai-600 dark:text-ai-400" /> Phiên phỏng vấn (
              {mySessions.length})
            </h2>
            {mySessions.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-500 dark:text-ink-400">
                Chưa có phiên phỏng vấn nào.
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
                        Vòng {s.roundNumber} ·{' '}
                        {s.roundType === 'technical' ? 'Chuyên môn' : 'Sơ loại'} ·{' '}
                        {s.sessionType === 'practice' ? 'Thử' : 'Thật'}
                      </p>
                      <p className="flex items-center gap-1 text-xs text-ink-400">
                        <Clock className="h-3 w-3" />
                        {s.durationSeconds
                          ? `${Math.round(s.durationSeconds / 60)} phút`
                          : '—'} · {timeAgo(s.createdAt)}
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

        {/* Right */}
        <div className="space-y-6">
          {/* Actions */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">Thao tác</h2>
            <div className="space-y-2">
              {app.cvFileUrl && (
                <button
                  type="button"
                  onClick={() => openDocument(app.cvFileUrl!, `${app.candidateName || 'Ứng viên'} - CV`)}
                  className="flex w-full items-center gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 text-sm text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> Xem CV{' '}
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
                Gửi magic link
              </button>
              <button
                onClick={genCode}
                disabled={coding}
                className="flex w-full items-center gap-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-3 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50"
              >
                {coding ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <KeyRound className="h-4 w-4" />
                )}{' '}
                Cấp Interview Code
              </button>

              {code && (
                <div className="rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-3">
                  <p className="mb-1 text-xs text-emerald-700 dark:text-emerald-400">
                    Mã On-site (1 lần):
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

          {/* Info */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">Thông tin</h2>
            <dl className="space-y-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">Nguồn</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.source === 'job_board' ? 'Job Board' : 'Được mời'}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">Phỏng vấn thử</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.practiceSessionUsed ? 'Đã dùng' : 'Chưa dùng'}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">Ngày ứng tuyển</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {new Date(app.createdAt).toLocaleDateString('vi-VN')}
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </div>
    </div>
  )
}
