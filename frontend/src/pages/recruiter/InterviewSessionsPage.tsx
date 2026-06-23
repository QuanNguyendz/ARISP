import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Video, Search, Clock, FileVideo } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState } from '@components/shared'
import { interviewService, type HrInterviewSessionItem } from '@services/interview/interviewService'
import { applicationService } from '@services/application/applicationService'
import {
  sessionStatusBadge,
  sessionStatusLabel,
  verdictBadge,
  verdictLabel,
  initials,
} from './_jobUi'
import { StatsGridSkeleton, ApplicantsSkeleton } from './_skeletons'

const FILTERS: { value: string; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'active', label: 'Đang diễn ra' },
  { value: 'completed', label: 'Hoàn thành' },
  { value: 'pending', label: 'Chờ' },
]

export default function RecruiterInterviewSessionsPage() {
  const [sessions, setSessions] = useState<HrInterviewSessionItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState('all')

  useEffect(() => {
    ;(async () => {
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
        setError(e?.response?.data?.message || 'Không tải được danh sách phiên phỏng vấn.')
      } finally {
        setLoading(false)
      }
    })()
  }, [])

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
    const t = q.trim().toLowerCase()
    return sessions
      .filter((s) => (filter === 'all' ? true : s.status === filter))
      .filter((s) => (t ? (s.candidateName + (s.jobTitle || '')).toLowerCase().includes(t) : true))
  }, [sessions, q, filter])

  const statCards = [
    { label: 'Tổng phiên', value: counts.total, color: 'text-brand-600' },
    { label: 'Đang diễn ra', value: counts.active, color: 'text-amber-600' },
    { label: 'Hoàn thành', value: counts.completed, color: 'text-emerald-600' },
    { label: 'Có bản ghi', value: counts.recorded, color: 'text-ai-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Phiên phỏng vấn"
        description="Theo dõi phiên phỏng vấn AI của ứng viên thuộc tin của bạn"
      />

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
                placeholder="Tìm ứng viên, vị trí..."
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
              title="Chưa có phiên phỏng vấn"
              description="Phiên phỏng vấn AI của ứng viên sẽ xuất hiện ở đây."
            />
          ) : (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
              <div className="divide-y divide-ink-100 dark:divide-white/10">
                {filtered.map((s, i) => (
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
                        {s.jobTitle || 'Vị trí'} · Vòng {s.roundNumber} ·{' '}
                        {s.sessionType === 'practice' ? 'Thử' : 'Thật'}
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
        </>
      )}
    </div>
  )
}
