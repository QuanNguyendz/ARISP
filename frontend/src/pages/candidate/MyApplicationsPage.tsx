import { useState } from 'react';
import { motion } from 'framer-motion';
import { MapPin, DollarSign, Clock, Eye, CheckCircle, XCircle, FileText, Calendar } from 'lucide-react';

const applications = [
  {
    id: '1',
    jobTitle: 'Senior Frontend Developer',
    company: 'TechVision Corp',
    logo: 'TV',
    status: 'interview',
    appliedDate: '20/05/2026',
    interviewDate: '30/05/2026',
    score: null,
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
  },
  {
    id: '2',
    jobTitle: 'Backend Engineer',
    company: 'DataFlow Systems',
    logo: 'DF',
    status: 'reviewing',
    appliedDate: '18/05/2026',
    interviewDate: null,
    score: null,
    location: 'Hà Nội',
    salary: '$2,500 - $4,000',
  },
  {
    id: '3',
    jobTitle: 'UI/UX Designer',
    company: 'Creative Hub',
    logo: 'CH',
    status: 'pending',
    appliedDate: '15/05/2026',
    interviewDate: null,
    score: null,
    location: 'Remote',
    salary: '$1,800 - $3,000',
  },
  {
    id: '4',
    jobTitle: 'Data Scientist',
    company: 'AI Analytics',
    logo: 'AA',
    status: 'accepted',
    appliedDate: '10/05/2026',
    interviewDate: '25/05/2026',
    score: 87,
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
  },
  {
    id: '5',
    jobTitle: 'DevOps Engineer',
    company: 'CloudTech',
    logo: 'CT',
    status: 'rejected',
    appliedDate: '05/05/2026',
    interviewDate: null,
    score: 45,
    location: 'Remote',
    salary: '$2,800 - $4,500',
  },
];

const statusConfig: Record<string, { label: string; color: string; icon: any }> = {
  pending: { label: 'Chờ duyệt', color: 'bg-amber-500/20 text-amber-400', icon: Clock },
  reviewing: { label: 'Đang xem', color: 'bg-blue-500/20 text-blue-400', icon: Eye },
  interview: { label: 'Phỏng vấn', color: 'bg-violet-500/20 text-violet-400', icon: Calendar },
  accepted: { label: 'Nhận việc', color: 'bg-emerald-500/20 text-emerald-400', icon: CheckCircle },
  rejected: { label: 'Từ chối', color: 'bg-red-500/20 text-red-400', icon: XCircle },
};

export default function MyApplicationsPage() {
  const [filter, setFilter] = useState<string>('all');

  const filteredApps = filter === 'all' 
    ? applications 
    : applications.filter(a => a.status === filter);

  return (
    <div className="min-h-screen bg-bg-primary p-6 lg:p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-8"
      >
        <h1 className="text-3xl font-semibold text-white mb-2">Việc đã ứng tuyển</h1>
        <p className="text-text-secondary">Theo dõi và quản lý các đơn ứng tuyển của bạn</p>
      </motion.div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Tổng đơn', value: applications.length.toString(), color: 'text-blue-400' },
          { label: 'Đang chờ', value: applications.filter(a => a.status === 'pending' || a.status === 'reviewing').length.toString(), color: 'text-amber-400' },
          { label: 'Phỏng vấn', value: applications.filter(a => a.status === 'interview').length.toString(), color: 'text-violet-400' },
          { label: 'Đã nhận', value: applications.filter(a => a.status === 'accepted').length.toString(), color: 'text-emerald-400' },
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

      {/* Filter Pills */}
      <div className="flex flex-wrap gap-2 mb-6">
        {['all', 'pending', 'reviewing', 'interview', 'accepted', 'rejected'].map((f) => (
          <button
            key={f}
            onClick={() => setFilter(f)}
            className={`px-4 py-2 rounded-full text-sm font-medium transition-colors ${
              filter === f
                ? 'bg-accent-primary text-white'
                : 'bg-white/5 text-text-secondary hover:bg-white/10'
            }`}
          >
            {f === 'all' ? 'Tất cả' : statusConfig[f]?.label || f}
          </button>
        ))}
      </div>

      {/* Applications List */}
      <div className="space-y-4">
        {filteredApps.map((app, index) => {
          const status = statusConfig[app.status] || statusConfig.pending;
          const StatusIcon = status.icon;

          return (
            <motion.div
              key={app.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
              className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden group hover:bg-white/[0.05] transition-colors"
            >
              <div className="flex items-center gap-6">
                <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-lg font-semibold text-white">
                  {app.logo}
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-3 mb-1">
                    <h3 className="text-lg font-semibold text-white">{app.jobTitle}</h3>
                    <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium ${status.color}`}>
                      <StatusIcon className="w-3 h-3" />
                      {status.label}
                    </span>
                  </div>
                  <p className="text-sm text-text-secondary mb-2">{app.company}</p>
                  <div className="flex flex-wrap items-center gap-4 text-xs text-text-tertiary">
                    <span className="flex items-center gap-1"><MapPin className="w-3 h-3" />{app.location}</span>
                    <span className="flex items-center gap-1"><DollarSign className="w-3 h-3" />{app.salary}</span>
                    <span className="flex items-center gap-1"><FileText className="w-3 h-3" />Ứng tuyển: {app.appliedDate}</span>
                    {app.interviewDate && (
                      <span className="flex items-center gap-1"><Calendar className="w-3 h-3" />PV: {app.interviewDate}</span>
                    )}
                  </div>
                </div>
                <div className="text-right">
                  {app.score && (
                    <p className="text-xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                      {app.score}
                    </p>
                  )}
                  <button className="mt-2 px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm font-medium hover:bg-accent-primary/30 transition-colors">
                    Chi tiết
                  </button>
                </div>
              </div>
            </motion.div>
          );
        })}
      </div>
    </div>
  );
}
