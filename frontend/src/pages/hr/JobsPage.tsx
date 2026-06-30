import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Users, MapPin, Briefcase, Building2, Calendar, Languages, Zap } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert } from '@components/shared'
import { HrStatsSkeleton, JobListSkeleton } from './_skeletons'
import { jobService } from '@services/job/jobService'
import type { JobPosting } from '@/types/job'

type StatusKey = 'draft' | 'active' | 'paused' | 'closed'
type FilterKey = 'all' | StatusKey

const STATUS_META: Record<StatusKey, { label: string; badge: string }> = {
  active: {
    label: 'Đang tuyển',
    badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
  },
  draft: {
    label: 'Nháp',
    badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  },
  paused: {
    label: 'Tạm dừng',
    badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
  },
  closed: {
    label: 'Đã đóng',
    badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
  },
}

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'active', label: 'Đang tuyển' },
  { key: 'draft', label: 'Nháp' },
  { key: 'paused', label: 'Tạm dừng' },
  { key: 'closed', label: 'Đã đóng' },
]

function formatDate(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

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

export default function HrJobsPage() {
  const { data: jobsData, isLoading: loading, error: fetchError } = useQuery({
    queryKey: ['admin-jobs'],
    queryFn: () => jobService.getAdminJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const error = (fetchError as any)?.response?.data?.message || (fetchError ? 'Không tải được danh sách tin tuyển dụng.' : '')
  const [filter, setFilter] = useState<FilterKey>('all')

  const stats = useMemo(() => {
    const count = (s: StatusKey) => jobs.filter((j) => j.status === s).length
    return [
      { label: 'Tổng tin', value: jobs.length, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: 'Đang tuyển',
        value: count('active'),
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      { label: 'Nháp', value: count('draft'), color: 'text-amber-600 dark:text-amber-400' },
      { label: 'Đã đóng', value: count('closed'), color: 'text-ink-600 dark:text-ink-400' },
    ]
  }, [jobs])

  const filtered = useMemo(
    () => (filter === 'all' ? jobs : jobs.filter((j) => j.status === filter)),
    [jobs, filter]
  )

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Tin tuyển dụng"
        description="Quản lý tất cả tin tuyển dụng trong hệ thống"
      />

      {loading && <HrStatsSkeleton />}
      {!loading && !error && <StatsGrid stats={stats} />}

      {/* Status filter tabs */}
      {!loading && !error && jobs.length > 0 && (
        <div className="flex flex-wrap gap-2 mb-6">
          {FILTERS.map((f) => {
            const cnt =
              f.key === 'all' ? jobs.length : jobs.filter((j) => j.status === f.key).length
            const activeTab = filter === f.key
            return (
              <button
                key={f.key}
                onClick={() => setFilter(f.key)}
                className={`px-4 py-2 rounded-xl text-sm font-medium transition-all ${
                  activeTab
                    ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white'
                    : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'
                }`}
              >
                {f.label}
                <span className={`ml-2 text-xs ${activeTab ? 'text-white/80' : 'text-ink-400'}`}>
                  {cnt}
                </span>
              </button>
            )
          })}
        </div>
      )}

      {loading && <JobListSkeleton rows={5} />}
      {!loading && error && <ErrorAlert message={error} />}

      {!loading && !error && filtered.length === 0 && (
        <EmptyState
          icon={<Briefcase className="w-8 h-8 text-ink-400" />}
          title={jobs.length === 0 ? 'Chưa có tin tuyển dụng' : 'Không có tin phù hợp'}
          description={
            jobs.length === 0
              ? 'Hệ thống chưa có tin tuyển dụng nào. Tin do Recruiter tạo và HR duyệt sẽ hiển thị tại đây.'
              : 'Không có tin tuyển dụng nào ở trạng thái đã chọn. Thử bộ lọc khác.'
          }
        />
      )}

      {!loading && !error && filtered.length > 0 && (
        <div className="space-y-4">
          {filtered.map((job, index) => {
            const meta = STATUS_META[job.status as StatusKey] ?? STATUS_META.draft
            return (
              <motion.div
                key={job.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(index * 0.04, 0.3) }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all"
              >
                <div className="flex items-center justify-between gap-4">
                  <div className="flex items-center gap-4 min-w-0">
                    <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-white font-semibold shrink-0">
                      {job.title.charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-3 mb-1 flex-wrap">
                        <h3 className="text-lg font-semibold text-ink-900 dark:text-white truncate">
                          {job.title}
                        </h3>
                        <span
                          className={`px-2 py-0.5 rounded-full text-xs font-medium ${meta.badge}`}
                        >
                          {meta.label}
                        </span>
                        {job.isUrgent && (
                          <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-red-100 dark:bg-red-500/20 text-red-600 dark:text-red-400 flex items-center gap-1">
                            <Zap className="w-3 h-3" /> Gấp
                          </span>
                        )}
                      </div>
                      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-600 dark:text-ink-400">
                        {job.department && (
                          <span className="flex items-center gap-1">
                            <Building2 className="w-4 h-4" />
                            {job.department}
                          </span>
                        )}
                        {job.location && (
                          <span className="flex items-center gap-1">
                            <MapPin className="w-4 h-4" />
                            {job.location}
                          </span>
                        )}
                        {job.languageRequirement && (
                          <span className="flex items-center gap-1">
                            <Languages className="w-4 h-4" />
                            {job.languageRequirement}
                          </span>
                        )}
                        <span className="flex items-center gap-1">
                          <Users className="w-4 h-4" />
                          {job.applicantCount ?? 0} ứng viên
                        </span>
                        {job.applicationDeadline && (
                          <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                            <Calendar className="w-4 h-4" />
                            Hạn nộp: {getDeadlineText(job.applicationDeadline)}
                          </span>
                        )}
                        <span className="flex items-center gap-1">
                          <Calendar className="w-4 h-4" />
                          {formatDate(job.createdAt)}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Link
                      to={`/hr/jobs/${job.id}`}
                      className="px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium"
                    >
                      Chi tiết
                    </Link>
                  </div>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}
    </div>
  )
}
