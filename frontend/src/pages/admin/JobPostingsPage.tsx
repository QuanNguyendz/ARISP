import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Plus, MapPin, DollarSign, Users, MoreVertical, Eye, Edit2, Loader2, Briefcase } from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

export default function JobPostingsPage() {
  const [jobs, setJobs] = useState<JobPosting[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchJobs() {
      try {
        setLoading(true);
        const data = await jobService.getAdminJobPostings();
        setJobs(data);
      } catch (err: any) {
        console.error(err);
        setError('Không thể tải danh sách tin tuyển dụng.');
      } finally {
        setLoading(false);
      }
    }
    fetchJobs();
  }, []);

  const stats = [
    { label: 'Tổng tin', value: jobs.length.toString(), color: 'text-blue-400' },
    { label: 'Đang active', value: jobs.filter(j => j.status === 'active').length.toString(), color: 'text-emerald-400' },
    { label: 'Tạm dừng', value: jobs.filter(j => j.status === 'paused').length.toString(), color: 'text-amber-400' },
    { label: 'Bản nháp', value: jobs.filter(j => j.status === 'draft').length.toString(), color: 'text-white/40' },
  ];

  function formatSalary(job: JobPosting): string {
    if (job.salaryIsNegotiable) return 'Thỏa thuận';
    if (job.salaryMin && job.salaryMax) {
      return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`;
    }
    if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`;
    if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`;
    return 'Thỏa thuận';
  }

  function formatDate(dateStr: string): string {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN');
    } catch {
      return dateStr;
    }
  }

  return (
    <div className="p-6 lg:p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between mb-8"
      >
        <div>
          <h1 className="text-2xl font-semibold text-white mb-1">Tin tuyển dụng</h1>
          <p className="text-sm text-white/40">Quản lý các tin tuyển dụng và cấu hình phỏng vấn AI</p>
        </div>
        <a
          href="/admin/jobs/create"
          className="flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
        >
          <Plus className="w-4 h-4" />
          Tạo tin mới
        </a>
      </motion.div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {stats.map((stat) => (
          <motion.div
            key={stat.label}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-4 overflow-hidden"
          >
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-white/40">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      {/* Error State */}
      {error && (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm mb-6">
          {error}
        </div>
      )}

      {/* Content */}
      {loading ? (
        <div className="flex flex-col items-center justify-center py-20 gap-3">
          <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
          <p className="text-sm text-white/40">Đang tải dữ liệu từ máy chủ...</p>
        </div>
      ) : jobs.length === 0 ? (
        <div className="bg-white/[0.02] border border-white/[0.05] rounded-2xl p-12 text-center">
          <Briefcase className="w-12 h-12 text-white/20 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-white mb-2">Chưa có tin tuyển dụng</h3>
          <p className="text-text-secondary text-sm mb-6">Hãy tạo tin tuyển dụng đầu tiên của bạn để bắt đầu tiếp nhận hồ sơ.</p>
          <a
            href="/admin/jobs/create"
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
          >
            <Plus className="w-4.5 h-4.5" />
            Tạo tin tuyển dụng đầu tiên
          </a>
        </div>
      ) : (
        <div className="space-y-4">
          {jobs.map((job, index) => (
            <motion.div
              key={job.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
              className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden group hover:bg-white/[0.05] transition-colors"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold">
                    {job.title.charAt(0)}
                  </div>
                  <div>
                    <div className="flex items-center gap-3 mb-1">
                      <h3 className="text-lg font-semibold text-white">{job.title}</h3>
                      <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                        job.status === 'active' ? 'bg-emerald-500/20 text-emerald-400' :
                        job.status === 'paused' ? 'bg-amber-500/20 text-amber-400' :
                        job.status === 'draft' ? 'bg-blue-500/20 text-blue-400' :
                        'bg-white/10 text-white/40'
                      }`}>
                        {job.status === 'active' ? 'Active' : 
                         job.status === 'paused' ? 'Tạm dừng' : 
                         job.status === 'draft' ? 'Bản nháp' : 'Đã đóng'}
                      </span>
                    </div>
                    <div className="flex flex-wrap items-center gap-4 text-sm text-white/50">
                      <span className="flex items-center gap-1">
                        <MapPin className="w-4 h-4" />
                        {job.location || 'Chưa cấu hình địa điểm'}
                      </span>
                      <span className="flex items-center gap-1">
                        <DollarSign className="w-4 h-4" />
                        {formatSalary(job)}
                      </span>
                      <span className="flex items-center gap-1">
                        <Users className="w-4 h-4" />
                        Vòng: {job.roundConfigs?.length || 0}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-white/30">Tạo ngày: {formatDate(job.createdAt)}</span>
                  <div className="flex items-center gap-2">
                    <a href={`/admin/jobs/${job.id}`} className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                      <Eye className="w-4 h-4 text-white/50" />
                    </a>
                    <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                      <Edit2 className="w-4 h-4 text-white/50" />
                    </button>
                    <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                      <MoreVertical className="w-4 h-4 text-white/50" />
                    </button>
                  </div>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  );
}
