import { useState, useEffect, useRef, useCallback } from 'react'
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import {
  LayoutDashboard,
  Briefcase,
  Users,
  Video,
  ClipboardCheck,
  BookOpen,
  UserCog,
  Settings,
  CircleHelp,
  Search,
  Bell,
  ChevronDown,
  Plus,
  Menu,
  LogOut,
  User,
  Moon,
  Sun,
  CheckCircle2,
  XCircle,
  X,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useAuthStore } from '@store/auth/authStore'
import { useThemeStore } from '@store/theme'
import {
  staffNotificationService,
  STAFF_NOTIF_REFRESH_EVENT,
} from '@services/notification/notificationService'
import type { NotificationItem } from '@services/notification/notificationService'

// Thời gian tương đối ngắn gọn (vd "15 phút trước").
function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const m = Math.floor(diff / 60000)
  if (m < 1) return 'Vừa xong'
  if (m < 60) return `${m} phút trước`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} giờ trước`
  return `${Math.floor(h / 24)} ngày trước`
}

// Icon nền theo loại thông báo.
const notifIcon: Record<string, LucideIcon> = {
  applied: Users,
  pending: ClipboardCheck,
  approval: Briefcase,
  approved: CheckCircle2,
  rejected: XCircle,
}

// Logo component
function Logo() {
  return (
    <svg
      className="h-8 w-8"
      viewBox="0 0 96 96"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-label="ARISP"
    >
      <defs>
        <linearGradient
          id="lg-hr-layout"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-hr-layout)" />
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

const sidebarItems: { icon: LucideIcon; label: string; path: string; badge?: number }[] = [
  { icon: LayoutDashboard, label: 'Dashboard', path: '/hr/dashboard' },
  { icon: Briefcase, label: 'Tin tuyển dụng', path: '/hr/jobs' },
  { icon: Users, label: 'Ứng viên', path: '/hr/candidates' },
  { icon: Video, label: 'Phiên phỏng vấn', path: '/hr/interviews' },
  { icon: ClipboardCheck, label: 'Đánh giá', path: '/hr/evaluations' },
  { icon: BookOpen, label: 'Playbook', path: '/hr/playbooks' },
  { icon: UserCog, label: 'Nhóm HR', path: '/hr/team' },
  { icon: Settings, label: 'Cài đặt', path: '/hr/settings' },
]

export default function HrLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [notifOpen, setNotifOpen] = useState(false)
  const [notifs, setNotifs] = useState<NotificationItem[]>([])
  const [unread, setUnread] = useState(0)
  const { user, logout } = useAuthStore()
  const { isDark, toggleTheme } = useThemeStore()
  const notifRef = useRef<HTMLDivElement>(null)
  const userRef = useRef<HTMLDivElement>(null)

  // Tải thông báo nhân sự (sync sự kiện thực ở backend).
  const loadNotifs = useCallback(() => {
    staffNotificationService
      .list()
      .then((d) => {
        setNotifs(d.items)
        setUnread(d.unreadCount)
      })
      .catch(() => {})
  }, [])

  // Tải khi vào trang / đổi route.
  useEffect(() => {
    loadNotifs()
  }, [location.pathname, loadNotifs])

  // Tải lại tức thời khi có push SignalR liên quan nhân sự (real-time).
  useEffect(() => {
    window.addEventListener(STAFF_NOTIF_REFRESH_EVENT, loadNotifs)
    return () => window.removeEventListener(STAFF_NOTIF_REFRESH_EVENT, loadNotifs)
  }, [loadNotifs])

  const markAllRead = async () => {
    setNotifs((prev) => prev.map((n) => ({ ...n, isRead: true })))
    setUnread(0)
    try {
      await staffNotificationService.markAllRead()
    } catch {
      /* nuốt lỗi — đã cập nhật lạc quan */
    }
  }

  const openNotif = (n: NotificationItem) => {
    if (!n.isRead) {
      setNotifs((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      setUnread((u) => Math.max(0, u - 1))
      staffNotificationService.markRead(n.id).catch(() => {})
    }
    setNotifOpen(false)
    if (n.link) navigate(n.link)
  }

  const removeNotif = (n: NotificationItem) => {
    setNotifs((prev) => prev.filter((x) => x.id !== n.id))
    if (!n.isRead) setUnread((u) => Math.max(0, u - 1))
    staffNotificationService.remove(n.id).catch(() => {})
  }

  const clearAll = async () => {
    setNotifs([])
    setUnread(0)
    try {
      await staffNotificationService.clearAll()
    } catch {
      /* nuốt lỗi — đã cập nhật lạc quan */
    }
  }

  // Sync theme class on mount and when theme changes
  useEffect(() => {
    if (typeof window !== 'undefined') {
      if (isDark) {
        document.documentElement.classList.add('dark')
      } else {
        document.documentElement.classList.remove('dark')
      }
    }
  }, [isDark])

  // Đóng dropdown khi click ra ngoài (dùng ref thay overlay full-screen để không chặn click nút bên trong)
  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node
      if (notifRef.current && !notifRef.current.contains(t)) setNotifOpen(false)
      if (userRef.current && !userRef.current.contains(t)) setUserMenuOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [])

  const isActive = (path: string) => {
    return location.pathname === path || location.pathname.startsWith(path + '/')
  }

  const handleLogout = () => {
    logout()
    navigate('/auth/login')
  }

  return (
    <div className="flex h-screen overflow-hidden bg-ink-50 dark:bg-ink-950">
      {/* Desktop Sidebar */}
      <aside className="hidden lg:flex w-64 shrink-0 flex-col border-r border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 h-screen">
        <div className="flex items-center gap-2.5 px-5 h-16 border-b border-ink-200 dark:border-white/10">
          <Logo />
          <div>
            <div className="font-display text-base font-extrabold leading-none">ARISP</div>
            <div className="text-[11px] text-ink-400 leading-none mt-0.5">HR Leader Workspace</div>
          </div>
        </div>
        <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-1 text-sm font-medium">
          {sidebarItems.map((item) => (
            <Link
              key={item.path}
              to={item.path}
              className={`flex items-center gap-3 rounded-xl px-3 py-2.5 ${
                isActive(item.path)
                  ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                  : 'text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
              }`}
            >
              <item.icon className="w-[18px] h-[18px]" />
              <span className="flex-1">{item.label}</span>
              {item.badge && (
                <span className="rounded-full bg-amber-100 dark:bg-amber-500/20 px-2 py-0.5 text-xs font-bold text-amber-700 dark:text-amber-400">
                  {item.badge}
                </span>
              )}
            </Link>
          ))}
          <div className="pt-3 mt-3 border-t border-ink-100 dark:border-white/10"></div>
          <Link
            to="/hr/settings"
            className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <CircleHelp className="w-[18px] h-[18px]" />
            Trợ giúp & tài liệu
          </Link>
        </nav>
      </aside>

      {/* Main Content */}
      <div className="flex-1 min-w-0 flex flex-col">
        {/* Topbar */}
        <header className="sticky top-0 z-20 flex items-center gap-3 border-b border-ink-200 dark:border-white/10 bg-white/80 dark:bg-white/5 backdrop-blur px-6 h-16">
          {/* Mobile menu button */}
          <button
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            className="lg:hidden grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <Menu className="w-5 h-5" />
          </button>

          {/* Search */}
          <div className="flex flex-1 items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-2 max-w-md focus-within:border-brand-400">
            <Search className="w-4 h-4 text-ink-400" />
            <input
              className="w-full bg-transparent text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400"
              placeholder="Tìm ứng viên, tin tuyển dụng..."
            />
            <kbd className="hidden sm:inline rounded border border-ink-200 dark:border-white/10 bg-white dark:bg-white/10 px-1.5 text-[10px] font-semibold text-ink-400">
              ⌘K
            </kbd>
          </div>

          <div className="ml-auto flex items-center gap-1">
            {/* Notifications */}
            <div className="relative" ref={notifRef}>
              <button
                onClick={() => setNotifOpen(!notifOpen)}
                aria-label="Thông báo"
                className="relative grid h-10 w-10 place-items-center rounded-xl text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
              >
                <Bell className="w-5 h-5" />
                {unread > 0 && (
                  <span className="absolute top-1.5 right-1.5 grid h-4 w-4 place-items-center rounded-full bg-red-500 text-[10px] font-bold text-white ring-2 ring-white dark:ring-transparent">
                    {unread > 9 ? '9+' : unread}
                  </span>
                )}
              </button>
              <AnimatePresence>
                {notifOpen && (
                  <motion.div
                    initial={{ opacity: 0, y: -10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    className="absolute right-0 mt-2 w-80 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 shadow-xl z-40"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="flex items-center justify-between gap-2 px-4 py-3 border-b border-ink-100 dark:border-white/10">
                      <span className="font-display font-bold text-sm text-ink-900 dark:text-white">
                        Thông báo
                      </span>
                      <div className="flex items-center gap-3">
                        {unread > 0 && (
                          <button
                            onClick={markAllRead}
                            className="text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline"
                          >
                            Đánh dấu đã đọc
                          </button>
                        )}
                        {notifs.length > 0 && (
                          <button
                            onClick={clearAll}
                            className="text-xs font-medium text-red-600 dark:text-red-400 hover:underline"
                          >
                            Xóa tất cả
                          </button>
                        )}
                      </div>
                    </div>
                    <div className="max-h-80 overflow-y-auto divide-y divide-ink-100 dark:divide-white/10">
                      {notifs.length === 0 ? (
                        <div className="px-4 py-8 text-center text-sm text-ink-400">
                          Không có thông báo mới
                        </div>
                      ) : (
                        notifs.map((n) => {
                          const Icon = notifIcon[n.type] ?? Bell
                          return (
                            <div
                              key={n.id}
                              role="button"
                              tabIndex={0}
                              onClick={() => openNotif(n)}
                              onKeyDown={(e) => e.key === 'Enter' && openNotif(n)}
                              className={`group relative flex w-full cursor-pointer gap-3 px-4 py-3 text-left hover:bg-ink-50 dark:hover:bg-white/5 ${
                                n.isRead ? '' : 'bg-brand-50/40 dark:bg-brand-500/10'
                              }`}
                            >
                              <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                                <Icon className="w-4 h-4" />
                              </span>
                              <span className="min-w-0 flex-1">
                                <span className="block text-sm text-ink-900 dark:text-white font-medium">
                                  {n.title}
                                </span>
                                {n.body && (
                                  <span className="block text-sm text-ink-600 dark:text-ink-300 truncate">
                                    {n.body}
                                  </span>
                                )}
                                <span className="text-xs text-ink-400">{timeAgo(n.createdAt)}</span>
                              </span>
                              {!n.isRead && (
                                <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500 group-hover:hidden" />
                              )}
                              <button
                                onClick={(e) => {
                                  e.stopPropagation()
                                  removeNotif(n)
                                }}
                                aria-label="Xóa thông báo"
                                className="absolute right-2 top-2 hidden h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-ink-200 hover:text-red-600 dark:hover:bg-white/10 dark:hover:text-red-400 group-hover:grid"
                              >
                                <X className="h-4 w-4" />
                              </button>
                            </div>
                          )
                        })
                      )}
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>

            {/* Theme toggle */}
            <button
              onClick={toggleTheme}
              aria-label="Đổi giao diện sáng/tối"
              className="grid h-10 w-10 place-items-center rounded-xl text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
            >
              {isDark ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
            </button>

            <span className="mx-1 h-6 w-px bg-ink-200 dark:bg-white/10"></span>

            {/* Create button */}
            <Link
              to="/hr/jobs/create"
              className="rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 flex items-center gap-2"
            >
              <Plus className="w-4 h-4" />
              <span className="hidden sm:inline">Tạo tin</span>
            </Link>

            <span className="mx-1 h-6 w-px bg-ink-200 dark:bg-white/10"></span>

            {/* User menu */}
            <div className="relative" ref={userRef}>
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-2 rounded-xl p-1 pr-2 hover:bg-ink-100 dark:hover:bg-white/10"
              >
                <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                  {user?.name?.charAt(0) || 'U'}
                </span>
                <span className="hidden md:block text-left leading-tight">
                  <span className="block text-sm font-semibold text-ink-900 dark:text-white">
                    {user?.name || 'User'}
                  </span>
                  <span className="block text-[11px] text-ink-400">HR Leader</span>
                </span>
                <ChevronDown className="w-4 h-4 text-ink-400" />
              </button>
              <AnimatePresence>
                {userMenuOpen && (
                  <motion.div
                    initial={{ opacity: 0, y: -10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    className="absolute right-0 mt-2 w-60 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 shadow-xl z-40"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="flex items-center gap-3 px-4 py-3 border-b border-ink-100 dark:border-white/10">
                      <span className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                        {user?.name?.charAt(0) || 'U'}
                      </span>
                      <div className="min-w-0">
                        <div className="truncate text-sm font-semibold text-ink-900 dark:text-white">
                          {user?.name || 'User'}
                        </div>
                        <div className="truncate text-xs text-ink-400">{user?.email}</div>
                      </div>
                    </div>
                    <div className="p-1.5 text-sm">
                      <Link
                        to="/hr/settings"
                        className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 dark:text-ink-200 hover:bg-ink-100 dark:hover:bg-white/10"
                      >
                        <User className="w-4 h-4 text-ink-400" /> Tài khoản của tôi
                      </Link>
                      <Link
                        to="/hr/settings"
                        className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 dark:text-ink-200 hover:bg-ink-100 dark:hover:bg-white/10"
                      >
                        <Settings className="w-4 h-4 text-ink-400" /> Cài đặt workspace
                      </Link>
                    </div>
                    <div className="p-1.5 border-t border-ink-100 dark:border-white/10">
                      <button
                        onClick={handleLogout}
                        className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/20"
                      >
                        <LogOut className="w-4 h-4" /> Đăng xuất
                      </button>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </div>
        </header>

        {/* Mobile Sidebar */}
        <AnimatePresence>
          {mobileMenuOpen && (
            <>
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className="lg:hidden fixed inset-0 bg-black/40 z-40"
                onClick={() => setMobileMenuOpen(false)}
              />
              <motion.aside
                initial={{ x: -280 }}
                animate={{ x: 0 }}
                exit={{ x: -280 }}
                className="lg:hidden fixed left-0 top-0 bottom-0 w-72 bg-white dark:bg-ink-900 z-50 flex flex-col border-r border-ink-200 dark:border-white/10"
              >
                <div className="flex items-center justify-between px-5 h-16 border-b border-ink-200 dark:border-white/10">
                  <div className="flex items-center gap-2.5">
                    <Logo />
                    <div>
                      <div className="font-display text-base font-extrabold leading-none text-ink-900 dark:text-white">
                        ARISP
                      </div>
                      <div className="text-[11px] text-ink-400 leading-none mt-0.5">HR Leader</div>
                    </div>
                  </div>
                  <button
                    onClick={() => setMobileMenuOpen(false)}
                    className="p-2 rounded-lg hover:bg-ink-100 dark:hover:bg-white/10"
                  >
                    <span className="text-2xl text-ink-400">&times;</span>
                  </button>
                </div>
                <nav className="flex-1 px-3 py-4 space-y-1 text-sm font-medium overflow-y-auto">
                  {sidebarItems.map((item) => (
                    <Link
                      key={item.path}
                      to={item.path}
                      onClick={() => setMobileMenuOpen(false)}
                      className={`flex items-center gap-3 rounded-xl px-3 py-2.5 ${
                        isActive(item.path)
                          ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                          : 'text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
                      }`}
                    >
                      <item.icon className="w-[18px] h-[18px]" />
                      <span className="flex-1">{item.label}</span>
                      {item.badge && (
                        <span className="rounded-full bg-amber-100 dark:bg-amber-500/20 px-2 py-0.5 text-xs font-bold text-amber-700 dark:text-amber-400">
                          {item.badge}
                        </span>
                      )}
                    </Link>
                  ))}
                </nav>
              </motion.aside>
            </>
          )}
        </AnimatePresence>

        {/* Page Content */}
        <main className="flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
