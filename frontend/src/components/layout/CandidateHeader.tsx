import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import {
  Search,
  Bookmark,
  Globe,
  Moon,
  Sun,
  Bell,
  ChevronDown,
  Check,
  Clock,
  Menu,
  X,
  User,
  FileText,
  Clapperboard,
  Settings,
  LogOut,
} from 'lucide-react'
import { useAuthStore } from '@store/auth'
import { authService } from '@services/auth/authService'
import { notificationService } from '@/services/notification/notificationService'
import type { NotificationItem } from '@/services/notification/notificationService'
import { useQuery } from '@tanstack/react-query'

/** "2 giờ trước" / "Hôm qua" … từ ISO date. */
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

/** Icon + màu theo loại thông báo. */
function notifStyle(type: string): { Icon: typeof Bell; cls: string } {
  switch (type) {
    case 'result':
      return { Icon: Check, cls: 'bg-emerald-50 text-emerald-600' }
    case 'invite':
    case 'schedule':
      return { Icon: Clock, cls: 'bg-brand-50 text-brand-600' }
    case 'applied':
      return { Icon: FileText, cls: 'bg-ink-100 text-ink-500' }
    default:
      return { Icon: Bell, cls: 'bg-ai-50 text-ai-600' }
  }
}

/**
 * Header dùng chung cho toàn bộ khu vực ứng viên / job board (FindJobPage, JobDetailPage,
 * và các trang trong CandidateAppLayout) — đảm bảo mọi màn nhìn giống nhau.
 */

function Logo() {
  return (
    <svg
      className="h-9 w-9"
      viewBox="0 0 96 96"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-label="ARISP"
    >
      <defs>
        <linearGradient
          id="lg-cand-hdr"
          x1="12"
          y1="10"
          x2="84"
          y2="86"
          gradientUnits="userSpaceOnUse"
        >
          <stop stopColor="#4f46e5" />
          <stop offset="1" stopColor="#9333ea" />
        </linearGradient>
      </defs>
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-cand-hdr)" />
      <path
        d="M30 70 L48 26 L66 70"
        stroke="white"
        strokeWidth="8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M38 56 H58" stroke="white" strokeWidth="8" strokeLinecap="round" />
      <path
        d="M70 20 C71.4 27 72.5 28.1 79.5 29.5 C72.5 30.9 71.4 32 70 39 C68.6 32 67.5 30.9 60.5 29.5 C67.5 28.1 68.6 27 70 20 Z"
        fill="white"
        fillOpacity="0.95"
      />
    </svg>
  )
}

function initialsOf(name?: string, email?: string): string {
  const src = (name || email || 'U').trim()
  const parts = src.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return src.slice(0, 2).toUpperCase()
}

const NAV = [
  { label: 'Việc làm', to: '/jobs', match: (p: string) => p === '/' || p.startsWith('/jobs') },
  {
    label: 'Hồ sơ ứng tuyển',
    to: '/candidate/applications',
    match: (p: string) => p.startsWith('/candidate/applications'),
  },
  // "Phỏng vấn thử" không còn là điểm đến độc lập — practice được khởi động theo từng
  // hồ sơ ứng tuyển (trang Hồ sơ ứng tuyển / chi tiết) qua /interview/practice/:applicationId.
]

const USER_MENU = [
  { label: 'Hồ sơ của tôi', to: '/candidate/profile', icon: User },
  { label: 'Đơn ứng tuyển', to: '/candidate/applications', icon: FileText },
  { label: 'Việc đã lưu', to: '/candidate/saved-jobs', icon: Bookmark },
  { label: 'Kết quả & lịch phỏng vấn', to: '/candidate/results', icon: Clapperboard },
  { label: 'Cài đặt', to: '/candidate/settings', icon: Settings },
]

type Drop = 'user' | 'notif' | 'lang' | 'mobile' | null

export default function CandidateHeader() {
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const { user, isAuthenticated, logout } = useAuthStore()
  const [open, setOpen] = useState<Drop>(null)
  const [isDark, setIsDark] = useState(() => document.documentElement.classList.contains('dark'))
  const rootRef = useRef<HTMLElement>(null)

  const { data: notifData, refetch } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationService.list(),
    enabled: !!isAuthenticated,
  })

  const notifs = notifData?.items || []
  const unread = notifData?.unreadCount || 0

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(null)
    }
    document.addEventListener('click', onClick)
    return () => document.removeEventListener('click', onClick)
  }, [])

  // Đóng dropdown khi đổi trang.
  useEffect(() => setOpen(null), [pathname])

  const markAllRead = async () => {
    try {
      await notificationService.markAllRead()
      refetch()
    } catch {
      /* ignore */
    }
  }

  const openNotif = (n: NotificationItem) => {
    if (!n.isRead) {
      notificationService.markRead(n.id).then(() => refetch()).catch(() => {})
    }
    setOpen(null)
    navigate(n.link || '/candidate/notifications')
  }

  const toggle = (d: Drop) => (e: React.MouseEvent) => {
    e.stopPropagation()
    setOpen((cur) => (cur === d ? null : d))
  }

  const toggleTheme = () => {
    const next = !isDark
    setIsDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.theme = next ? 'dark' : 'light'
  }

  const handleLogout = async () => {
    try {
      await authService.logout()
    } catch {
      /* vẫn đăng xuất phía client */
    }
    logout()
    navigate('/auth/candidate-login')
  }

  const initials = initialsOf(user?.name, user?.email)

  return (
    <header
      ref={rootRef}
      className="sticky top-0 z-30 border-b border-ink-200 bg-white/80 backdrop-blur"
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-3 px-4 sm:px-6">
        {/* Mobile menu toggle */}
        <button
          onClick={toggle('mobile')}
          aria-label="Menu"
          className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-600 hover:bg-ink-100 lg:hidden"
        >
          {open === 'mobile' ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>

        {/* Logo */}
        <Link to="/jobs" className="flex shrink-0 items-center gap-2.5">
          <Logo />
          <span className="hidden font-display text-lg font-extrabold text-ink-900 sm:inline">
            ARISP <span className="font-medium text-ink-400">Careers</span>
          </span>
        </Link>

        {/* Primary nav */}
        <nav className="hidden items-center gap-1 text-sm font-medium text-ink-600 lg:flex">
          {NAV.map((item) => {
            const active = item.match(pathname)
            return (
              <Link
                key={item.to}
                to={item.to}
                className={`rounded-lg px-3 py-2 ${active ? 'bg-brand-50 text-brand-700' : 'hover:bg-ink-100'}`}
              >
                {item.label}
              </Link>
            )
          })}
        </nav>

        {/* Global search (trang trí) */}
        <div className="hidden max-w-sm flex-1 items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm focus-within:border-brand-400 focus-within:bg-white md:flex">
          <Search className="h-4 w-4 text-ink-400" />
          <input
            className="w-full bg-transparent outline-none placeholder:text-ink-400"
            placeholder="Tìm việc làm, kỹ năng..."
            onFocus={() => navigate('/jobs')}
          />
          <kbd className="hidden rounded border border-ink-200 bg-white px-1.5 text-[10px] font-semibold text-ink-400 lg:inline">
            ⌘K
          </kbd>
        </div>

        {/* Right cluster */}
        <div className="ml-auto flex items-center gap-1">
          {/* Saved jobs */}
          <button
            type="button"
            onClick={() => navigate('/candidate/saved-jobs')}
            aria-label="Việc đã lưu"
            className="relative hidden h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100 sm:grid"
          >
            <Bookmark className="h-5 w-5" />
          </button>

          {/* Language */}
          <div className="relative hidden sm:block">
            <button
              onClick={toggle('lang')}
              className="flex items-center gap-1 rounded-lg px-2 py-2 text-sm font-medium text-ink-600 hover:bg-ink-100"
            >
              <Globe className="h-[18px] w-[18px]" />
              VI <ChevronDown className="h-3.5 w-3.5" />
            </button>
            {open === 'lang' && (
              <div
                className="absolute right-0 mt-2 w-40 rounded-xl border border-ink-200 bg-white p-1 shadow-xl"
                onClick={(e) => e.stopPropagation()}
              >
                <button className="flex w-full items-center justify-between rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700">
                  Tiếng Việt <Check className="h-4 w-4" />
                </button>
                <button className="flex w-full items-center rounded-lg px-3 py-2 text-sm text-ink-600 hover:bg-ink-100">
                  English
                </button>
              </div>
            )}
          </div>

          {/* Theme */}
          <button
            onClick={toggleTheme}
            aria-label="Đổi giao diện sáng/tối"
            className="grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
          >
            {isDark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
          </button>

          {/* Notifications */}
          {isAuthenticated && (
            <div className="relative">
              <button
                onClick={toggle('notif')}
                aria-label="Thông báo"
                className="relative grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
              >
                <Bell className="h-5 w-5" />
                {unread > 0 && (
                  <span className="absolute right-1 top-1 grid h-4 min-w-[16px] place-items-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white ring-2 ring-white">
                    {unread > 9 ? '9+' : unread}
                  </span>
                )}
              </button>
              {open === 'notif' && (
                <div
                  className="absolute right-0 mt-2 w-80 origin-top-right rounded-2xl border border-ink-200 bg-white shadow-xl"
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className="flex items-center justify-between border-b border-ink-100 px-4 py-3">
                    <span className="font-display text-sm font-bold text-ink-900">Thông báo</span>
                    {unread > 0 && (
                      <button
                        onClick={markAllRead}
                        className="text-xs font-medium text-brand-600 hover:underline"
                      >
                        Đánh dấu đã đọc
                      </button>
                    )}
                  </div>
                  <div className="max-h-80 divide-y divide-ink-100 overflow-y-auto">
                    {notifs.length === 0 ? (
                      <div className="px-4 py-8 text-center text-sm text-ink-400">
                        Chưa có thông báo nào.
                      </div>
                    ) : (
                      notifs.slice(0, 6).map((n) => {
                        const { Icon, cls } = notifStyle(n.type)
                        return (
                          <button
                            key={n.id}
                            onClick={() => openNotif(n)}
                            className={`flex w-full gap-3 px-4 py-3 text-left hover:bg-ink-50 ${
                              !n.isRead ? 'bg-brand-50/40' : ''
                            }`}
                          >
                            <span
                              className={`mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full ${cls}`}
                            >
                              <Icon className="h-4 w-4" />
                            </span>
                            <span className="min-w-0 flex-1">
                              <span className="block text-sm text-ink-700">
                                <b className="text-ink-900">{n.title}</b>
                                {n.body ? ` — ${n.body}` : ''}
                              </span>
                              <span className="text-xs text-ink-400">{timeAgo(n.createdAt)}</span>
                            </span>
                            {!n.isRead && (
                              <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
                            )}
                          </button>
                        )
                      })
                    )}
                  </div>
                  <Link
                    to="/candidate/notifications"
                    className="block rounded-b-2xl border-t border-ink-100 px-4 py-3 text-center text-sm font-medium text-brand-600 hover:bg-ink-50"
                  >
                    Xem tất cả thông báo
                  </Link>
                </div>
              )}
            </div>
          )}

          {/* User menu */}
          <div className="relative">
            {isAuthenticated && user ? (
              <>
                <button
                  onClick={toggle('user')}
                  className="flex items-center gap-1.5 rounded-lg p-1 pr-1.5 hover:bg-ink-100"
                >
                  <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                    {initials}
                  </span>
                  <ChevronDown className="hidden h-4 w-4 text-ink-400 sm:block" />
                </button>
                {open === 'user' && (
                  <div
                    className="absolute right-0 mt-2 w-64 rounded-2xl border border-ink-200 bg-white shadow-xl"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="flex items-center gap-3 border-b border-ink-100 px-4 py-3">
                      <span className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                        {initials}
                      </span>
                      <div className="min-w-0">
                        <div className="truncate text-sm font-semibold text-ink-900">
                          {user.name || 'Ứng viên'}
                        </div>
                        <div className="truncate text-xs text-ink-400">{user.email}</div>
                      </div>
                    </div>
                    <div className="p-1.5 text-sm">
                      {USER_MENU.map((item) => {
                        const Icon = item.icon
                        return (
                          <Link
                            key={item.to}
                            to={item.to}
                            className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                          >
                            <Icon className="h-4 w-4 text-ink-400" /> {item.label}
                          </Link>
                        )
                      })}
                    </div>
                    <div className="border-t border-ink-100 p-1.5">
                      <button
                        onClick={handleLogout}
                        className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50"
                      >
                        <LogOut className="h-4 w-4" /> Đăng xuất
                      </button>
                    </div>
                  </div>
                )}
              </>
            ) : (
              <div className="flex items-center gap-2">
                <Link
                  to="/auth/candidate-login"
                  className="px-4 py-2 text-sm font-medium text-ink-600 hover:text-ink-900"
                >
                  Đăng nhập
                </Link>
                <Link
                  to="/auth/candidate-register"
                  className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  Đăng ký
                </Link>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Mobile menu */}
      {open === 'mobile' && (
        <div className="space-y-1 border-t border-ink-100 bg-white px-4 py-3 lg:hidden">
          <div className="mb-2 flex items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm">
            <Search className="h-4 w-4 text-ink-400" />
            <input
              className="w-full bg-transparent outline-none placeholder:text-ink-400"
              placeholder="Tìm việc làm, kỹ năng..."
              onFocus={() => navigate('/jobs')}
            />
          </div>
          {NAV.map((item) => {
            const active = item.match(pathname)
            return (
              <Link
                key={item.to}
                to={item.to}
                className={`block rounded-lg px-3 py-2 text-sm ${
                  active
                    ? 'bg-brand-50 font-medium text-brand-700'
                    : 'text-ink-700 hover:bg-ink-100'
                }`}
              >
                {item.label}
              </Link>
            )
          })}
          <Link
            to="/candidate/saved-jobs"
            className="block rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100"
          >
            Việc đã lưu
          </Link>
        </div>
      )}
    </header>
  )
}
