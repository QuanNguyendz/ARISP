import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Search, Users, Mail, FileText, ChevronRight } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState } from '@components/shared'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import { applicationService } from '@services/application/applicationService'
import type { HrApplicationItem } from '@/types/application'
import { appStatusBadge, appStatusLabel, initials, scoreColor, timeAgo } from './_jobUi'
import { StatsGridSkeleton, ApplicantsSkeleton } from './_skeletons'

const FILTERS: { value: string; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'cv_submitted', label: 'Mới ứng tuyển' },
  { value: 'screening', label: 'Đang sơ loại' },
  { value: 'interview', label: 'Phỏng vấn' },
  { value: 'pass', label: 'Đạt' },
  { value: 'not_pass', label: 'Không đạt' },
]

export default function RecruiterCandidatesPage() {
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState('all')

  useEffect(() => {
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        setApps(await applicationService.getApplications(true))
      } catch (e: any) {
        setError(e?.response?.data?.message || 'Không tải được danh sách ứng viên.')
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const counts = useMemo(() => {
    const by = (s: string) => apps.filter((a) => a.status === s).length
    return { total: apps.length, screening: by('screening'), interview: by('interview'), pass: by('pass') }
  }, [apps])

  const filtered = useMemo(() => {
    const t = q.trim().toLowerCase()
    return apps
      .filter((a) => (filter === 'all' ? true : a.status === filter))
      .filter((a) => (t ? (a.candidateName + a.candidateEmail + (a.jobTitle || '')).toLowerCase().includes(t) : true))
  }, [apps, q, filter])

  const statCards = [
    { label: 'Tổng ứng viên', value: counts.total, color: 'text-brand-600' },
    { label: 'Đang sơ loại', value: counts.screening, color: 'text-amber-600' },
    { label: 'Phỏng vấn', value: counts.interview, color: 'text-ai-600' },
    { label: 'Đạt', value: counts.pass, color: 'text-emerald-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title="Ứng viên" description="Ứng viên ứng tuyển vào các tin của bạn" />

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
            <EmptyState icon={<Users className="h-8 w-8 text-ink-400" />} title="Không có ứng viên" description="Không có ứng viên khớp bộ lọc hiện tại." />
          ) : (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
              <div className="divide-y divide-ink-100 dark:divide-white/10">
                {filtered.map((a, i) => (
                  <motion.div
                    key={a.id}
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: Math.min(i, 12) * 0.02 }}
                    onClick={() => navigate(`/recruiter/candidates/${a.id}`)}
                    className="flex cursor-pointer items-center gap-4 px-5 py-4 hover:bg-ink-50 dark:hover:bg-white/5"
                  >
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                      {initials(a.candidateName || a.candidateEmail)}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-ink-900 dark:text-white">{a.candidateName || 'Ứng viên'}</p>
                      <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                        <Mail className="h-3 w-3" /> {a.candidateEmail} · {a.jobTitle || 'Vị trí'}
                      </p>
                    </div>
                    <span className={`hidden text-sm font-bold sm:block ${scoreColor(a.matchScore)}`}>
                      {a.matchScore != null ? `${a.matchScore}%` : '—'}
                    </span>
                    <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(a.status)}`}>{appStatusLabel(a.status)}</span>
                    <span className="hidden text-xs text-ink-400 md:block">{timeAgo(a.createdAt)}</span>
                    {a.cvFileUrl && (
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation()
                          openDocument(a.cvFileUrl!, `${a.candidateName || 'Ứng viên'} - CV`)
                        }}
                        className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-brand-600 dark:hover:bg-white/10"
                        title="Xem CV"
                      >
                        <FileText className="h-4 w-4" />
                      </button>
                    )}
                    <ChevronRight className="h-4 w-4 text-ink-300" />
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
