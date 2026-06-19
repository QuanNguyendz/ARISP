import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import {
  Search,
  MapPin,
  Briefcase,
  Clock,
  DollarSign,
  Bookmark,
  Globe,
  TrendingUp,
  Loader2,
  Sparkles,
  Menu,
  X,
  Bell,
  ChevronDown,
  Check,
  SlidersHorizontal,
  Code2,
  Server,
  BrainCircuit,
  BookOpen,
  FileText,
  Settings,
  LogOut,
  User,
  Moon,
  Sun,
} from 'lucide-react'
import { useAuthStore } from '@store/auth'
import jobService from '@/services/job/jobService'
import type { JobPosting } from '@/types/job'

// ============== CONSTANTS ==============
const POPULAR_KEYWORDS = ['Frontend', 'Backend', 'C# / .NET', 'AI Engineer']

const DEPARTMENTS = ['Engineering', 'Data & AI', 'DevOps / Infra', 'QA / Testing']
const EMPLOYMENT_TYPES = ['Full-time', 'Part-time', 'Hợp đồng', 'Thực tập']
const EXPERIENCE_LEVELS = ['Intern / Fresher', 'Junior', 'Middle', 'Senior', 'Lead / Manager']
const LOCATIONS = ['TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng']
const WORK_MODES = ['Onsite', 'Hybrid', 'Remote']

// ============== TYPE DEFINITIONS ==============
interface FiltersState {
  departments: string[]
  employmentTypes: string[]
  experienceLevels: string[]
  workModes: string[]
  locations: string[]
  salaryRange: string | null
  skills: string[]
  interviewLanguage: string[]
  minMatchScore: number
}

// ============== HELPER FUNCTIONS ==============
function formatSalary(job: JobPosting): string {
  if (job.salaryIsNegotiable) return 'Thỏa thuận'
  if (job.salaryMin && job.salaryMax) {
    return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`
  }
  if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`
  if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`
  return 'Thỏa thuận'
}

function formatWorkMode(mode?: string): string {
  if (!mode) return 'Full-time'
  const mappings: Record<string, string> = {
    fulltime: 'Full-time',
    parttime: 'Part-time',
    contract: 'Hợp đồng',
    internship: 'Thực tập',
  }
  return mappings[mode.toLowerCase()] || mode
}

function formatPostedDate(dateStr: string): string {
  try {
    const diffTime = Math.abs(new Date().getTime() - new Date(dateStr).getTime())
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24))
    if (diffDays <= 1) return 'Hôm nay'
    if (diffDays <= 2) return 'Hôm qua'
    return `${diffDays} ngày trước`
  } catch {
    return 'Gần đây'
  }
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml')) {
    return <BrainCircuit className="w-6 h-6" />
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return <Server className="w-6 h-6" />
  }
  return <Code2 className="w-6 h-6" />
}

function getIconColor(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) {
    return 'bg-ai-50 text-ai-600'
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return 'bg-emerald-50 text-emerald-600'
  }
  return 'bg-brand-50 text-brand-600'
}

// ============== HEADER COMPONENT ==============
function Header() {
  const { user, isAuthenticated, logout } = useAuthStore()
  const navigate = useNavigate()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [notifOpen, setNotifOpen] = useState(false)
  const [langMenuOpen, setLangMenuOpen] = useState(false)
  const [isDark, setIsDark] = useState(() => {
    if (typeof window !== 'undefined') {
      return document.documentElement.classList.contains('dark')
    }
    return false
  })

  const toggleTheme = () => {
    const newIsDark = !isDark
    setIsDark(newIsDark)
    if (newIsDark) {
      document.documentElement.classList.add('dark')
      localStorage.theme = 'dark'
    } else {
      document.documentElement.classList.remove('dark')
      localStorage.theme = 'light'
    }
  }

  return (
    <header className="sticky top-0 z-30 border-b border-ink-200 bg-white/80 backdrop-blur">
      <div className="mx-auto max-w-6xl px-4 sm:px-6 h-16 flex items-center gap-3">
        {/* Mobile menu */}
        <button
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-label="Menu"
          className="lg:hidden grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
        >
          {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
        </button>

        {/* Logo */}
        <a href="/" className="flex items-center gap-2.5 shrink-0">
          <svg
            className="h-9 w-9"
            viewBox="0 0 96 96"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-label="ARISP"
          >
            <defs>
              <linearGradient
                id="lg-jb"
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
            <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-jb)" />
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
          <span className="hidden sm:inline font-display text-lg font-extrabold text-ink-900">
            ARISP <span className="text-ink-400 font-medium">Careers</span>
          </span>
        </a>

        {/* Primary nav */}
        <nav className="hidden lg:flex items-center gap-1 text-sm font-medium text-ink-600">
          <a href="/" className="rounded-lg px-3 py-2 text-brand-700 bg-brand-50">
            Việc làm
          </a>
          <a
            href="/candidate/applications"
            className="flex items-center gap-1.5 rounded-lg px-3 py-2 hover:bg-ink-100"
          >
            Hồ sơ ứng tuyển
          </a>
          <a href="/interview/practice/demo" className="rounded-lg px-3 py-2 hover:bg-ink-100">
            Phỏng vấn thử
          </a>
        </nav>

        {/* Global search */}
        <div className="hidden md:flex flex-1 max-w-sm items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm focus-within:border-brand-400 focus-within:bg-white">
          <Search className="w-4 h-4 text-ink-400" />
          <input
            className="w-full bg-transparent outline-none placeholder:text-ink-400"
            placeholder="Tìm việc làm, kỹ năng..."
          />
          <kbd className="hidden lg:inline rounded border border-ink-200 bg-white px-1.5 text-[10px] font-semibold text-ink-400">
            ⌘K
          </kbd>
        </div>

        {/* Right cluster */}
        <div className="flex items-center gap-1 ml-auto">
          {/* Saved jobs */}
          <a
            href="#"
            aria-label="Việc đã lưu"
            className="relative hidden sm:grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
          >
            <Bookmark className="w-5 h-5" />
          </a>

          {/* Language toggle */}
          <div className="relative hidden sm:block">
            <button
              onClick={() => setLangMenuOpen(!langMenuOpen)}
              className="flex items-center gap-1 rounded-lg px-2 py-2 text-sm font-medium text-ink-600 hover:bg-ink-100"
            >
              <Globe className="w-[18px] h-[18px]" />
              VI <ChevronDown className="w-3.5 h-3.5" />
            </button>
            <AnimatePresence>
              {langMenuOpen && (
                <motion.div
                  initial={{ opacity: 0, y: -10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="absolute right-0 mt-2 w-40 rounded-xl border border-ink-200 bg-white shadow-xl z-40 p-1"
                  onClick={(e) => e.stopPropagation()}
                >
                  <button className="flex w-full items-center justify-between rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700">
                    Tiếng Việt <Check className="w-4 h-4" />
                  </button>
                  <button className="flex w-full items-center rounded-lg px-3 py-2 text-sm text-ink-600 hover:bg-ink-100">
                    English
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* Theme toggle */}
          <button
            onClick={toggleTheme}
            aria-label="Đổi giao diện sáng/tối"
            className="grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
          >
            {isDark ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
          </button>

          {/* Notifications */}
          <div className="relative">
            <button
              onClick={() => setNotifOpen(!notifOpen)}
              aria-label="Thông báo"
              className="relative grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
            >
              <Bell className="w-5 h-5" />
              <span className="absolute top-1.5 right-1.5 grid h-4 w-4 place-items-center rounded-full bg-red-500 text-[10px] font-bold text-white ring-2 ring-white">
                3
              </span>
            </button>
            <AnimatePresence>
              {notifOpen && (
                <motion.div
                  initial={{ opacity: 0, scale: 0.95 }}
                  animate={{ opacity: 1, scale: 1 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                  className="absolute right-0 mt-2 w-80 origin-top-right rounded-2xl border border-ink-200 bg-white shadow-xl z-40"
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className="flex items-center justify-between px-4 py-3 border-b border-ink-100">
                    <span className="font-display font-bold text-sm text-ink-900">Thông báo</span>
                    <button className="text-xs font-medium text-brand-600 hover:underline">
                      Đánh dấu đã đọc
                    </button>
                  </div>
                  <div className="max-h-80 overflow-y-auto divide-y divide-ink-100">
                    <a href="#" className="flex gap-3 px-4 py-3 hover:bg-ink-50">
                      <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-50 text-brand-600">
                        <Clock className="w-4 h-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm text-ink-700">
                          <b className="text-ink-900">Lời mời phỏng vấn vòng 2</b> — Senior Frontend
                          Engineer
                        </span>
                        <span className="text-xs text-ink-400">2 giờ trước</span>
                      </span>
                      <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
                    </a>
                    <a href="#" className="flex gap-3 px-4 py-3 hover:bg-ink-50">
                      <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-emerald-50 text-emerald-600">
                        <Check className="w-4 h-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm text-ink-700">
                          <b className="text-ink-900">Kết quả vòng 1: Pass</b> — bạn đã vượt qua
                          vòng screening
                        </span>
                        <span className="text-xs text-ink-400">Hôm qua</span>
                      </span>
                      <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
                    </a>
                  </div>
                  <a
                    href="#"
                    className="block px-4 py-3 text-center text-sm font-medium text-brand-600 hover:bg-ink-50 rounded-b-2xl border-t border-ink-100"
                  >
                    Xem tất cả thông báo
                  </a>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* User menu */}
          <div className="relative">
            {isAuthenticated && user ? (
              <>
                <button
                  onClick={() => setUserMenuOpen(!userMenuOpen)}
                  className="flex items-center gap-1.5 rounded-lg p-1 pr-1.5 hover:bg-ink-100"
                >
                  <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                    {user.name?.charAt(0) || 'U'}
                  </span>
                  <ChevronDown className="hidden sm:block w-4 h-4 text-ink-400" />
                </button>
                <AnimatePresence>
                  {userMenuOpen && (
                    <motion.div
                      initial={{ opacity: 0, y: -10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                      className="absolute right-0 mt-2 w-64 rounded-2xl border border-ink-200 bg-white shadow-xl z-40"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <div className="flex items-center gap-3 px-4 py-3 border-b border-ink-100">
                        <span className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                          {user.name?.charAt(0) || 'U'}
                        </span>
                        <div className="min-w-0">
                          <div className="truncate text-sm font-semibold text-ink-900">
                            {user.name || 'User'}
                          </div>
                          <div className="truncate text-xs text-ink-400">
                            {user.email || 'email@example.com'}
                          </div>
                        </div>
                      </div>
                      <div className="p-1.5 text-sm">
                        <a
                          href="/candidate/profile"
                          className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                        >
                          <User className="w-4 h-4 text-ink-400" /> Hồ sơ của tôi
                        </a>
                        <a
                          href="/candidate/applications"
                          className="flex items-center justify-between rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                        >
                          <span className="flex items-center gap-3">
                            <FileText className="w-4 h-4 text-ink-400" /> Đơn ứng tuyển
                          </span>
                        </a>
                        <a
                          href="/candidate/applications"
                          className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                        >
                          <Bookmark className="w-4 h-4 text-ink-400" /> Việc đã lưu
                        </a>
                        <a
                          href="/candidate/results"
                          className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                        >
                          <BookOpen className="w-4 h-4 text-ink-400" /> Kết quả & lịch phỏng vấn
                        </a>
                        <a
                          href="/candidate/settings"
                          className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                        >
                          <Settings className="w-4 h-4 text-ink-400" /> Cài đặt
                        </a>
                      </div>
                      <div className="p-1.5 border-t border-ink-100">
                        <button
                          onClick={() => {
                            logout()
                            navigate('/')
                          }}
                          className="flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 w-full"
                        >
                          <LogOut className="w-4 h-4" /> Đăng xuất
                        </button>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </>
            ) : (
              <div className="flex items-center gap-2">
                <a
                  href="/auth/candidate-login"
                  className="px-4 py-2 text-sm font-medium text-ink-600 hover:text-ink-900"
                >
                  Đăng nhập
                </a>
                <a
                  href="/auth/candidate-register"
                  className="px-4 py-2 rounded-lg bg-brand-600 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  Đăng ký
                </a>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Mobile menu panel */}
      <AnimatePresence>
        {mobileMenuOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="lg:hidden border-t border-ink-100 bg-white px-4 py-3 space-y-1"
          >
            <div className="mb-2 flex items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm">
              <Search className="w-4 h-4 text-ink-400" />
              <input
                className="w-full bg-transparent outline-none placeholder:text-ink-400"
                placeholder="Tìm việc làm, kỹ năng..."
              />
            </div>
            <a
              href="/"
              className="block rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700"
            >
              Việc làm
            </a>
            <a
              href="/candidate/applications"
              className="block rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100"
            >
              Hồ sơ ứng tuyển
            </a>
            <a
              href="/interview/practice/demo"
              className="block rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100"
            >
              Phỏng vấn thử
            </a>
            <a
              href="#"
              className="block rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100"
            >
              Việc đã lưu
            </a>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  )
}

// ============== FILTER SIDEBAR COMPONENT ==============
interface FilterSidebarProps {
  filters: FiltersState
  setFilters: React.Dispatch<React.SetStateAction<FiltersState>>
  onClearAll: () => void
}

function FilterSidebar({ filters, setFilters, onClearAll }: FilterSidebarProps) {
  const hasActiveFilters =
    filters.departments.length > 0 ||
    filters.employmentTypes.length > 0 ||
    filters.experienceLevels.length > 0 ||
    filters.workModes.length > 0 ||
    filters.locations.length > 0 ||
    filters.salaryRange !== null ||
    filters.minMatchScore > 0

  const toggleStringFilter = (
    key:
      | 'departments'
      | 'employmentTypes'
      | 'experienceLevels'
      | 'workModes'
      | 'locations'
      | 'skills'
      | 'interviewLanguage',
    value: string
  ) => {
    setFilters((prev) => ({
      ...prev,
      [key]: (prev[key] as string[]).includes(value)
        ? (prev[key] as string[]).filter((v) => v !== value)
        : [...(prev[key] as string[]), value],
    }))
  }

  const activeFilters = [
    ...filters.employmentTypes.map((v) => ({ key: 'employmentTypes' as const, value: v })),
    ...filters.experienceLevels.map((v) => ({ key: 'experienceLevels' as const, value: v })),
  ]

  return (
    <aside className="space-y-6">
      <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
        <div className="flex items-center justify-between">
          <span className="font-semibold flex items-center gap-2">
            <SlidersHorizontal className="w-4 h-4 text-brand-600" />
            Bộ lọc
          </span>
          {hasActiveFilters && (
            <button
              onClick={onClearAll}
              className="text-xs font-medium text-brand-600 hover:underline"
            >
              Xoá tất cả
            </button>
          )}
        </div>

        {/* Active filters */}
        {activeFilters.length > 0 && (
          <div className="mt-4 flex flex-wrap gap-1.5">
            {activeFilters.map(({ key, value }) => (
              <span
                key={`${key}-${value}`}
                className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-medium text-brand-700 cursor-pointer hover:bg-brand-100"
                onClick={() => toggleStringFilter(key, value)}
              >
                {value} <X className="w-3 h-3" />
              </span>
            ))}
          </div>
        )}

        <div className="mt-5 divide-y divide-ink-100 text-sm">
          {/* Phòng ban */}
          <div className="pb-4">
            <div className="mb-2.5 font-semibold text-ink-700">Phòng ban</div>
            {DEPARTMENTS.map((dept) => (
              <label
                key={dept}
                className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer"
              >
                <input
                  type="checkbox"
                  checked={filters.departments.includes(dept)}
                  onChange={() => toggleStringFilter('departments', dept)}
                  className="rounded border-ink-300 text-brand-600"
                />
                {dept}
              </label>
            ))}
          </div>

          {/* Hình thức */}
          <div className="py-4">
            <div className="mb-2.5 font-semibold text-ink-700">Hình thức</div>
            {EMPLOYMENT_TYPES.map((type) => (
              <label
                key={type}
                className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer"
              >
                <input
                  type="checkbox"
                  checked={filters.employmentTypes.includes(type)}
                  onChange={() => toggleStringFilter('employmentTypes', type)}
                  className="rounded border-ink-300 text-brand-600"
                />
                {type}
              </label>
            ))}
          </div>

          {/* Cấp bậc */}
          <div className="py-4">
            <div className="mb-2.5 font-semibold text-ink-700">Cấp bậc</div>
            {EXPERIENCE_LEVELS.map((level) => (
              <label
                key={level}
                className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer"
              >
                <input
                  type="checkbox"
                  checked={filters.experienceLevels.includes(level)}
                  onChange={() => toggleStringFilter('experienceLevels', level)}
                  className="rounded border-ink-300 text-brand-600"
                />
                {level}
              </label>
            ))}
          </div>

          {/* Hình thức làm việc */}
          <div className="py-4">
            <div className="mb-2.5 font-semibold text-ink-700">Nơi làm việc</div>
            <div className="grid grid-cols-3 gap-1.5">
              {WORK_MODES.map((mode) => (
                <button
                  key={mode}
                  onClick={() => {
                    setFilters((prev) => ({
                      ...prev,
                      workModes: prev.workModes.includes(mode)
                        ? prev.workModes.filter((m) => m !== mode)
                        : [...prev.workModes, mode],
                    }))
                  }}
                  className={`rounded-lg border px-2 py-1.5 text-xs font-medium transition-colors ${
                    filters.workModes.includes(mode)
                      ? 'border-brand-300 bg-brand-50 text-brand-700'
                      : 'border-ink-200 text-ink-600 hover:border-brand-300'
                  }`}
                >
                  {mode}
                </button>
              ))}
            </div>
            <div className="mt-3 mb-2 font-medium text-ink-600">Địa điểm</div>
            {LOCATIONS.map((loc) => (
              <label
                key={loc}
                className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer"
              >
                <input
                  type="checkbox"
                  checked={filters.locations.includes(loc)}
                  onChange={() => toggleStringFilter('locations', loc)}
                  className="rounded border-ink-300 text-brand-600"
                />
                {loc}
              </label>
            ))}
          </div>

          {/* Kỹ năng */}
          <div className="py-4">
            <div className="mb-2.5 font-semibold text-ink-700">Kỹ năng / Công nghệ</div>
            <div className="flex flex-wrap gap-1.5">
              {POPULAR_KEYWORDS.map((skill) => (
                <button
                  key={skill}
                  onClick={() => {
                    setFilters((prev) => ({
                      ...prev,
                      skills: prev.skills.includes(skill)
                        ? prev.skills.filter((s) => s !== skill)
                        : [...prev.skills, skill],
                    }))
                  }}
                  className={`rounded-lg px-2.5 py-1 text-xs font-medium transition-colors ${
                    filters.skills.includes(skill)
                      ? 'bg-brand-600 text-white'
                      : 'border border-ink-200 text-ink-600 hover:border-brand-300'
                  }`}
                >
                  {skill}
                </button>
              ))}
            </div>
          </div>

          {/* Ngôn ngữ phỏng vấn */}
          <div className="pt-4">
            <div className="mb-1 font-semibold text-ink-700">Ngôn ngữ phỏng vấn</div>
            <p className="mb-2 text-xs text-ink-400">AI phỏng vấn theo ngôn ngữ của JD</p>
            <label className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer">
              <input
                type="checkbox"
                checked={filters.interviewLanguage.includes('Tiếng Việt')}
                onChange={() => toggleStringFilter('interviewLanguage', 'Tiếng Việt')}
                className="rounded border-ink-300 text-brand-600"
              />
              Tiếng Việt
            </label>
            <label className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer">
              <input
                type="checkbox"
                checked={filters.interviewLanguage.includes('English')}
                onChange={() => toggleStringFilter('interviewLanguage', 'English')}
                className="rounded border-ink-300 text-brand-600"
              />
              English
            </label>
          </div>
        </div>
      </div>

      {/* Tip card */}
      <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-5 shadow-card">
        <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
          <Sparkles className="w-4 h-4" />
          Mẹo
        </div>
        <p className="mt-2 text-sm text-ink-500">
          Tải CV lên để AI gợi ý việc làm khớp nhất và xem điểm Match trước khi ứng tuyển.
        </p>
        <button
          onClick={() => (window.location.href = '/auth/candidate-register')}
          className="mt-3 w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
        >
          Tải CV lên
        </button>
      </div>
    </aside>
  )
}

// ============== JOB CARD COMPONENT ==============
interface JobCardProps {
  job: JobPosting
}

function JobCard({ job }: JobCardProps) {
  const navigate = useNavigate()
  const [isSaved, setIsSaved] = useState(false)

  return (
    <article
      className="group rounded-2xl border border-ink-200 bg-white p-5 shadow-card hover:shadow-card-hover hover:border-brand-200 transition cursor-pointer"
      onClick={() => navigate(`/jobs/${job.id}`)}
    >
      <div className="flex gap-4">
        <div
          className={`grid h-12 w-12 shrink-0 place-items-center rounded-xl ${getIconColor(job.department)}`}
        >
          {getJobIcon(job.department)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-ink-900 group-hover:text-brand-700">{job.title}</h3>
              <div className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-500">
                <MapPin className="w-3.5 h-3.5" />
                {job.location || 'Remote'} · {job.department || 'Engineering'}
              </div>
            </div>
            <button
              onClick={(e) => {
                e.stopPropagation()
                setIsSaved(!isSaved)
              }}
              className={`shrink-0 p-2 rounded-lg transition-colors ${isSaved ? 'text-ai-600' : 'text-ink-300 hover:text-ai-600'}`}
            >
              <Bookmark className={`w-5 h-5 ${isSaved ? 'fill-current' : ''}`} />
            </button>
          </div>
          <div className="mt-3 flex flex-wrap gap-2 text-xs">
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <Briefcase className="w-3.5 h-3.5" />
              {formatWorkMode(job.workMode || job.employmentType)}
            </span>
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <DollarSign className="w-3.5 h-3.5" />
              {formatSalary(job)}
            </span>
            {job.experienceLevel && (
              <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
                <TrendingUp className="w-3.5 h-3.5" />
                {job.experienceLevel}
              </span>
            )}
          </div>
          <div className="mt-3 flex items-center justify-between">
            <span className="text-xs text-ink-400">Đăng {formatPostedDate(job.createdAt)}</span>
            <button
              onClick={(e) => {
                e.stopPropagation()
                navigate(`/jobs/${job.id}`)
              }}
              className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-1.5 text-sm font-semibold text-brand-700 hover:bg-brand-100"
            >
              Ứng tuyển
            </button>
          </div>
        </div>
      </div>
    </article>
  )
}

// ============== MAIN PAGE COMPONENT ==============
export default function FindJob() {
  const [searchQuery, setSearchQuery] = useState('')
  const [jobs, setJobs] = useState<JobPosting[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<FiltersState>({
    departments: [],
    employmentTypes: [],
    experienceLevels: [],
    workModes: [],
    locations: [],
    salaryRange: null,
    skills: [],
    interviewLanguage: [],
    minMatchScore: 0,
  })
  const [sortBy, setSortBy] = useState<'newest' | 'match' | 'salary'>('newest')

  // Load jobs from API
  useEffect(() => {
    async function loadJobs() {
      try {
        setLoading(true)
        const data = await jobService.getPublicJobPostings()
        setJobs(data)
      } catch (err: any) {
        console.error('Failed to load jobs:', err)
        setError('Không thể kết nối máy chủ để tải danh sách công việc.')
      } finally {
        setLoading(false)
      }
    }
    loadJobs()
  }, [])

  // Filter jobs
  const filteredJobs = jobs.filter((job) => {
    // Search filter
    const matchesSearch =
      job.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (job.department || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
      (job.skills || []).some((s) => s.toLowerCase().includes(searchQuery.toLowerCase()))

    // Department filter
    const matchesDepartment =
      filters.departments.length === 0 ||
      filters.departments.some((d) => job.department?.toLowerCase().includes(d.toLowerCase()))

    // Employment type filter
    const matchesEmployment =
      filters.employmentTypes.length === 0 ||
      filters.employmentTypes.some((et) => {
        const jobType = formatWorkMode(job.employmentType || job.workMode)
        return jobType === et
      })

    // Experience level filter
    const matchesExperience =
      filters.experienceLevels.length === 0 ||
      filters.experienceLevels.includes(job.experienceLevel || '')

    // Work mode filter
    const matchesWorkMode =
      filters.workModes.length === 0 ||
      filters.workModes.some((wm) => {
        const mode = job.workMode?.toLowerCase() || ''
        return (
          (wm === 'Onsite' && (mode === 'onsite' || mode === 'fulltime')) ||
          (wm === 'Hybrid' && mode === 'hybrid') ||
          (wm === 'Remote' && (job.interviewMode === 'remote' || mode === 'remote'))
        )
      })

    // Location filter
    const matchesLocation =
      filters.locations.length === 0 ||
      filters.locations.some((loc) => job.location?.toLowerCase().includes(loc.toLowerCase()))

    // Skills filter
    const matchesSkills =
      filters.skills.length === 0 ||
      filters.skills.some((skill) =>
        job.skills?.some((s) => s.toLowerCase().includes(skill.toLowerCase()))
      )

    return (
      matchesSearch &&
      matchesDepartment &&
      matchesEmployment &&
      matchesExperience &&
      matchesWorkMode &&
      matchesLocation &&
      matchesSkills
    )
  })

  // Sort jobs
  const sortedJobs = [...filteredJobs].sort((a, b) => {
    switch (sortBy) {
      case 'newest':
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      case 'salary':
        return (b.salaryMax || 0) - (a.salaryMax || 0)
      default:
        return 0
    }
  })

  const clearFilters = () => {
    setFilters({
      departments: [],
      employmentTypes: [],
      experienceLevels: [],
      workModes: [],
      locations: [],
      salaryRange: null,
      skills: [],
      interviewLanguage: [],
      minMatchScore: 0,
    })
    setSearchQuery('')
  }

  return (
    <div className="min-h-screen bg-ink-50">
      <Header />

      {/* Hero + search */}
      <section className="relative overflow-hidden border-b border-ink-200 bg-white">
        <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-white to-ai-50" />
        <div className="relative mx-auto max-w-6xl px-6 py-14">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-ai-50 px-3 py-1 text-xs font-semibold text-ai-700 ring-1 ring-ai-200">
            <Sparkles className="w-3.5 h-3.5" />
            Phỏng vấn AI tự động · Đánh giá khách quan
          </span>
          <h1 className="mt-4 font-display text-4xl sm:text-5xl font-extrabold leading-[1.4] max-w-2xl text-ink-900">
            Tìm công việc IT phù hợp với{' '}
            <span className="inline-block pb-1 bg-gradient-to-r from-brand-600 to-ai-600 bg-clip-text text-transparent">
              bạn nhất
            </span>
          </h1>
          <p className="mt-3 max-w-xl text-ink-500">
            Ứng tuyển trực tiếp, AI phân tích độ phù hợp CV–JD và phỏng vấn bạn qua nhiều vòng —
            minh bạch, không thiên vị.
          </p>

          {/* Search bar */}
          <div className="mt-7 rounded-2xl border border-ink-200 bg-white p-2 shadow-card flex flex-col gap-2 sm:flex-row">
            <div className="flex flex-1 items-center gap-2 rounded-xl px-3 py-2.5 bg-ink-50">
              <Search className="w-5 h-5 text-ink-400" />
              <input
                className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                placeholder="Chức danh, kỹ năng (React, .NET...)"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className="hidden sm:block w-px bg-ink-200 my-1.5" />
            <div className="flex flex-1 items-center gap-2 rounded-xl px-3 py-2.5 bg-ink-50">
              <MapPin className="w-5 h-5 text-ink-400" />
              <input
                className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                placeholder="Địa điểm"
              />
            </div>
            <button className="rounded-xl bg-brand-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-brand-700">
              Tìm kiếm
            </button>
          </div>

          {/* Popular keywords */}
          <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-ink-500">
            <span>Phổ biến:</span>
            {POPULAR_KEYWORDS.map((keyword) => (
              <button
                key={keyword}
                onClick={() => setSearchQuery(keyword)}
                className="rounded-full bg-white px-3 py-1 ring-1 ring-ink-200 hover:ring-brand-300 hover:text-brand-700"
              >
                {keyword}
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* Body: filters + list */}
      <main className="mx-auto max-w-6xl px-6 py-10 grid gap-8 lg:grid-cols-[260px_1fr]">
        {/* Filters */}
        <FilterSidebar filters={filters} setFilters={setFilters} onClearAll={clearFilters} />

        {/* Job List */}
        <section>
          {/* Section Header */}
          <div className="flex items-center justify-between">
            <h2 className="font-display text-lg font-bold text-ink-900">
              {loading ? 'Đang tải...' : `${sortedJobs.length} việc làm phù hợp`}
            </h2>
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as typeof sortBy)}
              className="rounded-xl border border-ink-200 px-3 py-2 text-sm outline-none focus:border-brand-500 cursor-pointer"
            >
              <option value="newest">Mới nhất</option>
              <option value="match">Phù hợp nhất</option>
              <option value="salary">Lương cao</option>
            </select>
          </div>

          {error && (
            <div className="mt-4 p-4 rounded-xl bg-red-50 border border-red-200 text-red-700 text-sm">
              {error}
            </div>
          )}

          {/* Job List */}
          <div className="mt-5 space-y-4">
            {loading ? (
              <div className="flex flex-col items-center justify-center py-20 gap-3">
                <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
                <p className="text-sm text-ink-500">Đang tải tin tuyển dụng mới nhất...</p>
              </div>
            ) : sortedJobs.length === 0 ? (
              <div className="rounded-2xl border border-ink-200 bg-white p-12 text-center">
                <p className="text-ink-500">
                  Không tìm thấy tin tuyển dụng nào phù hợp với bộ lọc tìm kiếm.
                </p>
                <button
                  onClick={clearFilters}
                  className="mt-4 px-4 py-2 rounded-lg bg-brand-600 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  Xóa bộ lọc
                </button>
              </div>
            ) : (
              sortedJobs.map((job) => <JobCard key={job.id} job={job} />)
            )}
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="border-t border-ink-200 bg-white">
        <div className="mx-auto max-w-6xl px-6 py-8 text-sm text-ink-400 flex items-center justify-between">
          <span>© 2026 ARISP — Nền tảng tuyển dụng & phỏng vấn AI</span>
          <span className="flex items-center gap-1.5">
            <Check className="w-4 h-4" />
            Đánh giá minh bạch
          </span>
        </div>
      </footer>
    </div>
  )
}
