import { motion } from 'framer-motion'
import { Users, MapPin, DollarSign } from 'lucide-react'
import { PageHeader } from '@components/shared'

const stats = [
  { label: 'Tổng tin', value: '12', color: 'text-blue-600 dark:text-blue-400' },
  { label: 'Đang active', value: '8', color: 'text-emerald-600 dark:text-emerald-400' },
  { label: 'Đã đóng', value: '3', color: 'text-amber-600 dark:text-amber-400' },
  { label: 'Chờ duyệt', value: '1', color: 'text-amber-600 dark:text-amber-400' },
]

const jobs = [
  {
    id: '1',
    title: 'Senior Backend Developer',
    location: 'Hà Nội',
    salary: '$2000 - $3000',
    status: 'active',
    createdAt: '2026-06-01',
    applicants: 45,
  },
  {
    id: '2',
    title: 'Frontend Developer',
    location: 'TP.HCM',
    salary: '$1500 - $2500',
    status: 'active',
    createdAt: '2026-06-05',
    applicants: 32,
  },
  {
    id: '3',
    title: 'DevOps Engineer',
    location: 'Hà Nội',
    salary: '$1800 - $2800',
    status: 'active',
    createdAt: '2026-06-08',
    applicants: 18,
  },
  {
    id: '4',
    title: 'Product Designer',
    location: 'Đà Nẵng',
    salary: '$1200 - $2000',
    status: 'closed',
    createdAt: '2026-05-20',
    applicants: 28,
  },
]

export default function HrJobsPage() {
  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Tin tuyển dụng"
        description="Quản lý tất cả tin tuyển dụng trong hệ thống"
      />

      {/* Stats */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card"
          >
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-ink-600 dark:text-ink-400 mt-1">{stat.label}</p>
          </div>
        ))}
      </div>

      <div className="space-y-4">
        {jobs.map((job, index) => (
          <motion.div
            key={job.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all"
          >
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-4 min-w-0">
                <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-white font-semibold shrink-0">
                  {job.title.charAt(0)}
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-3 mb-1 flex-wrap">
                    <h3 className="text-lg font-semibold text-ink-900 dark:text-white truncate">
                      {job.title}
                    </h3>
                    <span
                      className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                        job.status === 'active'
                          ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                          : 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400'
                      }`}
                    >
                      {job.status === 'active' ? 'Active' : 'Closed'}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-4 text-sm text-ink-600 dark:text-ink-400">
                    <span className="flex items-center gap-1">
                      <MapPin className="w-4 h-4" />
                      {job.location}
                    </span>
                    <span className="flex items-center gap-1">
                      <DollarSign className="w-4 h-4" />
                      {job.salary}
                    </span>
                    <span className="flex items-center gap-1">
                      <Users className="w-4 h-4" />
                      {job.applicants} ứng viên
                    </span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <a
                  href={`/hr/jobs/${job.id}`}
                  className="px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium"
                >
                  Chi tiết
                </a>
              </div>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
