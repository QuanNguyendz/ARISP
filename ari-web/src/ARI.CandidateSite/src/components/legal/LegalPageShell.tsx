import { type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

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
          id="lg-legal"
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
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-legal)" />
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

/** Một mục văn bản pháp lý: tiêu đề + nội dung. */
export function LegalSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="font-display text-xl font-bold text-ink-900">{title}</h2>
      <div className="space-y-3 text-sm leading-7 text-ink-600">{children}</div>
    </section>
  )
}

interface LegalPageShellProps {
  title: string
  lastUpdated: string
  intro?: ReactNode
  children: ReactNode
}

export default function LegalPageShell({
  title,
  lastUpdated,
  intro,
  children,
}: LegalPageShellProps) {
  return (
    <div className="min-h-screen bg-ink-50">
      {/* Header */}
      <header className="sticky top-0 z-10 border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <Link to="/" className="flex items-center gap-2.5">
            <Logo />
            <span className="font-display text-xl font-extrabold text-ink-900">ARISP</span>
          </Link>
          <button
            type="button"
            onClick={() =>
              window.history.length > 1 ? window.history.back() : (window.location.href = '/')
            }
            className="flex items-center gap-1.5 text-sm font-medium text-ink-500 transition hover:text-brand-600"
          >
            <ArrowLeft className="h-4 w-4" />
            Quay lại
          </button>
        </div>
      </header>

      {/* Content */}
      <main className="mx-auto max-w-3xl px-6 py-10 sm:py-14">
        <h1 className="font-display text-3xl font-extrabold text-ink-900 sm:text-4xl">{title}</h1>
        <p className="mt-2 text-sm text-ink-400">Cập nhật lần cuối: {lastUpdated}</p>

        {intro && <div className="mt-6 text-sm leading-7 text-ink-600">{intro}</div>}

        <div className="mt-10 space-y-10">{children}</div>

        {/* Cross link + footer */}
        <div className="mt-14 flex flex-wrap items-center gap-x-6 gap-y-2 border-t border-ink-200 pt-6 text-sm">
          <Link to="/terms" className="font-medium text-brand-600 hover:underline">
            Điều khoản sử dụng
          </Link>
          <Link to="/privacy" className="font-medium text-brand-600 hover:underline">
            Chính sách bảo mật
          </Link>
          <span className="ml-auto text-ink-400">© 2026 ARISP</span>
        </div>
      </main>
    </div>
  )
}
