import { useEffect, useMemo, useState, type ComponentType } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  Bell,
  Users,
  ClipboardCheck,
  Briefcase,
  CheckCircle2,
  XCircle,
  CheckCheck,
  Trash2,
  X,
  ChevronRight,
} from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert, Pagination } from '@components/shared'
import { Skeleton } from '@components/ui/Skeleton'
import { staffNotificationService } from '@services/notification/notificationService'
import type { NotificationItem } from '@services/notification/notificationService'

const PAGE_SIZE = 10

type FilterKey = 'all' | 'unread' | 'applicant' | 'evaluation' | 'job' | 'system'

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'unread', label: 'Chưa đọc' },
  { key: 'applicant', label: 'Ứng viên' },
  { key: 'evaluation', label: 'Đánh giá' },
  { key: 'job', label: 'Tin tuyển dụng' },
  { key: 'system', label: 'Hệ thống' },
]

function categoryOf(type: string): Exclude<FilterKey, 'all' | 'unread'> {
  if (type === 'applied') return 'applicant'
  if (type === 'pending') return 'evaluation'
  if (type === 'approval' || type === 'approved' || type === 'rejected') return 'job'
  return 'system'
}

// Icon + màu nền theo loại thông báo nhân sự (đồng bộ với chuông ở layout).
function notifStyle(type: string): { Icon: ComponentType<{ className?: string }>; cls: string } {
  switch (type) {
    case 'applied':
      return {
        Icon: Users,
        cls: 'bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400',
      }
    case 'pending':
      return {
        Icon: ClipboardCheck,
        cls: 'bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400',
      }
    case 'approval':
      return {
        Icon: Briefcase,
        cls: 'bg-blue-50 dark:bg-blue-500/20 text-blue-600 dark:text-blue-400',
      }
    case 'approved':
      return {
        Icon: CheckCircle2,
        cls: 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400',
      }
    case 'rejected':
      return {
        Icon: XCircle,
        cls: 'bg-red-50 dark:bg-red-500/20 text-red-600 dark:text-red-400',
      }
    default:
      return { Icon: Bell, cls: 'bg-ai-50 dark:bg-ai-500/20 text-ai-600 dark:text-ai-400' }
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

function NotifRow({
  n,
  onOpen,
  onRemove,
}: {
  n: NotificationItem
  onOpen: (n: NotificationItem) => void
  onRemove: (n: NotificationItem) => void
}) {
  const { Icon, cls } = notifStyle(n.type)
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={() => onOpen(n)}
      onKeyDown={(e) => e.key === 'Enter' && onOpen(n)}
      className={`group relative flex w-full cursor-pointer gap-3 px-4 py-4 text-left transition-colors hover:bg-ink-50 dark:hover:bg-white/5 ${
        n.isRead ? '' : 'bg-brand-50/40 dark:bg-brand-500/10'
      }`}
    >
      <span className={`mt-0.5 grid h-10 w-10 shrink-0 place-items-center rounded-full ${cls}`}>
        <Icon className="h-[18px] w-[18px]" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="text-sm text-ink-700 dark:text-ink-200">
          <b className="text-ink-900 dark:text-white">{n.title}</b>
          {n.body ? ` — ${n.body}` : ''}
        </div>
        <div className="mt-1.5 flex items-center gap-3">
          <span className="text-xs text-ink-400">{timeAgo(n.createdAt)}</span>
          {n.link && (
            <span className="inline-flex items-center gap-0.5 text-xs font-semibold text-brand-600 dark:text-brand-400">
              Xem chi tiết <ChevronRight className="h-3.5 w-3.5" />
            </span>
          )}
        </div>
      </div>
      {!n.isRead && (
        <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500 group-hover:hidden" />
      )}
      <button
        onClick={(e) => {
          e.stopPropagation()
          onRemove(n)
        }}
        aria-label="Xóa thông báo"
        className="absolute right-2 top-2 hidden h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-200 hover:text-red-600 dark:hover:bg-white/10 dark:hover:text-red-400 group-hover:grid"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}

/**
 * Màn thông báo đầy đủ cho nhân sự nội bộ (HR Admin / Recruiter). Dùng chung `staffNotificationService`
 * (backend tự khoanh vùng theo người đăng nhập); `n.link` đã trỏ đúng khu vực role nên component
 * hoàn toàn dùng chung cho cả hai vai trò.
 */
export default function StaffNotificationsView() {
  const navigate = useNavigate()
  const [items, setItems] = useState<NotificationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState<FilterKey>('all')
  const [page, setPage] = useState(1)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    staffNotificationService
      .list()
      .then((d) => active && setItems(d.items))
      .catch(() => active && setError('Không tải được danh sách thông báo. Vui lòng thử lại.'))
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

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  // Về trang 1 khi đổi bộ lọc
  useEffect(() => {
    setPage(1)
  }, [filter])

  // Kẹp trang khi danh sách co lại (sau khi xóa)
  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const markAllRead = async () => {
    if (unreadCount === 0) return
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })))
    try {
      await staffNotificationService.markAllRead()
    } catch {
      /* đã cập nhật lạc quan */
    }
  }

  const clearAll = async () => {
    if (items.length === 0) return
    if (!window.confirm('Xóa tất cả thông báo? Hành động này không thể hoàn tác.')) return
    const snapshot = items
    setItems([])
    try {
      await staffNotificationService.clearAll()
    } catch {
      setItems(snapshot) // rollback nếu lỗi
      setError('Không thể xóa tất cả thông báo. Vui lòng thử lại.')
    }
  }

  const openNotif = (n: NotificationItem) => {
    if (!n.isRead) {
      setItems((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      staffNotificationService.markRead(n.id).catch(() => {})
    }
    if (n.link) navigate(n.link)
  }

  const removeNotif = (n: NotificationItem) => {
    setItems((prev) => prev.filter((x) => x.id !== n.id))
    staffNotificationService.remove(n.id).catch(() => {})
  }

  return (
    <div className="min-h-screen bg-ink-50 p-6 dark:bg-ink-950 lg:p-8">
      <PageHeader
        title="Thông báo"
        description={
          unreadCount > 0 ? `Bạn có ${unreadCount} thông báo chưa đọc` : 'Bạn đã đọc hết thông báo'
        }
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {/* Toolbar: bộ lọc + hành động */}
      <div className="mb-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap gap-2">
          {FILTERS.map((f) => {
            const activeTab = filter === f.key
            return (
              <button
                key={f.key}
                onClick={() => setFilter(f.key)}
                className={`rounded-xl px-3.5 py-2 text-sm font-medium transition-all ${
                  activeTab
                    ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white'
                    : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                }`}
              >
                {f.label}
                {f.key === 'unread' && unreadCount > 0 && (
                  <span
                    className={`ml-1.5 ${activeTab ? 'text-white/80' : 'text-brand-600 dark:text-brand-400'}`}
                  >
                    {unreadCount}
                  </span>
                )}
              </button>
            )
          })}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={markAllRead}
            disabled={unreadCount === 0}
            className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50"
          >
            <CheckCheck className="h-4 w-4" /> Đánh dấu đã đọc
          </button>
          <button
            onClick={clearAll}
            disabled={items.length === 0}
            className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-3 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 disabled:opacity-50"
          >
            <Trash2 className="h-4 w-4" /> Xóa tất cả
          </button>
        </div>
      </div>

      {loading ? (
        <div className="overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
          {Array.from({ length: 6 }).map((_, i) => (
            <div
              key={i}
              className="flex gap-3 border-b border-ink-100 px-4 py-4 last:border-0 dark:border-white/10"
            >
              <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-24" />
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<Bell className="h-8 w-8 text-ink-400" />}
          title="Không có thông báo"
          description="Cập nhật về ứng viên mới, đánh giá chờ duyệt và tin tuyển dụng sẽ hiển thị tại đây."
        />
      ) : (
        <>
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            className="divide-y divide-ink-100 dark:divide-white/10 overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card"
          >
            {paged.map((n) => (
              <NotifRow key={n.id} n={n} onOpen={openNotif} onRemove={removeNotif} />
            ))}
          </motion.div>

          <Pagination
            page={page}
            totalPages={totalPages}
            total={filtered.length}
            label="thông báo"
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  )
}
