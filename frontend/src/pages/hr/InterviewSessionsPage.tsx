import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { useNavigate } from 'react-router-dom'
import { Search, Eye, Video, Clock, MonitorPlay } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, LoadingSpinner, ErrorAlert } from '@components/shared'
import { interviewService, type HrInterviewSessionItem } from '@services/interview/interviewService'

type StatusGroup = 'all' | 'active' | 'completed' | 'pending' | 'aborted'

const STATUS_TABS: { key: StatusGroup; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'active', label: 'Đang diễn ra' },
  { key: 'completed', label: 'Hoàn thành' },
  { key: 'pending', label: 'Đang chờ' },
  { key: 'aborted', label: 'Đã hủy' },
]

function statusMeta(status: string): { label: string; cls: string } {
  switch (status) {
    case 'completed':
      return {
        label: 'Hoàn thành',
        cls: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
      }
    case 'active':
      return {
        label: 'Đang diễn ra',
        cls: 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400',
      }
    case 'aborted':
    case 'error':
      return {
        label: status === 'error' ? 'Lỗi' : 'Đã hủy',
        cls: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
      }
    default:
      return {
        label: 'Đang chờ',
        cls: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
      }
  }
}

function verdictMeta(verdict?: string | null): { label: string; cls: string } | null {
  if (!verdict) return null
  const pass = verdict.toLowerCase() === 'pass'
  return {
    label: pass ? 'Pass' : 'Not Pass',
    cls: pass
      ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-500/20'
      : 'bg-red-50 dark:bg-red-500/10 text-red-600 dark:text-red-400 border border-red-200 dark:border-red-500/20',
  }
}

function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((n) => n[0])
    .join('')
    .toUpperCase()
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatDuration(seconds?: number | null): string | null {
  if (!seconds || seconds <= 0) return null
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return s === 0 ? `${m} phút` : `${m}p ${s}s`
}

export default function InterviewSessionsPage() {
  const navigate = useNavigate()
  const [sessions, setSessions] = useState<HrInterviewSessionItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [tab, setTab] = useState<StatusGroup>('all')

  useEffect(() => {
    let active = true
    ;(async () => {
      try {
        setLoading(true)
        const data = await interviewService.getHrSessions()
        if (active) setSessions(data)
      } catch {
        if (active) setError('Không tải được danh sách phiên phỏng vấn. Vui lòng thử lại.')
      } finally {
        if (active) setLoading(false)
      }
    })()
    return () => {
      active = false
    }
  }, [])

  const stats = useMemo(
    () => [
      { label: 'Tổng phiên', value: sessions.length, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: 'Đang diễn ra',
        value: sessions.filter((s) => s.status === 'active').length,
        color: 'text-brand-600 dark:text-brand-400',
      },
      {
        label: 'Hoàn thành',
        value: sessions.filter((s) => s.status === 'completed').length,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: 'Có ghi hình',
        value: sessions.filter((s) => s.hasRecording).length,
        color: 'text-ai-600 dark:text-ai-400',
      },
    ],
    [sessions]
  )

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return sessions.filter((s) => {
      if (tab !== 'all') {
        if (tab === 'aborted') {
          if (s.status !== 'aborted' && s.status !== 'error') return false
        } else if (s.status !== tab) {
          return false
        }
      }
      if (!q) return true
      return (
        s.candidateName.toLowerCase().includes(q) ||
        (s.jobTitle ?? '').toLowerCase().includes(q)
      )
    })
  }, [sessions, search, tab])

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Phiên phỏng vấn"
        description="Theo dõi các phiên phỏng vấn AI và xem kết quả đánh giá"
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

      <StatsGrid stats={stats} />

      {/* Bộ lọc */}
      <div className="flex flex-col lg:flex-row lg:items-center gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-ink-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên ứng viên hoặc vị trí..."
            className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500/40"
          />
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {STATUS_TABS.map((t) => (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`px-3.5 py-2 rounded-xl text-sm font-medium transition-colors ${
                tab === t.key
                  ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white'
                  : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <LoadingSpinner message="Đang tải phiên phỏng vấn..." />
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<MonitorPlay className="w-7 h-7 text-ink-400" />}
          title={sessions.length === 0 ? 'Chưa có phiên phỏng vấn' : 'Không có kết quả phù hợp'}
          description={
            sessions.length === 0
              ? 'Các phiên phỏng vấn AI sẽ xuất hiện ở đây khi ứng viên bắt đầu phỏng vấn.'
              : 'Thử thay đổi từ khóa tìm kiếm hoặc bộ lọc trạng thái.'
          }
        />
      ) : (
        <div className="space-y-4">
          {filtered.map((session, index) => {
            const sm = statusMeta(session.status)
            const vm = verdictMeta(session.verdict)
            const duration = formatDuration(session.durationSeconds)
            return (
              <motion.div
                key={session.id}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(index * 0.04, 0.3) }}
              >
                <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 lg:p-6 shadow-card hover:shadow-card-hover transition-all">
                  <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                    <div className="flex items-center gap-4 min-w-0">
                      <div className="w-12 h-12 shrink-0 rounded-full bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-sm font-semibold text-white">
                        {initials(session.candidateName)}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <h3 className="text-base lg:text-lg font-semibold text-ink-900 dark:text-white truncate">
                            {session.candidateName}
                          </h3>
                          <span className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300">
                            Vòng {session.roundNumber}
                          </span>
                          <span className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300 uppercase">
                            {session.sessionType === 'practice' ? 'Thử' : 'Thật'}
                          </span>
                        </div>
                        <p className="text-sm text-ink-500 dark:text-ink-400 truncate">
                          {session.jobTitle ?? 'Chưa rõ vị trí'}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-3 flex-wrap lg:justify-end">
                      {duration && (
                        <span className="hidden sm:flex items-center gap-1 text-xs text-ink-500 dark:text-ink-400">
                          <Clock className="w-3.5 h-3.5" />
                          {duration}
                        </span>
                      )}
                      {session.hasRecording && (
                        <span className="hidden sm:flex items-center gap-1 text-xs text-ai-600 dark:text-ai-400">
                          <Video className="w-3.5 h-3.5" />
                          Ghi hình
                        </span>
                      )}
                      <span className="text-sm text-ink-500 dark:text-ink-400">
                        {formatDate(session.createdAt)}
                      </span>
                      {vm && (
                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${vm.cls}`}>
                          {vm.label}
                        </span>
                      )}
                      <span className={`px-3 py-1 rounded-full text-xs font-medium ${sm.cls}`}>
                        {sm.label}
                      </span>
                      <button
                        type="button"
                        onClick={() =>
                          navigate(
                            session.evaluationId
                              ? `/hr/evaluations?evaluationId=${session.evaluationId}`
                              : `/hr/candidates/${session.applicationId}`
                          )
                        }
                        className="flex items-center gap-2 px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 text-sm font-medium transition-colors"
                      >
                        <Eye className="w-4 h-4" />
                        Xem
                      </button>
                    </div>
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
