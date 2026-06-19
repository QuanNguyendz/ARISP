import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  TrendingUp,
  Gavel,
  FileText,
  Sparkles,
  KeyRound,
  ChevronRight,
  CalendarDays,
  ArrowRight,
  Clock,
  Briefcase,
  Users,
  Video,
  CheckCircle2,
  XCircle,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useAuthStore } from '@store/auth/authStore'
import { dashboardService } from '@services/dashboard/dashboardService'
import type { HrDashboard } from '@services/dashboard/dashboardService'
import { LoadingSpinner, ErrorAlert } from '@components/shared'

function KpiCard({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon
  label: string
  value: number | string
}) {
  return (
    <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card transition hover:shadow-card-hover">
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400">
            <Icon className="w-[18px] h-[18px]" />
          </span>
          {label}
        </span>
      </div>
      <div className="mt-4 flex items-end justify-between">
        <div className="font-display text-3xl font-extrabold leading-none text-ink-900 dark:text-white">
          {value}
        </div>
        <svg viewBox="0 0 72 28" className="h-7 w-20 text-brand-500">
          <polyline
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            points="0,22 12,18 24,20 36,12 48,14 60,8 72,4"
          />
        </svg>
      </div>
    </div>
  )
}

function PendingCard({ count }: { count: number }) {
  return (
    <div className="flex flex-col rounded-2xl border border-amber-300 dark:border-amber-500/30 bg-gradient-to-br from-amber-50 to-white dark:from-amber-500/10 dark:to-transparent p-5 shadow-card">
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2 text-sm font-medium text-amber-700 dark:text-amber-400">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-amber-100 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
            <Gavel className="w-[18px] h-[18px]" />
          </span>
          Chờ xác nhận
        </span>
        <span className="rounded-full bg-amber-200 dark:bg-amber-500/30 px-2 py-0.5 text-xs font-bold text-amber-800 dark:text-amber-300">
          Cần xử lý
        </span>
      </div>
      <div className="mt-4 font-display text-3xl font-extrabold leading-none text-amber-700 dark:text-amber-400">
        {count}
      </div>
      <div className="mt-1 text-sm text-amber-700/80 dark:text-amber-400/70">Verdict chờ HR xác nhận</div>
      <Link
        to="/hr/evaluations"
        className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-amber-700 dark:text-amber-400 transition-all hover:gap-2"
      >
        Xử lý ngay <ArrowRight className="w-4 h-4" />
      </Link>
    </div>
  )
}

function initials(name: string): string {
  return (name || '?')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(-2)
    .join('')
    .toUpperCase()
}

function VerdictBadge({ verdict }: { verdict?: string | null }) {
  const v = (verdict || '').toLowerCase()
  if (v === 'pass')
    return (
      <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400">
        <CheckCircle2 className="w-3.5 h-3.5" /> Pass
      </span>
    )
  if (v === 'not_pass' || v === 'notpass')
    return (
      <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400">
        <XCircle className="w-3.5 h-3.5" /> Not Pass
      </span>
    )
  return (
    <span className="inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400">
      <Clock className="w-3.5 h-3.5" /> Chờ duyệt
    </span>
  )
}

const todayLabel = new Date().toLocaleDateString('vi-VN', {
  weekday: 'long',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

export default function HrDashboardPage() {
  const user = useAuthStore((s) => s.user)
  const [data, setData] = useState<HrDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        const d = await dashboardService.getHrOverview()
        if (active) setData(d)
      } catch (err) {
        if (active) setError(err instanceof Error ? err.message : 'Không tải được dữ liệu tổng quan.')
      } finally {
        if (active) setLoading(false)
      }
    })()
    return () => {
      active = false
    }
  }, [])

  const funnelMax = data?.funnel?.[0]?.value || 0

  return (
    <main className="p-6 space-y-6 bg-ink-50 dark:bg-ink-950 min-h-screen">
      {/* Page header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-extrabold leading-snug text-ink-900 dark:text-white">
            Xin chào, {user?.name || 'HR'} 👋
          </h1>
          <p className="mt-1 flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <CalendarDays className="w-4 h-4" />
            {todayLabel} · Tổng quan tuyển dụng
          </p>
        </div>
      </div>

      {loading && <LoadingSpinner message="Đang tải tổng quan..." />}
      {!loading && error && <ErrorAlert message={error} />}

      {!loading && !error && data && (
        <>
          {/* KPI */}
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <KpiCard icon={Briefcase} label="Tin đang tuyển" value={data.activeJobs} />
            <KpiCard icon={Users} label="Ứng viên đang xử lý" value={data.totalApplications} />
            <KpiCard icon={Video} label="Phỏng vấn AI" value={data.aiInterviews} />
            <PendingCard count={data.pendingReviews} />
          </div>

          {/* Recruitment funnel */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
            <div className="mb-5 flex items-center justify-between">
              <div>
                <h2 className="font-display text-lg font-bold text-ink-900 dark:text-white">Phễu tuyển dụng</h2>
                <p className="text-sm text-ink-400 dark:text-ink-500">Tỉ lệ chuyển đổi qua từng bước</p>
              </div>
              <span className="hidden sm:inline-flex items-center gap-1 rounded-lg bg-emerald-50 dark:bg-emerald-500/20 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:text-emerald-400">
                <TrendingUp className="w-3.5 h-3.5" /> {data.hired} tuyển
              </span>
            </div>
            <div className="space-y-3">
              {data.funnel.map((item, i) => {
                const percent = funnelMax > 0 ? Math.round((item.value / funnelMax) * 100) : 0
                return (
                  <div key={item.label} className="flex items-center gap-3">
                    <div className="w-32 shrink-0 text-sm font-medium text-ink-600 dark:text-ink-400">
                      {item.label}
                    </div>
                    <div className="h-8 flex-1 rounded-lg bg-ink-100 dark:bg-white/10">
                      <div
                        className={`h-full rounded-lg ${
                          i === data.funnel.length - 1
                            ? 'bg-gradient-to-r from-emerald-500 to-emerald-600'
                            : 'bg-gradient-to-r from-brand-600 to-ai-600'
                        }`}
                        style={{ width: `${Math.max(percent, item.value > 0 ? 4 : 0)}%` }}
                      />
                    </div>
                    <div className="w-10 shrink-0 text-right text-sm font-bold text-ink-900 dark:text-white">
                      {item.value}
                    </div>
                    <div className="w-12 shrink-0 text-right text-xs text-ink-400 dark:text-ink-500">
                      {i === 0 ? '—' : `${percent}%`}
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
            {/* Recent candidates */}
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card overflow-hidden">
              <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 border-b border-ink-100 dark:border-white/10">
                <h2 className="font-display font-bold text-ink-900 dark:text-white">Ứng viên gần đây</h2>
                <Link to="/hr/candidates" className="text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
                  Xem tất cả
                </Link>
              </div>
              {data.recentCandidates.length === 0 ? (
                <p className="px-5 py-8 text-center text-sm text-ink-400">Chưa có ứng viên nào.</p>
              ) : (
                <table className="w-full text-sm">
                  <thead className="text-left bg-ink-50/50 dark:bg-white/5">
                    <tr>
                      <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Ứng viên</th>
                      <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Vị trí</th>
                      <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Vòng</th>
                      <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Verdict AI</th>
                      <th className="font-medium px-5 py-3" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                    {data.recentCandidates.map((c) => (
                      <tr key={c.id} className="hover:bg-ink-50/60 dark:hover:bg-white/5">
                        <td className="px-5 py-3">
                          <div className="flex items-center gap-3">
                            <div className="grid h-9 w-9 place-items-center rounded-full bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 text-xs font-bold">
                              {initials(c.candidateName)}
                            </div>
                            <div>
                              <div className="font-medium text-ink-900 dark:text-white">
                                {c.candidateName || 'Ẩn danh'}
                              </div>
                              <div className="text-xs text-ink-400">
                                {typeof c.matchScore === 'number' ? `Match ${c.matchScore}` : 'Chưa phân tích'}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-3 text-ink-600 dark:text-ink-400">{c.jobTitle || '—'}</td>
                        <td className="px-5 py-3">
                          <span className="rounded-full px-2.5 py-1 text-xs font-semibold bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400">
                            {c.latestRound ? `R${c.latestRound}` : '—'}
                          </span>
                        </td>
                        <td className="px-5 py-3">
                          <VerdictBadge verdict={c.latestVerdict} />
                        </td>
                        <td className="px-5 py-3 text-right">
                          <Link
                            to={`/hr/candidates/${c.id}`}
                            className="rounded-lg px-3 py-1.5 text-xs font-semibold text-brand-600 hover:bg-brand-50 dark:hover:bg-brand-500/10"
                          >
                            Xem
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {/* Side */}
            <div className="space-y-5">
              {/* Cần làm */}
              <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
                <h3 className="font-display font-bold mb-3 text-ink-900 dark:text-white">Cần làm</h3>
                <div className="space-y-1 text-sm">
                  <Link
                    to="/hr/evaluations"
                    className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                      <Gavel className="w-4 h-4" />
                    </span>
                    <span className="flex-1 text-ink-700 dark:text-ink-200">Xác nhận verdict phỏng vấn</span>
                    <span className="rounded-full bg-amber-100 dark:bg-amber-500/20 px-1.5 text-xs font-bold text-amber-700 dark:text-amber-400">
                      {data.pendingReviews}
                    </span>
                    <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
                  </Link>
                  <Link
                    to="/hr/jobs/pending"
                    className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400">
                      <FileText className="w-4 h-4" />
                    </span>
                    <span className="flex-1 text-ink-700 dark:text-ink-200">Duyệt tin nháp</span>
                    <span className="rounded-full bg-ink-100 dark:bg-white/10 px-1.5 text-xs font-bold text-ink-600 dark:text-ink-400">
                      {data.draftJobs}
                    </span>
                    <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
                  </Link>
                  <Link
                    to="/hr/interviews"
                    className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400">
                      <KeyRound className="w-4 h-4" />
                    </span>
                    <span className="flex-1 text-ink-700 dark:text-ink-200">Quản lý phiên phỏng vấn</span>
                    <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
                  </Link>
                </div>
              </div>

              {/* AI Insight (tip) */}
              <div className="rounded-2xl border border-ai-200 dark:border-ai-500/30 bg-gradient-to-b from-ai-50/70 to-white dark:from-ai-500/10 dark:to-transparent p-5 shadow-card">
                <div className="flex items-center gap-2 text-sm font-semibold text-ai-700 dark:text-ai-400">
                  <Sparkles className="w-4 h-4" />
                  AI Insight
                </div>
                <p className="mt-2 text-sm text-ink-600 dark:text-ink-300 leading-relaxed">
                  Có <b className="text-ink-900 dark:text-white">{data.totalApplications}</b> hồ sơ trong hệ thống,{' '}
                  <b className="text-ink-900 dark:text-white">{data.pendingReviews}</b> verdict đang chờ bạn xác nhận.
                  Ưu tiên xử lý để không làm chậm ứng viên.
                </p>
                <Link
                  to="/hr/candidates"
                  className="mt-3 block w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-center text-sm font-semibold text-white"
                >
                  Xem ứng viên
                </Link>
              </div>
            </div>
          </div>
        </>
      )}
    </main>
  )
}
