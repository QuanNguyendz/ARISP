import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Briefcase, Users, MapPin, Clock, ChevronRight } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState } from '@components/shared'
import jobService from '@services/job/jobService'
import type { JobPosting } from '@/types/job'
import { jobStatusBadge, jobStatusLabel, formatSalary, timeAgo } from './_jobUi'
import { JobsGridSkeleton, StatsGridSkeleton } from './_skeletons'

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

const FILTERS: { value: string; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'active', label: 'Đang đăng' },
  { value: 'pending', label: 'Chờ HR duyệt' },
  { value: 'rejected', label: 'Bị từ chối' },
  { value: 'draft', label: 'Nháp' },
  { value: 'closed', label: 'Đã đóng' },
]

export default function RecruiterMyJobsPage() {
  const { data: jobsData, isLoading: loading, error: fetchError } = useQuery({
    queryKey: ['my-jobs'],
    queryFn: () => jobService.getMyJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const error = (fetchError as any)?.response?.data?.message || (fetchError ? 'Không tải được danh sách tin tuyển dụng.' : '')
  const [filter, setFilter] = useState('all')

  const counts = useMemo(() => {
    const by = (s: string) => jobs.filter((j) => j.status === s).length
    return { all: jobs.length, active: by('active'), pending: by('pending'), rejected: by('rejected'), draft: by('draft'), closed: by('closed') }
  }, [jobs])

  const filtered = useMemo(
    () => (filter === 'all' ? jobs : jobs.filter((j) => j.status === filter)),
    [jobs, filter],
  )

  const statCards = [
    { label: 'Tổng tin', value: counts.all, color: 'text-brand-600' },
    { label: 'Đang đăng', value: counts.active, color: 'text-emerald-600' },
    { label: 'Chờ HR duyệt', value: counts.pending, color: 'text-amber-600' },
    { label: 'Bị từ chối', value: counts.rejected, color: 'text-red-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Tin tuyển dụng của tôi"
        description="Quản lý tin đã tạo — bấm vào tin để xem ứng viên & gửi duyệt"
        actions={[{ label: 'Tạo tin', href: '/recruiter/jobs/create', variant: 'primary' }]}
      />

      {error && <ErrorAlert message={error} />}

      {loading ? (
        <>
          <StatsGridSkeleton />
          <JobsGridSkeleton count={6} />
        </>
      ) : (
        <>
          <StatsGrid stats={statCards} />

          <div className="mb-6 flex flex-wrap gap-2">
            {FILTERS.map((f) => (
              <button
                key={f.value}
                onClick={() => setFilter(f.value)}
                className={`rounded-xl px-4 py-2 text-sm font-medium transition-colors ${
                  filter === f.value
                    ? 'bg-brand-600 text-white'
                    : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>

          {filtered.length === 0 ? (
            <EmptyState
              icon={<Briefcase className="h-8 w-8 text-ink-400" />}
              title="Không có tin nào"
              description={filter === 'all' ? 'Hãy tạo tin tuyển dụng đầu tiên của bạn.' : 'Không có tin ở trạng thái này.'}
              action={filter === 'all' ? { label: 'Tạo tin', href: '/recruiter/jobs/create' } : undefined}
            />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {filtered.map((j, i) => (
                <motion.div key={j.id} initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.03 }}>
                  <Link
                    to={`/recruiter/my-jobs/${j.id}`}
                    className={`group block h-full rounded-2xl border bg-white dark:bg-white/5 p-5 shadow-card transition-all hover:shadow-card-hover ${
                      j.status === 'rejected'
                        ? 'border-red-200 dark:border-red-500/30 hover:border-red-300'
                        : 'border-ink-200 dark:border-white/10 hover:border-brand-300 dark:hover:border-brand-500/40'
                    }`}
                  >
                    <div className="mb-3 flex items-start justify-between gap-3">
                      <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-50 dark:bg-brand-500/15 text-brand-600 dark:text-brand-400">
                        <Briefcase className="h-5 w-5" />
                      </span>
                      <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(j.status)}`}>
                        {jobStatusLabel(j.status)}
                      </span>
                    </div>
                    <h3 className="truncate font-semibold text-ink-900 dark:text-white group-hover:text-brand-600 dark:group-hover:text-brand-400">
                      {j.title}
                    </h3>
                    <p className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-500 dark:text-ink-400">
                      <span>{j.department || 'Chưa phân phòng ban'}</span>
                      {j.location ? <><span>·</span><span className="flex items-center gap-0.5"><MapPin className="h-3 w-3" />{j.location}</span></> : null}
                      {j.applicationDeadline ? (
                        <>
                          <span>·</span>
                          <span className="text-amber-600 dark:text-amber-400 font-medium">
                            Hạn nộp: {getDeadlineText(j.applicationDeadline)}
                          </span>
                        </>
                      ) : null}
                    </p>

                    {j.status === 'rejected' && j.rejectionReason && (
                      <p className="mt-2 rounded-lg bg-red-50 dark:bg-red-500/10 px-2.5 py-1.5 text-xs text-red-600 dark:text-red-400">
                        Lý do: {j.rejectionReason}
                      </p>
                    )}

                    <div className="mt-4 flex items-center justify-between border-t border-ink-100 dark:border-white/10 pt-3 text-xs">
                      <span className="flex items-center gap-1.5 font-medium text-ink-600 dark:text-ink-300">
                        <Users className="h-3.5 w-3.5" /> {j.applicantCount ?? 0}
                      </span>
                      <span className="flex items-center gap-1 text-ink-400">
                        <Clock className="h-3 w-3" /> {timeAgo(j.createdAt)}
                      </span>
                      <span className="text-ink-400">{formatSalary(j)}</span>
                      <ChevronRight className="h-4 w-4 text-ink-300 transition-transform group-hover:translate-x-0.5" />
                    </div>
                  </Link>
                </motion.div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}
