import { motion } from 'framer-motion';
import { Plus, MapPin, DollarSign, Users, MoreVertical, Eye, Edit2 } from 'lucide-react';

const jobPostings = [
  {
    id: '1',
    title: 'Senior Frontend Developer',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
    applicants: 24,
    status: 'active',
    createdAt: '15/05/2026',
  },
  {
    id: '2',
    title: 'Backend Engineer',
    location: 'Hà Nội',
    salary: '$2,500 - $4,000',
    applicants: 18,
    status: 'active',
    createdAt: '12/05/2026',
  },
  {
    id: '3',
    title: 'UI/UX Designer',
    location: 'Remote',
    salary: '$1,800 - $3,000',
    applicants: 31,
    status: 'paused',
    createdAt: '10/05/2026',
  },
  {
    id: '4',
    title: 'Data Scientist',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,500 - $5,500',
    applicants: 12,
    status: 'closed',
    createdAt: '05/05/2026',
  },
];

export default function JobPostingsPage() {
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
          <p className="text-sm text-white/40">Quản lý các tin tuyển dụng</p>
        </div>
        <a
          href="/quan-ly/tin-tuyen-dung/tao-moi"
          className="flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
        >
          <Plus className="w-4 h-4" />
          Tạo tin mới
        </a>
      </motion.div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Tổng tin', value: '12', color: 'text-blue-400' },
          { label: 'Đang active', value: '8', color: 'text-emerald-400' },
          { label: 'Tạm dừng', value: '2', color: 'text-amber-400' },
          { label: 'Đã đóng', value: '2', color: 'text-white/40' },
        ].map((stat) => (
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

      {/* Job Postings List */}
      <div className="space-y-4">
        {jobPostings.map((job, index) => (
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
                      'bg-white/10 text-white/40'
                    }`}>
                      {job.status === 'active' ? 'Active' : job.status === 'paused' ? 'Tạm dừng' : 'Đã đóng'}
                    </span>
                  </div>
                  <div className="flex items-center gap-4 text-sm text-white/50">
                    <span className="flex items-center gap-1"><MapPin className="w-4 h-4" />{job.location}</span>
                    <span className="flex items-center gap-1"><DollarSign className="w-4 h-4" />{job.salary}</span>
                    <span className="flex items-center gap-1"><Users className="w-4 h-4" />{job.applicants} ứng viên</span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-xs text-white/30">{job.createdAt}</span>
                <div className="flex items-center gap-2">
                  <a href={`/quan-ly/tin-tuyen-dung/${job.id}`} className="p-2 rounded-lg hover:bg-white/10 transition-colors">
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
    </div>
  );
}
