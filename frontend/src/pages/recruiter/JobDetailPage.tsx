import { useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  ArrowLeft,
  Pencil,
  Send,
  XCircle,
  FileText,
  Users,
  MapPin,
  Briefcase,
  Mail,
  Loader2,
  CheckCircle2,
  AlertCircle,
  Layers,
  Target,
  CalendarClock,
} from 'lucide-react'
import { ErrorAlert } from '@components/shared'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import jobService from '@services/job/jobService'
import { applicationService } from '@services/application/applicationService'
import type { JobPosting } from '@/types/job'
import type { HrApplicationItem } from '@/types/application'
import {
  jobStatusBadge,
  jobStatusLabel,
  appStatusBadge,
  appStatusLabel,
  formatSalary,
  initials,
  timeAgo,
} from './_jobUi'
import { JobDetailSkeleton } from './_skeletons'

function getDeadlineText(deadlineStr?: string | null): string {
  if (!deadlineStr) return ''
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return ''
  const formattedDate = d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return `${formattedDate} (Đã hết hạn)`
  if (diffDays === 0) return `${formattedDate} (Hết hạn hôm nay)`
  return `${formattedDate} (Còn ${diffDays} ngày)`
}

const FUNNEL: { key: string; label: string }[] = [
  { key: 'cv_submitted', label: 'Mới ứng tuyển' },
  { key: 'screening', label: 'Đang sơ loại' },
  { key: 'interview', label: 'Phỏng vấn' },
  { key: 'pass', label: 'Đạt' },
]

const matchColor = (s?: number | null) =>
  s == null
    ? 'text-ink-400'
    : s >= 75
      ? 'text-emerald-600 dark:text-emerald-400'
      : s >= 50
        ? 'text-amber-600 dark:text-amber-400'
        : 'text-red-600 dark:text-red-400'

export default function RecruiterJobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const { data: job, isLoading: loadingJob, error: jobError } = useQuery({
    queryKey: ['job', id],
    queryFn: () => jobService.getJobPostingById(id!),
    enabled: !!id,
    retry: false
  })

  const { data: appsData, isLoading: loadingApps, refetch: refetchApps } = useQuery({
    queryKey: ['job', id, 'applications'],
    queryFn: () => jobService.getJobApplications(id!).catch(() => [] as HrApplicationItem[]),
    enabled: !!id,
  })

  const apps = appsData || []
  const loading = loadingJob || loadingApps
  
  // Keep error state for mutations like status change
  const [mutationError, setMutationError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [invitingId, setInvitingId] = useState<string | null>(null)
  
  const error = mutationError || (jobError as any)?.response?.data?.message || (jobError ? 'Không tải được chi tiết tin tuyển dụng.' : '')

  const load = refetchApps

  const funnel = useMemo(() => {
    const by = (s: string) => apps.filter((a) => a.status === s).length
    return FUNNEL.map((f) => ({ ...f, count: by(f.key) }))
  }, [apps])

  const hired = useMemo(() => apps.filter((a) => a.status === 'pass').length, [apps])
  const isFull = job?.vacancies != null && job.vacancies > 0 && hired >= job.vacancies

  const changeStatus = async (status: JobPosting['status']) => {
    if (!id) return
    setBusy(true)
    setMutationError('')
    setNotice('')
    try {
      await jobService.updateJobStatus(id, status)
      setNotice(
        status === 'pending'
          ? 'Đã gửi tin cho HR Leader duyệt.'
          : status === 'closed'
            ? 'Đã đóng tin tuyển dụng.'
            : 'Đã cập nhật trạng thái.'
      )
      await load()
    } catch (e: any) {
      setMutationError(e?.response?.data?.message || 'Không thể cập nhật trạng thái tin.')
    } finally {
      setBusy(false)
    }
  }

  const sendInvite = async (appId: string) => {
    setInvitingId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.sendInvite(appId)
      setNotice('Đã gửi lời mời (magic link) cho ứng viên.')
    } catch (e: any) {
      setMutationError(e?.response?.data?.message || 'Không thể gửi lời mời.')
    } finally {
      setInvitingId(null)
    }
  }

  if (loading) return <JobDetailSkeleton />
  if (!job) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || 'Không tìm thấy tin tuyển dụng.'} />
        <Link
          to="/recruiter/my-jobs"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← Quay lại danh sách
        </Link>
      </div>
    )
  }

  const canSubmit = job.status === 'draft' || job.status === 'rejected'
  const canClose = job.status === 'active'
  const canEdit = job.status !== 'archived'

  return (
    <div className="p-6 lg:p-8">
      <Link
        to="/recruiter/my-jobs"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> Quay lại danh sách
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setMutationError('')} />}
      {notice && (
        <div className="mb-6 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
          <span className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4" /> {notice}
          </span>
          <button onClick={() => setNotice('')} className="hover:opacity-70">
            Đóng
          </button>
        </div>
      )}

      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
      >
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="flex items-start gap-4">
            <span className="grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-brand-50 dark:bg-brand-500/15 text-brand-600 dark:text-brand-400">
              <Briefcase className="h-6 w-6" />
            </span>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="text-xl font-bold text-ink-900 dark:text-white">{job.title}</h1>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(job.status)}`}
                >
                  {jobStatusLabel(job.status)}
                </span>
              </div>
              <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                <span>{job.department || 'Chưa phân phòng ban'}</span>
                {job.location ? (
                  <span className="flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" />
                    {job.location}
                  </span>
                ) : null}
                <span>{formatSalary(job)}</span>
                {job.vacancies != null && job.vacancies > 0 && (
                  <span
                    className={`flex items-center gap-1 font-medium ${isFull ? 'text-emerald-600 dark:text-emerald-400' : 'text-ink-600 dark:text-ink-300'}`}
                  >
                    <Target className="h-3.5 w-3.5" /> Tuyển {hired}/{job.vacancies}
                  </span>
                )}
                {job.applicationDeadline && (
                  <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                    <CalendarClock className="h-3.5 w-3.5" />
                    Hạn nộp: {getDeadlineText(job.applicationDeadline)}
                  </span>
                )}
                <span className="text-ink-400">· tạo {timeAgo(job.createdAt)}</span>
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {job.jdFileUrl && (
              <button
                type="button"
                onClick={() => openDocument(job.jdFileUrl!, job.jdFileName || `${job.title} - JD`)}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <FileText className="h-4 w-4" /> JD gốc
              </button>
            )}
            <Link
              to={`/recruiter/my-jobs/${job.id}/schedule`}
              className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              <CalendarClock className="h-4 w-4" /> Lịch phỏng vấn
            </Link>
            {canEdit && (
              <Link
                to={`/recruiter/my-jobs/${job.id}/edit`}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <Pencil className="h-4 w-4" /> Sửa tin
              </Link>
            )}
            {canSubmit && (
              <button
                onClick={() => changeStatus('pending')}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3.5 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}{' '}
                Gửi HR duyệt
              </button>
            )}
            {canClose && (
              <button
                onClick={() => changeStatus('closed')}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-3.5 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 disabled:opacity-50"
              >
                {busy ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <XCircle className="h-4 w-4" />
                )}{' '}
                Đóng tin
              </button>
            )}
          </div>
        </div>

        {job.status === 'rejected' && job.rejectionReason && (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <b>HR từ chối:</b> {job.rejectionReason}. Sửa tin và gửi duyệt lại.
            </span>
          </div>
        )}

        {isFull && canClose && (
          <div className="mt-4 flex items-center justify-between gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400">
            <span className="flex items-center gap-2">
              <Target className="h-4 w-4" /> Đã tuyển đủ chỉ tiêu ({hired}/{job.vacancies}) — cân
              nhắc đóng tin.
            </span>
            <button
              onClick={() => changeStatus('closed')}
              disabled={busy}
              className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              Đóng tin
            </button>
          </div>
        )}
      </motion.div>

      {/* Phễu ứng viên */}
      <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-5">
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <span className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <Users className="h-4 w-4" /> Tổng UV
          </span>
          <div className="mt-2 text-2xl font-bold text-brand-600 dark:text-brand-400">
            {apps.length}
          </div>
        </div>
        {funnel.map((f) => (
          <div
            key={f.key}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
          >
            <span className="text-sm text-ink-500 dark:text-ink-400">{f.label}</span>
            <div className="mt-2 text-2xl font-bold text-ink-900 dark:text-white">{f.count}</div>
          </div>
        ))}
      </div>

      {/* Cấu hình vòng phỏng vấn */}
      {job.roundConfigs?.length > 0 && (
        <div className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
            <Layers className="h-4 w-4 text-ai-600 dark:text-ai-400" /> Vòng phỏng vấn (
            {job.roundConfigs.length})
          </h2>
          <div className="flex flex-wrap gap-2">
            {job.roundConfigs.map((r) => (
              <span
                key={r.roundNumber}
                className="rounded-lg border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-1.5 text-xs text-ink-700 dark:text-ink-200"
              >
                Vòng {r.roundNumber}: {r.roundType === 'technical' ? 'Chuyên môn' : 'Sơ loại'} ·{' '}
                {r.maxDurationMinutes}′
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Danh sách ứng viên của job */}
      <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
        <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 px-5 py-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
            <Users className="h-5 w-5 text-brand-600 dark:text-brand-400" /> Ứng viên ({apps.length}
            )
          </h2>
        </div>

        {apps.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-12 text-center">
            <Users className="h-8 w-8 text-ink-300" />
            <p className="text-sm text-ink-500 dark:text-ink-400">
              Chưa có ứng viên nào cho tin này.
            </p>
          </div>
        ) : (
          <div className="divide-y divide-ink-100 dark:divide-white/10">
            {apps.map((a) => (
              <div
                key={a.id}
                className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
              >
                <Link
                  to={`/recruiter/candidates/${a.id}`}
                  className="flex min-w-0 items-center gap-3"
                >
                  <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                    {initials(a.candidateName || a.candidateEmail)}
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                      {a.candidateName || 'Ứng viên'}
                    </p>
                    <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                      <Mail className="h-3 w-3" /> {a.candidateEmail}
                    </p>
                  </div>
                </Link>

                <div className="flex shrink-0 items-center gap-3">
                  <span className={`text-sm font-bold ${matchColor(a.matchScore)}`}>
                    {a.matchScore != null ? `${a.matchScore}%` : '—'}
                  </span>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(a.status)}`}
                  >
                    {appStatusLabel(a.status)}
                  </span>
                  {a.cvFileUrl && (
                    <button
                      type="button"
                      onClick={() =>
                        openDocument(a.cvFileUrl!, `${a.candidateName || 'Ứng viên'} - CV`)
                      }
                      className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-brand-600 dark:hover:bg-white/10"
                      title="Xem CV"
                    >
                      <FileText className="h-4 w-4" />
                    </button>
                  )}
                  <button
                    onClick={() => sendInvite(a.id)}
                    disabled={invitingId === a.id}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                  >
                    {invitingId === a.id ? (
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Send className="h-3.5 w-3.5" />
                    )}{' '}
                    Mời
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
