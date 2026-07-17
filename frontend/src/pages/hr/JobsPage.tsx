import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { Users, MapPin, Briefcase, Building2, Calendar, Languages, Zap } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, Pagination } from '@components/shared'
import { HrStatsSkeleton, JobListSkeleton } from './_skeletons'
import { jobService } from '@services/job/jobService'

type StatusKey = 'draft' | 'active' | 'paused' | 'closed'
type FilterKey = 'all' | StatusKey

function formatDate(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function getDeadlineText(deadlineStr?: string | null, t: (key: string) => string): string {
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

export default function HrJobsPage() {
  const { t } = useTranslation('modules/hr/jobs')
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
  const error =
    (fetchError as any)?.response?.data?.message || (fetchError ? t('loadingError') : '')
  const [filter, setFilter] = useState<FilterKey>('all')
  const [page, setPage] = useState(1)

  const statusMeta: Record<StatusKey, { label: string; badge: string }> = {
    active: {
      label: t('status.active'),
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    draft: {
      label: t('status.draft'),
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    paused: {
      label: t('status.paused'),
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    closed: {
      label: t('status.closed'),
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
    },
  }

  const filters: { key: FilterKey; label: string }[] = [
    { key: 'all', label: t('filters.all') },
    { key: 'active', label: t('status.active') },
    { key: 'draft', label: t('status.draft') },
    { key: 'paused', label: t('status.paused') },
    { key: 'closed', label: t('status.closed') },
  ]

  const stats = useMemo(() => {
    const count = (s: StatusKey) => jobs.filter((j) => j.status === s).length
    return [
      { label: t('stats.total'), value: jobs.length, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: t('status.active'),
        value: count('active'),
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: t('status.draft'),
        value: count('draft'),
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: t('status.closed'),
        value: count('closed'),
        color: 'text-ink-600 dark:text-ink-400',
      },
    ]
  }, [jobs, t])

  const filtered = useMemo(
    () => (filter === 'all' ? jobs : jobs.filter((j) => j.status === filter)),
    [jobs, filter]
  )

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  useEffect(() => {
    setPage(1)
  }, [filter])

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title')} description={t('subtitle')} />

      {loading && <HrStatsSkeleton />}
      {!loading && !error && <StatsGrid stats={stats} />}

      {!loading && !error && jobs.length > 0 && (
        <div className="flex flex-wrap gap-2 mb-6">
          {filters.map((f) => {
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
          title={jobs.length === 0 ? t('noJobs') : t('noMatchingJobs')}
          description={jobs.length === 0 ? t('noJobsHint') : t('noMatchingJobsHint')}
        />
      )}

      {!loading && !error && filtered.length > 0 && (
        <div className="space-y-4">
          {paged.map((job, index) => {
            const meta = statusMeta[job.status as StatusKey] ?? statusMeta.draft
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
                            <Zap className="w-3 h-3" /> {t('urgent')}
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
                          {job.applicantCount ?? 0} {t('applicants')}
                        </span>
                        {job.applicationDeadline && (
                          <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                            <Calendar className="w-4 h-4" />
                            {t('deadlineLabel')}: {getDeadlineText(job.applicationDeadline, t)}
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
                      {t('viewDetails')}
                    </Link>
                  </div>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {!loading && !error && filtered.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={filtered.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}
    </div>
  )
}
