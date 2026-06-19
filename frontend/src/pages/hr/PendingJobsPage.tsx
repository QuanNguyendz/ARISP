import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import {
  Clock,
  CheckCircle,
  XCircle,
  Eye,
  FileText,
  MapPin,
  DollarSign,
  Loader2,
} from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, LoadingSpinner, ErrorAlert } from '@components/shared'
import { jobService } from '@services/job/jobService'
import type { JobPosting } from '@/types/job'

function formatSalary(job: JobPosting): string {
  if (job.salaryIsNegotiable && !job.salaryMin && !job.salaryMax) return 'Thỏa thuận'
  const cur = job.salaryCurrency || 'USD'
  const fmt = (n: number) => (cur === 'VND' ? `${(n / 1_000_000).toLocaleString('vi-VN')}tr` : `${n.toLocaleString('en-US')}`)
  const sign = cur === 'VND' ? '' : '$'
  if (job.salaryMin && job.salaryMax) return `${sign}${fmt(job.salaryMin)} - ${sign}${fmt(job.salaryMax)}`
  if (job.salaryMin) return `Từ ${sign}${fmt(job.salaryMin)}`
  if (job.salaryMax) return `Đến ${sign}${fmt(job.salaryMax)}`
  return 'Chưa rõ'
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
  const navigate = useNavigate()
  const [jobs, setJobs] = useState<JobPosting[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionId, setActionId] = useState<string | null>(null)
  const [rejectTarget, setRejectTarget] = useState<JobPosting | null>(null)
  const [rejectReason, setRejectReason] = useState('')

  const load = async () => {
    try {
      setLoading(true)
      const data = await jobService.getAdminJobPostings()
      setJobs(data)
    } catch {
      setError('Không tải được danh sách tin tuyển dụng. Vui lòng thử lại.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const pendingJobs = useMemo(() => jobs.filter((j) => j.status === 'pending'), [jobs])

  const stats = useMemo(
    () => [
      { label: 'Chờ duyệt', value: pendingJobs.length, color: 'text-amber-600 dark:text-amber-400' },
      {
        label: 'Đang tuyển',
        value: jobs.filter((j) => j.status === 'active').length,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: 'Bị từ chối',
        value: jobs.filter((j) => j.status === 'rejected').length,
        color: 'text-red-600 dark:text-red-400',
      },
      {
        label: 'Tạo hôm nay',
        value: jobs.filter((j) => isToday(j.createdAt)).length,
        color: 'text-blue-600 dark:text-blue-400',
      },
    ],
    [jobs, pendingJobs]
  )

  const approve = async (job: JobPosting) => {
    setActionId(job.id)
    setError(null)
    try {
      await jobService.updateJobStatus(job.id, 'active')
      setJobs((prev) => prev.map((j) => (j.id === job.id ? { ...j, status: 'active' } : j)))
    } catch {
      setError(`Không thể duyệt tin "${job.title}". Vui lòng thử lại.`)
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
      setJobs((prev) =>
        prev.map((j) =>
          j.id === job.id ? { ...j, status: 'rejected', rejectionReason: rejectReason.trim() } : j
        )
      )
      setRejectTarget(null)
      setRejectReason('')
    } catch {
      setError(`Không thể từ chối tin "${job.title}". Vui lòng thử lại.`)
    } finally {
      setActionId(null)
    }
  }

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Tin chờ duyệt"
        description="Xem xét và phê duyệt tin tuyển dụng từ Recruiter"
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      <StatsGrid stats={stats} />

      {loading ? (
        <LoadingSpinner message="Đang tải tin chờ duyệt..." />
      ) : pendingJobs.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="w-8 h-8 text-emerald-600 dark:text-emerald-400" />}
          title="Không có tin chờ duyệt"
          description="Tất cả tin tuyển dụng đã được xử lý."
        />
      ) : (
        <div className="space-y-4">
          {pendingJobs.map((job, index) => {
            const busy = actionId === job.id
            return (
              <motion.div
                key={job.id}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(index * 0.05, 0.3) }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
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
                        <span>Bởi: {job.createdByName || '—'}</span>
                        {job.location && (
                          <span className="flex items-center gap-1">
                            <MapPin className="w-3 h-3" />
                            {job.location}
                          </span>
                        )}
                        <span className="flex items-center gap-1">
                          <DollarSign className="w-3 h-3" />
                          {formatSalary(job)}
                        </span>
                        {job.department && <span>{job.department}</span>}
                      </div>
                      <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
                        <Clock className="w-3 h-3" />
                        Tạo lúc: {formatDateTime(job.createdAt)}
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
                      Xem
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => approve(job)}
                      className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 text-sm font-medium hover:bg-emerald-50 dark:hover:bg-emerald-500/30 transition-colors disabled:opacity-50"
                    >
                      {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
                      Duyệt
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
                      Từ chối
                    </button>
                  </div>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {/* Modal từ chối */}
      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/50 backdrop-blur-sm">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-card-hover"
          >
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">Từ chối tin tuyển dụng</h3>
            <p className="text-sm text-ink-500 dark:text-ink-400 mb-4">
              Nhập lý do từ chối tin "{rejectTarget.title}". Recruiter sẽ thấy lý do này để chỉnh sửa.
            </p>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder="Lý do từ chối..."
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
                Hủy
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
                Xác nhận từ chối
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
