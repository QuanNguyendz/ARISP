import { motion } from 'framer-motion'
import { Mail } from 'lucide-react'

export default function TeamPage() {
  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-ink-900 dark:text-white mb-2">Nhóm</h1>
        <p className="text-ink-600 dark:text-ink-400">Quản lý thành viên nhóm</p>
      </motion.div>
      <div className="space-y-4">
        {[
          { name: 'Minh Anh', role: 'Admin', email: 'minhanh@company.com' },
          { name: 'Thu Hà', role: 'Recruiter', email: 'thuha@company.com' },
          { name: 'Hoàng Nam', role: 'HR Leader', email: 'hoangnam@company.com' },
        ].map((member, i) => (
          <motion.div
            key={member.name}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.1 }}
          >
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card flex items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-full bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-sm font-medium text-white">
                  {member.name
                    .split(' ')
                    .map((n: string) => n[0])
                    .join('')}
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                    {member.name}
                  </h3>
                  <p className="text-sm text-ink-500 dark:text-ink-400 flex items-center gap-1">
                    <Mail className="w-3 h-3" />
                    {member.email}
                  </p>
                </div>
              </div>
              <span
                className={`px-3 py-1 rounded-full text-xs font-medium ${
                  member.role === 'Admin'
                    ? 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400'
                    : member.role === 'HR Leader'
                      ? 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                      : 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                }`}
              >
                {member.role}
              </span>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
