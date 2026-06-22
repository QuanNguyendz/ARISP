import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import { Search, Mail, Eye, Loader2 } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, NoticeAlert } from '@components/shared'
import { HrStatsSkeleton, CandidatesTableSkeleton } from './_skeletons'
import { applicationService } from '@services/application/applicationService'
import type { HrApplicationItem } from '@/types/application'

type Group = 'pending' | 'interviewing' | 'passed' | 'rejected' | 'other'

interface StatusMeta {
  label: string
  group: Group
  badge: string
}

function statusMeta(status: string): StatusMeta {
  const s = (status || '').toLowerCase()
  const map: Record<string, StatusMeta> = {
    invited: {
      label: 'Đã mời',
      group: 'pending',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    },
    cv_submitted: {
      label: 'Đã nộp CV',
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    pending: {
      label: 'Chờ duyệt',
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    pending_review: {
      label: 'Chờ duyệt',
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    screening: {
      label: 'Đang phỏng vấn',
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview: {
      label: 'Đang phỏng vấn',
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview_code_generated: {
      label: 'Đã cấp mã',
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    interview_code_used: {
      label: 'Đang phỏng vấn',
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    practice: {
      label: 'Phỏng vấn thử',
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    pass: {
      label: 'Đạt',
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    approved: {
      label: 'Đạt',
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    completed: {
      label: 'Hoàn thành',
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    not_pass: {
      label: 'Không đạt',
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    rejected: {
      label: 'Không đạt',
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    failed: {
      label: 'Không đạt',
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    withdrawn: {
      label: 'Đã rút',
      group: 'rejected',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
    },
  }
  return (
    map[s] ?? {
      label: status || '—',
      group: 'other',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    }
  )
}

const FILTERS: { key: 'all' | Group; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'pending', label: 'Chờ duyệt' },
  { key: 'interviewing', label: 'Đang phỏng vấn' },
  { key: 'passed', label: 'Đạt' },
  { key: 'rejected', label: 'Không đạt' },
]

function initials(name: string): string {
  return (name || '?')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(-2)
    .join('')
    .toUpperCase()
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? '—'
    : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function scoreColor(score: number): string {
  if (score >= 80) return 'text-emerald-600 dark:text-emerald-400'
  if (score >= 60) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

export default function CandidatesPage() {
  const navigate = useNavigate()
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<'all' | Group>('all')
  const [invitingId, setInvitingId] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        const data = await applicationService.getApplications()
        if (active) setApps(data)
      } catch (err) {
        if (active)
          setError(err instanceof Error ? err.message : 'Không tải được danh sách ứng viên.')
      } finally {
        if (active) setLoading(false)
      }
    })()
    return () => {
      active = false
    }
  }, [])

  const stats = useMemo(() => {
    const byGroup = (g: Group) => apps.filter((a) => statusMeta(a.status).group === g).length
    return [
      { label: 'Tổng ứng viên', value: apps.length, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: 'Chờ duyệt',
        value: byGroup('pending'),
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: 'Đang phỏng vấn',
        value: byGroup('interviewing'),
        color: 'text-violet-600 dark:text-violet-400',
      },
      { label: 'Đạt', value: byGroup('passed'), color: 'text-emerald-600 dark:text-emerald-400' },
    ]
  }, [apps])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return apps.filter((a) => {
      const matchesFilter = filter === 'all' || statusMeta(a.status).group === filter
      const matchesSearch =
        !q ||
        a.candidateName?.toLowerCase().includes(q) ||
        a.candidateEmail?.toLowerCase().includes(q) ||
        a.jobTitle?.toLowerCase().includes(q)
      return matchesFilter && matchesSearch
    })
  }, [apps, search, filter])

  const handleInvite = async (e: React.MouseEvent, app: HrApplicationItem) => {
    e.stopPropagation()
    setNotice('')
    setError('')
    setInvitingId(app.id)
    try {
      await applicationService.sendInvite(app.id)
      setNotice(`Đã gửi magic link mời phỏng vấn tới ${app.candidateEmail}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Gửi lời mời thất bại.')
    } finally {
      setInvitingId(null)
    }
  }

  return (
    <div className="min-h-screen bg-ink-50 dark:bg-ink-950 p-6 lg:p-8">
      <PageHeader title="Ứng viên" description="Quản lý và theo dõi tất cả hồ sơ ứng tuyển" />

      {notice && <NoticeAlert message={notice} onDismiss={() => setNotice('')} />}
      {!loading && error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading && <HrStatsSkeleton />}
      {!loading && !error && <StatsGrid stats={stats} />}

      {/* Search + filter */}
      {!loading && !error && apps.length > 0 && (
        <div className="p-4 rounded-2xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10 mb-6">
          <div className="flex flex-col md:flex-row gap-4 md:items-center">
            <div className="flex-1 relative">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-ink-400" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Tìm theo tên, email, vị trí..."
                className="w-full pl-12 pr-4 py-3 rounded-xl bg-ink-50 dark:bg-white/5 border border-ink-200 dark:border-white/10 text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
              />
            </div>
            <div className="flex flex-wrap gap-2">
              {FILTERS.map((f) => {
                const activeTab = filter === f.key
                return (
                  <button
                    key={f.key}
                    onClick={() => setFilter(f.key)}
                    className={`px-3.5 py-2 rounded-xl text-sm font-medium transition-all ${
                      activeTab
                        ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white'
                        : 'border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'
                    }`}
                  >
                    {f.label}
                  </button>
                )
              })}
            </div>
          </div>
        </div>
      )}

      {loading && <CandidatesTableSkeleton rows={6} />}

      {!loading && !error && filtered.length === 0 && (
        <EmptyState
          icon={<Search className="w-8 h-8 text-ink-400" />}
          title={apps.length === 0 ? 'Chưa có ứng viên' : 'Không tìm thấy ứng viên'}
          description={
            apps.length === 0
              ? 'Khi ứng viên nộp hồ sơ qua Job Board, họ sẽ xuất hiện tại đây.'
              : 'Thử từ khoá khác hoặc bỏ bớt bộ lọc.'
          }
        />
      )}

      {!loading && !error && filtered.length > 0 && (
        <div className="p-2 sm:p-4 rounded-2xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-ink-200 dark:border-white/10">
                  {['Ứng viên', 'Vị trí', 'Trạng thái', 'Match', 'Ngày ứng tuyển'].map((h) => (
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
                {filtered.map((app, index) => {
                  const meta = statusMeta(app.status)
                  return (
                    <motion.tr
                      key={app.id}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: Math.min(index * 0.03, 0.3) }}
                      onClick={() => navigate(`/hr/candidates/${app.id}`)}
                      className="border-b border-ink-100 dark:border-white/5 hover:bg-ink-50 dark:hover:bg-white/[0.02] cursor-pointer transition-colors"
                    >
                      <td className="py-4 px-4">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-full bg-gradient-to-br from-brand-500 to-ai-500 flex items-center justify-center text-sm font-medium text-white shrink-0">
                            {initials(app.candidateName)}
                          </div>
                          <div className="min-w-0">
                            <p className="font-medium text-ink-900 dark:text-white truncate">
                              {app.candidateName || 'Ẩn danh'}
                            </p>
                            <p className="text-sm text-ink-600 dark:text-ink-400 truncate">
                              {app.candidateEmail}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="py-4 px-4 text-ink-700 dark:text-ink-200">
                        {app.jobTitle || '—'}
                      </td>
                      <td className="py-4 px-4">
                        <span
                          className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${meta.badge}`}
                        >
                          {meta.label}
                        </span>
                      </td>
                      <td className="py-4 px-4">
                        {typeof app.matchScore === 'number' ? (
                          <span className={`font-semibold ${scoreColor(app.matchScore)}`}>
                            {app.matchScore}
                          </span>
                        ) : (
                          <span className="text-ink-400">—</span>
                        )}
                      </td>
                      <td className="py-4 px-4 text-ink-600 dark:text-ink-400">
                        {formatDate(app.createdAt)}
                      </td>
                      <td className="py-4 px-4">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={(e) => handleInvite(e, app)}
                            disabled={invitingId === app.id}
                            title="Gửi magic link mời phỏng vấn"
                            className="p-2 rounded-lg hover:bg-ink-100 dark:hover:bg-white/10 transition-colors disabled:opacity-50"
                          >
                            {invitingId === app.id ? (
                              <Loader2 className="w-4 h-4 text-ink-500 animate-spin" />
                            ) : (
                              <Mail className="w-4 h-4 text-ink-500" />
                            )}
                          </button>
                          <button
                            onClick={(e) => {
                              e.stopPropagation()
                              navigate(`/hr/candidates/${app.id}`)
                            }}
                            title="Xem chi tiết"
                            className="p-2 rounded-lg hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
                          >
                            <Eye className="w-4 h-4 text-ink-500" />
                          </button>
                        </div>
                      </td>
                    </motion.tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}
