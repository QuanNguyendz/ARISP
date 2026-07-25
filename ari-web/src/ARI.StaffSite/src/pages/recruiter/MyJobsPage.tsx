import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { Briefcase, Users, MapPin, Clock, ChevronRight } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState, Pagination } from '@ari/shared/ui'
import jobService from '@ari/shared/fservices/job'
import { jobStatusBadge, jobStatusLabel, formatSalary, timeAgo } from './_jobUi'
import { JobsGridSkeleton, StatsGridSkeleton } from './_skeletons'

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

export default function RecruiterMyJobsPage() {
  const { t } = useTranslation('modules/recruiter/jobs')
  const {
    data: jobsData,
    isLoading: loading,
    error: fetchError,
  } = useQuery({
    queryKey: ['my-jobs'],
    queryFn: () => jobService.getMyJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const error =
    (fetchError as any)?.response?.data?.message || (fetchError ? t('loadingError') : '')
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)

  const counts = useMemo(() => {
    const by = (s: string) => jobs.filter((j) => j.status === s).length
    return {
      all: jobs.length,
      active: by('active'),
      pending: by('pending'),
      rejected: by('rejected'),
      draft: by('draft'),
      closed: by('closed'),
    }
  }, [jobs])

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

  const statCards = [
    { label: t('stats.total'), value: counts.all, color: 'text-brand-600' },
    { label: t('status.active'), value: counts.active, color: 'text-emerald-600' },
    { label: t('status.pending'), value: counts.pending, color: 'text-amber-600' },
    { label: t('status.rejected'), value: counts.rejected, color: 'text-red-600' },
  ]

  const filters = [
    { value: 'all', label: t('filters.all') },
    { value: 'active', label: t('status.active') },
    { value: 'pending', label: t('status.pending') },
    { value: 'rejected', label: t('status.rejected') },
    { value: 'draft', label: t('status.draft') },
    { value: 'closed', label: t('status.closed') },
  ]

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[{ label: t('createJob'), href: '/recruiter/jobs/create', variant: 'primary' }]}
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
            {filters.map((f) => (
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
              title={filter === 'all' ? t('noJobs') : t('noMatchingJobs')}
              description={filter === 'all' ? t('noJobsHint') : t('noMatchingJobsHint')}
              action={
                filter === 'all'
                  ? { label: t('createJob'), href: '/recruiter/jobs/create' }
                  : undefined
              }
            />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {paged.map((j, i) => (
                <motion.div
                  key={j.id}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: i * 0.03 }}
                >
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
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(j.status)}`}
                      >
                        {jobStatusLabel(j.status)}
                      </span>
                    </div>
                    <h3 className="truncate font-semibold text-ink-900 dark:text-white group-hover:text-brand-600 dark:group-hover:text-brand-400">
                      {j.title}
                    </h3>
                    <p className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-500 dark:text-ink-400">
                      <span>{j.department || t('noDepartment')}</span>
                      {j.location ? (
                        <>
                          <span>·</span>
                          <span className="flex items-center gap-0.5">
                            <MapPin className="h-3 w-3" />
                            {j.location}
                          </span>
                        </>
                      ) : null}
                      {j.applicationDeadline ? (
                        <>
                          <span>·</span>
                          <span className="text-amber-600 dark:text-amber-400 font-medium">
                            {t('deadlineLabel')}: {getDeadlineText(j.applicationDeadline, t)}
                          </span>
                        </>
                      ) : null}
                    </p>

                    {j.status === 'rejected' && j.rejectionReason && (
                      <p className="mt-2 rounded-lg bg-red-50 dark:bg-red-500/10 px-2.5 py-1.5 text-xs text-red-600 dark:text-red-400">
                        {t('rejectionReason')}: {j.rejectionReason}
                      </p>
                    )}

                    <div className="mt-4 flex flex-col gap-1.5 border-t border-ink-100 dark:border-white/10 pt-3 text-xs sm:flex-row sm:flex-wrap sm:items-center sm:justify-between sm:gap-x-3 sm:gap-y-2">
                      <div className="flex items-center justify-between gap-2 sm:flex-1 sm:justify-start">
                        <span className="flex items-center gap-1.5 whitespace-nowrap font-medium text-ink-600 dark:text-ink-300">
                          <Users className="h-3.5 w-3.5" /> {j.applicantCount ?? 0}
                        </span>
                        <span className="flex items-center gap-1 whitespace-nowrap text-ink-400">
                          <Clock className="h-3 w-3" /> {timeAgo(j.createdAt)}
                        </span>
                      </div>
                      <div className="flex items-center justify-between gap-2 sm:flex-1 sm:justify-end">
                        <span className="min-w-0 truncate whitespace-nowrap text-ink-400 sm:text-right">
                          {formatSalary(j)}
                        </span>
                        <ChevronRight className="h-4 w-4 shrink-0 text-ink-300 transition-transform group-hover:translate-x-0.5" />
                      </div>
                    </div>
                  </Link>
                </motion.div>
              ))}
            </div>
          )}

          {filtered.length > 0 && (
            <Pagination
              page={page}
              totalPages={totalPages}
              total={filtered.length}
              label={t('paginationLabel')}
              onPageChange={setPage}
            />
          )}
        </>
      )}
    </div>
  )
}
