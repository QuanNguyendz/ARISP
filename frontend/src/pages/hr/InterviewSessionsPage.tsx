import { motion } from 'framer-motion'
import { Eye } from 'lucide-react'

const sessions = [
  {
    id: '1',
    candidate: 'Nguyễn Văn An',
    position: 'Senior Frontend',
    date: '19/05/2026',
    status: 'completed',
  },
  {
    id: '2',
    candidate: 'Trần Thị Minh',
    position: 'Product Designer',
    date: '19/05/2026',
    status: 'completed',
  },
  {
    id: '3',
    candidate: 'Lê Hoàng Nam',
    position: 'Data Scientist',
    date: '18/05/2026',
    status: 'pending',
  },
  {
    id: '4',
    candidate: 'Phạm Thu Hà',
    position: 'Backend Developer',
    date: '17/05/2026',
    status: 'completed',
  },
]

export default function InterviewSessionsPage() {
  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-ink-900 dark:text-white mb-2">
          Phiên phỏng vấn
        </h1>
        <p className="text-ink-600 dark:text-ink-400">Quản lý các phiên phỏng vấn</p>
      </motion.div>
      <div className="space-y-4">
        {sessions.map((session, index) => (
          <motion.div
            key={session.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-full bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-sm font-medium text-white">
                    {session.candidate
                      .split(' ')
                      .map((n: string) => n[0])
                      .join('')}
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                      {session.candidate}
                    </h3>
                    <p className="text-sm text-ink-500 dark:text-ink-400">{session.position}</p>
                  </div>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-sm text-ink-500 dark:text-ink-400">{session.date}</span>
                  <span
                    className={`px-3 py-1 rounded-full text-xs font-medium ${
                      session.status === 'completed'
                        ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                        : 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                    }`}
                  >
                    {session.status === 'completed' ? 'Hoàn thành' : 'Đang chờ'}
                  </span>
                  <a
                    href="/hr/evaluations"
                    className="flex items-center gap-2 px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 text-sm font-medium transition-colors"
                  >
                    <Eye className="w-4 h-4" />
                    Xem
                  </a>
                </div>
              </div>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
