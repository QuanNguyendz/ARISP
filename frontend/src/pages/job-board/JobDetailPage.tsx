import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import {
  MapPin,
  DollarSign,
  Clock,
  Loader2,
  Send,
  Languages,
  Briefcase,
  Sparkles,
  Users,
  HeartPulse,
  Plane,
  GraduationCap,
  Home,
  Code2,
  Server,
  BrainCircuit,
  CheckCircle2,
  Bookmark,
  Bell,
  Moon,
  Sun,
  Menu,
  X,
  Search,
  Globe,
  ChevronDown,
  ChevronRight,
  LogOut,
  User,
  Settings,
} from 'lucide-react'
import jobService from '@/services/job/jobService'
import type { JobPosting } from '@/types/job'
import { useAuthStore } from '@store/auth/authStore'

// Logo component
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
          id="lg-jd-fe"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-jd-fe)" />
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

function formatPostedDate(dateStr?: string): string {
  if (!dateStr) return 'Mới đăng'
  try {
    const diff = Math.abs(new Date().getTime() - new Date(dateStr).getTime())
    const days = Math.ceil(diff / (1000 * 60 * 60 * 24))
    if (days <= 1) return 'Hôm nay'
    if (days <= 2) return 'Hôm qua'
    return `${days} ngày trước`
  } catch {
    return 'Gần đây'
  }
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml')) {
    return <BrainCircuit className="w-8 h-8" />
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return <Server className="w-8 h-8" />
  }
  return <Code2 className="w-8 h-8" />
}

function getIconBg(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) {
    return 'bg-ai-50 text-ai-600'
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return 'bg-emerald-50 text-emerald-600'
  }
  return 'bg-brand-50 text-brand-600'
}

// Nav component - giống FindJobPage
function Nav() {
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
        <Link to="/" className="flex items-center gap-2.5 shrink-0">
          <Logo />
          <span className="hidden sm:inline font-display text-lg font-extrabold text-ink-900">
            ARISP <span className="text-ink-400 font-medium">Careers</span>
          </span>
        </Link>

        {/* Primary nav */}
        <nav className="hidden lg:flex items-center gap-1 text-sm font-medium text-ink-600">
          <Link to="/jobs" className="rounded-lg px-3 py-2 text-brand-700 bg-brand-50">
            Việc làm
          </Link>
          <Link
            to="/candidate/applications"
            className="flex items-center gap-1.5 rounded-lg px-3 py-2 hover:bg-ink-100"
          >
            Hồ sơ ứng tuyển
          </Link>
          <Link to="/interview/practice/demo" className="rounded-lg px-3 py-2 hover:bg-ink-100">
            Phỏng vấn thử
          </Link>
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
          <Link
            to="#"
            aria-label="Việc đã lưu"
            className="relative hidden sm:grid h-9 w-9 place-items-center rounded-lg text-ink-600 hover:bg-ink-100"
          >
            <Bookmark className="w-5 h-5" />
          </Link>

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
                    Tiếng Việt <CheckCircle2 className="w-4 h-4" />
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
              <span className="absolute top-1.5 right-1.5 grid h-4 w-4 place-items-center rounded-full bg-red-500 text-[10px] font-bold text-white ring-2 ring-white dark:ring-ink-50">
                3
              </span>
            </button>
            <AnimatePresence>
              {notifOpen && (
                <motion.div
                  initial={{ opacity: 0, y: -10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  className="absolute right-0 mt-2 w-80 rounded-2xl border border-ink-200 bg-white shadow-xl z-40"
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className="flex items-center justify-between px-4 py-3 border-b border-ink-100">
                    <span className="font-display font-bold text-sm text-ink-900">Thông báo</span>
                    <button className="text-xs font-medium text-brand-600 hover:underline">
                      Đánh dấu đã đọc
                    </button>
                  </div>
                  <div className="max-h-80 overflow-y-auto divide-y divide-ink-100">
                    <Link to="#" className="flex gap-3 px-4 py-3 hover:bg-ink-50">
                      <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-50 text-brand-600">
                        <Sparkles className="w-4 h-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm text-ink-700">
                          <b className="text-ink-900">Lời mời phỏng vấn</b> — Senior Frontend
                          Engineer
                        </span>
                        <span className="text-xs text-ink-400">2 giờ trước</span>
                      </span>
                      <span className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-500"></span>
                    </Link>
                    <Link to="#" className="flex gap-3 px-4 py-3 hover:bg-ink-50 opacity-60">
                      <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-full bg-ink-100 text-ink-500">
                        <Bell className="w-4 h-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm text-ink-700">
                          Nhà tuyển dụng đã xem hồ sơ
                        </span>
                        <span className="text-xs text-ink-400">1 ngày trước</span>
                      </span>
                    </Link>
                  </div>
                  <Link
                    to="#"
                    className="block px-4 py-3 text-center text-sm font-medium text-brand-600 hover:bg-ink-50 rounded-b-2xl border-t border-ink-100"
                  >
                    Xem tất cả thông báo
                  </Link>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          <span className="mx-1 hidden sm:block h-5 w-px bg-ink-200"></span>

          {isAuthenticated ? (
            <div className="relative">
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-2 rounded-xl p-1 pr-2 hover:bg-ink-100"
              >
                <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                  {user?.name?.charAt(0) || 'U'}
                </span>
                <ChevronDown className="hidden sm:block w-4 h-4 text-ink-400" />
              </button>
              <AnimatePresence>
                {userMenuOpen && (
                  <motion.div
                    initial={{ opacity: 0, y: -10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    className="absolute right-0 mt-2 w-60 rounded-2xl border border-ink-200 bg-white shadow-xl z-40"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <div className="flex items-center gap-3 px-4 py-3 border-b border-ink-100">
                      <span className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                        {user?.name?.charAt(0) || 'U'}
                      </span>
                      <div className="min-w-0">
                        <div className="truncate text-sm font-semibold text-ink-900">
                          {user?.name || 'User'}
                        </div>
                        <div className="truncate text-xs text-ink-400">{user?.email}</div>
                      </div>
                    </div>
                    <div className="p-1.5 text-sm">
                      <Link
                        to="/candidate/applications"
                        className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                      >
                        <User className="w-4 h-4 text-ink-400" /> Hồ sơ của tôi
                      </Link>
                      <Link
                        to="/candidate/settings"
                        className="flex items-center gap-3 rounded-lg px-3 py-2 text-ink-700 hover:bg-ink-100"
                      >
                        <Settings className="w-4 h-4 text-ink-400" /> Cài đặt
                      </Link>
                    </div>
                    <div className="p-1.5 border-t border-ink-100">
                      <button
                        onClick={() => {
                          logout()
                          navigate('/')
                        }}
                        className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50"
                      >
                        <LogOut className="w-4 h-4" /> Đăng xuất
                      </button>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          ) : (
            <Link
              to="/auth/candidate-login"
              className="rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
            >
              Đăng nhập
            </Link>
          )}
        </div>
      </div>

      {/* Mobile menu */}
      <AnimatePresence>
        {mobileMenuOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="lg:hidden border-t border-ink-200 bg-white"
          >
            <nav className="px-4 py-3 space-y-1">
              <Link
                to="/jobs"
                className="block rounded-lg px-3 py-2 text-brand-700 bg-brand-50 font-medium"
              >
                Việc làm
              </Link>
              <Link
                to="/candidate/applications"
                className="block rounded-lg px-3 py-2 text-ink-600 hover:bg-ink-100"
              >
                Hồ sơ ứng tuyển
              </Link>
              <Link
                to="/interview/practice/demo"
                className="block rounded-lg px-3 py-2 text-ink-600 hover:bg-ink-100"
              >
                Phỏng vấn thử
              </Link>
              {!isAuthenticated && (
                <Link
                  to="/auth/candidate-login"
                  className="block rounded-lg px-3 py-2 text-ink-600 hover:bg-ink-100"
                >
                  Đăng nhập
                </Link>
              )}
            </nav>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  )
}

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function loadJobDetail() {
      if (!id) return
      try {
        setLoading(true)
        const data = await jobService.getJobPostingById(id)
        setJob(data)
      } catch (err) {
        console.error(err)
        setError('Không tìm thấy tin tuyển dụng này hoặc tin tuyển dụng đã ngừng nhận hồ sơ.')
      } finally {
        setLoading(false)
      }
    }

    loadJobDetail()
  }, [id])

  if (loading) {
    return (
      <div className="min-h-screen bg-ink-50">
        <Nav />
        <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3">
          <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
          <p className="text-sm text-ink-500">Đang tải thông tin...</p>
        </div>
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="min-h-screen bg-ink-50">
        <Nav />
        <div className="py-20 text-center">
          <h2 className="mb-2 text-2xl font-display font-bold text-ink-900">Đã xảy ra lỗi</h2>
          <p className="mb-6 text-ink-500">{error || 'Không tìm thấy dữ liệu.'}</p>
          <button
            type="button"
            onClick={() => navigate('/jobs')}
            className="inline-flex items-center gap-2 rounded-xl border border-ink-200 bg-white px-6 py-3 font-medium text-ink-700 hover:bg-ink-50"
          >
            <ChevronRight className="w-4 h-4 rotate-180" />
            Xem việc làm khác
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased">
      <Nav />

      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            Việc làm
          </Link>
          <ChevronRight className="w-4 h-4" />
          <span className="text-ink-600 font-medium">{job.title}</span>
        </div>
      </div>

      <main className="mx-auto max-w-6xl px-6 py-6 grid gap-8 lg:grid-cols-[1fr_360px]">
        {/* Left: content */}
        <div className="space-y-6">
          {/* Header card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <div className="flex items-start gap-4">
              <div
                className={`grid h-16 w-16 shrink-0 place-items-center rounded-2xl ${getIconBg(job.department)}`}
              >
                {getJobIcon(job.department)}
              </div>
              <div className="min-w-0 flex-1">
                <h1 className="font-display text-2xl font-extrabold leading-snug">{job.title}</h1>
                <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-500">
                  <span className="inline-flex items-center gap-1.5">
                    <Users className="w-4 h-4" /> {job.department || 'Phòng ban'}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="w-4 h-4" /> {job.location || 'Remote'}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Clock className="w-4 h-4" /> Đăng {formatPostedDate(job.createdAt)}
                  </span>
                </div>
                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600">
                    <Briefcase className="w-3.5 h-3.5" />{' '}
                    {formatWorkMode(job.workMode || job.employmentType)}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2.5 py-1 text-emerald-700">
                    <DollarSign className="w-3.5 h-3.5" /> {formatSalary(job)}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600 capitalize">
                    <Sparkles className="w-3.5 h-3.5" /> {job.experienceLevel || 'Không yêu cầu'}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-brand-50 px-2.5 py-1 text-brand-700">
                    <Languages className="w-3.5 h-3.5" /> {job.languageRequirement || 'Tiếng Việt'}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* JD sections */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card space-y-6">
            <section>
              <h2 className="font-display text-lg font-bold mb-3">Mô tả công việc</h2>
              <p className="text-ink-600 leading-relaxed whitespace-pre-line">
                {job.jobDescription}
              </p>
            </section>

            {job.skills && job.skills.length > 0 && (
              <section>
                <h2 className="font-display text-lg font-bold mb-3">Yêu cầu</h2>
                <ul className="space-y-2 text-ink-600">
                  {job.skills.map((skill, i) => (
                    <li key={i} className="flex gap-2.5">
                      <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0" /> {skill}
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h2 className="font-display text-lg font-bold mb-3">Quyền lợi</h2>
              <div className="grid sm:grid-cols-2 gap-3 text-ink-600">
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <HeartPulse className="w-5 h-5 text-brand-600 shrink-0" /> Bảo hiểm sức khoẻ cao
                  cấp
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <Plane className="w-5 h-5 text-brand-600 shrink-0" /> Du lịch & team building
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <GraduationCap className="w-5 h-5 text-brand-600 shrink-0" /> Ngân sách học tập
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <Home className="w-5 h-5 text-brand-600 shrink-0" /> Hybrid working
                </div>
              </div>
            </section>
          </div>
        </div>

        {/* Right: sticky apply + match */}
        <aside className="space-y-5 lg:sticky lg:top-24 self-start">
          {/* Apply card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <button
              onClick={() => navigate(`/candidate/applications/${job.id}`)}
              className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700"
            >
              Ứng tuyển ngay
            </button>
            <button className="mt-2 w-full rounded-xl border border-ink-200 px-4 py-3 text-sm font-semibold text-ink-700 hover:bg-ink-50 flex items-center justify-center gap-2">
              <Bookmark className="w-4 h-4" /> Lưu việc làm
            </button>
          </div>

          {/* CV-JD Match (signature AI) */}
          <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-6 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="w-4 h-4" /> Độ phù hợp CV–JD
            </div>
            <div className="mt-3 flex items-end gap-2">
              <div className="font-display text-5xl font-extrabold text-ai-700 leading-none">
                87
              </div>
              <div className="pb-1 text-sm text-ink-500">/ 100</div>
            </div>
            <div className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-ai-100">
              <div className="h-full w-[87%] rounded-full bg-gradient-to-r from-brand-600 to-ai-600"></div>
            </div>
            <div className="mt-4 space-y-2 text-sm">
              <div className="flex items-center gap-2 text-emerald-700">
                <CheckCircle2 className="w-4 h-4" /> Khớp: React, .NET, EF Core
              </div>
              <div className="flex items-center gap-2 text-amber-700">
                <span className="w-4 h-4 flex items-center justify-center">⚠️</span> Thiếu: kinh
                nghiệm WebRTC
              </div>
            </div>
            <p className="mt-3 text-xs text-ink-400">
              Phân tích bởi Gemini 2.5 Flash · chỉ mang tính tham khảo
            </p>
            <button className="mt-3 w-full rounded-xl border border-ai-300 bg-white px-3 py-2 text-sm font-semibold text-ai-700 hover:bg-ai-50">
              Tải CV khác để phân tích
            </button>
          </div>

          {/* Company */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <div className="text-sm font-semibold mb-3">Về phòng ban</div>
            <div className="flex items-center gap-3">
              <div className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-brand-600">
                <Users className="w-5 h-5" />
              </div>
              <div>
                <div className="font-semibold text-sm">{job.department || 'Engineering'}</div>
                <div className="text-xs text-ink-400">Đội ngũ ~40 kỹ sư · Hybrid</div>
              </div>
            </div>
          </div>
        </aside>
      </main>
    </div>
  )
}
