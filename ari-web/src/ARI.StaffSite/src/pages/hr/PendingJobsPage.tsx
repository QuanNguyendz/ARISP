import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Clock, CheckCircle, XCircle, Eye, FileText, MapPin, Loader2 } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, Pagination } from '@ari/shared/ui'
import { HrStatsSkeleton, JobListSkeleton } from './_skeletons'
import { jobService } from '@ari/shared/fservices/job'
import type { JobPosting } from '@ari/shared/types/job'

function formatSalary(job: JobPosting, t: (key: string) => string): string {
  if (
    job.salaryIsNegotiable ||
    (job.salaryMin == null && job.salaryMax == null) ||
    (job.salaryMin === 0 && job.salaryMax === 0)
  ) {
    return t('negotiable')
  }
  const cur = (job.salaryCurrency || 'VND').toUpperCase()
  const formatVal = (n: number) =>
    cur === 'VND' ? n.toLocaleString('vi-VN') : n.toLocaleString('en-US')
  const unit = cur === 'VND' ? ' ₫' : ` ${cur}`
  if (
    job.salaryMin != null &&
    job.salaryMax != null &&
    job.salaryMin !== 0 &&
    job.salaryMax !== 0
  ) {
    return `${formatVal(job.salaryMin)} - ${formatVal(job.salaryMax)}${unit}`
  }
  if (job.salaryMin != null && job.salaryMin !== 0)
    return `${t('from')} ${formatVal(job.salaryMin)}${unit}`
  if (job.salaryMax != null && job.salaryMax !== 0)
    return `${t('to')} ${formatVal(job.salaryMax)}${unit}`
  return t('negotiable')
}

function formatDateTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function getDeadlineText(
  deadlineStr: string | null | undefined,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
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
  if (diffDays < 0) return `${formattedDate} (${t('deadline.expired')})`
  if (diffDays === 0) return `${formattedDate} (${t('deadline.today')})`
  return `${formattedDate} (${t('deadline.daysLeft', { count: diffDays })})`
}

function isToday(iso?: string): boolean {
  if (!iso) return false
  const d = new Date(iso)
  const now = new Date()
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  )
}

export default function PendingJobsPage() {
  const { t } = useTranslation('modules/hr/jobs')
  const navigate = useNavigate()
  const {
    data: jobsData,
    isLoading: loading,
    error: fetchError,
  } = useQuery({
    queryKey: ['admin-jobs'],
    queryFn: () => jobService.getAdminJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const errorMsg =
    (fetchError as any)?.response?.data?.message || (fetchError ? t('loadingError') : null)

  const [error, setError] = useState<string | null>(errorMsg)

  useEffect(() => {
    if (errorMsg) setError(errorMsg)
  }, [errorMsg])

  const [actionId, setActionId] = useState<string | null>(null)
  const [rejectTarget, setRejectTarget] = useState<JobPosting | null>(null)
  const [rejectReason, setRejectReason] = useState('')
  const [page, setPage] = useState(1)

  const pendingJobs = useMemo(() => jobs.filter((j) => j.status === 'pending'), [jobs])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(pendingJobs.length / PAGE_SIZE))
  const pagedJobs = useMemo(
    () => pendingJobs.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [pendingJobs, page]
  )

  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const stats = useMemo(
    () => [
      {
        label: t('approval.pending'),
        value: pendingJobs.length,
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: t('status.active'),
        value: jobs.filter((j) => j.status === 'active').length,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: t('status.rejected'),
        value: jobs.filter((j) => j.status === 'rejected').length,
        color: 'text-red-600 dark:text-red-400',
      },
      {
        label: t('approval.createdToday'),
        value: jobs.filter((j) => isToday(j.createdAt)).length,
        color: 'text-blue-600 dark:text-blue-400',
      },
    ],
    [jobs, pendingJobs, t]
  )

  const queryClient = useQueryClient()

  const approve = async (job: JobPosting) => {
    setActionId(job.id)
    setError(null)
    try {
      await jobService.updateJobStatus(job.id, 'active')
      queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
    } catch (err) {
      // Server nêu lý do cụ thể (vd tin có vòng trắc nghiệm nhưng ngân hàng đề chưa đủ câu).
      const e = err as { response?: { data?: { message?: string } } }
      setError(e?.response?.data?.message || t('approval.approveErrorMsg', { title: job.title }))
    } finally {
      setActionId(null)
    }
  }

  const confirmReject = async () => {
    if (!rejectTarget) return
    const job = rejectTarget
    setActionId(job.id)
    setError(null)
    try {
      await jobService.updateJobStatus(job.id, 'rejected', rejectReason.trim())
      queryClient.invalidateQueries({ queryKey: ['admin-jobs'] })
      setRejectTarget(null)
      setRejectReason('')
    } catch {
      setError(t('approval.rejectErrorMsg', { title: job.title }))
    } finally {
      setActionId(null)
    }
  }

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('pendingJobs')} description={t('pendingJobsHint')} />

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      {loading ? <HrStatsSkeleton /> : <StatsGrid stats={stats} />}

      {loading ? (
        <JobListSkeleton rows={4} />
      ) : pendingJobs.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="w-8 h-8 text-emerald-600 dark:text-emerald-400" />}
          title={t('noPendingJobs')}
          description={t('approval.allProcessed')}
        />
      ) : (
        <div className="space-y-4">
          {pagedJobs.map((job, index) => {
            const busy = actionId === job.id
            return (
              <motion.div
                key={job.id}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(index * 0.05, 0.3) }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card"
              >
                <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-4">
                  <div className="flex items-start gap-4 min-w-0">
                    <div className="w-12 h-12 shrink-0 rounded-xl bg-amber-100 dark:bg-amber-500/20 flex items-center justify-center">
                      <FileText className="w-6 h-6 text-amber-600 dark:text-amber-400" />
                    </div>
                    <div className="min-w-0">
                      <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">
                        {job.title}
                      </h3>
                      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-600 dark:text-ink-400 mb-3">
                        <span>
                          {t('approval.by')}: {job.createdByName || '—'}
                        </span>
                        {job.location && (
                          <span className="flex items-center gap-1">
                            <MapPin className="w-3 h-3" />
                            {job.location}
                          </span>
                        )}
                        <span className="flex items-center">{formatSalary(job, t)}</span>
                        {job.department && <span>{job.department}</span>}
                        {job.applicationDeadline && (
                          <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                            {t('deadlineLabel')}: {getDeadlineText(job.applicationDeadline, t)}
                          </span>
                        )}
                      </div>
                      <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
                        <Clock className="w-3 h-3" />
                        {t('approval.createdAt')}: {formatDateTime(job.createdAt)}
                      </span>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 flex-wrap lg:justify-end">
                    <button
                      type="button"
                      onClick={() => navigate(`/hr/jobs/${job.id}`)}
                      className="flex items-center gap-2 px-3 py-2 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium"
                    >
                      <Eye className="w-4 h-4" />
                      {t('approval.view')}
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => approve(job)}
                      className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 text-sm font-medium hover:bg-emerald-50 dark:hover:bg-emerald-500/30 transition-colors disabled:opacity-50"
                    >
                      {busy ? (
                        <Loader2 className="w-4 h-4 animate-spin" />
                      ) : (
                        <CheckCircle className="w-4 h-4" />
                      )}
                      {t('approval.approve')}
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => {
                        setRejectTarget(job)
                        setRejectReason('')
                      }}
                      className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400 text-sm font-medium hover:bg-red-50 dark:hover:bg-red-500/30 transition-colors disabled:opacity-50"
                    >
                      <XCircle className="w-4 h-4" />
                      {t('approval.rejectJob')}
                    </button>
                  </div>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {!loading && pendingJobs.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={pendingJobs.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}

      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/50 backdrop-blur-sm">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-card-hover"
          >
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">
              {t('approval.rejectModalTitle')}
            </h3>
            <p className="text-sm text-ink-500 dark:text-ink-400 mb-4">
              {t('approval.rejectModalDescription', { title: rejectTarget.title })}
            </p>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder={t('approval.rejectPlaceholder')}
              className="w-full px-3 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-red-500/40 resize-none"
            />
            <div className="flex items-center justify-end gap-2 mt-4">
              <button
                type="button"
                onClick={() => {
                  setRejectTarget(null)
                  setRejectReason('')
                }}
                className="px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 text-sm font-medium transition-colors"
              >
                {t('approval.cancel')}
              </button>
              <button
                type="button"
                disabled={!rejectReason.trim() || actionId === rejectTarget.id}
                onClick={confirmReject}
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-600 text-white text-sm font-medium hover:bg-red-500 transition-colors disabled:opacity-50"
              >
                {actionId === rejectTarget.id ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <XCircle className="w-4 h-4" />
                )}
                {t('approval.confirmReject')}
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
