import { motion } from 'framer-motion'
import { ArrowLeft, FileText, Eye, CheckCircle, XCircle, Mail, Calendar } from 'lucide-react'

export default function CandidateDetailPage() {
  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <a
          href="/hr/candidates"
          className="inline-flex items-center gap-2 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 mb-6 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại
        </a>

        <div className="flex items-start gap-6 mb-8">
          <div className="w-20 h-20 rounded-full bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-2xl font-bold text-white">
            NVA
          </div>
          <div className="flex-1">
            <h1 className="text-3xl font-semibold text-ink-900 dark:text-white mb-2">
              Nguyễn Văn An
            </h1>
            <p className="text-xl text-ink-600 dark:text-ink-400 mb-4">Senior Backend Developer</p>
            <div className="flex items-center gap-4 text-sm text-ink-600 dark:text-ink-400">
              <span className="px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400">
                Đang phỏng vấn
              </span>
              <span className="flex items-center gap-1">
                <Calendar className="w-4 h-4" />
                Vòng 2
              </span>
            </div>
          </div>
          <div className="text-right">
            <div className="text-4xl font-semibold bg-gradient-to-r from-brand-600 to-ai-600 bg-clip-text text-transparent">
              87
            </div>
            <div className="text-xs text-ink-400 dark:text-ink-500">Overall Score</div>
          </div>
        </div>

        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">CV</h2>
              <div className="flex items-center gap-3 p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                <FileText className="w-5 h-5 text-brand-600 dark:text-brand-400" />
                <span className="text-ink-900 dark:text-white">an_nguyen_cv.pdf</span>
                <button className="ml-auto px-4 py-2 rounded-lg bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 text-sm hover:bg-brand-50 dark:hover:bg-brand-500/30 transition-colors">
                  Tải xuống
                </button>
              </div>
            </motion.div>
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.2 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                Lịch sử phỏng vấn
              </h2>
              <div className="space-y-4">
                {[
                  { type: 'Technical Interview', date: '19/05/2026', score: 92 },
                  { type: 'Coding Assessment', date: '17/05/2026', score: 85 },
                ].map((interview) => (
                  <div
                    key={interview.type}
                    className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5"
                  >
                    <div>
                      <p className="font-medium text-ink-900 dark:text-white">{interview.type}</p>
                      <p className="text-sm text-ink-500 dark:text-ink-400">{interview.date}</p>
                    </div>
                    <span className="text-xl font-semibold text-brand-600 dark:text-brand-400">
                      {interview.score}
                    </span>
                  </div>
                ))}
              </div>
            </motion.div>
          </div>
          <div className="space-y-6">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.3 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">Thao tác</h2>
              <div className="space-y-2">
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors text-sm font-medium">
                  <Eye className="w-5 h-5" />
                  Xem chi tiết CV
                </button>
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-500/30 transition-colors text-sm font-medium">
                  <CheckCircle className="w-5 h-5" />
                  Chấp nhận
                </button>
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/30 transition-colors text-sm font-medium">
                  <XCircle className="w-5 h-5" />
                  Từ chối
                </button>
              </div>
            </motion.div>
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.4 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">Điểm số</h2>
              <div className="space-y-4">
                {[
                  { label: 'Technical', score: 92 },
                  { label: 'Communication', score: 85 },
                  { label: 'Problem Solving', score: 84 },
                ].map((m) => (
                  <div key={m.label}>
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-sm text-ink-600 dark:text-ink-400">{m.label}</span>
                      <span className="text-sm font-medium text-ink-900 dark:text-white">
                        {m.score}%
                      </span>
                    </div>
                    <div className="h-2 bg-ink-100 dark:bg-white/10 rounded-full overflow-hidden">
                      <div
                        className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600"
                        style={{ width: `${m.score}%` }}
                      />
                    </div>
                  </div>
                ))}
              </div>
            </motion.div>
          </div>
        </div>
      </motion.div>
    </div>
  )
}
