import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Responsive, WidthProvider } from 'react-grid-layout'
import type { Layout, Layouts } from 'react-grid-layout'
import 'react-grid-layout/css/styles.css'
import 'react-resizable/css/styles.css'
import {
  TrendingUp,
  Gavel,
  ChevronRight,
  CalendarDays,
  ArrowRight,
  Clock,
  Briefcase,
  Users,
  Video,
  CheckCircle2,
  XCircle,
  Target,
  Trophy,
  BarChart3,
  GripVertical,
  RotateCcw,
  Sparkles,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  BarChart,
  Bar,
  Cell,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from 'recharts'
import { useQuery } from '@tanstack/react-query'
import { useAuthStore } from '@store/auth/authStore'
import { dashboardService } from '@services/dashboard/dashboardService'
import { ErrorAlert } from '@components/shared'
import { HrDashboardSkeleton } from './_skeletons'
import { jobStatusBadge, jobStatusLabel } from '../recruiter/_jobUi'

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

// ===== Phân tích tuyển dụng (backend tính sẵn) =====
// Tone cho 5 mức điểm match theo thứ tự backend trả (85–100 → dưới 50)
const TONE_ORDER = ['emerald', 'brand', 'ai', 'amber', 'red']

interface TrendTooltipProps {
  active?: boolean
  payload?: Array<{ payload: { label: string; count: number } }>
}

function TrendTooltip({ active, payload }: TrendTooltipProps) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className="rounded-lg border border-ink-200 bg-white px-3 py-1.5 text-xs shadow-card dark:border-white/10 dark:bg-ink-900">
      <div className="font-semibold text-ink-900 dark:text-white">Ngày {p.label}</div>
      <div className="font-medium text-brand-600 dark:text-brand-400">{p.count} hồ sơ</div>
    </div>
  )
}

function TrendChart({ trend }: { trend: { label: string; count: number }[] }) {
  if (!trend.length) {
    return (
      <div className="grid h-full min-h-[120px] place-items-center text-sm text-ink-400">
        Chưa có dữ liệu ứng tuyển.
      </div>
    )
  }

  const purple = 'rgb(124 58 237)' // brand/violet
  const axis = '#94a3b8' // slate-400 — đọc được trên cả nền sáng & tối

  return (
    <div className="h-full min-h-[140px] w-full">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={trend} margin={{ top: 8, right: 6, bottom: 0, left: -18 }}>
          <defs>
            <linearGradient id="hr-trend-fill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={purple} stopOpacity={0.25} />
              <stop offset="100%" stopColor={purple} stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid
            strokeDasharray="3 3"
            stroke={axis}
            strokeOpacity={0.18}
            vertical={false}
          />
          <XAxis
            dataKey="label"
            tick={{ fontSize: 10, fill: axis }}
            tickLine={false}
            axisLine={false}
            interval="preserveStartEnd"
            minTickGap={16}
          />
          <YAxis
            allowDecimals={false}
            width={34}
            tick={{ fontSize: 10, fill: axis }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip
            content={<TrendTooltip />}
            cursor={{ stroke: purple, strokeOpacity: 0.3, strokeWidth: 1 }}
          />
          <Area
            type="monotone"
            dataKey="count"
            stroke={purple}
            strokeWidth={2}
            fill="url(#hr-trend-fill)"
            dot={{ r: 2.5, fill: purple, strokeWidth: 0 }}
            activeDot={{ r: 4, fill: purple, stroke: '#fff', strokeWidth: 1.5 }}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}

// Màu hex theo tone (đồng bộ với recharts; brand/violet làm chủ đạo)
const TONE_HEX: Record<string, string> = {
  emerald: '#10b981',
  brand: '#7c3aed',
  ai: '#6366f1',
  amber: '#f59e0b',
  red: '#ef4444',
}
const CHART_AXIS = '#94a3b8' // slate-400 — đọc được trên nền sáng & tối

// ===== Phân bố điểm match (BarChart) =====
interface MatchTooltipProps {
  active?: boolean
  payload?: Array<{ payload: { label: string; count: number; pct: number } }>
}

function MatchTooltip({ active, payload }: MatchTooltipProps) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className="rounded-lg border border-ink-200 bg-white px-3 py-1.5 text-xs shadow-card dark:border-white/10 dark:bg-ink-900">
      <div className="font-semibold text-ink-900 dark:text-white">{p.label} điểm</div>
      <div className="font-medium text-brand-600 dark:text-brand-400">
        {p.count} hồ sơ · {p.pct}%
      </div>
    </div>
  )
}

function MatchBarChart({
  buckets,
  analyzed,
}: {
  buckets: { label: string; count: number; tone: string }[]
  analyzed: number
}) {
  const data = buckets.map((b) => ({
    ...b,
    pct: analyzed ? Math.round((b.count / analyzed) * 100) : 0,
  }))
  return (
    <div className="h-full min-h-[120px] w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 6, bottom: 0, left: -22 }}>
          <CartesianGrid
            strokeDasharray="3 3"
            stroke={CHART_AXIS}
            strokeOpacity={0.18}
            vertical={false}
          />
          <XAxis
            dataKey="label"
            tick={{ fontSize: 10, fill: CHART_AXIS }}
            tickLine={false}
            axisLine={false}
          />
          <YAxis
            allowDecimals={false}
            width={34}
            tick={{ fontSize: 10, fill: CHART_AXIS }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip content={<MatchTooltip />} cursor={{ fill: CHART_AXIS, fillOpacity: 0.08 }} />
          <Bar dataKey="count" radius={[6, 6, 0, 0]} maxBarSize={48}>
            {data.map((d, i) => (
              <Cell key={i} fill={TONE_HEX[d.tone] ?? TONE_HEX.brand} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

// ===== Hiệu suất Recruiter (horizontal BarChart) =====
interface RecruiterTooltipProps {
  active?: boolean
  payload?: Array<{ payload: { name: string; jobs: number; applicants: number; hired: number } }>
}

function RecruiterTooltip({ active, payload }: RecruiterTooltipProps) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className="rounded-lg border border-ink-200 bg-white px-3 py-2 text-xs shadow-card dark:border-white/10 dark:bg-ink-900">
      <div className="mb-1 font-semibold text-ink-900 dark:text-white">{p.name}</div>
      <div className="text-ink-600 dark:text-ink-300">Tin tuyển: {p.jobs}</div>
      <div className="text-ink-600 dark:text-ink-300">Ứng viên: {p.applicants}</div>
      <div className="font-medium text-emerald-600 dark:text-emerald-400">Đã tuyển: {p.hired}</div>
    </div>
  )
}

function RecruiterBarChart({
  recruiters,
}: {
  recruiters: { name: string; jobs: number; applicants: number; hired: number }[]
}) {
  const data = [...recruiters].sort((a, b) => b.applicants - a.applicants)
  return (
    <div className="h-full min-h-[160px] w-full px-4 py-3">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} layout="vertical" margin={{ top: 4, right: 14, bottom: 0, left: 0 }}>
          <CartesianGrid
            strokeDasharray="3 3"
            stroke={CHART_AXIS}
            strokeOpacity={0.18}
            horizontal={false}
          />
          <XAxis
            type="number"
            allowDecimals={false}
            tick={{ fontSize: 10, fill: CHART_AXIS }}
            tickLine={false}
            axisLine={false}
          />
          <YAxis
            type="category"
            dataKey="name"
            width={96}
            tick={{ fontSize: 11, fill: CHART_AXIS }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip content={<RecruiterTooltip />} cursor={{ fill: CHART_AXIS, fillOpacity: 0.08 }} />
          <Bar dataKey="applicants" fill={TONE_HEX.brand} radius={[0, 6, 6, 0]} maxBarSize={22} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

// ===== Widget shell có thể kéo–thả =====
type AccentKey = 'brand' | 'ai' | 'amber' | 'emerald'
const ACCENT: Record<AccentKey, string> = {
  brand: 'bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400',
  ai: 'bg-ai-50 dark:bg-ai-500/20 text-ai-600 dark:text-ai-400',
  amber: 'bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400',
  emerald: 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400',
}

interface WidgetMeta {
  title: string
  subtitle?: string
  icon: LucideIcon
  accent?: AccentKey
  action?: ReactNode
  noPad?: boolean
  body: ReactNode
}

function DashWidget({ meta }: { meta: WidgetMeta }) {
  const { title, subtitle, icon: Icon, accent = 'brand', action, noPad, body } = meta
  return (
    <div className="flex h-full flex-col overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card dark:border-white/10 dark:bg-white/5">
      <div className="flex shrink-0 items-center gap-3 border-b border-ink-100 px-5 py-4 dark:border-white/10">
        <span
          className="widget-drag-handle shrink-0 cursor-grab touch-none rounded-lg p-1 text-ink-300 transition-colors hover:bg-ink-100 hover:text-ink-500 active:cursor-grabbing dark:text-ink-500 dark:hover:bg-white/10"
          title="Kéo để di chuyển bảng"
          aria-label="Kéo để di chuyển bảng"
        >
          <GripVertical className="h-4 w-4" />
        </span>
        <span className={`grid h-8 w-8 shrink-0 place-items-center rounded-lg ${ACCENT[accent]}`}>
          <Icon className="h-4 w-4" />
        </span>
        <div className="min-w-0">
          <h2 className="font-display font-bold leading-tight text-ink-900 dark:text-white">
            {title}
          </h2>
          {subtitle && <p className="truncate text-xs text-ink-400">{subtitle}</p>}
        </div>
        {action && <div className="ml-auto shrink-0">{action}</div>}
      </div>
      <div className={`flex-1 overflow-auto ${noPad ? '' : 'p-5'}`}>{body}</div>
    </div>
  )
}

const ResponsiveGridLayout = WidthProvider(Responsive)

const WIDGET_KEYS = ['funnel', 'match', 'trend', 'recruiters', 'vacancies', 'jobs', 'candidates']

// Bố cục mặc định trên lưới 12 cột (rowHeight 36px). Bảng ít data (vacancies, trend, match)
// chiếm ít cột/ít hàng; bảng nhiều data (funnel, tin, ứng viên) rộng & cao hơn.
const DEFAULT_LG: Layout[] = [
  { i: 'funnel', x: 0, y: 0, w: 7, h: 9, minW: 4, minH: 6 },
  { i: 'match', x: 7, y: 0, w: 5, h: 7, minW: 3, minH: 5 },
  { i: 'trend', x: 7, y: 7, w: 5, h: 6, minW: 3, minH: 5 },
  { i: 'recruiters', x: 0, y: 9, w: 7, h: 8, minW: 4, minH: 5 },
  { i: 'vacancies', x: 7, y: 13, w: 5, h: 7, minW: 3, minH: 5 },
  { i: 'jobs', x: 0, y: 17, w: 7, h: 8, minW: 4, minH: 5 },
  { i: 'candidates', x: 0, y: 25, w: 12, h: 8, minW: 4, minH: 5 },
]

/** Giữ lại key hợp lệ + bổ sung widget mới (nếu sau này thêm) để không mất bảng. */
function reconcileLayout(saved: Layout[]): Layout[] {
  const valid = saved.filter((l) => WIDGET_KEYS.includes(l.i))
  const have = new Set(valid.map((l) => l.i))
  return [...valid, ...DEFAULT_LG.filter((l) => !have.has(l.i))]
}

export default function HrDashboardPage() {
  const user = useAuthStore((s) => s.user)

  // 1 request duy nhất, có cache (react-query staleTime 5') → vào lại dashboard hiện tức thì.
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr-dashboard'],
    queryFn: () => dashboardService.getHrOverview(),
    staleTime: 1000 * 60 * 5,
  })
  // Chỉ hiện skeleton ở lần tải đầu (chưa có cache); refetch nền không chớp skeleton.
  const loading = isLoading

  // Bố cục các bảng phân tích — HR kéo–thả / đổi cỡ tuỳ ý, lưu theo từng tài khoản
  const layoutKey = `arisp:hr-dash-layout:${user?.id ?? 'me'}`
  const [lgLayout, setLgLayout] = useState<Layout[]>(DEFAULT_LG)

  useEffect(() => {
    try {
      const raw = localStorage.getItem(layoutKey)
      if (raw) {
        const saved = JSON.parse(raw) as Layout[]
        if (Array.isArray(saved)) setLgLayout(reconcileLayout(saved))
      }
    } catch {
      /* ignore */
    }
  }, [layoutKey])

  const handleLayoutChange = (_current: Layout[], all: Layouts) => {
    if (!all.lg) return
    setLgLayout(all.lg)
    try {
      localStorage.setItem(layoutKey, JSON.stringify(all.lg))
    } catch {
      /* ignore */
    }
  }

  const resetLayout = () => {
    setLgLayout(DEFAULT_LG)
    try {
      localStorage.removeItem(layoutKey)
    } catch {
      /* ignore */
    }
  }

  const funnelMax = data?.funnel?.[0]?.value || 0
  const errorMessage = isError
    ? error instanceof Error
      ? error.message
      : 'Không tải được dữ liệu tổng quan.'
    : ''

  // Phân tích lấy thẳng từ backend (đã tính sẵn) — chỉ thêm tone cho bucket + conv/hireRate (rẻ).
  const analytics = useMemo(() => {
    const funnel = data?.funnel ?? []
    const first = funnel[0]?.value || 0
    const funnelSteps = funnel.map((f, i) => ({
      conv:
        i === 0
          ? null
          : funnel[i - 1].value
            ? Math.round((f.value / funnel[i - 1].value) * 100)
            : 0,
    }))
    const hireRate = first ? Math.round(((funnel[funnel.length - 1]?.value || 0) / first) * 100) : 0
    const a = data?.analytics
    return {
      matchBuckets: (a?.matchBuckets ?? []).map((b, i) => ({
        ...b,
        tone: TONE_ORDER[i] ?? 'brand',
      })),
      analyzedCount: a?.analyzedCount ?? 0,
      avgMatch: a?.avgMatch ?? null,
      trend: a?.trend ?? [],
      recruiters: a?.recruiters ?? [],
      totalVacancies: a?.totalVacancies ?? 0,
      totalHired: a?.totalHired ?? 0,
      jobsWithQuota: a?.jobsWithQuota ?? [],
      funnelSteps,
      hireRate,
    }
  }, [data])

  // Top tin & việc cần xử lý — backend trả sẵn
  const topJobs = data?.topJobs ?? []
  const pendingJobsList = data?.pendingJobs ?? []
  const awaitingVerdict = (data?.recentCandidates ?? []).filter((c) => c.latestVerdict)

  // Bản đồ widget (xây trong render — chỉ chạy khi đã có data)
  const widgetMap: Record<string, WidgetMeta> = data
    ? {
        funnel: {
          title: 'Phễu tuyển dụng',
          subtitle: 'Tỉ lệ chuyển đổi qua từng bước',
          icon: TrendingUp,
          accent: 'brand',
          action: (
            <span className="hidden items-center gap-1 rounded-lg bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400 sm:inline-flex">
              <TrendingUp className="h-3.5 w-3.5" /> {data.hired} tuyển
            </span>
          ),
          body: (
            <>
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
                      <div
                        className="w-16 shrink-0 text-right text-xs text-ink-400 dark:text-ink-500"
                        title="Tỉ lệ chuyển đổi từ bước trước"
                      >
                        {i === 0
                          ? `${percent || 100}%`
                          : `↓ ${analytics.funnelSteps[i]?.conv ?? 0}%`}
                      </div>
                    </div>
                  )
                })}
              </div>
              <div className="mt-4 flex items-center justify-between border-t border-ink-100 pt-3 text-sm dark:border-white/10">
                <span className="text-ink-500 dark:text-ink-400">
                  Tỉ lệ tuyển thành công (Ứng tuyển → Tuyển)
                </span>
                <span className="font-display text-lg font-extrabold text-emerald-600 dark:text-emerald-400">
                  {analytics.hireRate}%
                </span>
              </div>
            </>
          ),
        },
        match: {
          title: 'Chất lượng nguồn ứng viên',
          subtitle: `Phân bố điểm match CV–JD (${analytics.analyzedCount} hồ sơ đã phân tích)`,
          icon: BarChart3,
          accent: 'brand',
          action:
            analytics.avgMatch != null ? (
              <div className="text-right">
                <div className="font-display text-xl font-extrabold text-ink-900 dark:text-white">
                  {analytics.avgMatch}
                </div>
                <div className="text-[11px] text-ink-400">điểm TB</div>
              </div>
            ) : undefined,
          body:
            analytics.analyzedCount === 0 ? (
              <p className="py-8 text-center text-sm text-ink-400">
                Chưa có hồ sơ nào được phân tích CV–JD.
              </p>
            ) : (
              <MatchBarChart buckets={analytics.matchBuckets} analyzed={analytics.analyzedCount} />
            ),
        },
        trend: {
          title: 'Xu hướng ứng tuyển',
          subtitle: `14 ngày gần nhất · ${analytics.trend.reduce((s, t) => s + t.count, 0)} hồ sơ`,
          icon: TrendingUp,
          accent: 'ai',
          body: <TrendChart trend={analytics.trend} />,
        },
        recruiters: {
          title: 'Hiệu suất Recruiter',
          subtitle: 'HR Leader theo dõi toàn bộ Recruiter',
          icon: Trophy,
          accent: 'amber',
          noPad: true,
          body:
            analytics.recruiters.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-ink-400">Chưa có dữ liệu.</p>
            ) : (
              <RecruiterBarChart recruiters={analytics.recruiters} />
            ),
        },
        vacancies: {
          title: 'Lấp đầy chỉ tiêu',
          subtitle: 'Số đã tuyển trên chỉ tiêu (vacancies)',
          icon: Target,
          accent: 'emerald',
          body:
            analytics.totalVacancies === 0 ? (
              <p className="py-6 text-center text-sm text-ink-400">
                Chưa có tin nào đặt chỉ tiêu tuyển.
              </p>
            ) : (
              <>
                <div className="mb-1 flex items-end justify-between">
                  <span className="font-display text-2xl font-extrabold text-ink-900 dark:text-white">
                    {analytics.totalHired}/{analytics.totalVacancies}
                  </span>
                  <span className="text-sm text-ink-400">
                    {Math.round((analytics.totalHired / analytics.totalVacancies) * 100)}%
                  </span>
                </div>
                <div className="mb-4 h-2.5 rounded-full bg-ink-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-emerald-600"
                    style={{
                      width: `${Math.min(100, Math.round((analytics.totalHired / analytics.totalVacancies) * 100))}%`,
                    }}
                  />
                </div>
                <div className="grid gap-2.5 sm:grid-cols-2">
                  {analytics.jobsWithQuota.map((j) => {
                    const pct = Math.min(100, Math.round((j.hired / j.vacancies) * 100))
                    return (
                      <Link key={j.id} to={`/hr/jobs/${j.id}`} className="block">
                        <div className="mb-1 flex items-center justify-between text-xs">
                          <span className="truncate text-ink-600 dark:text-ink-300">{j.title}</span>
                          <span
                            className={`shrink-0 font-semibold ${j.hired >= j.vacancies ? 'text-emerald-600 dark:text-emerald-400' : 'text-ink-500 dark:text-ink-400'}`}
                          >
                            {j.hired}/{j.vacancies}
                          </span>
                        </div>
                        <div className="h-1.5 rounded-full bg-ink-100 dark:bg-white/10">
                          <div
                            className={`h-full rounded-full ${j.hired >= j.vacancies ? 'bg-emerald-500' : 'bg-brand-500'}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </Link>
                    )
                  })}
                </div>
              </>
            ),
        },
        jobs: {
          title: 'Tin tuyển dụng',
          subtitle: 'Toàn bộ Recruiter · sắp theo số ứng viên',
          icon: Briefcase,
          accent: 'brand',
          noPad: true,
          action: (
            <Link
              to="/hr/jobs"
              className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
            >
              Xem tất cả
            </Link>
          ),
          body:
            topJobs.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-ink-400">
                Chưa có tin tuyển dụng nào.
              </p>
            ) : (
              <div className="divide-y divide-ink-100 dark:divide-white/10">
                {topJobs.map((j) => (
                  <Link
                    key={j.id}
                    to={`/hr/jobs/${j.id}`}
                    className="flex items-center gap-4 px-5 py-3.5 hover:bg-ink-50/60 dark:hover:bg-white/5"
                  >
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-100 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400">
                      <Briefcase className="h-5 w-5" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="truncate font-medium text-ink-900 dark:text-white">
                        {j.title}
                      </div>
                      <div className="truncate text-xs text-ink-400">
                        {j.department || 'Chưa phân phòng ban'}
                        {j.createdByName ? ` · ${j.createdByName}` : ''}
                      </div>
                    </div>
                    <span className="hidden items-center gap-1 text-xs text-ink-500 dark:text-ink-400 sm:flex">
                      <Users className="h-3.5 w-3.5" /> {j.applicantCount ?? 0}
                    </span>
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${jobStatusBadge(j.status)}`}
                    >
                      {jobStatusLabel(j.status)}
                    </span>
                    <ChevronRight className="h-4 w-4 text-ink-300 dark:text-ink-500" />
                  </Link>
                ))}
              </div>
            ),
        },
        candidates: {
          title: 'Ứng viên gần đây',
          subtitle: 'Hồ sơ mới nhất trong hệ thống',
          icon: Users,
          accent: 'ai',
          noPad: true,
          action: (
            <Link
              to="/hr/candidates"
              className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
            >
              Xem tất cả
            </Link>
          ),
          body:
            data.recentCandidates.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-ink-400">Chưa có ứng viên nào.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-ink-50/50 text-left dark:bg-white/5">
                    <tr>
                      <th className="px-5 py-3 font-medium text-ink-600 dark:text-ink-400">
                        Ứng viên
                      </th>
                      <th className="px-5 py-3 font-medium text-ink-600 dark:text-ink-400">
                        Vị trí
                      </th>
                      <th className="px-5 py-3 font-medium text-ink-600 dark:text-ink-400">Vòng</th>
                      <th className="px-5 py-3 font-medium text-ink-600 dark:text-ink-400">
                        Verdict AI
                      </th>
                      <th className="px-5 py-3 font-medium" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                    {data.recentCandidates.map((c) => (
                      <tr key={c.id} className="hover:bg-ink-50/60 dark:hover:bg-white/5">
                        <td className="px-5 py-3">
                          <div className="flex items-center gap-3">
                            <div className="grid h-9 w-9 place-items-center rounded-full bg-brand-100 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-400">
                              {initials(c.candidateName)}
                            </div>
                            <div>
                              <div className="font-medium text-ink-900 dark:text-white">
                                {c.candidateName || 'Ẩn danh'}
                              </div>
                              <div className="text-xs text-ink-400">
                                {typeof c.matchScore === 'number'
                                  ? `Match ${c.matchScore}`
                                  : 'Chưa phân tích'}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-3 text-ink-600 dark:text-ink-400">
                          {c.jobTitle || '—'}
                        </td>
                        <td className="px-5 py-3">
                          <span className="rounded-full bg-ink-100 px-2.5 py-1 text-xs font-semibold text-ink-600 dark:bg-white/10 dark:text-ink-400">
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
              </div>
            ),
        },
      }
    : {}

  return (
    <main className="min-h-screen space-y-6 bg-ink-50 p-6 dark:bg-ink-950">
      {/* Page header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-extrabold leading-snug text-ink-900 dark:text-white">
            Xin chào, {user?.name || 'HR'} 👋
          </h1>
          <p className="mt-1 flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <CalendarDays className="h-4 w-4" />
            {todayLabel} · Tổng quan tuyển dụng
          </p>
        </div>
      </div>

      {loading && <HrDashboardSkeleton />}
      {!loading && errorMessage && <ErrorAlert message={errorMessage} />}

      {!loading && !errorMessage && data && (
        <>
          {/* ===== Ưu tiên xử lý (mục tiêu chính của HR Leader) — luôn ở trên đầu ===== */}
          <section className="grid gap-4 lg:grid-cols-2">
            {/* Tin chờ duyệt */}
            <div className="flex flex-col rounded-2xl border border-amber-300 bg-gradient-to-br from-amber-50 to-white p-5 shadow-card dark:border-amber-500/30 dark:from-amber-500/10 dark:to-transparent">
              <div className="flex items-center justify-between">
                <span className="flex items-center gap-2 text-sm font-semibold text-amber-700 dark:text-amber-400">
                  <span className="grid h-9 w-9 place-items-center rounded-xl bg-amber-100 text-amber-600 dark:bg-amber-500/20 dark:text-amber-400">
                    <Gavel className="h-[18px] w-[18px]" />
                  </span>
                  Tin chờ bạn duyệt
                </span>
                <span className="font-display text-3xl font-extrabold leading-none text-amber-700 dark:text-amber-400">
                  {data.pendingJobsCount}
                </span>
              </div>
              <div className="mt-4 flex-1 space-y-1">
                {pendingJobsList.length === 0 ? (
                  <p className="py-2 text-sm text-amber-700/80 dark:text-amber-400/70">
                    Không có tin nào chờ duyệt 🎉
                  </p>
                ) : (
                  pendingJobsList.slice(0, 3).map((j) => (
                    <Link
                      key={j.id}
                      to={`/hr/jobs/${j.id}`}
                      className="flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-amber-100/60 dark:hover:bg-amber-500/10"
                    >
                      <Briefcase className="h-3.5 w-3.5 shrink-0 text-amber-600 dark:text-amber-400" />
                      <span className="min-w-0 flex-1 truncate text-ink-700 dark:text-ink-200">
                        {j.title}
                      </span>
                      <span className="shrink-0 truncate text-xs text-ink-400">
                        {j.createdByName || '—'}
                      </span>
                    </Link>
                  ))
                )}
              </div>
              <Link
                to="/hr/jobs/pending"
                className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-amber-700 transition-all hover:gap-2 dark:text-amber-400"
              >
                Duyệt tin ngay <ArrowRight className="h-4 w-4" />
              </Link>
            </div>

            {/* Verdict chờ xác nhận */}
            <div className="flex flex-col rounded-2xl border border-brand-300 bg-gradient-to-br from-brand-50 to-white p-5 shadow-card dark:border-brand-500/30 dark:from-brand-500/10 dark:to-transparent">
              <div className="flex items-center justify-between">
                <span className="flex items-center gap-2 text-sm font-semibold text-brand-700 dark:text-brand-400">
                  <span className="grid h-9 w-9 place-items-center rounded-xl bg-brand-100 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400">
                    <CheckCircle2 className="h-[18px] w-[18px]" />
                  </span>
                  Verdict chờ xác nhận
                </span>
                <span className="font-display text-3xl font-extrabold leading-none text-brand-700 dark:text-brand-400">
                  {data.pendingReviews}
                </span>
              </div>
              <div className="mt-4 flex-1 space-y-1">
                {awaitingVerdict.length === 0 ? (
                  <p className="py-2 text-sm text-brand-700/80 dark:text-brand-400/70">
                    Không có verdict nào chờ xác nhận.
                  </p>
                ) : (
                  awaitingVerdict.slice(0, 3).map((c) => (
                    <Link
                      key={c.id}
                      to={`/hr/candidates/${c.id}`}
                      className="flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-brand-100/60 dark:hover:bg-brand-500/10"
                    >
                      <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-100 text-[10px] font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-400">
                        {initials(c.candidateName)}
                      </span>
                      <span className="min-w-0 flex-1 truncate text-ink-700 dark:text-ink-200">
                        {c.candidateName || 'Ẩn danh'}
                      </span>
                      <VerdictBadge verdict={c.latestVerdict} />
                    </Link>
                  ))
                )}
              </div>
              <Link
                to="/hr/evaluations"
                className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-brand-700 transition-all hover:gap-2 dark:text-brand-400"
              >
                Xác nhận ngay <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
          </section>

          {/* KPI */}
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <KpiCard icon={Briefcase} label="Tin đang tuyển" value={data.activeJobs} />
            <KpiCard icon={Users} label="Ứng viên đang xử lý" value={data.totalApplications} />
            <KpiCard icon={Video} label="Phỏng vấn AI" value={data.aiInterviews} />
            <KpiCard icon={Trophy} label="Đã tuyển" value={data.hired} />
          </div>

          {/* ===== Phân tích (lưới kéo–thả + đổi cỡ) ===== */}
          <div className="flex items-center justify-between pt-1">
            <div className="flex items-center gap-2 text-sm font-semibold text-ink-500 dark:text-ink-400">
              <Sparkles className="h-4 w-4 text-ai-500" />
              Phân tích tuyển dụng
              <span className="hidden text-xs font-normal text-ink-400 sm:inline">
                · kéo <GripVertical className="inline h-3 w-3" /> để di chuyển, kéo góc dưới–phải để
                đổi cỡ
              </span>
            </div>
            <button
              type="button"
              onClick={resetLayout}
              className="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 bg-white px-2.5 py-1.5 text-xs font-medium text-ink-600 transition-colors hover:bg-ink-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-300 dark:hover:bg-white/10"
            >
              <RotateCcw className="h-3.5 w-3.5" /> Khôi phục bố cục
            </button>
          </div>

          <ResponsiveGridLayout
            className="-mx-2"
            layouts={{ lg: lgLayout }}
            breakpoints={{ lg: 1280, md: 996, sm: 768, xs: 480, xxs: 0 }}
            // sm/xs/xxs = 4 cột (≥ minW lớn nhất của widget) → widget clamp về full-width
            // xếp dọc trên màn nhỏ mà không vi phạm minW (tránh cảnh báo RGL).
            cols={{ lg: 12, md: 12, sm: 4, xs: 4, xxs: 4 }}
            rowHeight={36}
            margin={[16, 16]}
            draggableHandle=".widget-drag-handle"
            onLayoutChange={handleLayoutChange}
            isResizable
            resizeHandles={['se']}
            compactType="vertical"
          >
            {WIDGET_KEYS.map((key) =>
              widgetMap[key] ? (
                <div key={key} className="h-full">
                  <DashWidget meta={widgetMap[key]} />
                </div>
              ) : null
            )}
          </ResponsiveGridLayout>
        </>
      )}
    </main>
  )
}
