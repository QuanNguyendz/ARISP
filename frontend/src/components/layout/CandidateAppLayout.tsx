import { Outlet } from 'react-router-dom'
import { ShieldCheck } from 'lucide-react'
import CandidateHeader from './CandidateHeader'

export default function CandidateAppLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-ink-50 text-ink-900">
      <CandidateHeader />

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
