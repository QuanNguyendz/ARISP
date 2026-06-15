import { motion } from 'framer-motion'
import { Clock, CheckCircle, XCircle, Eye, FileText, MapPin, DollarSign } from 'lucide-react'
import { useState } from 'react'
import { PageHeader, EmptyState } from '@components/shared'

const stats = [
  { label: 'Chờ duyệt', value: '5', color: 'text-amber-600 dark:text-amber-400' },
  { label: 'Đã duyệt hôm nay', value: '8', color: 'text-emerald-600 dark:text-emerald-400' },
  { label: 'Từ chối hôm nay', value: '2', color: 'text-red-600 dark:text-red-400' },
  { label: 'Tổng tháng này', value: '45', color: 'text-blue-600 dark:text-blue-400' },
]

const pendingJobs = [
  {
    id: '1',
    title: 'Senior Backend Developer',
    recruiter: 'Lê Minh C',
    submittedAt: '2026-06-11 12:30',
    location: 'Hà Nội',
    salary: '$2000 - $3000',
  },
  {
    id: '2',
    title: 'Frontend Developer',
    recruiter: 'Phạm Thu D',
    submittedAt: '2026-06-11 10:15',
    location: 'TP.HCM',
    salary: '$1500 - $2500',
  },
  {
    id: '3',
    title: 'DevOps Engineer',
    recruiter: 'Đỗ Hoàng E',
    submittedAt: '2026-06-10 16:45',
    location: 'Hà Nội',
    salary: '$1800 - $2800',
  },
  {
    id: '4',
    title: 'Product Designer',
    recruiter: 'Vũ Thị F',
    submittedAt: '2026-06-10 09:20',
    location: 'Đà Nẵng',
    salary: '$1200 - $2000',
  },
  {
    id: '5',
    title: 'Data Scientist',
    recruiter: 'Bùi Minh G',
    submittedAt: '2026-06-09 14:00',
    location: 'Hà Nội',
    salary: '$2500 - $4000',
  },
]

const formatDate = (dateStr: string) => {
  const date = new Date(dateStr)
  return date.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function PendingJobsPage() {
  const [selectedJob, setSelectedJob] = useState<string | null>(null)

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Tin chờ duyệt"
        description="Xem xét và phê duyệt tin tuyển dụng từ Recruiter"
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

      {pendingJobs.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="w-8 h-8 text-emerald-600 dark:text-emerald-400" />}
          title="Không có tin chờ duyệt"
          description="Tất cả tin tuyển dụng đã được xử lý."
        />
      ) : (
        <div className="space-y-4">
          {pendingJobs.map((job, index) => (
            <motion.div
              key={job.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 rounded-xl bg-amber-100 dark:bg-amber-500/20 flex items-center justify-center">
                    <FileText className="w-6 h-6 text-amber-600 dark:text-amber-400" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">
                      {job.title}
                    </h3>
                    <div className="flex flex-wrap items-center gap-4 text-sm text-ink-600 dark:text-ink-400 mb-3">
                      <span>Bởi: {job.recruiter}</span>
                      <span className="flex items-center gap-1">
                        <MapPin className="w-3 h-3" />
                        {job.location}
                      </span>
                      <span className="flex items-center gap-1">
                        <DollarSign className="w-3 h-3" />
                        {job.salary}
                      </span>
                    </div>
                    <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
                      <Clock className="w-3 h-3" />
                      Gửi lúc: {formatDate(job.submittedAt)}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <a
                    href={`/hr/jobs/${job.id}`}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium"
                  >
                    <Eye className="w-4 h-4" />
                    Xem
                  </a>
                  <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 text-sm font-medium hover:bg-emerald-50 dark:hover:bg-emerald-500/30 transition-colors">
                    <CheckCircle className="w-4 h-4" />
                    Duyệt
                  </button>
                  <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400 text-sm font-medium hover:bg-red-50 dark:hover:bg-red-500/30 transition-colors">
                    <XCircle className="w-4 h-4" />
                    Từ chối
                  </button>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  )
}
