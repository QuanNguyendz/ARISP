import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Search, Users, FileText, Eye } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState, Pagination } from '@components/shared'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import { applicationService } from '@services/application/applicationService'
import type { HrApplicationItem } from '@/types/application'
import { appStatusBadge, appStatusLabel, initials, scoreColor } from './_jobUi'
import { StatsGridSkeleton, ApplicantsSkeleton } from './_skeletons'
import { resolveAssetUrl } from '@/config/constants'

const FILTERS: { value: string; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'cv_submitted', label: 'Mới ứng tuyển' },
  { value: 'screening', label: 'Đang sơ loại' },
  { value: 'interview', label: 'Phỏng vấn' },
  { value: 'pass', label: 'Đạt' },
  { value: 'not_pass', label: 'Không đạt' },
]

function formatDate(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? '—'
    : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default function RecruiterCandidatesPage() {
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)

  useEffect(() => {
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        setApps(await applicationService.getApplications(true))
      } catch (e) {
        const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
        setError(msg || 'Không tải được danh sách ứng viên.')
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const counts = useMemo(() => {
    const by = (s: string) => apps.filter((a) => a.status === s).length
    return {
      total: apps.length,
      screening: by('screening'),
      interview: by('interview'),
      pass: by('pass'),
    }
  }, [apps])

  const filtered = useMemo(() => {
    const t = q.trim().toLowerCase()
    return apps
      .filter((a) => (filter === 'all' ? true : a.status === filter))
      .filter((a) =>
        t
          ? (a.candidateName + a.candidateEmail + (a.jobTitle || '')).toLowerCase().includes(t)
          : true
      )
  }, [apps, q, filter])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  // Về trang 1 khi đổi từ khóa/bộ lọc
  useEffect(() => {
    setPage(1)
  }, [q, filter])

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
            <EmptyState
              icon={<Users className="h-8 w-8 text-ink-400" />}
              title="Không có ứng viên"
              description="Không có ứng viên khớp bộ lọc hiện tại."
            />
          ) : (
            <div className="p-2 sm:p-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b border-ink-200 dark:border-white/10">
                      {['Ứng viên', 'Vị trí', 'Trạng thái', 'Điểm CV', 'Ngày ứng tuyển'].map((h) => (
                        <th
                          key={h}
                          className="text-left py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400"
                        >
                          {h}
                        </th>
                      ))}
                      <th className="text-right py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400">
                        Thao tác
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {paged.map((a, i) => (
                      <motion.tr
                        key={a.id}
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ delay: Math.min(i * 0.03, 0.3) }}
                        onClick={() => navigate(`/recruiter/candidates/${a.id}`)}
                        className="border-b border-ink-100 dark:border-white/5 hover:bg-ink-50 dark:hover:bg-white/[0.02] cursor-pointer transition-colors"
                      >
                        <td className="py-4 px-4">
                          <div className="flex items-center gap-3">
                            <div className="w-10 h-10 rounded-full bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-xs font-bold text-white shrink-0">
                              {initials(a.candidateName || a.candidateEmail)}
                            </div>
                            <div className="min-w-0">
                              <p className="font-medium text-ink-900 dark:text-white truncate">
                                {a.candidateName || 'Ứng viên'}
                              </p>
                              <p className="text-sm text-ink-600 dark:text-ink-400 truncate">
                                {a.candidateEmail}
                              </p>
                            </div>
                          </div>
                        </td>
                        <td className="py-4 px-4 text-ink-700 dark:text-ink-200">
                          {a.jobTitle || '—'}
                        </td>
                        <td className="py-4 px-4">
                          <span
                            className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${appStatusBadge(a.status)}`}
                          >
                            {appStatusLabel(a.status)}
                          </span>
                        </td>
                        <td className="py-4 px-4">
                          {typeof a.matchScore === 'number' ? (
                            <span className={`font-semibold ${scoreColor(a.matchScore)}`}>
                              {a.matchScore}
                            </span>
                          ) : (
                            <span className="text-ink-400">—</span>
                          )}
                        </td>
                        <td className="py-4 px-4 text-ink-600 dark:text-ink-400">
                          {formatDate(a.createdAt)}
                        </td>
                        <td className="py-4 px-4">
                          <div className="flex items-center justify-end gap-2">
                            {a.cvFileUrl && (
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation()
                                  openDocument(
                                    resolveAssetUrl(a.cvFileUrl),
                                    `${a.candidateName || 'Ứng viên'} - CV`
                                  )
                                }}
                                title="Xem CV"
                                className="p-2 rounded-lg hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
                              >
                                <FileText className="w-4 h-4 text-ink-500" />
                              </button>
                            )}
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation()
                                navigate(`/recruiter/candidates/${a.id}`)
                              }}
                              title="Xem chi tiết"
                              className="p-2 rounded-lg hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
                            >
                              <Eye className="w-4 h-4 text-ink-500" />
                            </button>
                          </div>
                        </td>
                      </motion.tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {filtered.length > 0 && (
            <Pagination
              page={page}
              totalPages={totalPages}
              total={filtered.length}
              label="ứng viên"
              onPageChange={setPage}
            />
          )}
        </>
      )}
    </div>
  )
}
