import { motion } from 'framer-motion';
import { Clock, CheckCircle, XCircle, Eye, FileText } from 'lucide-react';
import { useState } from 'react';
import { PageHeader, StatsGrid, EmptyState, LoadingSpinner } from '@components/shared';

const stats = [
  { label: 'Chờ duyệt', value: '5', color: 'text-amber-400' },
  { label: 'Đã duyệt hôm nay', value: '8', color: 'text-emerald-400' },
  { label: 'Từ chối hôm nay', value: '2', color: 'text-red-400' },
  { label: 'Tổng tháng này', value: '45', color: 'text-blue-400' },
];

const pendingJobs = [
  { id: '1', title: 'Senior Backend Developer', recruiter: 'Lê Minh C', submittedAt: '2026-06-11 12:30', location: 'Hà Nội', salary: '$2000 - $3000' },
  { id: '2', title: 'Frontend Developer', recruiter: 'Phạm Thu D', submittedAt: '2026-06-11 10:15', location: 'TP.HCM', salary: '$1500 - $2500' },
  { id: '3', title: 'DevOps Engineer', recruiter: 'Đỗ Hoàng E', submittedAt: '2026-06-10 16:45', location: 'Hà Nội', salary: '$1800 - $2800' },
  { id: '4', title: 'Product Designer', recruiter: 'Vũ Thị F', submittedAt: '2026-06-10 09:20', location: 'Đà Nẵng', salary: '$1200 - $2000' },
  { id: '5', title: 'Data Scientist', recruiter: 'Bùi Minh G', submittedAt: '2026-06-09 14:00', location: 'Hà Nội', salary: '$2500 - $4000' },
];

const formatDate = (dateStr: string) => {
  const date = new Date(dateStr);
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

export default function PendingJobsPage() {
  const [selectedJob, setSelectedJob] = useState<string | null>(null);

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Tin chờ duyệt"
        description="Xem xét và phê duyệt tin tuyển dụng từ Recruiter"
      />

      <StatsGrid stats={stats} />

      {pendingJobs.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="w-8 h-8 text-emerald-400" />}
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
              className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500/30 to-orange/30 flex items-center justify-center">
                    <FileText className="w-6 h-6 text-amber-400" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">{job.title}</h3>
                    <div className="flex flex-wrap items-center gap-4 text-sm text-white/50 mb-3">
                      <span>Bởi: {job.recruiter}</span>
                      <span>{job.location}</span>
                      <span>{job.salary}</span>
                    </div>
                    <span className="flex items-center gap-1 text-xs text-amber-400">
                      <Clock className="w-3 h-3" />
                      Gửi lúc: {formatDate(job.submittedAt)}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <a 
                    href={`/hr/jobs/${job.id}`}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg bg-white/10 text-white/70 hover:text-white hover:bg-white/20 transition-colors"
                  >
                    <Eye className="w-4 h-4" />
                    Xem
                  </a>
                  <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-500/20 text-emerald-400 text-sm font-medium hover:bg-emerald-500/30 transition-colors">
                    <CheckCircle className="w-4 h-4" />
                    Duyệt
                  </button>
                  <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-500/20 text-red-400 text-sm font-medium hover:bg-red-500/30 transition-colors">
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
  );
}
