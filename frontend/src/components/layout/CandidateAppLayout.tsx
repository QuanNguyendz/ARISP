import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  Menu,
  Moon,
  Sun,
  Bell,
  ChevronDown,
  User,
  FileText,
  Bookmark,
  Clapperboard,
  Settings,
  LogOut,
  ShieldCheck,
} from 'lucide-react'
import { useAuthStore } from '@store/auth/authStore'
import { authService } from '@services/auth/authService'

function Logo() {
  return (
    <svg className="h-9 w-9" viewBox="0 0 96 96" fill="none" xmlns="http://www.w3.org/2000/svg" aria-label="ARISP">
      <defs>
        <linearGradient id="lg-cand-app" x1="12" y1="10" x2="84" y2="86" gradientUnits="userSpaceOnUse">
          <stop stopColor="#4f46e5" />
          <stop offset="1" stopColor="#9333ea" />
        </linearGradient>
      </defs>
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-cand-app)" />
      <path d="M30 70 L48 26 L66 70" stroke="white" strokeWidth="8" strokeLinecap="round" strokeLinejoin="round" />
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

const navItems = [
  { label: 'Việc làm', to: '/jobs', end: false },
  { label: 'Hồ sơ ứng tuyển', to: '/candidate/applications', end: false },
  { label: 'Phỏng vấn thử', to: '/candidate/interviews', end: false },
]

const userMenuItems = [
  { label: 'Hồ sơ của tôi', to: '/candidate/profile', icon: User },
  { label: 'Đơn ứng tuyển', to: '/candidate/applications', icon: FileText },
  { label: 'Việc đã lưu', to: '/candidate/saved-jobs', icon: Bookmark },
  { label: 'Kết quả & lịch phỏng vấn', to: '/candidate/results', icon: Clapperboard },
  { label: 'Cài đặt', to: '/candidate/settings', icon: Settings },
]

export default function CandidateAppLayout() {
  const navigate = useNavigate()
  const { user, logout } = useAuthStore()
  const [isDark, setIsDark] = useState(() => document.documentElement.classList.contains('dark'))
  const [menuOpen, setMenuOpen] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false)
    }
    document.addEventListener('click', onClick)
    return () => document.removeEventListener('click', onClick)
  }, [])

  const toggleTheme = () => {
    const next = !isDark
    setIsDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.theme = next ? 'dark' : 'light'
  }

  const handleLogout = async () => {
    await authService.logout()
    logout()
    navigate('/auth/candidate-login')
  }

  const initials = initialsOf(user?.name, user?.email)

  return (
    <div className="flex min-h-screen flex-col bg-ink-50 text-ink-900">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-3 px-4 sm:px-6">
          <button
            onClick={() => setMobileOpen((v) => !v)}
            aria-label="Menu"
            className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-600 hover:bg-ink-100 lg:hidden"
          >
            <Menu className="h-5 w-5" />
          </button>

          <Link to="/jobs" className="flex shrink-0 items-center gap-2.5">
            <Logo />
            <span className="hidden font-display text-lg font-extrabold sm:inline">
              ARISP <span className="font-medium text-ink-400">Careers</span>
            </span>
          </Link>

          <nav className="hidden items-center gap-1 text-sm font-medium text-ink-600 lg:flex">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `rounded-lg px-3 py-2 ${isActive ? 'bg-brand-50 text-brand-700' : 'hover:bg-ink-100'}`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-1">
            <button
              onClick={toggleTheme}
              aria-label="Đổi giao diện sáng/tối"
              className="grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
            >
              {isDark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
            </button>

            <Link
              to="/candidate/notifications"
              aria-label="Thông báo"
              className="relative grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
            >
              <Bell className="h-5 w-5" />
            </Link>

            {/* User menu */}
            <div className="relative" ref={menuRef}>
              <button
                onClick={() => setMenuOpen((v) => !v)}
                className="flex items-center gap-1.5 rounded-lg p-1 pr-1.5 hover:bg-ink-100"
              >
                <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                  {initials}
                </span>
                <ChevronDown className="hidden h-4 w-4 text-ink-400 sm:block" />
              </button>

              {menuOpen && (
                <div className="absolute right-0 mt-2 w-64 rounded-2xl border border-ink-200 bg-white shadow-xl">
                  <div className="flex items-center gap-3 border-b border-ink-100 px-4 py-3">
                    <span className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                      {initials}
                    </span>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-ink-900">{user?.name || 'Ứng viên'}</div>
                      <div className="truncate text-xs text-ink-400">{user?.email}</div>
                    </div>
                  </div>
                  <div className="p-1.5 text-sm">
                    {userMenuItems.map((item) => {
                      const Icon = item.icon
                      return (
                        <NavLink
                          key={item.to}
                          to={item.to}
                          onClick={() => setMenuOpen(false)}
                          className={({ isActive }) =>
                            `flex items-center gap-3 rounded-lg px-3 py-2 ${
                              isActive ? 'bg-brand-50 text-brand-700' : 'text-ink-700 hover:bg-ink-100'
                            }`
                          }
                        >
                          <Icon className="h-4 w-4 text-ink-400" /> {item.label}
                        </NavLink>
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
            </div>
          </div>
        </div>

        {/* Mobile menu */}
        {mobileOpen && (
          <div className="space-y-1 border-t border-ink-100 bg-white px-4 py-3 lg:hidden">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={() => setMobileOpen(false)}
                className={({ isActive }) =>
                  `block rounded-lg px-3 py-2 text-sm ${
                    isActive ? 'bg-brand-50 font-medium text-brand-700' : 'text-ink-700 hover:bg-ink-100'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </div>
        )}
      </header>

      {/* Content */}
      <main className="flex-1">
        <Outlet />
      </main>

      {/* Footer */}
      <footer className="border-t border-ink-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-8 text-sm text-ink-400">
          <span>© 2026 ARISP — Nền tảng tuyển dụng &amp; phỏng vấn AI</span>
          <span className="flex items-center gap-1.5">
            <ShieldCheck className="h-4 w-4" /> Đánh giá minh bạch
          </span>
        </div>
      </footer>
    </div>
  )
}
