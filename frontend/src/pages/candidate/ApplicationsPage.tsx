import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Sparkles,
  MapPin,
  Check,
  Clock,
  Eye,
  XCircle,
  FileText,
  Briefcase,
  Loader2,
  AlertCircle,
  ChevronRight,
} from 'lucide-react'
import { applicationService } from '@services/application/applicationService'
import { useAuthStore } from '@store/auth/authStore'
import type { MyApplicationItem, MyApplicationRound } from '../../types/application'

type FilterKey = 'all' | 'action' | 'processing' | 'done'

const STATUS_META: Record<
  string,
  { label: string; cls: string; group: Exclude<FilterKey, 'all'>; icon: typeof Clock }
> = {
  invited: { label: 'Được mời', cls: 'bg-brand-50 text-brand-700 ring-brand-200', group: 'processing', icon: Eye },
  cv_submitted: { label: 'Đã nộp CV', cls: 'bg-ink-100 text-ink-600 ring-ink-200', group: 'processing', icon: FileText },
  screening: { label: 'Đang sàng lọc', cls: 'bg-brand-50 text-brand-700 ring-brand-200', group: 'processing', icon: Clock },
  interview: { label: 'Đang phỏng vấn', cls: 'bg-amber-50 text-amber-700 ring-amber-200', group: 'action', icon: AlertCircle },
  pass: { label: 'Đạt', cls: 'bg-emerald-50 text-emerald-700 ring-emerald-200', group: 'done', icon: Check },
  not_pass: { label: 'Không phù hợp', cls: 'bg-red-50 text-red-700 ring-red-200', group: 'done', icon: XCircle },
  withdrawn: { label: 'Đã rút', cls: 'bg-ink-100 text-ink-500 ring-ink-200', group: 'done', icon: XCircle },
}

function metaOf(status: string) {
  return STATUS_META[status] ?? { label: status, cls: 'bg-ink-100 text-ink-600 ring-ink-200', group: 'processing' as const, icon: Clock }
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function initialsOf(name?: string, email?: string): string {
  const src = (name || email || 'U').trim()
  const parts = src.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return src.slice(0, 2).toUpperCase()
}

function RoundStepper({ rounds }: { rounds: MyApplicationRound[] }) {
  if (!rounds.length) return null
  return (
    <div className="mt-4 flex flex-wrap items-center gap-1.5 text-xs">
      {rounds.map((r, i) => {
        const done = r.status === 'completed'
        const active = r.status === 'in_progress'
        const cls = done
          ? 'bg-emerald-50 text-emerald-700'
          : active
            ? 'bg-brand-50 text-brand-700'
            : 'bg-ink-100 text-ink-400'
        return (
          <span key={i} className="flex items-center gap-1.5">
            {i > 0 && <span className="h-px w-4 bg-ink-200" />}
            <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 font-semibold ${cls}`}>
              {done && <Check className="h-3.5 w-3.5" />}
              V{r.roundNumber} {r.roundType || ''}
              {r.verdict && (
                <span className={r.verdict === 'pass' ? 'text-emerald-700' : 'text-red-600'}>
                  · {r.verdict === 'pass' ? 'Pass' : 'Not Pass'}
                </span>
              )}
            </span>
          </span>
        )
      })}
    </div>
  )
}

function ApplicationCard({ app }: { app: MyApplicationItem }) {
  const meta = metaOf(app.status)
  const StatusIcon = meta.icon
  return (
    <article className="group rounded-2xl border border-ink-200 bg-white p-5 shadow-card transition hover:border-brand-200 hover:shadow-card-hover">
      <div className="flex gap-4">
        <div className="grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-600">
          <Briefcase className="h-6 w-6" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-ink-900 group-hover:text-brand-700">
                {app.jobTitle || 'Vị trí tuyển dụng'}
              </h3>
              <div className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-500">
                <MapPin className="h-3.5 w-3.5" />
                {[app.location, app.department].filter(Boolean).join(' · ') || 'Không rõ địa điểm'}
              </div>
            </div>
            <div className="flex shrink-0 flex-col items-end gap-1.5">
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ring-1 ${meta.cls}`}
              >
                <StatusIcon className="h-3.5 w-3.5" />
                {meta.label}
              </span>
              {typeof app.matchScore === 'number' && (
                <span className="inline-flex items-center gap-1 rounded-full bg-ai-50 px-2.5 py-1 text-xs font-bold text-ai-700 ring-1 ring-ai-200">
                  <Sparkles className="h-3.5 w-3.5" /> Match {app.matchScore}
                </span>
              )}
            </div>
          </div>

          <RoundStepper rounds={app.rounds} />

          <div className="mt-3 flex items-center justify-between border-t border-ink-100 pt-3">
            <span className="text-xs text-ink-400">
              Ứng tuyển {formatDate(app.createdAt)}
              {app.updatedAt && app.updatedAt !== app.createdAt && ` · Cập nhật ${formatDate(app.updatedAt)}`}
            </span>
            <Link
              to={`/candidate/applications/${app.id}`}
              className="inline-flex items-center gap-1 rounded-xl border border-ink-200 px-4 py-1.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
            >
              Xem chi tiết
              <ChevronRight className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </div>
    </article>
  )
}

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'action', label: 'Cần hành động' },
  { key: 'processing', label: 'Đang xử lý' },
  { key: 'done', label: 'Đã hoàn tất' },
]

export default function ApplicationsPage() {
  const { user } = useAuthStore()
  const [apps, setApps] = useState<MyApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState<FilterKey>('all')

  useEffect(() => {
    let active = true
    setLoading(true)
    applicationService
      .getMyApplications()
      .then((data) => active && setApps(data))
      .catch((err: any) => active && setError(err?.message || 'Không tải được danh sách hồ sơ.'))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  const counts = useMemo(() => {
    const c = { all: apps.length, action: 0, processing: 0, done: 0 }
    apps.forEach((a) => {
      c[metaOf(a.status).group]++
    })
    return c
  }, [apps])

  const filtered = useMemo(
    () => (filter === 'all' ? apps : apps.filter((a) => metaOf(a.status).group === filter)),
    [apps, filter]
  )

  const passedRounds = useMemo(
    () => apps.reduce((n, a) => n + a.rounds.filter((r) => r.verdict === 'pass').length, 0),
    [apps]
  )

  const initials = initialsOf(user?.name, user?.email)

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            Trang chủ
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">Hồ sơ ứng tuyển của tôi</span>
        </div>
      </div>

      <div className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[1fr_320px]">
        {/* LEFT */}
        <div className="space-y-6">
          {/* Profile banner */}
          <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
            <div className="h-20 bg-gradient-to-r from-brand-600 via-ai-600 to-ai-500" />
            <div className="px-6 pb-6">
              <div className="-mt-10">
                <span className="grid h-20 w-20 shrink-0 place-items-center rounded-2xl bg-gradient-to-br from-brand-600 to-ai-600 text-2xl font-extrabold text-white shadow-card ring-4 ring-white dark:ring-ink-900">
                  {initials}
                </span>
              </div>
              <div className="mt-3 flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h1 className="font-display text-2xl font-extrabold leading-tight">{user?.name || 'Ứng viên'}</h1>
                  <p className="text-sm text-ink-500">{user?.email}</p>
                </div>
                <Link
                  to="/candidate/profile"
                  className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                >
                  Sửa hồ sơ
                </Link>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="font-display text-2xl font-extrabold">{counts.all}</div>
              <div className="mt-0.5 text-xs text-ink-500">Tổng đơn ứng tuyển</div>
            </div>
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="font-display text-2xl font-extrabold text-brand-600">{counts.processing}</div>
              <div className="mt-0.5 text-xs text-ink-500">Đang xử lý</div>
            </div>
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="font-display text-2xl font-extrabold text-amber-600">{counts.action}</div>
              <div className="mt-0.5 text-xs text-ink-500">Cần hành động</div>
            </div>
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="font-display text-2xl font-extrabold text-emerald-600">{passedRounds}</div>
              <div className="mt-0.5 text-xs text-ink-500">Đã Pass vòng</div>
            </div>
          </div>

          {/* Filter tabs */}
          <div className="flex items-center gap-2 overflow-x-auto pb-1 text-sm">
            {FILTERS.map((f) => (
              <button
                key={f.key}
                onClick={() => setFilter(f.key)}
                className={`shrink-0 rounded-full px-4 py-1.5 font-medium ${
                  filter === f.key
                    ? 'bg-brand-600 font-semibold text-white'
                    : 'border border-ink-200 bg-white text-ink-600 hover:border-brand-300'
                }`}
              >
                {f.label} <span className={filter === f.key ? 'opacity-80' : 'text-ink-400'}>{counts[f.key]}</span>
              </button>
            ))}
          </div>

          {/* List */}
          {loading ? (
            <div className="flex items-center justify-center rounded-2xl border border-ink-200 bg-white py-16 text-ink-400 shadow-card">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : error ? (
            <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
              <AlertCircle className="h-4 w-4" /> {error}
            </div>
          ) : filtered.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-ink-300 bg-white p-10 text-center shadow-card">
              <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
                <FileText className="h-6 w-6" />
              </div>
              <p className="mt-3 font-semibold text-ink-700">Chưa có hồ sơ ứng tuyển</p>
              <p className="mt-1 text-sm text-ink-500">Khám phá việc làm IT phù hợp và ứng tuyển ngay.</p>
              <Link
                to="/jobs"
                className="mt-4 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
              >
                <Briefcase className="h-4 w-4" /> Tìm việc ngay
              </Link>
            </div>
          ) : (
            <div className="space-y-4">
              {filtered.map((app) => (
                <ApplicationCard key={app.id} app={app} />
              ))}
            </div>
          )}
        </div>

        {/* RIGHT sidebar */}
        <aside className="space-y-5 self-start lg:sticky lg:top-24">
          <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold">
              <FileText className="h-4 w-4 text-brand-600" /> Hồ sơ của bạn
            </div>
            <p className="mt-2 text-sm text-ink-500">
              Cập nhật thông tin cá nhân và CV để tăng cơ hội được mời phỏng vấn.
            </p>
            <Link
              to="/candidate/profile"
              className="mt-3 block w-full rounded-xl border border-ink-200 px-3 py-2 text-center text-sm font-semibold text-ink-700 hover:bg-ink-50"
            >
              Quản lý hồ sơ
            </Link>
          </div>

          <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50 to-white p-5 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="h-4 w-4" /> Mẹo từ AI
            </div>
            <p className="mt-2 text-sm text-ink-500">
              Cập nhật CV mới nhất và hoàn thiện hồ sơ để cải thiện điểm Match với các vị trí đang mở.
            </p>
            <Link
              to="/jobs"
              className="mt-3 block w-full rounded-xl border border-ai-300 bg-white px-3 py-2 text-center text-sm font-semibold text-ai-700 hover:bg-ai-50"
            >
              Khám phá việc phù hợp
            </Link>
          </div>
        </aside>
      </div>
    </>
  )
}
