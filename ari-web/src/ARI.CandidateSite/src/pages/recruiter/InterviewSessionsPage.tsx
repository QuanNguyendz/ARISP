import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { Video, Search, Clock, FileVideo } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState, Pagination } from '@ari/shared/ui'
import { interviewService, type HrInterviewSessionItem } from '@ari/shared/fservices/interview'
import { applicationService } from '@ari/shared/fservices/application'
import {
  sessionStatusBadge,
  sessionStatusLabel,
  verdictBadge,
  verdictLabel,
  initials,
} from './_jobUi'
import { StatsGridSkeleton, ApplicantsSkeleton } from './_skeletons'

export default function RecruiterInterviewSessionsPage() {
  const { t } = useTranslation('modules/recruiter/interviews')

  const [sessions, setSessions] = useState<HrInterviewSessionItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)

  useEffect(() => {
    (async () => {
      setLoading(true)
      setError('')
      try {
        const [all, myApps] = await Promise.all([
          interviewService.getHrSessions(),
          applicationService.getApplications(true),
        ])
        const myIds = new Set(myApps.map((a) => a.id))
        setSessions(all.filter((s) => myIds.has(s.applicationId)))
      } catch (e: any) {
        setError(e?.response?.data?.message || t('loadingError'))
      } finally {
        setLoading(false)
      }
    })()
  }, [t])

  const counts = useMemo(() => {
    const by = (s: string) => sessions.filter((x) => x.status === s).length
    return {
      total: sessions.length,
      active: by('active'),
      completed: by('completed'),
      recorded: sessions.filter((x) => x.hasRecording).length,
    }
  }, [sessions])

  const filtered = useMemo(() => {
    const searchTerm = q.trim().toLowerCase()
    return sessions
      .filter((s) => (filter === 'all' ? true : s.status === filter))
      .filter((s) =>
        searchTerm
          ? (s.candidateName + (s.jobTitle || '')).toLowerCase().includes(searchTerm)
          : true
      )
  }, [sessions, q, filter])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  useEffect(() => {
    setPage(1)
  }, [q, filter])

  const statCards = [
    { label: t('stats.totalSessions'), value: counts.total, color: 'text-brand-600' },
    { label: t('stats.active'), value: counts.active, color: 'text-amber-600' },
    { label: t('stats.completed'), value: counts.completed, color: 'text-emerald-600' },
    { label: t('stats.recorded'), value: counts.recorded, color: 'text-ai-600' },
  ]

  const FILTERS = [
    { value: 'all', label: t('filters.all') },
    { value: 'active', label: t('filters.active') },
    { value: 'completed', label: t('filters.completed') },
    { value: 'pending', label: t('filters.pending') },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('description')} />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <>
          <StatsGridSkeleton />
          <ApplicantsSkeleton rows={6} />
        </>
      ) : (
        <>
          <StatsGrid stats={statCards} />

          <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 sm:max-w-xs sm:flex-1">
              <Search className="h-4 w-4 text-ink-400" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t('searchPlaceholder')}
                className="w-full bg-transparent text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400"
              />
            </div>
            <div className="flex flex-wrap gap-2">
              {FILTERS.map((f) => (
                <button
                  key={f.value}
                  onClick={() => setFilter(f.value)}
                  className={`rounded-lg px-3 py-1.5 text-xs font-medium transition-colors ${
                    filter === f.value
                      ? 'bg-brand-600 text-white'
                      : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                  }`}
                >
                  {f.label}
                </button>
              ))}
            </div>
          </div>

          {filtered.length === 0 ? (
            <EmptyState
              icon={<Video className="h-8 w-8 text-ink-400" />}
              title={t('noSessions')}
              description={t('noSessionsHint')}
            />
          ) : (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
              <div className="divide-y divide-ink-100 dark:divide-white/10">
                {paged.map((s, i) => (
                  <motion.div
                    key={s.id}
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: Math.min(i, 12) * 0.02 }}
                    className="flex items-center gap-4 px-5 py-4"
                  >
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                      {initials(s.candidateName)}
                    </span>
                    <div className="min-w-0 flex-1">
                      <Link
                        to={`/recruiter/candidates/${s.applicationId}`}
                        className="truncate text-sm font-medium text-ink-900 dark:text-white hover:text-brand-600 dark:hover:text-brand-400"
                      >
                        {s.candidateName}
                      </Link>
                      <p className="truncate text-xs text-ink-500 dark:text-ink-400">
                        {s.jobTitle || t('position')} · {t('round')} {s.roundNumber} ·{' '}
                        {s.sessionType === 'practice' ? t('practice') : t('real')}
                      </p>
                    </div>
                    {s.hasRecording && (
                      <FileVideo className="hidden h-4 w-4 text-ink-400 sm:block" />
                    )}
                    <span className="hidden items-center gap-1 text-xs text-ink-400 md:flex">
                      <Clock className="h-3 w-3" />{' '}
                      {s.durationSeconds ? `${Math.round(s.durationSeconds / 60)}′` : '—'}
                    </span>
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
                  </motion.div>
                ))}
              </div>
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
