import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  Bell,
  Clock,
  FileText,
  CheckCircle2,
  CalendarClock,
  CheckCheck,
  Settings,
  ChevronRight,
} from 'lucide-react'
import { notificationService } from '@services/notification/notificationService'
import type { NotificationItem } from '@services/notification/notificationService'
import { Skeleton } from '@components/ui/Skeleton'

type FilterKey = 'all' | 'unread' | 'interview' | 'result' | 'system'

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'unread', label: 'Chưa đọc' },
  { key: 'interview', label: 'Phỏng vấn' },
  { key: 'result', label: 'Kết quả' },
  { key: 'system', label: 'Hệ thống' },
]

function categoryOf(type: string): Exclude<FilterKey, 'all' | 'unread'> {
  if (type === 'result') return 'result'
  if (type === 'invite' || type === 'schedule' || type === 'pending') return 'interview'
  return 'system'
}

function notifStyle(type: string): { Icon: typeof Bell; cls: string } {
  switch (type) {
    case 'result':
      return { Icon: CheckCircle2, cls: 'bg-emerald-50 text-emerald-600' }
    case 'invite':
      return { Icon: CalendarClock, cls: 'bg-brand-50 text-brand-600' }
    case 'schedule':
      return { Icon: Clock, cls: 'bg-brand-50 text-brand-600' }
    case 'applied':
      return { Icon: FileText, cls: 'bg-ink-100 text-ink-500' }
    default:
      return { Icon: Bell, cls: 'bg-ai-50 text-ai-600' }
  }
}

function actionLabel(type: string): string {
  switch (type) {
    case 'result':
      return 'Xem báo cáo'
    case 'invite':
      return 'Xem mã phỏng vấn'
    case 'schedule':
      return 'Xem lịch'
    default:
      return 'Xem chi tiết'
  }
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'Vừa xong'
  if (mins < 60) return `${mins} phút trước`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs} giờ trước`
  const days = Math.floor(hrs / 24)
  if (days === 1) return 'Hôm qua'
  return `${days} ngày trước`
}

/** Nhóm theo ngày: Hôm nay / Hôm qua / Trước đó. */
function dayGroupOf(iso: string): 'today' | 'yesterday' | 'older' {
  const d = new Date(iso)
  const now = new Date()
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const t = d.getTime()
  if (t >= startOfToday) return 'today'
  if (t >= startOfToday - 86400000) return 'yesterday'
  return 'older'
}

const GROUP_LABEL: Record<string, string> = {
  today: 'Hôm nay',
  yesterday: 'Hôm qua',
  older: 'Trước đó',
}

function NotifRow({
  n,
  onOpen,
}: {
  n: NotificationItem
  onOpen: (n: NotificationItem) => void
}) {
  const { Icon, cls } = notifStyle(n.type)
  return (
    <button
      onClick={() => onOpen(n)}
      className={`relative flex w-full gap-3 px-4 py-3.5 text-left hover:bg-ink-50 ${
        !n.isRead ? 'bg-brand-50/40' : ''
      }`}
    >
      <span className={`mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-full ${cls}`}>
        <Icon className="h-[18px] w-[18px]" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="text-sm text-ink-700">
          <b className="text-ink-900">{n.title}</b>
          {n.body ? ` — ${n.body}` : ''}
        </div>
        <div className="mt-1.5 flex items-center gap-3">
          <span className="text-xs text-ink-400">{timeAgo(n.createdAt)}</span>
          <span className="inline-flex items-center gap-0.5 text-xs font-semibold text-brand-600">
            {actionLabel(n.type)} <ChevronRight className="h-3.5 w-3.5" />
          </span>
        </div>
      </div>
      {!n.isRead && <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500" />}
    </button>
  )
}

export default function NotificationsPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState<NotificationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<FilterKey>('all')

  useEffect(() => {
    let active = true
    setLoading(true)
    notificationService
      .list()
      .then((d) => active && setItems(d.items))
      .catch(() => {})
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  const unreadCount = useMemo(() => items.filter((n) => !n.isRead).length, [items])

  const filtered = useMemo(() => {
    if (filter === 'all') return items
    if (filter === 'unread') return items.filter((n) => !n.isRead)
    return items.filter((n) => categoryOf(n.type) === filter)
  }, [items, filter])

  // Gom theo ngày, giữ thứ tự mới → cũ.
  const groups = useMemo(() => {
    const g: Record<string, NotificationItem[]> = { today: [], yesterday: [], older: [] }
    for (const n of filtered) g[dayGroupOf(n.createdAt)].push(n)
    return g
  }, [filtered])

  const markAllRead = async () => {
    if (unreadCount === 0) return
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })))
    try {
      await notificationService.markAllRead()
    } catch {
      /* đã cập nhật lạc quan */
    }
  }

  const openNotif = (n: NotificationItem) => {
    if (!n.isRead) {
      setItems((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      notificationService.markRead(n.id).catch(() => {})
    }
    navigate(n.link || '/candidate/applications')
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-6">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center gap-2 text-sm text-ink-400">
        <Link to="/jobs" className="hover:text-brand-600">
          Trang chủ
        </Link>
        <ChevronRight className="h-4 w-4" />
        <span className="font-medium text-ink-600">Thông báo</span>
      </div>

      {/* Header */}
      <div className="mb-5 flex items-center justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-extrabold text-ink-900">Thông báo</h1>
          <p className="text-sm text-ink-500">
            {unreadCount > 0 ? `Bạn có ${unreadCount} thông báo chưa đọc` : 'Bạn đã đọc hết thông báo'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={markAllRead}
            disabled={unreadCount === 0}
            className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm font-medium text-ink-700 hover:bg-ink-50 disabled:opacity-50"
          >
            <CheckCheck className="h-4 w-4" /> Đánh dấu đã đọc
          </button>
          <Link
            to="/candidate/settings"
            title="Cài đặt thông báo"
            className="grid h-9 w-9 place-items-center rounded-xl border border-ink-200 bg-white text-ink-600 hover:bg-ink-50"
          >
            <Settings className="h-4 w-4" />
          </Link>
        </div>
      </div>

      {/* Filter tabs */}
      <div className="mb-5 flex items-center gap-2 overflow-x-auto pb-1 text-sm">
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
            {f.label}
            {f.key === 'unread' && unreadCount > 0 && (
              <span className={filter === f.key ? 'ml-1 opacity-80' : 'ml-1 text-brand-600'}>
                {unreadCount}
              </span>
            )}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="flex gap-3">
                <Skeleton className="h-9 w-9 shrink-0 rounded-full" />
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-4 w-3/4" />
                  <Skeleton className="h-3 w-24" />
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-ink-300 bg-white p-12 text-center shadow-card">
          <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
            <Bell className="h-6 w-6" />
          </div>
          <p className="mt-3 font-semibold text-ink-700">Không có thông báo</p>
          <p className="mt-1 text-sm text-ink-500">
            Các cập nhật về hồ sơ, lời mời phỏng vấn và kết quả sẽ hiển thị tại đây.
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          {(['today', 'yesterday', 'older'] as const).map((key) =>
            groups[key].length === 0 ? null : (
              <section key={key}>
                <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                  {GROUP_LABEL[key]}
                </div>
                <div className="divide-y divide-ink-100 overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
                  {groups[key].map((n) => (
                    <NotifRow key={n.id} n={n} onOpen={openNotif} />
                  ))}
                </div>
              </section>
            )
          )}
        </div>
      )}
    </div>
  )
}
