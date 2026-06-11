import { motion } from 'framer-motion';
import { Users, BarChart3, Plus, Bell, Clock, CheckCircle, XCircle, FileText } from 'lucide-react';
import { PageHeader, StatsGrid } from '@components/shared';
import { useAuthStore } from '@store/auth/authStore';

const stats = [
  { label: 'Tin chờ duyệt', value: '5', change: '+2', color: 'text-amber-400' },
  { label: 'Ứng viên mới', value: '12', change: '+3', color: 'text-blue-400' },
  { label: 'Phỏng vấn hôm nay', value: '8', change: '+2', color: 'text-violet-400' },
  { label: 'Đã tuyển tháng này', value: '15', change: '+15%', color: 'text-emerald-400' },
];

const pendingJobs = [
  { id: '1', title: 'Senior Backend Developer', recruiter: 'Lê Minh C', submittedAt: '2 giờ trước' },
  { id: '2', title: 'Frontend Developer', recruiter: 'Phạm Thu D', submittedAt: '4 giờ trước' },
  { id: '3', title: 'DevOps Engineer', recruiter: 'Đỗ Hoàng E', submittedAt: '1 ngày trước' },
];

const recentEvaluations = [
  { id: '1', candidate: 'Nguyễn Văn A', role: 'Senior Backend', score: 87, status: 'pending', timeAgo: '2 giờ trước' },
  { id: '2', candidate: 'Trần Thị B', role: 'Product Designer', score: 91, status: 'pending', timeAgo: '4 giờ trước' },
  { id: '3', candidate: 'Lê Hoàng C', role: 'Data Scientist', score: 79, status: 'pending', timeAgo: '1 ngày trước' },
];

export default function HrDashboardPage() {
  const { user } = useAuthStore();

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={`Xin chào, ${user?.name || 'HR Admin'}`}
        description="Tổng quan hoạt động tuyển dụng"
        actions={[
          { label: 'Tạo tin mới', href: '/hr/jobs', variant: 'secondary' },
        ]}
      />

      <StatsGrid stats={stats} />

      <div className="grid lg:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-8">
          {/* Pending Job Approvals */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-500/20 flex items-center justify-center">
                  <Clock className="w-5 h-5 text-amber-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Tin chờ duyệt</h2>
                  <p className="text-xs text-white/40">Cần HR xem xét và phê duyệt</p>
                </div>
              </div>
              <a href="/hr/jobs/pending" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {pendingJobs.map((job) => (
                <div key={job.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-500/30 to-orange/30 flex items-center justify-center">
                      <FileText className="w-5 h-5 text-amber-400" />
                    </div>
                    <div>
                      <h3 className="text-sm font-medium text-white">{job.title}</h3>
                      <p className="text-xs text-white/40">Bởi: {job.recruiter} • {job.submittedAt}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <button className="p-2 rounded-lg bg-emerald-500/20 hover:bg-emerald-500/30 transition-colors">
                      <CheckCircle className="w-4 h-4 text-emerald-400" />
                    </button>
                    <button className="p-2 rounded-lg bg-red-500/20 hover:bg-red-500/30 transition-colors">
                      <XCircle className="w-4 h-4 text-red-400" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </motion.div>

          {/* Pending Evaluations */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-violet-500/20 flex items-center justify-center">
                  <BarChart3 className="w-5 h-5 text-violet-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Chờ xác nhận đánh giá</h2>
                  <p className="text-xs text-white/40">Cần HR review và confirm/override</p>
                </div>
              </div>
              <a href="/hr/evaluations" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {recentEvaluations.map((eval_) => (
                <div key={eval_.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-gradient-to-br from-violet-500/30 to-purple/30 flex items-center justify-center text-xs font-medium text-white">
                      {eval_.candidate.split(' ').map((n) => n[0]).join('')}
                    </div>
                    <div>
                      <h3 className="text-sm font-medium text-white">{eval_.candidate}</h3>
                      <p className="text-xs text-white/40">{eval_.role}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="text-right">
                      <p className="text-lg font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                        {eval_.score}
                      </p>
                    </div>
                    <span className="px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-400 text-xs font-medium">
                      {eval_.status}
                    </span>
                    <a href="/hr/evaluations" className="px-3 py-1.5 rounded-lg bg-accent-primary/20 text-accent-primary text-xs font-medium hover:bg-accent-primary/30 transition-colors">
                      Review
                    </a>
                  </div>
                </div>
              ))}
            </div>
          </motion.div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Quick Actions */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Thao tác nhanh</h2>
            <div className="space-y-2">
              <a href="/hr/jobs/pending" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-amber-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
                  <Clock className="w-4 h-4 text-amber-400" />
                </div>
                <span className="text-sm text-white">Duyệt tin tuyển dụng</span>
              </a>
              <a href="/hr/evaluations" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-violet-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-violet-500/20 flex items-center justify-center">
                  <BarChart3 className="w-4 h-4 text-violet-400" />
                </div>
                <span className="text-sm text-white">Xem đánh giá</span>
              </a>
              <a href="/hr/reports" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-emerald-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-emerald-500/20 flex items-center justify-center">
                  <BarChart3 className="w-4 h-4 text-emerald-400" />
                </div>
                <span className="text-sm text-white">Xem báo cáo</span>
              </a>
              <a href="/hr/playbooks" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-blue-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
                  <FileText className="w-4 h-4 text-blue-400" />
                </div>
                <span className="text-sm text-white">Quản lý Playbooks</span>
              </a>
            </div>
          </motion.div>

          {/* Performance Overview */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Hiệu suất tháng 6</h2>
            <div className="space-y-4">
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs text-white/40">Tin đã duyệt</span>
                  <span className="text-xs text-white font-medium">45/50</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/10 overflow-hidden">
                  <div className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 w-[90%]" />
                </div>
              </div>
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs text-white/40">Đánh giá đã confirm</span>
                  <span className="text-xs text-white font-medium">32/40</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/10 overflow-hidden">
                  <div className="h-full rounded-full bg-gradient-to-r from-accent-primary to-violet w-[80%]" />
                </div>
              </div>
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs text-white/40">Tỷ lệ pass</span>
                  <span className="text-xs text-white font-medium">68%</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/10 overflow-hidden">
                  <div className="h-full rounded-full bg-gradient-to-r from-violet to-purple w-[68%]" />
                </div>
              </div>
            </div>
          </motion.div>
        </div>
      </div>
    </div>
  );
}
