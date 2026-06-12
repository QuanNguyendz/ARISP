import { motion } from 'framer-motion';
import { Users } from 'lucide-react';
import { PageHeader, StatsGrid } from '@components/shared';

const stats = [
  { label: 'Tổng tin', value: '12', color: 'text-blue-400' },
  { label: 'Đang active', value: '8', color: 'text-emerald-400' },
  { label: 'Đã đóng', value: '3', color: 'text-amber-400' },
  { label: 'Chờ duyệt', value: '1', color: 'text-amber-400' },
];

const jobs = [
  { id: '1', title: 'Senior Backend Developer', location: 'Hà Nội', salary: '$2000 - $3000', status: 'active', createdAt: '2026-06-01', applicants: 45 },
  { id: '2', title: 'Frontend Developer', location: 'TP.HCM', salary: '$1500 - $2500', status: 'active', createdAt: '2026-06-05', applicants: 32 },
  { id: '3', title: 'DevOps Engineer', location: 'Hà Nội', salary: '$1800 - $2800', status: 'active', createdAt: '2026-06-08', applicants: 18 },
  { id: '4', title: 'Product Designer', location: 'Đà Nẵng', salary: '$1200 - $2000', status: 'closed', createdAt: '2026-05-20', applicants: 28 },
];

export default function HrJobsPage() {
  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Tin tuyển dụng"
        description="Quản lý tất cả tin tuyển dụng trong hệ thống"
      />

      <StatsGrid stats={stats} />

      <div className="space-y-4">
        {jobs.map((job, index) => (
          <motion.div
            key={job.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden group hover:bg-white/[0.05] transition-colors"
          >
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-4 min-w-0">
                <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold shrink-0">
                  {job.title.charAt(0)}
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-3 mb-1 flex-wrap">
                    <h3 className="text-lg font-semibold text-white truncate">{job.title}</h3>
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                      job.status === 'active' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-white/10 text-white/50'
                    }`}>
                      {job.status === 'active' ? 'Active' : 'Closed'}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-4 text-sm text-white/50">
                    <span className="flex items-center gap-1">
                      <Users className="w-4 h-4" />
                      {job.location}
                    </span>
                    <span>{job.salary}</span>
                    <span>{job.applicants} ứng viên</span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <a 
                  href={`/hr/jobs/${job.id}`}
                  className="px-4 py-2 rounded-xl bg-white/10 text-white/70 hover:text-white hover:bg-white/20 transition-colors text-sm"
                >
                  Chi tiết
                </a>
              </div>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
