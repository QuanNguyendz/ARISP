import { motion } from 'framer-motion'
import { BookOpen } from 'lucide-react'

export default function PlaybooksPage() {
  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-ink-900 dark:text-white mb-2">Playbooks</h1>
        <p className="text-ink-600 dark:text-ink-400">Quản lý kịch bản phỏng vấn</p>
      </motion.div>
      <div className="grid md:grid-cols-3 gap-6">
        {['Technical Interview', 'Behavioral Interview', 'System Design'].map((item, i) => (
          <motion.div
            key={item}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.1 }}
          >
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all cursor-pointer">
              <div className="w-12 h-12 rounded-xl bg-brand-100 dark:bg-brand-500/20 flex items-center justify-center mb-4">
                <BookOpen className="w-6 h-6 text-brand-600 dark:text-brand-400" />
              </div>
              <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-2">{item}</h3>
              <p className="text-sm text-ink-500 dark:text-ink-400">10 câu hỏi</p>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
